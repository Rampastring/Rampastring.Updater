using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    /// <summary>
    /// Represents a file on the local system.
    /// </summary>
    class LocalFileInfo
    {
        public string FilePath { get; private set; }
        public byte[] Hash { get; private set; }
        public long Size { get; private set; }

        public static LocalFileInfo Parse(string[] parts)
        {
            // TODO implement
            return null;
        }
    }
}
