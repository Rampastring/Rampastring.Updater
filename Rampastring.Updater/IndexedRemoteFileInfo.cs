using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    public class IndexedRemoteFileInfo
    {
        public IndexedRemoteFileInfo(RemoteFileInfo fileInfo, int index)
        {
            FileInfo = fileInfo;
            Index = index;
        }

        public RemoteFileInfo FileInfo { get; private set; }

        public int Index { get; private set; }
    }
}
