using System.Collections.Generic;

namespace Rampastring.Updater.CustomComponents
{
    /// <summary>
    /// Represents information on a locally installed custom component.
    /// </summary>
    public class InstalledCustomComponent
    {
        public InstalledCustomComponent(string id)
        {
            ID = id;
        }

        public string ID { get; }
        public List<string> Files { get; } = new List<string>();
    }
}
