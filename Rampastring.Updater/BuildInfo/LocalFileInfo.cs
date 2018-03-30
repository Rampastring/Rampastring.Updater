using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater.BuildInfo
{
    /// <summary>
    /// Represents a file on the local system.
    /// </summary>
    public class LocalFileInfo : IFileInfo
    {
        public LocalFileInfo() { }

        public LocalFileInfo(string filePath, byte[] hash, long size)
        {
            FilePath = filePath;
            Hash = hash;
            Size = size;
        }

        /// <summary>
        /// The relative path to the file.
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// The (SHA1) hash of the file.
        /// </summary>
        public byte[] Hash { get; private set; }

        /// <summary>
        /// The size of the file in bytes.
        /// </summary>
        public long Size { get; private set; }


        /// <summary>
        /// Parses a string array that represents a LocalFileInfo object
        /// and assigns the properties of this instance based on the given string array.
        /// </summary>
        /// <param name="parts">The string array.</param>
        public void Parse(string[] parts)
        {
            if (parts.Length != 3)
                throw new ParseException("The input string array has an invalid number of items.");

            LocalFileInfo fInfo = new LocalFileInfo();
            fInfo.FilePath = parts[0];
            fInfo.Hash = HashHelper.BytesFromHexString(parts[1]);
            fInfo.Size = long.Parse(parts[2]);
        }

        /// <summary>
        /// Gets a string representation of this object in a format
        /// that can be parsed by the static <see cref="Parse"/> method.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public string GetString()
        {
            return String.Join(",",
                FilePath,
                HashHelper.BytesToString(Hash),
                Size.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Checks whether this file information matches an actual file on the
        /// file system.
        /// </summary>
        /// <param name="buildPath">The base path of the build.</param>
        public bool MatchesActualFile(string buildPath)
        {
            return HashHelper.ByteArraysMatch(Hash,
                HashHelper.ComputeHashForFile(buildPath + FilePath));
        }
    }
}
