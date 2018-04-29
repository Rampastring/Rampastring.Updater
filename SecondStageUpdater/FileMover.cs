using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace SecondStageUpdater
{
    /// <summary>
    /// Moves files from the temporary updater directory to the build directory.
    /// </summary>
    class FileMover
    {
        private const string TEMPORARY_UPDATER_DIRECTORY = "Updater";

        public event EventHandler<LogEventArgs> LogEntry;
        public event EventHandler FilesMoved;

        /// <summary>
        /// Creates a new file mover.
        /// </summary>
        /// <param name="buildPath">The path of the local build.</param>
        /// <param name="appGuid">The GUID of the main application. The file mover 
        /// uses it to wait for the application to exit before moving files.</param>
        public FileMover(string buildPath, string appGuid)
        {
            this.buildPath = buildPath;
            this.appGuid = appGuid;
        }

        private string buildPath;

        private string appGuid;

        private string[] filesToMove;

        private Mutex mutex;

        private int fileId = 0;

        private EventWaitHandle waitHandle;

        private volatile bool aborted = false;

        /// <summary>
        /// Starts moving files asynchronously.
        /// </summary>
        public void Start()
        {
            waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            Thread thread = new Thread(new ThreadStart(InternalStart));
            thread.Start();
        }

        private void InternalStart()
        {
            try
            {
                filesToMove = Directory.GetFiles(buildPath + TEMPORARY_UPDATER_DIRECTORY);
            }
            catch (DirectoryNotFoundException)
            {
                Log("Invalid BuildPath specified in SecondStageUpdaterConfig.ini. " + 
                    buildPath + TEMPORARY_UPDATER_DIRECTORY + " is not a valid directory.");
                Log("Update halted.");
                Log("Please inform about this to the product developers.");
                return;
            }

            Log("Second-Stage Updater");
            Log("Written by Rampastring");
            Log("http://www.moddb.com/members/rampastring");
            Log("");

            Log("Waiting for the main application to exit.");

            string mutexId = string.Format("Global\\{{{0}}}", appGuid);

            var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl, AccessControlType.Allow);
            var securitySettings = new MutexSecurity();
            securitySettings.AddAccessRule(allowEveryoneRule);

            mutex = new Mutex(false, mutexId, out bool createdNew, securitySettings);
            while (true)
            {
                try
                {
                    bool hasHandle = mutex.WaitOne(int.MaxValue, false);
                    if (hasHandle)
                        break;

                    continue;
                }
                catch (AbandonedMutexException)
                {
                    break;
                }
            }

            MoveFiles();
        }

        private void MoveFiles()
        {
            // This loop is executed until all files have been moved, or the user
            // has aborted the program.
            while (fileId < filesToMove.Length)
            {
                if (aborted)
                    break;

                string sourceFile = filesToMove[fileId];
                string targetFile = buildPath + filesToMove[fileId].Substring(
                    buildPath.Length + TEMPORARY_UPDATER_DIRECTORY.Length + 1);

                LogEntry?.Invoke(this, new LogEventArgs(sourceFile + " -> " + targetFile));

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(sourceFile));

                    File.Copy(sourceFile, targetFile, true);
                }
                catch (UnauthorizedAccessException)
                {
                    Log("Access denied when updating file " + targetFile);
                    LogErrorInstructions();
                    waitHandle.WaitOne();
                    continue;
                }
                catch (IOException ex)
                {
                    Log("I/O error when updating file! Message: " + ex.Message);
                    LogErrorInstructions();
                    waitHandle.WaitOne();
                    continue;
                }

                fileId++;
            }

            Log("Moving files finished.");

            // If we reach this point it means we're done with moving the files,
            // so release the mutex
            mutex.ReleaseMutex();
            mutex.Dispose();

            Directory.Delete(buildPath + TEMPORARY_UPDATER_DIRECTORY, true);

            FilesMoved(this, EventArgs.Empty);
        }

        /// <summary>
        /// Makes the file mover resume operation after an error has occured.
        /// </summary>
        public void Proceed()
        {
            if (waitHandle != null)
                waitHandle.Set();
        }

        /// <summary>
        /// Aborts the file-moving operation.
        /// </summary>
        public void Abort()
        {
            aborted = true;
            waitHandle.Set();
        }

        private void LogErrorInstructions()
        {
            Log("Press any key to retry. If the problem persists, " + 
                "try to move the content of the \"Updater\" directory " +
                "to the main directory manually or contact the staff for support.");
        }

        private void Log(string message)
        {
            LogEntry?.Invoke(this, new LogEventArgs(message));
        }
    }

    class LogEventArgs : EventArgs
    {
        public LogEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }
}
