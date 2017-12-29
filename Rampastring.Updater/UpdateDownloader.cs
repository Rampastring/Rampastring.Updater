using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    /// <summary>
    /// Handles downloading files from an update mirror.
    /// </summary>
    class UpdateDownloader
    {
        private WebClient webClient;

        private List<RemoteFileInfo> filesToDownload;

        private int fileId = 0;

        private string downloadDirectory;

        private UpdateMirror updateMirror;

        private readonly object locker = new object();

        private long totalUpdateSize = 0;

        /// <summary>
        /// Starts downloading an update.
        /// </summary>
        /// <param name="buildPath">The local build path.</param>
        /// <param name="downloadDirectory">The download directory.</param>
        /// <param name="filesToDownload">A list of files to download.</param>
        /// <param name="updateMirror">The update mirror to use.</param>
        public void Update(string buildPath, string downloadDirectory,
            List<RemoteFileInfo> filesToDownload, UpdateMirror updateMirror)
        {
            this.filesToDownload = filesToDownload;
            this.downloadDirectory = downloadDirectory;
            this.updateMirror = updateMirror;

            foreach (var fileInfo in filesToDownload)
                totalUpdateSize += fileInfo.GetDownloadSize();

            webClient = new WebClient()
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore),
                Encoding = Encoding.GetEncoding(1252)
            };
            webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
            webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;

            DownloadNextFile();
        }

        /// <summary>
        /// Cancels the update.
        /// </summary>
        public void Cancel()
        {
            webClient.CancelAsync();
        }

        /// <summary>
        /// Downloads the next file in the list of files to download.
        /// </summary>
        private void DownloadNextFile()
        {
            RemoteFileInfo remoteFileInfo = filesToDownload[fileId];

            // Create the path if it doesn't already exist
            Directory.CreateDirectory(Path.GetDirectoryName(
                downloadDirectory + remoteFileInfo.FilePath));

            UpdaterLogger.Log("Starting download of file " + remoteFileInfo.FilePath);

            webClient.DownloadFileAsync(new Uri(updateMirror.URL + remoteFileInfo.GetDownloadFileName().Replace('\\', '/')),
                downloadDirectory + remoteFileInfo.GetDownloadFileName());
        }

        private void WebClient_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                UpdaterLogger.Log("The update was cancelled.");
                DisposeClient();
                // TODO raise an event
                return;
            }

            // TODO verify file integrity
            // if the file is compressed, uncompress it preferably in a separate
            // thread

            fileId++;
            DownloadNextFile();
        }

        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void DisposeClient()
        {
            webClient.DownloadProgressChanged -= WebClient_DownloadProgressChanged;
            webClient.DownloadFileCompleted -= WebClient_DownloadFileCompleted;
            webClient.Dispose();
        }
    }
}
