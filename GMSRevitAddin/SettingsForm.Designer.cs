namespace SettingsForm
{
    partial class SettingsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            label1 = new System.Windows.Forms.Label();
            textBoxPMLocation = new System.Windows.Forms.TextBox();
            buttonBrowseForPM = new System.Windows.Forms.Button();
            openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            buttonClose = new System.Windows.Forms.Button();
            textBoxBuildInfo = new System.Windows.Forms.TextBox();
            groupBox1 = new System.Windows.Forms.GroupBox();
            groupBox2 = new System.Windows.Forms.GroupBox();
            numericUpDownFastenerScale = new System.Windows.Forms.NumericUpDown();
            label4 = new System.Windows.Forms.Label();
            checkBoxViewName = new System.Windows.Forms.CheckBox();
            checkBoxUnitLines = new System.Windows.Forms.CheckBox();
            checkBoxOpenNew = new System.Windows.Forms.CheckBox();
            checkBoxOpenOrig = new System.Windows.Forms.CheckBox();
            label2 = new System.Windows.Forms.Label();
            checkBoxWarningSuppress = new System.Windows.Forms.CheckBox();
            groupBox3 = new System.Windows.Forms.GroupBox();
            buttonPurge = new System.Windows.Forms.Button();
            label3 = new System.Windows.Forms.Label();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDownFastenerScale).BeginInit();
            groupBox3.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label1.Location = new System.Drawing.Point(9, 32);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(354, 29);
            label1.TabIndex = 0;
            label1.Text = "Project Parts Manager Location:";
            // 
            // textBoxPMLocation
            // 
            textBoxPMLocation.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            textBoxPMLocation.Location = new System.Drawing.Point(9, 63);
            textBoxPMLocation.Margin = new System.Windows.Forms.Padding(4);
            textBoxPMLocation.Name = "textBoxPMLocation";
            textBoxPMLocation.Size = new System.Drawing.Size(860, 30);
            textBoxPMLocation.TabIndex = 1;
            // 
            // buttonBrowseForPM
            // 
            buttonBrowseForPM.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            buttonBrowseForPM.Location = new System.Drawing.Point(880, 58);
            buttonBrowseForPM.Margin = new System.Windows.Forms.Padding(4);
            buttonBrowseForPM.Name = "buttonBrowseForPM";
            buttonBrowseForPM.Size = new System.Drawing.Size(112, 38);
            buttonBrowseForPM.TabIndex = 2;
            buttonBrowseForPM.Text = "Select";
            buttonBrowseForPM.UseVisualStyleBackColor = true;
            buttonBrowseForPM.Click += buttonBrowseForPM_Click;
            // 
            // openFileDialog1
            // 
            openFileDialog1.DefaultExt = "accdb";
            openFileDialog1.FileName = "openFileDialog1";
            openFileDialog1.Filter = "Access DB Files (.accdb)|*.accdb";
            openFileDialog1.InitialDirectory = "\\\\gmsfs01\\Common\\Projects";
            openFileDialog1.Title = "Select Project Parts Manager File";
            // 
            // buttonClose
            // 
            buttonClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            buttonClose.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            buttonClose.Location = new System.Drawing.Point(830, 594);
            buttonClose.Margin = new System.Windows.Forms.Padding(4);
            buttonClose.Name = "buttonClose";
            buttonClose.Size = new System.Drawing.Size(176, 40);
            buttonClose.TabIndex = 5;
            buttonClose.Text = "Save and Close";
            buttonClose.UseVisualStyleBackColor = true;
            buttonClose.Click += buttonClose_Click;
            //
            // textBoxBuildInfo
            //
            // Read-only, not tab-stopped — purely informational (see GmsVersion.BuildTimestamp),
            // not a field the user edits. Sits on the same row as buttonClose, to its left, since
            // that row otherwise has a lot of empty width and this form has no room to spare below.
            textBoxBuildInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            textBoxBuildInfo.Location = new System.Drawing.Point(12, 597);
            textBoxBuildInfo.Margin = new System.Windows.Forms.Padding(4);
            textBoxBuildInfo.Name = "textBoxBuildInfo";
            textBoxBuildInfo.ReadOnly = true;
            textBoxBuildInfo.Size = new System.Drawing.Size(700, 30);
            textBoxBuildInfo.TabStop = false;
            textBoxBuildInfo.Text = "GMSRevitAddin";
            //
            // groupBox1
            // 
            groupBox1.Controls.Add(textBoxPMLocation);
            groupBox1.Controls.Add(label1);
            groupBox1.Controls.Add(buttonBrowseForPM);
            groupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            groupBox1.Location = new System.Drawing.Point(12, 18);
            groupBox1.Margin = new System.Windows.Forms.Padding(4);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new System.Windows.Forms.Padding(4);
            groupBox1.Size = new System.Drawing.Size(1002, 123);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Project-wide Settings:";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(numericUpDownFastenerScale);
            groupBox2.Controls.Add(label4);
            groupBox2.Controls.Add(checkBoxViewName);
            groupBox2.Controls.Add(checkBoxUnitLines);
            groupBox2.Controls.Add(checkBoxOpenNew);
            groupBox2.Controls.Add(checkBoxOpenOrig);
            groupBox2.Controls.Add(label2);
            groupBox2.Controls.Add(checkBoxWarningSuppress);
            groupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            groupBox2.Location = new System.Drawing.Point(12, 152);
            groupBox2.Margin = new System.Windows.Forms.Padding(4);
            groupBox2.Name = "groupBox2";
            groupBox2.Padding = new System.Windows.Forms.Padding(4);
            groupBox2.Size = new System.Drawing.Size(1002, 304);
            groupBox2.TabIndex = 3;
            groupBox2.TabStop = false;
            groupBox2.Text = "Personal Settings:";
            // 
            // numericUpDownFastenerScale
            // 
            numericUpDownFastenerScale.Location = new System.Drawing.Point(231, 250);
            numericUpDownFastenerScale.Margin = new System.Windows.Forms.Padding(4);
            numericUpDownFastenerScale.Name = "numericUpDownFastenerScale";
            numericUpDownFastenerScale.Size = new System.Drawing.Size(110, 30);
            numericUpDownFastenerScale.TabIndex = 10;
            numericUpDownFastenerScale.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(9, 254);
            label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(209, 25);
            label4.TabIndex = 9;
            label4.Text = "Fastener Image Scale:";
            // 
            // checkBoxViewName
            // 
            checkBoxViewName.AutoSize = true;
            checkBoxViewName.Checked = true;
            checkBoxViewName.CheckState = System.Windows.Forms.CheckState.Checked;
            checkBoxViewName.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            checkBoxViewName.Location = new System.Drawing.Point(14, 202);
            checkBoxViewName.Margin = new System.Windows.Forms.Padding(4);
            checkBoxViewName.Name = "checkBoxViewName";
            checkBoxViewName.Size = new System.Drawing.Size(470, 33);
            checkBoxViewName.TabIndex = 8;
            checkBoxViewName.Text = "Enable automatic view name from sheet.";
            checkBoxViewName.UseVisualStyleBackColor = true;
            // 
            // checkBoxUnitLines
            // 
            checkBoxUnitLines.AutoSize = true;
            checkBoxUnitLines.Checked = true;
            checkBoxUnitLines.CheckState = System.Windows.Forms.CheckState.Checked;
            checkBoxUnitLines.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            checkBoxUnitLines.Location = new System.Drawing.Point(14, 160);
            checkBoxUnitLines.Margin = new System.Windows.Forms.Padding(4);
            checkBoxUnitLines.Name = "checkBoxUnitLines";
            checkBoxUnitLines.Size = new System.Drawing.Size(588, 33);
            checkBoxUnitLines.TabIndex = 7;
            checkBoxUnitLines.Text = "Auto-convert unit Control Lines to \"Not a Reference\"";
            checkBoxUnitLines.UseVisualStyleBackColor = true;
            checkBoxUnitLines.CheckedChanged += checkBoxUnitLines_CheckedChanged;
            // 
            // checkBoxOpenNew
            // 
            checkBoxOpenNew.AutoSize = true;
            checkBoxOpenNew.Checked = true;
            checkBoxOpenNew.CheckState = System.Windows.Forms.CheckState.Checked;
            checkBoxOpenNew.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            checkBoxOpenNew.Location = new System.Drawing.Point(273, 114);
            checkBoxOpenNew.Margin = new System.Windows.Forms.Padding(4);
            checkBoxOpenNew.Name = "checkBoxOpenNew";
            checkBoxOpenNew.Size = new System.Drawing.Size(335, 33);
            checkBoxOpenNew.TabIndex = 6;
            checkBoxOpenNew.Text = "Open New Duplicated View";
            checkBoxOpenNew.UseVisualStyleBackColor = true;
            // 
            // checkBoxOpenOrig
            // 
            checkBoxOpenOrig.AutoSize = true;
            checkBoxOpenOrig.Checked = true;
            checkBoxOpenOrig.CheckState = System.Windows.Forms.CheckState.Checked;
            checkBoxOpenOrig.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            checkBoxOpenOrig.Location = new System.Drawing.Point(36, 114);
            checkBoxOpenOrig.Margin = new System.Windows.Forms.Padding(4);
            checkBoxOpenOrig.Name = "checkBoxOpenOrig";
            checkBoxOpenOrig.Size = new System.Drawing.Size(249, 33);
            checkBoxOpenOrig.TabIndex = 5;
            checkBoxOpenOrig.Text = "Open Original View";
            checkBoxOpenOrig.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label2.Location = new System.Drawing.Point(9, 82);
            label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(374, 29);
            label2.TabIndex = 3;
            label2.Text = "When Duplicating a Drafting View:";
            // 
            // checkBoxWarningSuppress
            // 
            checkBoxWarningSuppress.AutoSize = true;
            checkBoxWarningSuppress.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            checkBoxWarningSuppress.Location = new System.Drawing.Point(14, 34);
            checkBoxWarningSuppress.Margin = new System.Windows.Forms.Padding(4);
            checkBoxWarningSuppress.Name = "checkBoxWarningSuppress";
            checkBoxWarningSuppress.Size = new System.Drawing.Size(595, 33);
            checkBoxWarningSuppress.TabIndex = 4;
            checkBoxWarningSuppress.Text = "Suppress Family Type Parameter Change Warnings";
            checkBoxWarningSuppress.UseVisualStyleBackColor = true;
            checkBoxWarningSuppress.CheckedChanged += checkBoxWarningSuppress_CheckedChanged;
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(buttonPurge);
            groupBox3.Controls.Add(label3);
            groupBox3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            groupBox3.Location = new System.Drawing.Point(12, 476);
            groupBox3.Margin = new System.Windows.Forms.Padding(4);
            groupBox3.Name = "groupBox3";
            groupBox3.Padding = new System.Windows.Forms.Padding(4);
            groupBox3.Size = new System.Drawing.Size(1002, 100);
            groupBox3.TabIndex = 12;
            groupBox3.TabStop = false;
            groupBox3.Text = "Commands:";
            // 
            // buttonPurge
            // 
            buttonPurge.Location = new System.Drawing.Point(382, 32);
            buttonPurge.Margin = new System.Windows.Forms.Padding(4);
            buttonPurge.Name = "buttonPurge";
            buttonPurge.Size = new System.Drawing.Size(159, 38);
            buttonPurge.TabIndex = 13;
            buttonPurge.Text = "Purge";
            buttonPurge.UseVisualStyleBackColor = true;
            buttonPurge.Click += buttonPurge_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label3.Location = new System.Drawing.Point(9, 38);
            label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(368, 29);
            label3.TabIndex = 12;
            label3.Text = "Purge Elements in Linked Model:";
            // 
            // SettingsForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            CancelButton = buttonClose;
            ClientSize = new System.Drawing.Size(1026, 639);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(buttonClose);
            Controls.Add(textBoxBuildInfo);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4);
            MaximizeBox = false;
            MaximumSize = new System.Drawing.Size(1060, 695);
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(1030, 665);
            Name = "SettingsForm";
            Padding = new System.Windows.Forms.Padding(8);
            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "GMS Settings";
            Load += SettingsForm_Load;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDownFastenerScale).EndInit();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxPMLocation;
        private System.Windows.Forms.Button buttonBrowseForPM;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Button buttonClose;
        private System.Windows.Forms.TextBox textBoxBuildInfo;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.CheckBox checkBoxWarningSuppress;
        private System.Windows.Forms.CheckBox checkBoxOpenNew;
        private System.Windows.Forms.CheckBox checkBoxOpenOrig;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox checkBoxUnitLines;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.CheckBox checkBoxViewName;
        private System.Windows.Forms.Button buttonPurge;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown numericUpDownFastenerScale;
        private System.Windows.Forms.Label label4;
    }
}