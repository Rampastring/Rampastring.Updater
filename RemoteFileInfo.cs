using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DTAUpdater2
{
    /// <summary>
    /// Represents a file on the update server.
    /// </summary>
    class RemoteFileInfo
    {
        public string FilePath { get; private set; }
        public byte[] Hash { get; private set; }
        public bool Compressed { get; private set; }
    }
}
