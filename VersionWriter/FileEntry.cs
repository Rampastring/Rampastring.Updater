using Rampastring.Updater;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VersionWriter
{
    /// <summary>
    /// A file entry for the version file generator.
    /// </summary>
    struct FileEntry
    {
        public FileEntry(string filePath, bool compressed)
        {
            FilePath = filePath;
            Compressed = compressed;
        }

        public static FileEntry Parse(string descriptor)
        {
            string[] parts = descriptor.Split(',');
            if (parts.Length != 2)
                throw new ParseException("Failed to parse file entry " + descriptor);

            bool compressed = Convert.ToBoolean(int.Parse(parts[1]));
            return new FileEntry(parts[0], compressed);
        }

        public string FilePath { get; private set; }
        public bool Compressed { get; private set; }

        public override string ToString()
        {
            return FilePath + "," + (Convert.ToInt32(Compressed)).ToString();
        }
    }
}
