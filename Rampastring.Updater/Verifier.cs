using Rampastring.Updater.BuildInfo;
using Rampastring.Updater.Compression;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    /// <summary>
    /// Decompresses and verifies downloaded files using a separate thread.
    /// </summary>
    class Verifier
    {
        public Verifier(string downloadDirectory)
        {
            this.downloadDirectory = downloadDirectory;
            verifierTask = new Task(VerifyFiles);
        }

        /// <summary>
        /// Raised when a downloaded file didn't pass the verification check
        /// (meaning its hash is different from what the version information claims).
        /// </summary>
        public event EventHandler<IndexEventArgs> VerificationFailed;

        /// <summary>
        /// Raised when the thread has succesfully finished decompressing
        /// and verifying files.
        /// </summary>
        public event EventHandler Completed;

        private string downloadDirectory;

        private List<IndexedRemoteFileInfo> filesToCheck = new List<IndexedRemoteFileInfo>();

        private Task verifierTask;
        private EventWaitHandle waitHandle;

        private volatile bool queueReady = false;
        private volatile bool stopped = false;

        private readonly object locker = new object();

        /// <summary>
        /// Adds the specified file to the decompress-and-verify queue.
        /// If the file is not compressed, it'll only be verified.
        /// Also starts a Task to verify the files when called for the first time.
        /// </summary>
        /// <param name="remoteFileInfo">The file.</param>
        public void VerifyFile(IndexedRemoteFileInfo remoteFileInfo)
        {
            lock (locker)
            {
                filesToCheck.Add(remoteFileInfo);

                if (waitHandle == null)
                {
                    waitHandle = new EventWaitHandle(true, EventResetMode.AutoReset);

                    verifierTask.Start();
                }
            }

            waitHandle.Set();
        }

        /// <summary>
        /// Signals the verifier that no more files will be added to the verify queue,
        /// assuming that the currently pending files are correctly verified.
        /// Allows the verifier thread to exit.
        /// </summary>
        public void SetQueueReady()
        {
            queueReady = true;

            if (waitHandle != null)
                waitHandle.Set();
        }

        /// <summary>
        /// Signals the verifier to stop.
        /// It's safe to call this 
        /// </summary>
        public void Stop()
        {
            stopped = true;

            if (waitHandle != null)
                waitHandle.Set();
        }

        private void VerifyFiles()
        {
            while (true)
            {
                IndexedRemoteFileInfo indexedFileInfo;

                if (stopped)
                    break;

                lock (locker)
                {
                    indexedFileInfo = filesToCheck[0];
                }

                RemoteFileInfo fileInfo = indexedFileInfo.FileInfo;

                bool checkFileHash = true;

                if (fileInfo.Compressed)
                {
                    try
                    {
                        CompressionHelper.DecompressFile(downloadDirectory + fileInfo.GetFilePathWithCompression(),
                            downloadDirectory + fileInfo.FilePath);
                    }
                    catch (Exception ex) 
                    {
                        // The SevenZip compressor doesn't define what exceptions
                        // it might throw, so we'll just catch them all

                        UpdaterLogger.Log("Decompressing file " + fileInfo.FilePath + " failed! Message: " + ex.Message);
                        VerificationFailed?.Invoke(this, new IndexEventArgs(indexedFileInfo.Index));
                        queueReady = false;
                        checkFileHash = false;
                    }
                }

                if (checkFileHash)
                {
                    if (!HashHelper.FileHashMatches(downloadDirectory + fileInfo.FilePath, fileInfo.UncompressedHash))
                    {
                        UpdaterLogger.Log("File " + fileInfo.FilePath + " failed verification!");
                        VerificationFailed?.Invoke(this, new IndexEventArgs(indexedFileInfo.Index));
                        queueReady = false;
                    }
                    else
                        UpdaterLogger.Log("File " + fileInfo.FilePath + " passed verification.");
                }

                bool waitingForWork = false;

                lock (locker)
                {
                    filesToCheck.RemoveAt(0);

                    waitingForWork = filesToCheck.Count == 0;

                    waitHandle.Reset();

                    if (queueReady && waitingForWork)
                    {
                        Completed?.Invoke(this, EventArgs.Empty);
                        break;
                    }

                    //if (stopped)
                    //{
                    //    filesToCheck.Clear();
                    //    break;
                    //}
                }

                if (waitingForWork)
                    waitHandle.WaitOne();
            }

            waitHandle.Dispose();

            // We could also dispose of verifierTask, but it sounds like we don't need to bother
            // https://blogs.msdn.microsoft.com/pfxteam/2012/03/25/do-i-need-to-dispose-of-tasks/
            // In case we'd still want to do it, it'd be safest for this class to have a function
            // for disposing the task (maybe this class could implement IDisposable), and the
            // user of this class would then call it
        }
    }

    class IndexEventArgs : EventArgs
    {
        public IndexEventArgs(int index)
        {
            Index = index;
        }

        public int Index { get; private set; }
    }
}
