using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SecondStageUpdater
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            // DpiUnawareGdiScaled is not available on Windows 7
            Application.SetHighDpiMode(Environment.OSVersion.Version.Major >= 10
                ? HighDpiMode.DpiUnawareGdiScaled
                : HighDpiMode.DpiUnaware);
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
