using Rampastring.Updater.BuildInfo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Rampastring.Updater
{
    public class Component<T> where T : IFileInfo, new()
    {
        public Component() { }

        public Component(string internalName, string uiName, bool optional)
        {
            InternalName = internalName;
            UIName = uiName;
            Optional = optional;
        }

        public string InternalName { get; private set; }
        public string UIName { get; private set; }
        public bool Optional { get; private set; }

        public List<T> FileInfos { get; } = new List<T>();

        public void AddFileInfo(T fileInfo)
        {
            FileInfos.Add(fileInfo);
        }

        public void ReadOptionsFromIniSection(IniSection iniSection)
        {
            InternalName = iniSection.SectionName;
            UIName = iniSection.GetStringValue(nameof(UIName), "Unnamed Component");
            Optional = iniSection.GetBooleanValue("Optional", false);
        }

        public void WriteOptionsToIniSection(IniSection iniSection)
        {
            if (iniSection.SectionName != InternalName)
                throw new ArgumentException($"INI section name must match component internal name. Expected: {InternalName}, actual: {iniSection.SectionName}");

            iniSection.SetStringValue(nameof(UIName), UIName);
            iniSection.SetBooleanValue(nameof(Optional), Optional);
        }

        public void ReadFilesFromIniSection(IniSection iniSection)
        {
            FileInfos.Clear();

            if (iniSection == null)
                return;

            foreach (var kvp in iniSection.Keys)
            {
                string[] parts = kvp.Value.Split(',');

                try
                {
                    var fileInfo = new T();
                    fileInfo.Parse(parts);

                    FileInfos.Add(fileInfo);
                }
                catch (FormatException) { UpdaterLogger.Log("FormatException when parsing file information, INI key " + kvp.Key + ", Component: " + InternalName); }
                catch (ParseException) { UpdaterLogger.Log("ParseException when parsing file information, INI key: " + kvp.Key + ", Component: " + InternalName); }
            }
        }

        public void WriteFilesToIniSection(IniSection iniSection)
        {
            for (int i = 0; i < FileInfos.Count; i++)
            {
                iniSection.SetStringValue(i.ToString(CultureInfo.InvariantCulture), FileInfos[i].GetString());
            }
        }

        /// <summary>
        /// Removes all locally installed files related to the component.
        /// </summary>
        public void Remove(string buildPath)
        {
            if (!Optional)
                throw new ArgumentException("Attempted to remove an a component that is not optional.");

            DoRemove(buildPath);
        }

        public void DoRemove(string buildPath)
        {
            foreach (var fileInfo in FileInfos)
            {
                File.Delete(buildPath + fileInfo.FilePath);
            }
        }
    }
}
