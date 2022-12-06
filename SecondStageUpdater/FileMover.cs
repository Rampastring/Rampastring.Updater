using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace SecondStageUpdater
{
    enum ProcessCheckMode
    {
        Mutex,
        ProcessName
    }

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
        /// <param name="checkMode">Specifies whether the file mover uses a mutex GUID
        /// or checks for the existence of a process when waiting for the main 
        /// application to exit before moving files.</param>
        /// <param name="appGuid">The GUID of the main application. The file mover 
        /// can use it to wait for the application to exit before moving files.</param>
        /// <param name="processName">The name of the main application's process.
        /// The file mover can use it to wait for the application to exit before moving files.</param>
        public FileMover(string buildPath, ProcessCheckMode checkMode, 
            string appGuid, string processName)
        {
            this.buildPath = buildPath;
            this.processCheckMode = checkMode;
            this.appGuid = appGuid;
            this.processNameToCheck = processName;
        }

        private string buildPath;

        private string appGuid;

        private List<string> filesToMove;

        private Mutex mutex;

        private int fileId = 0;

        private EventWaitHandle waitHandle;

        private ProcessCheckMode processCheckMode;
        private string processNameToCheck;

        private volatile bool aborted = false;

        private int version = 0;

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
                filesToMove = Directory.GetFiles(buildPath + TEMPORARY_UPDATER_DIRECTORY, "*", SearchOption.AllDirectories).ToList();
                int migrationsIndex = filesToMove.FindIndex(f => Path.GetFileName(f) == Migrations.FileName);
                if (migrationsIndex > -1)
                    filesToMove.RemoveAt(migrationsIndex);

                version = new IniFile(buildPath + TEMPORARY_UPDATER_DIRECTORY + Path.DirectorySeparatorChar + "LocalVersion").GetIntValue("Version", "VersionNumber", version);
            }
            catch (DirectoryNotFoundException)
            {
                Log("Invalid BuildPath specified in SecondStageUpdaterConfig.ini. " + 
                    buildPath + TEMPORARY_UPDATER_DIRECTORY + " is not a valid directory.");
                Log("Update halted.");
                Log("Please contact the product developers for support.");
                return;
            }

            Log("Second-Stage Updater");
            Log("Written by Rampastring");
            Log("http://www.moddb.com/members/rampastring");
            Log("");

            Log("Waiting for the main application to exit.");


            if (processCheckMode == ProcessCheckMode.Mutex)
            {
                string mutexId = string.Format("Global\\{{{0}}}", appGuid);

                mutex = new Mutex(false, mutexId, out bool createdNew);
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
            }
            else if (processCheckMode == ProcessCheckMode.ProcessName)
            {
                while (true)
                {
                    Process[] processes = Process.GetProcessesByName(processNameToCheck);

                    if (processes.Length == 0)
                        break;

                    foreach (Process process in processes)
                        process.Dispose();

                    Thread.Sleep(1000);
                }
            }

            Thread.Sleep(3000);

            MoveFiles();
        }

        private void MoveFiles()
        {
            // This loop is executed until all files have been moved, or the user
            // has aborted the program.
            while (fileId < filesToMove.Count)
            {
                if (aborted)
                    break;

                string sourceFile = filesToMove[fileId];

                // Do not copy LZMA files
                if (Path.GetExtension(sourceFile).Equals(".lzma", StringComparison.OrdinalIgnoreCase))
                    continue;

                string targetFile = buildPath + filesToMove[fileId].Substring(
                    buildPath.Length + TEMPORARY_UPDATER_DIRECTORY.Length + 1);

                LogEntry?.Invoke(this, new LogEventArgs(sourceFile + " -> " + targetFile));

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile));

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

            Log("Moving files finished. Running migrations next.");

            var migrations = new Migrations();
            migrations.LogEntry += (s, e) => Log(e.Message);
            migrations.ReadMigrations(buildPath, buildPath + TEMPORARY_UPDATER_DIRECTORY + Path.DirectorySeparatorChar);
            migrations.PerformMigrations(version);

            Log("Deleting temporary update files.");

            try
            {
                Directory.Delete(buildPath + TEMPORARY_UPDATER_DIRECTORY, true);
            }
            catch (IOException ex)
            {
                Log("I/O error when deleting update files! Message: " + ex.Message);
            }

            if (processCheckMode == ProcessCheckMode.Mutex)
            {
                // If we reach this point it means we're done with moving the files,
                // so release the mutex
                mutex.ReleaseMutex();
                mutex.Dispose();
            }

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
}
