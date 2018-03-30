using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater.BuildInfo
{
    /// <summary>
    /// Represents a file on the update server.
    /// </summary>
    public class RemoteFileInfo : IFileInfo
    {
        public RemoteFileInfo() { }

        public RemoteFileInfo(string filePath, byte[] uncompressedHash,
            long uncompressedSize, bool compressed,
            byte[] compressedHash = null, long compressedSize = 0)
        {
            FilePath = filePath;
            UncompressedHash = uncompressedHash;
            UncompressedSize = uncompressedSize;
            Compressed = compressed;
            CompressedHash = compressedHash;
            CompressedSize = compressedSize;
        }

        public string FilePath { get; private set; }
        public byte[] UncompressedHash { get; private set; }
        public long UncompressedSize { get; private set; }
        public bool Compressed { get; private set; }
        public byte[] CompressedHash { get; private set; }
        public long CompressedSize { get; private set; }

        /// <summary>
        /// Parses a string array that represents a RemoteFileInfo object
        /// and assigns the properties of this instance based on the given string array.
        /// </summary>
        /// <param name="parts">The string array.</param>
        public void Parse(string[] parts)
        {
            if (parts.Length < 4)
                throw new ParseException("Invalid size for parts: " + parts.Length);

            var fileInfo = new RemoteFileInfo();
            fileInfo.FilePath = parts[0];
            fileInfo.UncompressedHash = HashHelper.BytesFromHexString(parts[1]);
            fileInfo.UncompressedSize = long.Parse(parts[2], CultureInfo.InvariantCulture);
            bool compressed = int.Parse(parts[3]) > 0;

            if (compressed)
            {
                if (parts.Length != 6)
                    throw new ParseException("Invalid size for parts: " + parts.Length);

                fileInfo.CompressedHash = HashHelper.BytesFromHexString(parts[4]);
                fileInfo.CompressedSize = long.Parse(parts[5], CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Gets a string representation of this object in a format
        /// that can be parsed by the static <see cref="Parse"/> method.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public string GetString()
        {
            if (Compressed)
            {
                return String.Join(",",
                FilePath,
                HashHelper.BytesToString(UncompressedHash),
                UncompressedSize.ToString(CultureInfo.InvariantCulture),
                Convert.ToInt32(Compressed).ToString());
            }

            return String.Join(",",
                FilePath,
                HashHelper.BytesToString(UncompressedHash),
                UncompressedSize.ToString(CultureInfo.InvariantCulture),
                Convert.ToInt32(Compressed).ToString(),
                HashHelper.BytesToString(CompressedHash),
                CompressedSize.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Returns the size of the file for downloading.
        /// (UncompressedSize for uncompressed files, CompressedSize for compressed files)
        /// </summary>
        public long GetDownloadSize()
        {
            long size = Compressed ? CompressedSize : UncompressedSize;
            return size;
        }

        /// <summary>
        /// Returns the expected hash of this file for downloading.
        /// </summary>
        public byte[] GetDownloadHash()
        {
            byte[] hash = Compressed ? CompressedHash : UncompressedHash;
            return hash;
        }

        /// <summary>
        /// Returns the name of this file for downloading.
        /// </summary>
        public string GetDownloadFileName()
        {
            if (Compressed)
                return FilePath + ".lzma";

            return FilePath;
        }
    }
}
