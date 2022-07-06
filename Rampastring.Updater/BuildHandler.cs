using Rampastring.Updater.BuildInfo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Handles information about the local build and updating the local build.
    /// </summary>
    public class BuildHandler
    {
        public const string REMOTE_BUILD_INFO_FILE = "ServerVersion";
        public const string LOCAL_BUILD_INFO_FILE = "LocalVersion";
        public const string TEMPORARY_UPDATER_DIRECTORY = "Updater";

        private const string SECOND_STAGE_UPDATER_CONFIGURATION_FILE = "SecondStageUpdaterConfig.ini";

        public delegate void ProgressDelegate(UpdateProgressState updateState, 
            string statusString, int currentPercent, int totalPercent);

        #region Events

        /// <summary>
        /// Raised when a check for updates fails.
        /// </summary>
        public event EventHandler UpdateCheckFailed;

        /// <summary>
        /// Raised when the local build is determined to be up to
        /// date in an update check.
        /// </summary>
        public event EventHandler BuildUpToDate;

        /// <summary>
        /// Raised when the local build is determined to be out of
        /// date in an update check.
        /// </summary>
        public event EventHandler<BuildOutdatedEventArgs> BuildOutdated;

        /// <summary>
        /// Raised when the updater has finished downloading all files.
        /// The program needs to clean its session and shut itself down.
        /// </summary>
        public event EventHandler DownloadCompleted;

        /// <summary>
        /// Raised when the update has been cancelled due to the host program
        /// requesting so.
        /// </summary>
        public event EventHandler UpdateCancelled;

        /// <summary>
        /// Raised when there's been progress during an update.
        /// </summary>
        public event EventHandler<UpdateProgressEventArgs> UpdateProgressChanged;

        /// <summary>
        /// Raised when performing an update fails.
        /// </summary>
        public event EventHandler<UpdateFailureEventArgs> UpdateFailed;
        
        #endregion

        public BuildHandler(string localBuildPath, string secondStageUpdaterPath)
        {
            localBuildInfo = new LocalBuildInfo();
            
            if (!localBuildPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                localBuildInfo.BuildPath = localBuildPath + Path.DirectorySeparatorChar;
            else
                localBuildInfo.BuildPath = localBuildPath;

            SecondStageUpdaterPath = secondStageUpdaterPath;
        }

        public BuildState BuildState { get; private set; }

        /// <summary>
        /// The path to the second-stage updater executable,
        /// relative to the local build path.
        /// The second-stage updater is launched during the update process when
        /// all files have been downloaded and extracted.
        /// The second-stage updater applies the downloaded files by moving
        /// them from the temporary updater directory to the local build's main
        /// directory.
        /// </summary>
        public string SecondStageUpdaterPath { get; private set; }

        private LocalBuildInfo localBuildInfo;

        private RemoteBuildInfo remoteBuildInfo;

        private List<UpdateMirror> updateMirrors = new List<UpdateMirror>();

        private readonly object locker = new object();

        private volatile bool updateCheckInProgress = false;
        private volatile bool updateInProgress = false;

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
            try
            {
                if (File.Exists(localBuildInfo.BuildPath + LOCAL_BUILD_INFO_FILE))
                    localBuildInfo.Parse(localBuildInfo.BuildPath + LOCAL_BUILD_INFO_FILE);
            }
            catch (ParseException ex)
            {
                UpdaterLogger.Log("Failed to parse local build information. Message: " + ex.Message);
            }
        }

        public string GetLocalVersionDisplayString()
        {
            return localBuildInfo.ProductVersionInfo.DisplayString;
        }

        /// <summary>
        /// Reads update mirrors from an INI file in the specified file system path.
        /// </summary>
        /// <param name="filePath">The path to the INI file.</param>
        public void ParseUpdateMirrors(string filePath)
        {
            ParseUpdateMirrors(new IniFile(filePath));
        }

        /// <summary>
        /// Reads update mirrors from an INI file.
        /// </summary>
        /// <param name="iniFile">The INI file.</param>
        public void ParseUpdateMirrors(IniFile iniFile)
        {
            if (iniFile == null)
                throw new ArgumentNullException("iniFile");

            ParseUpdateMirrors(iniFile.GetSection("UpdateMirrors"));
        }

        /// <summary>
        /// Adds an update mirror to the updater's internal list of update mirrors.
        /// </summary>
        /// <param name="url">The address of the update mirror.</param>
        /// <param name="uiName">The name of the update mirror.</param>
        public void AddUpdateMirror(string url, string uiName)
        {
            updateMirrors.Add(new UpdateMirror(url, uiName));
        }

        /// <summary>
        /// Reads update mirrors from an INI section.
        /// </summary>
        /// <param name="section">The INI section.</param>
        public void ParseUpdateMirrors(IniSection section)
        {
            if (section == null)
                throw new ArgumentNullException("section");

            var keys = section.GetKeys();

            foreach (string key in keys)
            {
                string value = section.GetStringValue(key, string.Empty);

                updateMirrors.Add(UpdateMirror.FromString(value));
            }
        }

        /// <summary>
        /// Starts an asynchronous check for updates if an update check or update
        /// isn't already in progress.
        /// </summary>
        public void CheckForUpdates()
        {
            lock (locker)
            {
                if (updateCheckInProgress || updateInProgress)
                    return;

                updateCheckInProgress = true;
            }

            Thread thread = new Thread(new ThreadStart(CheckForUpdatesInternal));
            thread.Start();
        }

        private void CheckForUpdatesInternal()
        {
            UpdaterLogger.Log("Checking for updates.");

            updateMirrors = updateMirrors.OrderBy(um => um.Rating).ToList();

            using (WebClient webClient = CreateWebClient())
            {
                webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                webClient.Encoding = Encoding.GetEncoding(1252);

                CreateTemporaryDirectory();

                int i = 0;

                UpdateMirror updateMirror;

                string downloadDirectory = localBuildInfo.BuildPath + TEMPORARY_UPDATER_DIRECTORY + Path.DirectorySeparatorChar;

                while (i < updateMirrors.Count)
                {
                    updateMirror = updateMirrors[i];
                    UpdaterLogger.Log("Attempting to download version information from " +
                        updateMirror.UIName + " (" + updateMirror.URL + ")");

                    try
                    {
                        webClient.DownloadFile(updateMirror.URL + REMOTE_BUILD_INFO_FILE, downloadDirectory + REMOTE_BUILD_INFO_FILE);

                        UpdaterLogger.Log("Version information downloaded, proceeding to parsing it.");

                        remoteBuildInfo = new RemoteBuildInfo();
                        remoteBuildInfo.Parse(downloadDirectory + REMOTE_BUILD_INFO_FILE);

                        lastUpdateMirrorId = i;

                        lock (locker)
                        {
                            updateCheckInProgress = false;
                        }

                        if (remoteBuildInfo.ProductVersionInfo.VersionNumber == localBuildInfo.ProductVersionInfo.VersionNumber)
                        {
                            BuildState = BuildState.UPTODATE;
                            BuildUpToDate?.Invoke(this, EventArgs.Empty);
                            return;
                        }
                        else
                        {
                            BuildState = BuildState.OUTDATED;
                            BuildOutdated?.Invoke(this, new BuildOutdatedEventArgs(
                                remoteBuildInfo.ProductVersionInfo.DisplayString, GetUpdateSize()));
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

            UpdateCheckFailed?.Invoke(this, EventArgs.Empty);
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
            // (although it'll be correct for everyone who doesn't modify the product).

            long updateSize = 0;

            LocalBuildInfo lbi = localBuildInfo;
            RemoteBuildInfo rbi = remoteBuildInfo;

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
        public void PerformUpdate()
        {
            lock (locker)
            {
                if (updateCheckInProgress || updateInProgress)
                    return;

                if (remoteBuildInfo == null)
                    throw new InvalidOperationException("An update check needs to pass succesfully before PerformUpdate is called.");

                if (BuildState != BuildState.OUTDATED)
                    throw new InvalidOperationException("The current version is not out of date!");

                updateInProgress = true;
            }

            Thread thread = new Thread(new ThreadStart(
                PerformUpdateInternal));
            thread.Start();
        }

        private void PerformUpdateInternal()
        {
            UpdaterLogger.Log("Performing update.");

            CreateTemporaryDirectory();

            UpdateMirror updateMirror = updateMirrors[lastUpdateMirrorId];

            char dsc = Path.DirectorySeparatorChar;

            string buildPath = localBuildInfo.BuildPath;
            string downloadDirectory = buildPath + TEMPORARY_UPDATER_DIRECTORY + dsc;

            List<RemoteFileInfo> filesToDownload = GatherFilesToDownload(buildPath, downloadDirectory);

            CleanUpDownloadDirectory(filesToDownload, downloadDirectory);

            UpdaterLogger.Log("Creating downloader.");

            UpdateDownloader downloader = new UpdateDownloader();
            downloader.DownloadProgress += Downloader_DownloadProgress;
            UpdateDownloadResult result = downloader.DownloadUpdates(buildPath, downloadDirectory, filesToDownload, updateMirror);
            downloader.DownloadProgress -= Downloader_DownloadProgress;

            lock (locker)
            {
                updateInProgress = false;
            }

            switch (result.UpdateDownloadResultState)
            {
                case UpdateDownloadResultType.CANCELLED:
                    UpdateCancelled?.Invoke(this, EventArgs.Empty);
                    return;
                case UpdateDownloadResultType.COMPLETED:
                    // If a new second-stage updater was downloaded, update it
                    // first before launching it

                    UpdaterLogger.Log("Checking whether a new second-stage updater was downloaded...");
                    UpdaterLogger.Log("Second-stage updater path: " + SecondStageUpdaterPath);

                    string originalSecondStageUpdaterPath = localBuildInfo.BuildPath + SecondStageUpdaterPath;

                    string updatedSecondStageUpdaterPath = localBuildInfo.BuildPath +
                        TEMPORARY_UPDATER_DIRECTORY + dsc +
                        SecondStageUpdaterPath;

                    if (File.Exists(updatedSecondStageUpdaterPath))
                    {
                        UpdaterLogger.Log($"Updated second-stage updater found, applying it. " +
                            $"Original path: {originalSecondStageUpdaterPath}, updated path: {updatedSecondStageUpdaterPath}");
                        Directory.CreateDirectory(Path.GetDirectoryName(originalSecondStageUpdaterPath));
                        File.Delete(originalSecondStageUpdaterPath);
                        File.Move(updatedSecondStageUpdaterPath, originalSecondStageUpdaterPath);
                    }

                    // Also update the second-stage updater's config file

                    string originalSecondStageConfigPath = Path.GetDirectoryName(originalSecondStageUpdaterPath)
                        + dsc + SECOND_STAGE_UPDATER_CONFIGURATION_FILE;

                    string updatedSecondStageConfigPath = Path.GetDirectoryName(updatedSecondStageUpdaterPath)
                        + dsc + SECOND_STAGE_UPDATER_CONFIGURATION_FILE;

                    if (File.Exists(updatedSecondStageConfigPath))
                    {
                        UpdaterLogger.Log($"Updated second-stage updater config found, applying it. " +
                            $"Original path: {originalSecondStageConfigPath}, updated path: {updatedSecondStageConfigPath}");

                        Directory.CreateDirectory(Path.GetDirectoryName(originalSecondStageConfigPath));
                        File.Delete(originalSecondStageConfigPath);
                        File.Move(updatedSecondStageConfigPath, originalSecondStageConfigPath);

                        // Are there other potential files that the second-stage updater needs?
                        IniFile secondStageUpdaterIni = new IniFile(originalSecondStageConfigPath);

                        var section = secondStageUpdaterIni.GetSection("RelatedFiles");
                        if (section != null)
                        {
                            foreach (var kvp in section.Keys)
                            {
                                if (string.IsNullOrWhiteSpace(kvp.Value))
                                    continue;

                                string fileName = kvp.Value;
                                string downloadedFilePath = Path.GetDirectoryName(updatedSecondStageUpdaterPath) + dsc + fileName;
                                string targetFilePath = Path.GetDirectoryName(originalSecondStageUpdaterPath) + dsc + fileName;

                                if (File.Exists(downloadedFilePath))
                                {
                                    File.Move(downloadedFilePath, targetFilePath);
                                }
                            }
                        }
                    }


                    // Generate local build information file
                    LocalBuildInfo newBuildInfo = LocalBuildInfoFromRemoteBuildInfo();
                    newBuildInfo.Write(localBuildInfo.BuildPath + TEMPORARY_UPDATER_DIRECTORY + dsc + LOCAL_BUILD_INFO_FILE);

                    Process.Start(originalSecondStageUpdaterPath);

                    // No null checking necessary here, it's actually better to
                    // crash the application in case this is not subscribed to
                    DownloadCompleted.Invoke(this, EventArgs.Empty);
                    return;
                case UpdateDownloadResultType.FAILED:
                    UpdateFailed?.Invoke(this, new UpdateFailureEventArgs(result.ErrorDescription));
                    return;
            }
        }

        private LocalBuildInfo LocalBuildInfoFromRemoteBuildInfo()
        {
            var localBuildInfo = new LocalBuildInfo();
            localBuildInfo.ProductVersionInfo = remoteBuildInfo.ProductVersionInfo;
            foreach (RemoteFileInfo fileInfo in remoteBuildInfo.FileInfos)
            {
                localBuildInfo.AddFileInfo(new LocalFileInfo(fileInfo.FilePath,
                    fileInfo.UncompressedHash, fileInfo.UncompressedSize));
            }
            return localBuildInfo;
        }

        private void Downloader_DownloadProgress(object sender, DownloadProgressEventArgs e)
        {
            UpdateProgressChanged?.Invoke(this, new UpdateProgressEventArgs(UpdateProgressState.DOWNLOADING,
                (int)(e.TotalBytesReceived / (double)e.TotalBytesToDownload * 100.0),
                (int)(e.BytesReceivedFromFile / (double)e.CurrentFileSize * 100.0), e.CurrentFilePath));
        }

        /// <summary>
        /// Gathers a list of files to download for the update.
        /// </summary>
        /// <param name="buildPath">The full path to the main application directory.</param>
        /// <param name="downloadDirectory">The full path to the temporary updater directory,
        /// including a directory separator character.</param>
        private List<RemoteFileInfo> GatherFilesToDownload(string buildPath,
            string downloadDirectory)
        {
            List<RemoteFileInfo> filesToDownload = new List<RemoteFileInfo>();

            // This could be multithreaded later on for faster processing,
            // calculating the SHA1 hashes can take a lot of time

            for (int i = 0; i < remoteBuildInfo.FileInfos.Count; i++)
            {
                var remoteFileInfo = remoteBuildInfo.FileInfos[i];

                UpdateProgressChanged?.Invoke(this, new UpdateProgressEventArgs(
                    UpdateProgressState.PREPARING, 0,
                    (int)(i / (double)remoteBuildInfo.FileInfos.Count * 100.0), string.Empty));

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

            // Go through the files in the download queue and check if a matching local file already exists.
            // If one exists, then simply copy the local file to the update directory.
            for (int i = 0; i < filesToDownload.Count; i++)
            {
                var remoteFileInfo = filesToDownload[i];

                LocalFileInfo localFileInfo = localBuildInfo.FileInfos.Find(l => HashHelper.ByteArraysMatch(HashHelper.ComputeHashForFile(buildPath + l.FilePath), remoteFileInfo.UncompressedHash));
                if (localFileInfo != null)
                {
                    // A matching local file exists, copy it

                    UpdaterLogger.Log("File " + remoteFileInfo.FilePath + " already exists as " + localFileInfo.FilePath + 
                        ", copying the local file and removing the remote file from the download queue.");
                    Directory.CreateDirectory(Path.GetDirectoryName(downloadDirectory + remoteFileInfo.FilePath));
                    File.Copy(buildPath + localFileInfo.FilePath, downloadDirectory + remoteFileInfo.FilePath);
                    filesToDownload.RemoveAt(i);
                    i--;
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

                if (filesToDownload.Find(fi => fi.GetFilePathWithCompression() == subPath) == null)
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
            if (!Directory.Exists(localBuildInfo.BuildPath + TEMPORARY_UPDATER_DIRECTORY))
                Directory.CreateDirectory(localBuildInfo.BuildPath + TEMPORARY_UPDATER_DIRECTORY);
        }

        private WebClient CreateWebClient()
        {
            return new WebClient()
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore),
                Encoding = Encoding.GetEncoding(1252)
            };
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
        /// <summary>
        /// The updater is gathering information on which files it needs to download.
        /// </summary>
        PREPARING,

        /// <summary>
        /// The updater is downloading and extracting files.
        /// </summary>
        DOWNLOADING
    }

    public class BuildOutdatedEventArgs : EventArgs
    {
        public BuildOutdatedEventArgs(string versionDisplayString,
            long estimatedUpdateSize)
        {
            VersionDisplayString = versionDisplayString;
            EstimatedUpdateSize = estimatedUpdateSize;
        }

        public string VersionDisplayString { get; private set; }
        public long EstimatedUpdateSize { get; private set; }
    }

    public class UpdateProgressEventArgs : EventArgs
    {
        public UpdateProgressEventArgs(UpdateProgressState state,
            int totalPercentage, int processPercentage, string processDescription)
        {
            State = state;
            TotalPercentage = totalPercentage;
            ProcessPercentage = processPercentage;
            ProcessDescription = processDescription;
        }


        public UpdateProgressState State { get; private set; }

        /// <summary>
        /// The progress percentage of the whole update session.
        /// </summary>
        public int TotalPercentage { get; private set; }

        /// <summary>
        /// The progress percentage of the current operation.
        /// </summary>
        public int ProcessPercentage { get; private set; }

        /// <summary>
        /// The description of the current operation.
        /// </summary>
        public string ProcessDescription { get; private set; }
    }

    public class UpdateFailureEventArgs : EventArgs
    {
        public UpdateFailureEventArgs(string message)
        {
            ErrorMessage = message;
        }

        public string ErrorMessage { get; private set; }
    }
}
