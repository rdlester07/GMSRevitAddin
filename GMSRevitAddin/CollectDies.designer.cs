namespace CollectDiesForm
{
    partial class CollectDiesForm
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
            this.checkedListBox1 = new System.Windows.Forms.CheckedListBox();
            this.button_Start = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.checkedListBox2 = new System.Windows.Forms.CheckedListBox();
            this.checkBoxCollectAll = new System.Windows.Forms.CheckBox();
            this.checkBoxCollectAllViewTypes = new System.Windows.Forms.CheckBox();
            this.checkBoxFasteners = new System.Windows.Forms.CheckBox();
            this.checkBoxExtrusions = new System.Windows.Forms.CheckBox();
            this.checkBoxComponents = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // checkedListBox1
            // 
            this.checkedListBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.checkedListBox1.CheckOnClick = true;
            this.checkedListBox1.FormattingEnabled = true;
            this.checkedListBox1.Location = new System.Drawing.Point(8, 29);
            this.checkedListBox1.Margin = new System.Windows.Forms.Padding(2);
            this.checkedListBox1.Name = "checkedListBox1";
            this.checkedListBox1.Size = new System.Drawing.Size(275, 394);
            this.checkedListBox1.TabIndex = 0;
            // 
            // button_Start
            // 
            this.button_Start.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button_Start.Location = new System.Drawing.Point(480, 483);
            this.button_Start.Margin = new System.Windows.Forms.Padding(2);
            this.button_Start.Name = "button_Start";
            this.button_Start.Size = new System.Drawing.Size(87, 29);
            this.button_Start.TabIndex = 1;
            this.button_Start.Text = "Start";
            this.button_Start.UseVisualStyleBackColor = true;
            this.button_Start.Click += new System.EventHandler(this.button_Start_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(5, 9);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(232, 17);
            this.label1.TabIndex = 1;
            this.label1.Text = "Select Set(s) to Collect:";
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(289, 9);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(167, 17);
            this.label2.TabIndex = 3;
            this.label2.Text = "Select Phase(s) to Apply:";
            // 
            // checkedListBox2
            // 
            this.checkedListBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.checkedListBox2.CheckOnClick = true;
            this.checkedListBox2.FormattingEnabled = true;
            this.checkedListBox2.Location = new System.Drawing.Point(292, 29);
            this.checkedListBox2.Margin = new System.Windows.Forms.Padding(2);
            this.checkedListBox2.Name = "checkedListBox2";
            this.checkedListBox2.Size = new System.Drawing.Size(275, 394);
            this.checkedListBox2.TabIndex = 2;
            // 
            // checkBoxFasteners
            //
            this.checkBoxFasteners.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkBoxFasteners.AutoSize = true;
            this.checkBoxFasteners.Location = new System.Drawing.Point(8, 434);
            this.checkBoxFasteners.Margin = new System.Windows.Forms.Padding(2);
            this.checkBoxFasteners.Name = "checkBoxFasteners";
            this.checkBoxFasteners.TabIndex = 6;
            this.checkBoxFasteners.Text = "Fasteners";
            this.checkBoxFasteners.UseVisualStyleBackColor = true;
            //
            // checkBoxExtrusions
            //
            this.checkBoxExtrusions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkBoxExtrusions.AutoSize = true;
            this.checkBoxExtrusions.Location = new System.Drawing.Point(108, 434);
            this.checkBoxExtrusions.Margin = new System.Windows.Forms.Padding(2);
            this.checkBoxExtrusions.Name = "checkBoxExtrusions";
            this.checkBoxExtrusions.TabIndex = 7;
            this.checkBoxExtrusions.Text = "Extrusions";
            this.checkBoxExtrusions.UseVisualStyleBackColor = true;
            //
            // checkBoxComponents
            //
            this.checkBoxComponents.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkBoxComponents.AutoSize = true;
            this.checkBoxComponents.Location = new System.Drawing.Point(208, 434);
            this.checkBoxComponents.Margin = new System.Windows.Forms.Padding(2);
            this.checkBoxComponents.Name = "checkBoxComponents";
            this.checkBoxComponents.TabIndex = 8;
            this.checkBoxComponents.Text = "Components";
            this.checkBoxComponents.UseVisualStyleBackColor = true;
            //
            // checkBoxCollectAll
            //
            this.checkBoxCollectAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkBoxCollectAll.AutoSize = true;
            this.checkBoxCollectAll.Location = new System.Drawing.Point(8, 457);
            this.checkBoxCollectAll.Margin = new System.Windows.Forms.Padding(2);
            this.checkBoxCollectAll.Name = "checkBoxCollectAll";
            this.checkBoxCollectAll.Size = new System.Drawing.Size(341, 17);
            this.checkBoxCollectAll.TabIndex = 9;
            this.checkBoxCollectAll.Text = "Include families that are not Fasteners, Components and Extrusions";
            this.checkBoxCollectAll.UseVisualStyleBackColor = true;
            //
            // checkBoxCollectAllViewTypes
            //
            this.checkBoxCollectAllViewTypes.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkBoxCollectAllViewTypes.AutoSize = true;
            this.checkBoxCollectAllViewTypes.Location = new System.Drawing.Point(8, 480);
            this.checkBoxCollectAllViewTypes.Margin = new System.Windows.Forms.Padding(2);
            this.checkBoxCollectAllViewTypes.Name = "checkBoxCollectAllViewTypes";
            this.checkBoxCollectAllViewTypes.Size = new System.Drawing.Size(274, 17);
            this.checkBoxCollectAllViewTypes.TabIndex = 10;
            this.checkBoxCollectAllViewTypes.Text = "Include Elevation, Floor Plan and Section view types";
            this.checkBoxCollectAllViewTypes.UseVisualStyleBackColor = true;
            this.checkBoxCollectAllViewTypes.Visible = false;
            //
            // CollectDiesForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(575, 520);
            this.Controls.Add(this.checkedListBox2);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.checkedListBox1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.checkBoxCollectAllViewTypes);
            this.Controls.Add(this.checkBoxCollectAll);
            this.Controls.Add(this.checkBoxFasteners);
            this.Controls.Add(this.checkBoxExtrusions);
            this.Controls.Add(this.checkBoxComponents);
            this.Controls.Add(this.button_Start);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CollectDiesForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Collect Detail Items";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.CollectDiesForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckedListBox checkedListBox1;
        private System.Windows.Forms.Button button_Start;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckedListBox checkedListBox2;
        private System.Windows.Forms.CheckBox checkBoxCollectAll;
        private System.Windows.Forms.CheckBox checkBoxCollectAllViewTypes;
        private System.Windows.Forms.CheckBox checkBoxFasteners;
        private System.Windows.Forms.CheckBox checkBoxExtrusions;
        private System.Windows.Forms.CheckBox checkBoxComponents;
    }
}