using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        }

        /// <summary>
        /// Raised when decompressing a compressed file fails.
        /// </summary>
        public event EventHandler<FilePathEventArgs> DecompressionFailed;

        /// <summary>
        /// Raised when a downloaded file didn't pass the verification check
        /// (meaning its hash is different from what the version information claims).
        /// </summary>
        public event EventHandler<FilePathEventArgs> VerificationFailed;

        /// <summary>
        /// Raised when the thread has succesfully finished decompressing
        /// and verifying files.
        /// </summary>
        public event EventHandler Completed;

        private string downloadDirectory;

        private List<RemoteFileInfo> filesToCheck = new List<RemoteFileInfo>();

        private readonly object locker = new object();

        /// <summary>
        /// Adds the specified file to the decompress-and-verify queue.
        /// If the file is not compressed, it'll only be verified.
        /// </summary>
        /// <param name="remoteFileInfo">The file.</param>
        public void DecompressAndVerifyFile(RemoteFileInfo remoteFileInfo)
        {
            throw new NotImplementedException();
        }
    }

    class FilePathEventArgs : EventArgs
    {
        public FilePathEventArgs(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; private set; }
    }
}
