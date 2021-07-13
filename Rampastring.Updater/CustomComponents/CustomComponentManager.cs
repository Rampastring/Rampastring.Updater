using System.Collections.Generic;

namespace Rampastring.Updater.CustomComponents
{
    public class CustomComponentManager
    {
        public CustomComponentManager(string buildPath)
        {
            this.buildPath = buildPath;
        }

        public List<InstalledCustomComponent> InstalledCustomComponents { get; } = new List<InstalledCustomComponent>();
        public List<RemoteCustomComponent> RemoteCustomComponents { get; } = new List<RemoteCustomComponent>();

        private readonly string buildPath;
    }
}
