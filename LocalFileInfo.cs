using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DTAUpdater2
{
    /// <summary>
    /// Represents a file on the local system.
    /// </summary>
    class LocalFileInfo
    {
        public string FilePath { get; private set; }
        public byte[] Hash { get; private set; }
    }
}
