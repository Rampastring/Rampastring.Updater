using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater.BuildInfo
{
    /// <summary>
    /// Represents a product version.
    /// </summary>
    public class ProductVersionInfo
    {
        private const string VERSION_NUMBER_STRING = "VersionNumber";
        private const string DISPLAYED_VERSION_STRING = "DisplayString";

        public ProductVersionInfo() { }

        public ProductVersionInfo(int versionNumber, string displayString)
        {
            VersionNumber = versionNumber;
            DisplayString = displayString;
        }

        /// <summary>
        /// The internal version number.
        /// </summary>
        public int VersionNumber { get; private set; }

        /// <summary>
        /// The version string for the user interface.
        /// </summary>
        public string DisplayString { get; private set; }

        /// <summary>
        /// Parses the product version information from an INI section.
        /// </summary>
        /// <param name="section">The INI section.</param>
        public void Parse(IniSection section)
        {
            VersionNumber = section.GetIntValue(VERSION_NUMBER_STRING, 0);
            DisplayString = section.GetStringValue(DISPLAYED_VERSION_STRING, string.Empty);
        }

        public void Write(IniSection section)
        {
            section.SetIntValue(VERSION_NUMBER_STRING, VersionNumber);
            section.SetStringValue(DISPLAYED_VERSION_STRING, DisplayString);
        }
    }
}
