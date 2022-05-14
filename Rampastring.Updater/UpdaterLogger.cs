using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    /// <summary>
    /// A static class for logging updater events.
    /// </summary>
    public static class UpdaterLogger
    {
        private static bool enableLogging = false;
        private static string logFilePath = string.Empty;

        private static readonly object locker = new object();

        /// <summary>
        /// Enables logging for this session.
        /// </summary>
        /// <param name="logFilePath">The path of the log file.</param>
        public static void EnableLogging(string logFilePath)
        {
            UpdaterLogger.logFilePath = logFilePath;
            File.Delete(logFilePath);
            enableLogging = true;
            Log("Logging enabled");
        }

        /// <summary>
        /// Writes a string to the logfile.
        /// </summary>
        /// <param name="info">The information to write.</param>
        public static void Log(string info)
        {
            lock (locker)
            {
                if (enableLogging)
                {
                    try
                    {
                        DateTime now = DateTime.Now;

                        StringBuilder sb = new StringBuilder();
                        sb.Append(now.ToString("dd.MM. HH:mm:ss.fff"));
                        sb.Append("    ");
                        sb.Append(info);

                        Console.WriteLine(sb.ToString());

                        StreamWriter sw = new StreamWriter(logFilePath, true);
                        sw.WriteLine(sb.ToString());
                        sw.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to write to log: " + ex.Message);
                    }
                }
            }
        }
    }
}
