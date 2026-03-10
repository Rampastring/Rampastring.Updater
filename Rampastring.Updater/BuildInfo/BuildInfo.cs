using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Rampastring.Updater.BuildInfo
{
    /// <summary>
    /// A base class for build information.
    /// </summary>
    /// <typeparam name="T">A type that implements IFileInfo and has a default constructor.</typeparam>
    public abstract class BuildInfo<T> where T : IFileInfo, new()
    {
        private const string VERSION_SECTION = "Version";
        private const string COMPONENTS_SECTION = "Components";
        private const string FILES_SECTION = "Files";

        public BuildInfo()
        {
        }

        /// <summary>
        /// Gets information about the product version.
        /// </summary>
        public ProductVersionInfo ProductVersionInfo { get; set; } = new ProductVersionInfo();

        public List<Component<T>> Components { get; private set; }

        public List<T> GetFilesForAllComponents() => GetFiles(null);

        public List<T> GetFiles(IComponentInstallationInfoContainer installedComponents)
        {
            var list = new List<T>();

            foreach (var component in Components)
            {
                if (component.Optional && installedComponents != null && 
                    !installedComponents.IsComponentInstalled(component.InternalName))
                {
                    continue;
                }

                list.AddRange(component.FileInfos);
            }

            return list;
        }

        /// <summary>
        /// Parses build information from an INI file in the specified path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public void Parse(string filePath)
        {
            INIParse(new IniFile(filePath));
        }

        /// <summary>
        /// Parses build information from an INI file.
        /// </summary>
        /// <param name="iniFile">The INI file.</param>
        private void INIParse(IniFile iniFile)
        {
            var versionSection = iniFile.GetSection(VERSION_SECTION);

            if (versionSection == null)
                throw new ParseException("[" + VERSION_SECTION + "] section not found from " + iniFile.FileName);

            ProductVersionInfo = new ProductVersionInfo();
            ProductVersionInfo.Parse(versionSection);

            var componentsSection = iniFile.GetSection("Components");

            if (componentsSection == null)
                throw new ParseException("[" + COMPONENTS_SECTION + "] section not found from " + iniFile.FileName);

            foreach (var kvp in componentsSection.Keys)
            {
                string iniName = kvp.Value;

                var component = CreateComponent(iniFile, iniName);

                var filesSection = iniFile.GetSection(iniName + "." + FILES_SECTION);
                if (filesSection != null)
                    component.ReadFilesFromIniSection(filesSection);
            }

            SanityCheckComponents();
        }

        private Component<T> CreateComponent(IniFile iniFile, string iniName)
        {
            if (Components.Exists(c => c.InternalName.Equals(iniName, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Two components cannot share the same internal name.");

            var componentSection = iniFile.GetSection(iniName);

            var component = new Component<T>();
            component.ReadOptionsFromIniSection(componentSection);

            Components.Add(component);
            return component;
        }

        /// <summary>
        /// Verifies that components do not contain duplicate file entries.
        /// </summary>
        private void SanityCheckComponents()
        {
            var paths = new List<string>();

            foreach (var component in Components)
            {
                foreach (T fileInfo in component.FileInfos)
                {
                    if (paths.Exists(p => fileInfo.FilePath.Equals(p, StringComparison.OrdinalIgnoreCase)))
                        throw new ParseException("File is included in multiple components: " + fileInfo.FilePath);

                    paths.Add(fileInfo.FilePath);
                }
            }
        }

        /// <summary>
        /// Writes build information into the specified file path.
        /// Erases the file first if it already exists.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public void Write(string filePath)
        {
            File.Delete(filePath);

            var iniFile = new IniFile(filePath);

            var versionSection = new IniSection(VERSION_SECTION);
            iniFile.AddSection(versionSection);
            ProductVersionInfo.Write(versionSection);

            var componentsSection = new IniSection(COMPONENTS_SECTION);
            iniFile.AddSection(componentsSection);
            for (int i = 0; i < Components.Count; i++)
            {
                componentsSection.AddKey(i.ToString(CultureInfo.InvariantCulture), Components[i].InternalName);
            }

            for (int i = 0; i < Components.Count; i++)
            {
                var component = Components[i];
                iniFile.SetStringValue(component.InternalName, nameof(component.UIName), component.UIName);
                iniFile.SetBooleanValue(component.InternalName, nameof(component.Optional), component.Optional);

                var filesSection = new IniSection(component.InternalName + "." + FILES_SECTION);
                iniFile.AddSection(filesSection);
                component.WriteFilesToIniSection(filesSection);
            }

            iniFile.WriteIniFile();
        }
    }
}
