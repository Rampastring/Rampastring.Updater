using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    /// <summary>
    /// Handles downloading files from an update mirror.
    /// </summary>
    class UpdateDownloader
    {
        private const int MAX_ERROR_COUNT = 3;

        public event EventHandler<DownloadProgressEventArgs> DownloadProgress;

        private WebClient webClient;

        /// <summary>
        /// The list of all files that are downloaded from the server.
        /// Files aren't removed from this list when they've been downloaded.
        /// </summary>
        private List<RemoteFileInfo> filesToDownload;

        private string downloadDirectory;

        private UpdateMirror updateMirror;

        private readonly object cancellationLocker = new object();
        private readonly object fileListLocker = new object();
        private readonly object downloadedBytesLocker = new object();

        private long totalUpdateSize = 0;
        private long downloadedBytes = 0;


        private Verifier verifier;

        /// <summary>
        /// The list of file indexes (pointing to files in filesToDownload)
        /// that are still to be downloaded.
        /// </summary>
        private List<int> fileIndexesToDownload;

        /// <summary>
        /// Used for storing information on how many times downloading each 
        /// file has failed.
        /// </summary>
        private int[] fileIndexErrorCounts;

        private RemoteFileInfo currentlyDownloadedFile;

        private volatile bool cancelled = false;

        private EventWaitHandle verifierWaitHandle;

        /// <summary>
        /// Starts downloading an update.
        /// </summary>
        /// <param name="buildPath">The local build path.</param>
        /// <param name="downloadDirectory">The download directory.</param>
        /// <param name="filesToDownload">A list of files to download.</param>
        /// <param name="updateMirror">The update mirror to use.</param>
        public UpdateDownloadResult Update(string buildPath, string downloadDirectory,
            List<RemoteFileInfo> filesToDownload, UpdateMirror updateMirror)
        {
            if (filesToDownload.Count == 0)
                return new UpdateDownloadResult(UpdateDownloadResultType.COMPLETED);

            cancelled = false;

            this.filesToDownload = filesToDownload;
            this.downloadDirectory = downloadDirectory;
            this.updateMirror = updateMirror;

            foreach (var fileInfo in filesToDownload)
                totalUpdateSize += fileInfo.GetDownloadSize();

            verifierWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

            verifier = new Verifier(downloadDirectory);
            verifier.VerificationFailed += Verifier_VerificationFailed;
            verifier.Completed += Verifier_Completed;

            webClient = new WebClient()
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore),
                Encoding = Encoding.GetEncoding(1252)
            };
            webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;

            fileIndexesToDownload = new List<int>();
            for (int i = 0; i < filesToDownload.Count; i++)
                fileIndexesToDownload.Add(i);
            fileIndexesToDownload.Reverse();
            fileIndexErrorCounts = new int[filesToDownload.Count];

            while (true)
            {
                int fileIndex;

                lock (fileListLocker)
                {
                    // If we have downloaded all files and the verifier has
                    // succesfully verified them, the list of file indexes to
                    // download has no entries -> we don't have any work left
                    if (fileIndexesToDownload.Count == 0)
                        break;

                    fileIndex = fileIndexesToDownload[fileIndexesToDownload.Count - 1];
                    currentlyDownloadedFile = filesToDownload[fileIndex];
                }

                Task downloadTask = null;

                try
                {
                    // By both checking for cancellation and starting a new download
                    // task in the same lock block that's also used in Cancel() we're
                    // making sure that a call to Cancel() will take effect right
                    // away - either we're executing the code above meaning we'll
                    // check for cancellation soon, or we're waiting for a download to
                    // finish - and Cancel() cancels the download operation in case
                    // one is going on
                    lock (cancellationLocker)
                    {
                        if (cancelled)
                            return CleanUpAndReturnResult(UpdateDownloadResultType.CANCELLED);

                        downloadTask = webClient.DownloadFileTaskAsync(
                            new Uri(updateMirror.URL + currentlyDownloadedFile.GetDownloadFileName().Replace('\\', '/')),
                            downloadDirectory + currentlyDownloadedFile.GetDownloadFileName());
                    }

                    downloadTask.Wait();
                    downloadTask.Dispose();

                    lock (downloadedBytesLocker)
                        downloadedBytes += currentlyDownloadedFile.GetDownloadSize();

                    verifier.VerifyFile(new IndexedRemoteFileInfo(currentlyDownloadedFile, fileIndex));
                }
                catch (AggregateException ex)
                {
                    downloadTask.Dispose();

                    if (cancelled)
                        return CleanUpAndReturnResult(UpdateDownloadResultType.CANCELLED);

                    UpdaterLogger.Log("Exception while downloading file " +
                        currentlyDownloadedFile.FilePath + ": " + ex.InnerException.Message);

                    lock (fileListLocker)
                    {
                        fileIndexErrorCounts[fileIndex]++;

                        if (fileIndexErrorCounts[fileIndex] > MAX_ERROR_COUNT)
                        {
                            return CleanUpAndReturnResult(UpdateDownloadResultType.FAILED,
                                "Failed to download file " + currentlyDownloadedFile.FilePath);
                        }
                    }

                    continue;
                }

                bool waitingForVerifier = false;

                lock (fileListLocker)
                {
                    // Remove the downloaded file from the download queue
                    fileIndexesToDownload.Remove(fileIndex);

                    waitingForVerifier = fileIndexesToDownload.Count == 0;
                }

                if (waitingForVerifier)
                {
                    // We have downloaded all the files, wait for the verifier
                    // to finish verifying them
                    verifier.SetQueueReady();
                    verifierWaitHandle.WaitOne();
                }
            }

            return CleanUpAndReturnResult(UpdateDownloadResultType.COMPLETED);
        }

        /// <summary>
        /// Cleans up the download session and returns an UpdateDownloadResult 
        /// that matches the given parameters.
        /// </summary>
        /// <param name="state">The type of the update download result.</param>
        /// <param name="errorDescription">The description of the error that occured, if any.</param>
        /// <returns></returns>
        private UpdateDownloadResult CleanUpAndReturnResult(UpdateDownloadResultType state, string errorDescription = "")
        {
            verifier.VerificationFailed -= Verifier_VerificationFailed;
            verifier.Completed -= Verifier_Completed;
            verifier.Stop();

            webClient.DownloadProgressChanged -= WebClient_DownloadProgressChanged;
            webClient.Dispose();

            verifierWaitHandle.Dispose();

            return new UpdateDownloadResult(state, errorDescription);
        }

        /// <summary>
        /// Cancels the update.
        /// </summary>
        public void Cancel()
        {
            lock (cancellationLocker)
            {
                cancelled = true;

                if (webClient == null || !webClient.IsBusy)
                    return;

                webClient.CancelAsync();
                verifier.Stop();
            }
        }

        /// <summary>
        /// Raises an event for download progress.
        /// </summary>
        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            DownloadProgressEventArgs args = null;

            lock (downloadedBytesLocker)
            {
                args = new DownloadProgressEventArgs(downloadedBytes + e.BytesReceived,
                    totalUpdateSize, e.BytesReceived, currentlyDownloadedFile.GetDownloadSize(),
                    currentlyDownloadedFile.FilePath);
            }

            DownloadProgress?.Invoke(this, args);
        }

        private void Verifier_Completed(object sender, EventArgs e)
        {
            verifierWaitHandle.Set();
        }

        private void Verifier_VerificationFailed(object sender, IndexEventArgs e)
        {
            lock (fileListLocker)
            {
                fileIndexErrorCounts[e.Index]++;
                fileIndexesToDownload.Add(e.Index);

                lock (downloadedBytesLocker)
                    downloadedBytes -= filesToDownload[e.Index].GetDownloadSize();
            }

            verifierWaitHandle.Set();
        }
    }

    /// <summary>
    /// Stores the result of an update download attempt.
    /// </summary>
    class UpdateDownloadResult
    {
        public UpdateDownloadResult(UpdateDownloadResultType updateDownloadResultState)
        {
            UpdateDownloadResultState = updateDownloadResultState;
            ErrorDescription = string.Empty;
        }

        public UpdateDownloadResult(UpdateDownloadResultType updateDownloadResultState,
            string errorDescription)
        {
            UpdateDownloadResultState = updateDownloadResultState;
            ErrorDescription = errorDescription;
        }

        public UpdateDownloadResultType UpdateDownloadResultState { get; private set; }
        public string ErrorDescription { get; private set; }
    }

    enum UpdateDownloadResultType
    {
        COMPLETED,
        FAILED,
        CANCELLED
    }

    class DownloadProgressEventArgs : EventArgs
    {
        public DownloadProgressEventArgs(long totalBytesReceived,
            long totalBytesToDownload, long bytesReceivedFromFile,
            long currentFileSize, string currentFilePath)
        {
            TotalBytesReceived = totalBytesReceived;
            TotalBytesToDownload = totalBytesToDownload;
            BytesReceivedFromFile = bytesReceivedFromFile;
            CurrentFileSize = currentFileSize;
            CurrentFilePath = currentFilePath;
        }

        public long TotalBytesReceived { get; private set; }
        public long TotalBytesToDownload { get; private set; }
        public long BytesReceivedFromFile { get; private set; }
        public long CurrentFileSize { get; private set; }
        public string CurrentFilePath { get; private set; }
    }
}
