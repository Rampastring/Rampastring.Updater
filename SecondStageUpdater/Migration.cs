﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecondStageUpdater
{
    public class Migrations
    {
        public const string FileName = "Migrations.ini";

        public event EventHandler<LogEventArgs> LogEntry;

        private List<Migration> migrations = new List<Migration>();
        private string buildPath;

        public void ReadMigrations(string buildPath, string updaterDirectory)
        {
            this.buildPath = buildPath;

            if (!File.Exists(updaterDirectory + FileName))
                return;

            IniFile iniFile = new IniFile(updaterDirectory + FileName);
            foreach (string sectionName in iniFile.GetSections())
            {
                migrations.Add(new Migration(buildPath, iniFile.GetSection(sectionName)));
            }
        }

        public void PerformMigrations(int version)
        {
            migrations.ForEach(m => m.LogEntry += Log);

            foreach (Migration migration in migrations)
            {
                if (migration.MinimumVersion < version && migration.MaximumVersion > version)
                    migration.Perform(buildPath);
            }

            migrations.ForEach(m => m.LogEntry -= Log);
        }

        private void Log(object sender, LogEventArgs e)
        {
            LogEntry?.Invoke(sender, e);
        }
    }

    public class Migration
    {
        public Migration(string buildPath, IniSection iniSection)
        {
            this.buildPath = buildPath;
            this.iniSection = iniSection;
        }

        public event EventHandler<LogEventArgs> LogEntry;

        public string Name => iniSection.GetStringValue("Name", "Unknown migration");
        public int MinimumVersion => iniSection.GetIntValue("MinimumVersion", 0);
        public int MaximumVersion => iniSection.GetIntValue("MaximumVersion", int.MaxValue);

        private readonly string buildPath;
        private readonly IniSection iniSection;

        private void Log(string message)
        {
            LogEntry?.Invoke(this, new LogEventArgs(message));
        }

        public void Perform(string buildPath)
        {
            Log("Performing migration " + Name);

            int actionId = 0;
            
            while (true)
            {
                if (!iniSection.KeyExists(actionId.ToString()))
                    break;

                string value = iniSection.GetStringValue(actionId.ToString(), string.Empty);

                actionId++;

                int actionEndPoint = value.IndexOf(':');
                if (actionEndPoint == -1)
                    continue;

                string action = value.Substring(0, actionEndPoint).Trim();
                string param = value.Substring(actionEndPoint + 1).Trim();

                switch (action)
                {
                    case "DeleteFile":
                        DeleteFile(param);
                        break;
                }
            }
        }

        private void DeleteFile(string param)
        {
            File.Delete(buildPath + param);
        }
    }
}
