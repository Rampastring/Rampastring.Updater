using Rampastring.Updater.BuildInfo;
using System.Collections.Generic;

namespace Rampastring.Updater.CustomComponents
{
    /// <summary>
    /// Represents a custom component available from the update server.
    /// </summary>
    public class RemoteCustomComponent
    {
        public RemoteCustomComponent(string id)
        {
            ID = id;
        }

        public string ID { get; }
        public List<RemoteFileInfo> FileInfos { get; } = new List<RemoteFileInfo>();
    }
}
