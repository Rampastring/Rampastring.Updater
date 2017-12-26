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
    public class Updater
    {
        private const string REMOTE_BUILD_INFO_FILE = "ServerVersion";
        private const string LOCAL_BUILD_INFO_FILE = "LocalVersion";
        private const string TEMPORARY_UPDATER_DIRECTORY = "Updater";

        public delegate void ProgressDelegate(UpdateProgressState updateState, 
            string statusString, int currentPercent, int totalPercent);

        public Updater(string localBuildPath)
        {
            LocalBuildInfo = new LocalBuildInfo();
            LocalBuildInfo.BuildPath = localBuildPath;
        }

        private LocalBuildInfo LocalBuildInfo { get; set; }

        private RemoteBuildInfo RemoteBuildInfo { get; set; }

        public BuildState BuildState { get; set; }

        private List<UpdateMirror> updateMirrors = new List<UpdateMirror>();

        private readonly object locker = new object();

        private bool updateCheckInProgress = false;
        private bool updateInProgress = false;

        private int lastUpdateMirrorId = 0;

        /// <summary>
        /// Returns a bool that determines whether an update check is currently in progress.
        /// </summary>
        public bool UpdateCheckInProgress
        {
            get
            {
                lock (locker)
                    return updateCheckInProgress;
            }
        }

        /// <summary>
        /// Reads local build information.
        /// </summary>
        public void ReadLocalBuildInfo()
        {
            LocalBuildInfo.Parse(LocalBuildInfo.BuildPath + LOCAL_BUILD_INFO_FILE);
        }

        /// <summary>
        /// Starts an asynchronous check for updates if an update check or update
        /// isn't already in progress.
        /// </summary>
        /// <param name="onFailed">The function to call when the update check fails.</param>
        /// <param name="onUpToDate">The function to call when the local version
        /// is determined to be up to date.</param>
        /// <param name="onOutdated">The function to call when the local version
        /// is determined to be outdated.</param>
        public void CheckForUpdates(Action onFailed, Action onUpToDate, Action<string, long> onOutdated)
        {
            lock (locker)
            {
                if (updateCheckInProgress || updateInProgress)
                    return;

                updateCheckInProgress = true;
            }

            Thread thread = new Thread(new ParameterizedThreadStart(unused => 
                CheckForUpdatesInternal(onFailed, onUpToDate, onOutdated)));
            thread.Start();
        }

        private void CheckForUpdatesInternal(Action onFailed, Action onUpToDate, Action<string, long> onOutdated)
        {
            UpdaterLogger.Log("Checking for updates.");

            updateMirrors = updateMirrors.OrderBy(um => um.Rating).ToList();

            using (var webClient = new WebClient())
            {
                webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                webClient.Encoding = Encoding.GetEncoding(1252);

                CreateTemporaryDirectory();

                int i = 0;

                UpdateMirror updateMirror;

                string downloadDirectory = LocalBuildInfo.BuildPath + TEMPORARY_UPDATER_DIRECTORY + Path.DirectorySeparatorChar;

                while (i < updateMirrors.Count)
                {
                    updateMirror = updateMirrors[i];
                    UpdaterLogger.Log("Attempting to download version information from " +
                        updateMirror.UIName + " (" + updateMirror.URL + ")");

                    try
                    {
                        webClient.DownloadFile(updateMirror.URL + REMOTE_BUILD_INFO_FILE, downloadDirectory + REMOTE_BUILD_INFO_FILE);

                        UpdaterLogger.Log("Version information downloaded, proceeding to parsing it.");

                        RemoteBuildInfo = new RemoteBuildInfo();
                        RemoteBuildInfo.Parse(downloadDirectory + REMOTE_BUILD_INFO_FILE);

                        lastUpdateMirrorId = i;

                        lock (locker)
                        {
                            updateCheckInProgress = false;
                        }

                        if (RemoteBuildInfo.ProductVersionInfo.VersionNumber == LocalBuildInfo.ProductVersionInfo.VersionNumber)
                        {
                            BuildState = BuildState.UPTODATE;
                            onUpToDate?.Invoke();
                            return;
                        }
                        else
                        {
                            BuildState = BuildState.OUTDATED;
                            onOutdated?.Invoke(RemoteBuildInfo.ProductVersionInfo.DisplayString, GetUpdateSize());
                            return;
                        }
                    }
                    catch (WebException ex)
                    {
                        UpdaterLogger.Log("WebException when downloading version information: " + ex.Message);
                        i++;
                    }
                    catch (ParseException ex)
                    {
                        UpdaterLogger.Log("ParseException when parsing version information: " + ex.Message);
                        i++;
                    }
                    catch (FormatException ex)
                    {
                        UpdaterLogger.Log("FormatException when parsing version information: " + ex.Message);
                        i++;
                    }
                }
            }

            UpdaterLogger.Log("Failed to download version information from all update mirrors. Aborting.");

            lock (locker)
            {
                updateCheckInProgress = false;
            }

            onFailed?.Invoke();
        }

        /// <summary>
        /// Calculates estimated update size in bytes based on local and remote build information.
        /// </summary>
        /// <returns>The estimated update size in bytes.</returns>
        private long GetUpdateSize()
        {
            // We don't take into account potential local modified or corrupted files,
            // instead we assume that all files listed in the local build info match
            // the actual files on the local file system.
            // This is because checking the actual hashes of the files would take too
            // long for a quick update check. Instead we'll check the actual file
            // hashes when actually downloading the update.
            // This is why the calculated update size could only be an estimate
            // (although most of the time it'll be correct for everyone who 
            // doesn't modify the product).

            long updateSize = 0;

            LocalBuildInfo lbi = LocalBuildInfo;
            RemoteBuildInfo rbi = RemoteBuildInfo;

            foreach (var fileInfo in rbi.FileInfos)
            {
                var localMatch = lbi.FileInfos.Find(localFileInfo => localFileInfo.FilePath == fileInfo.FilePath);

                if (localMatch == null || !HashHelper.ByteArraysMatch(localMatch.Hash, fileInfo.UncompressedHash))
                    updateSize += fileInfo.GetDownloadSize();
            }

            return updateSize;
        }

        /// <summary>
        /// Performs an update asynchronously.
        /// </summary>
        /// <param name="onFailed">The function to call if the update fails.</param>
        /// <param name="onWaitingForRestart">The function to call when the updater
        /// has finished downloading files and is waiting for the restart command.</param>
        /// <param name="onProgress">The function to call when the update progress
        /// is changed.</param>
        public void PerformUpdate(Action<string> onFailed, Action onWaitingForRestart,
            ProgressDelegate onProgress)
        {
            lock (locker)
            {
                if (updateCheckInProgress || updateInProgress)
                    return;

                if (RemoteBuildInfo == null)
                    throw new InvalidOperationException("You need to succesfully check for updates before calling PerformUpdate.");

                if (BuildState != BuildState.OUTDATED)
                    throw new InvalidOperationException("The current version is not out of date!");

                updateInProgress = true;
            }

            Thread thread = new Thread(new ParameterizedThreadStart(unused =>
                PerformUpdateInternal(onFailed, onWaitingForRestart, onProgress)));
            thread.Start();
        }

        private void PerformUpdateInternal(Action<string> onFailed, Action onWaitingForRestart,
            ProgressDelegate onProgress)
        {
            UpdaterLogger.Log("Performing update.");

            CreateTemporaryDirectory();

            UpdateMirror updateMirror = updateMirrors[lastUpdateMirrorId];

            string buildPath = LocalBuildInfo.BuildPath;
            string downloadDirectory = buildPath + TEMPORARY_UPDATER_DIRECTORY + Path.DirectorySeparatorChar;

            List<RemoteFileInfo> filesToDownload = GatherFilesToDownload(buildPath, downloadDirectory,
                onProgress);

            CleanUpDownloadDirectory(filesToDownload, downloadDirectory);

            // TODO actually download the files
        }

        /// <summary>
        /// Gathers a list of files to download for the update.
        /// </summary>
        private List<RemoteFileInfo> GatherFilesToDownload(string buildPath,
            string downloadDirectory, ProgressDelegate onProgress)
        {
            List<RemoteFileInfo> filesToDownload = new List<RemoteFileInfo>();

            // This could be multithreaded later on for faster processing,
            // calculating the SHA1 hashes can take a lot of time

            for (int i = 0; i < RemoteBuildInfo.FileInfos.Count; i++)
            {
                var remoteFileInfo = RemoteBuildInfo.FileInfos[i];

                onProgress(UpdateProgressState.PREPARING, string.Empty,
                    (int)(i / (double)RemoteBuildInfo.FileInfos.Count * 100.0), 0);

                if (!File.Exists(buildPath + remoteFileInfo.FilePath))
                {
                    if (!File.Exists(downloadDirectory + remoteFileInfo.FilePath))
                    {
                        UpdaterLogger.Log("File " + remoteFileInfo.FilePath + " doesn't exist, adding it to the download queue.");
                        filesToDownload.Add(remoteFileInfo);
                    }
                    else if (!HashHelper.FileHashMatches(downloadDirectory + remoteFileInfo.FilePath,
                        remoteFileInfo.UncompressedHash))
                    {
                        UpdaterLogger.Log("File " + remoteFileInfo.FilePath + " exists in the " +
                            "temporary directory, but it's different from the remote version. Adding it to the download queue.");
                        filesToDownload.Add(remoteFileInfo);
                    }
                }
                else
                {
                    if (!HashHelper.FileHashMatches(buildPath + remoteFileInfo.FilePath, remoteFileInfo.UncompressedHash))
                    {
                        UpdaterLogger.Log("File " + remoteFileInfo.FilePath + " is different from the " +
                            "remote version, adding it to the download queue.");
                        filesToDownload.Add(remoteFileInfo);
                    }
                    else
                        UpdaterLogger.Log("File " + remoteFileInfo.FilePath + " is up to date.");
                }
            }

            return filesToDownload;
        }

        /// <summary>
        /// Cleans up the temporary download directory. Deletes files that don't
        /// exist in the download queue.
        /// </summary>
        /// <param name="filesToDownload">A list of files to download.</param>
        private void CleanUpDownloadDirectory(List<RemoteFileInfo> filesToDownload, string downloadDirectory)
        {
            string[] files = Directory.GetFiles(downloadDirectory);

            foreach (string filePath in files)
            {
                // Remove the download directory from the file path
                string subPath = filePath.Substring(downloadDirectory.Length);

                if (filesToDownload.Find(fi => fi.FilePath == subPath) == null)
                {
                    UpdaterLogger.Log("Deleting file " + subPath + " from the download " + 
                        "directory as part of the clean-up process.");
                    File.Delete(filePath);
                }
            }
        }

        /// <summary>
        /// Creates the temporary updater directory if it doesn't already exist.
        /// </summary>
        private void CreateTemporaryDirectory()
        {
            if (!Directory.Exists(LocalBuildInfo.BuildPath + TEMPORARY_UPDATER_DIRECTORY))
                Directory.CreateDirectory(LocalBuildInfo.BuildPath + TEMPORARY_UPDATER_DIRECTORY);
        }
    }

    public enum BuildState
    {
        UNKNOWN,
        UPTODATE,
        OUTDATED
    }

    public enum UpdateProgressState
    {
        PREPARING,
        DOWNLOADING,
        EXTRACTING
    }
}
