namespace SecondStageUpdater
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            listBox1 = new System.Windows.Forms.ListBox();
            lblDescription = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // listBox1
            // 
            listBox1.BackColor = System.Drawing.Color.Black;
            listBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            listBox1.Dock = System.Windows.Forms.DockStyle.Bottom;
            listBox1.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            listBox1.ForeColor = System.Drawing.Color.LimeGreen;
            listBox1.FormattingEnabled = true;
            listBox1.Location = new System.Drawing.Point(0, 74);
            listBox1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            listBox1.Name = "listBox1";
            listBox1.Size = new System.Drawing.Size(584, 287);
            listBox1.TabIndex = 0;
            listBox1.DrawItem += listBox1_DrawItem;
            listBox1.MeasureItem += listBox1_MeasureItem;
            listBox1.KeyPress += GenericKeyPress;
            // 
            // lblDescription
            // 
            lblDescription.AutoSize = true;
            lblDescription.BackColor = System.Drawing.Color.Transparent;
            lblDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            lblDescription.Location = new System.Drawing.Point(174, 31);
            lblDescription.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblDescription.Name = "lblDescription";
            lblDescription.Size = new System.Drawing.Size(220, 16);
            lblDescription.TabIndex = 1;
            lblDescription.Text = "Finishing update, please wait...";
            // 
            // Form1
            // 
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            AutoValidate = System.Windows.Forms.AutoValidate.EnablePreventFocusChange;
            BackColor = System.Drawing.Color.Black;
            ClientSize = new System.Drawing.Size(584, 361);
            Controls.Add(lblDescription);
            Controls.Add(listBox1);
            ForeColor = System.Drawing.Color.LimeGreen;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Form1";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Updater";
            FormClosing += Form1_FormClosing;
            FormClosed += Form1_FormClosed;
            Load += Form1_Load;
            Shown += Form1_Shown;
            KeyPress += GenericKeyPress;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Label lblDescription;
    }
}

