using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    public class DownloadProgressEventArgs : EventArgs
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
