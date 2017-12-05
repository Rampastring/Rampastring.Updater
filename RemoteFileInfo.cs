using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    /// <summary>
    /// Represents a file on the update server.
    /// </summary>
    class RemoteFileInfo
    {
        public string FilePath { get; private set; }
        public byte[] UncompressedHash { get; private set; }
        public byte[] CompressedHash { get; private set; }
        public bool Compressed { get; private set; }
        public long CompressedSize { get; private set; }
        public long UncompressedSize { get; private set; }

        public static RemoteFileInfo Parse(string[] parts)
        {
            var fileInfo = new RemoteFileInfo();
            fileInfo.FilePath = parts[0];
            // TODO
            return fileInfo;
        }
    }
}
