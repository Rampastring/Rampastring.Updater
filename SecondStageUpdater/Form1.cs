using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SecondStageUpdater
{
    public partial class Form1 : Form
    {
        private const string CONFIGURATION_FILE = "SecondStageUpdaterConfig.ini";
        private const string INI_SECTION = "SecondStageUpdater";
        private const string USER_INTERACE = "UserInterface";

        public Form1()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The path of the executable file (relative to the build path) to run
        /// after the files have been moved.
        /// </summary>
        private string targetExecutable;

        private string buildPath;

        private string productName;

        private FileMover fileMover;

        private void Form1_Load(object sender, EventArgs e)
        {
            IniFile configIni = new IniFile(Application.StartupPath +
                Path.DirectorySeparatorChar + CONFIGURATION_FILE);

            string basePath = Application.StartupPath + Path.DirectorySeparatorChar;

            productName = configIni.GetStringValue(USER_INTERACE, "ProductName", string.Empty);

            buildPath = basePath + configIni.GetStringValue(INI_SECTION, "BuildPath", string.Empty);
            targetExecutable = configIni.GetStringValue(INI_SECTION, "ProductExecutable", string.Empty);
            ProcessCheckMode checkMode = (ProcessCheckMode)Enum.Parse(typeof(ProcessCheckMode), 
                configIni.GetStringValue(INI_SECTION, "WaitMode", "Mutex"), true);
            string appGuid = configIni.GetStringValue(INI_SECTION, "TargetAppGuid", string.Empty);
            string processName = configIni.GetStringValue(INI_SECTION, "TargetProcessName", string.Empty);

            InitLogger();

            try
            {
                Text = configIni.GetStringValue(USER_INTERACE, "WindowTitle", string.Empty);

                string foreColorString = configIni.GetStringValue(USER_INTERACE, "ForeColor", string.Empty);
                if (!string.IsNullOrEmpty(foreColorString))
                {
                    int[] parts = Array.ConvertAll(foreColorString.Split(','), int.Parse);
                    ForeColor = Color.FromArgb(parts[0], parts[1], parts[2]);
                    listBox1.ForeColor = ForeColor;
                }

                string windowSizeString = configIni.GetStringValue(USER_INTERACE, "WindowSize", string.Empty);
                if (!string.IsNullOrEmpty(windowSizeString))
                {
                    int[] parts = Array.ConvertAll(windowSizeString.Split(','), int.Parse);
                    Size = new Size(parts[0], parts[1]);
                }

                ParseControlAttributes(configIni, "Label", lblDescription);

                string labelLocationString = configIni.GetStringValue(USER_INTERACE, "LabelLocation", string.Empty);
                if (!string.IsNullOrEmpty(labelLocationString))
                {
                    int[] parts = Array.ConvertAll(labelLocationString.Split(','), int.Parse);
                    lblDescription.Location = new Point(parts[0], parts[1]);
                }

                ParseControlAttributes(configIni, "ListBox", listBox1);

                string backgroundImage = configIni.GetStringValue(USER_INTERACE, "BackgroundImage", string.Empty);
                if (!string.IsNullOrWhiteSpace(backgroundImage) && File.Exists(basePath + backgroundImage))
                {
                    byte[] buffer = File.ReadAllBytes(basePath + backgroundImage);
                    var memoryStream = new MemoryStream(buffer);
                    // Image.FromStream needs the memory stream to be open
                    // when the image is actually drawn, so we don't
                    // dispose the stream
                    BackgroundImage = Image.FromStream(memoryStream);
                }

                string icon = configIni.GetStringValue(USER_INTERACE, "Icon", string.Empty);
                if (File.Exists(basePath + icon))
                    Icon = Icon.ExtractAssociatedIcon(basePath + icon);
            }
            catch (Exception ex)
            {
                listBox1.Items.Add("Parsing user interface information failed: " + ex.Message);
            }

            fileMover = new FileMover(buildPath, checkMode, appGuid, processName);
            fileMover.LogEntry += FileMover_LogEntry;
            fileMover.FilesMoved += FileMover_FilesMoved;
        }

        private void InitLogger()
        {
            string logFileName = "SecondStageUpdaterLog.txt";

            Logger.Initialize(buildPath, logFileName);
            Logger.WriteLogFile = true;

            try
            {
                File.Delete(Path.Combine(buildPath, logFileName));
            }
            catch (IOException) { }
        }

        private void FileMover_FilesMoved(object sender, EventArgs e)
        {
            BeginInvoke(new Action(Exit), null);
        }

        private void Exit()
        {
            listBox1.Items.Add("Launching " + buildPath + targetExecutable);
            Process.Start(buildPath + targetExecutable);
            FormClosing -= Form1_FormClosing;
            Close();
        }

        private void ParseControlAttributes(IniFile configIni, string controlKeyName, Control control)
        {
            control.Text = configIni.GetStringValue(USER_INTERACE, controlKeyName + "Text", lblDescription.Text);

            control.Font = new Font(configIni.GetStringValue(USER_INTERACE, controlKeyName + "Font", "Arial"),
                configIni.GetSingleValue(USER_INTERACE, controlKeyName + "FontSize", 10.0f),
                (FontStyle)Enum.Parse(typeof(FontStyle), configIni.GetStringValue(USER_INTERACE, controlKeyName + "FontStyle", "Regular")));
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            fileMover.Start();
        }

        private void FileMover_LogEntry(object sender, LogEventArgs e)
        {
            BeginInvoke(new Action<string>(LogEntry), e.Message);
        }

        private void LogEntry(string message)
        {
            listBox1.Items.Add(message);
            Logger.Log(message);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to close the updater? Doing so could break your " + productName + " installation!",
                "Close the updater?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                e.Cancel = true;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            fileMover.Abort();
        }

        private void GenericKeyPress(object sender, KeyPressEventArgs e)
        {
            fileMover.Proceed();
        }

        private void listBox1_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index > -1 && e.Index < listBox1.Items.Count)
            {
                e.ItemHeight = (int)e.Graphics.MeasureString(listBox1.Items[e.Index].ToString(),
                    listBox1.Font, listBox1.Width).Height;
            }
        }

        private void listBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            if (e.Index > -1 && e.Index < listBox1.Items.Count)
            {
                e.DrawFocusRectangle();
                e.Graphics.DrawString(listBox1.Items[e.Index].ToString(),
                    e.Font, new SolidBrush(e.ForeColor), e.Bounds);
            }
        }
    }
}
