namespace ShowHideSheetSets
{
    partial class ShowHideSheetSets
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
            this.buttonStart = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.checkBoxElevations = new System.Windows.Forms.CheckBox();
            this.checkBoxFloorPlans = new System.Windows.Forms.CheckBox();
            this.checkBoxPiece = new System.Windows.Forms.CheckBox();
            this.checkBoxGasket = new System.Windows.Forms.CheckBox();
            this.checkBoxGlazing = new System.Windows.Forms.CheckBox();
            this.checkBoxCustomer = new System.Windows.Forms.CheckBox();
            this.checkBoxSubUnit = new System.Windows.Forms.CheckBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // checkedListBox1
            // 
            this.checkedListBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.checkedListBox1.CheckOnClick = true;
            this.checkedListBox1.FormattingEnabled = true;
            this.checkedListBox1.Location = new System.Drawing.Point(12, 58);
            this.checkedListBox1.Name = "checkedListBox1";
            this.checkedListBox1.Size = new System.Drawing.Size(522, 326);
            this.checkedListBox1.TabIndex = 0;
            // 
            // buttonStart
            // 
            this.buttonStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonStart.Location = new System.Drawing.Point(416, 462);
            this.buttonStart.Name = "buttonStart";
            this.buttonStart.Size = new System.Drawing.Size(117, 49);
            this.buttonStart.TabIndex = 1;
            this.buttonStart.Text = "Start";
            this.buttonStart.UseVisualStyleBackColor = true;
            this.buttonStart.Click += new System.EventHandler(this.buttonStart_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonCancel.Location = new System.Drawing.Point(12, 462);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(117, 49);
            this.buttonCancel.TabIndex = 2;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // checkBoxElevations
            // 
            this.checkBoxElevations.AutoSize = true;
            this.checkBoxElevations.Checked = true;
            this.checkBoxElevations.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxElevations.Location = new System.Drawing.Point(16, 20);
            this.checkBoxElevations.Name = "checkBoxElevations";
            this.checkBoxElevations.Size = new System.Drawing.Size(108, 24);
            this.checkBoxElevations.TabIndex = 3;
            this.checkBoxElevations.Text = "Elevations";
            this.checkBoxElevations.UseVisualStyleBackColor = true;
            this.checkBoxElevations.CheckedChanged += new System.EventHandler(this.checkBoxElevations_CheckedChanged);
            // 
            // checkBoxFloorPlans
            // 
            this.checkBoxFloorPlans.AutoSize = true;
            this.checkBoxFloorPlans.Location = new System.Drawing.Point(146, 20);
            this.checkBoxFloorPlans.Name = "checkBoxFloorPlans";
            this.checkBoxFloorPlans.Size = new System.Drawing.Size(114, 24);
            this.checkBoxFloorPlans.TabIndex = 4;
            this.checkBoxFloorPlans.Text = "Floor Plans";
            this.checkBoxFloorPlans.UseVisualStyleBackColor = true;
            // 
            // checkBoxPiece
            // 
            this.checkBoxPiece.AutoSize = true;
            this.checkBoxPiece.Checked = true;
            this.checkBoxPiece.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxPiece.Location = new System.Drawing.Point(3, 3);
            this.checkBoxPiece.Name = "checkBoxPiece";
            this.checkBoxPiece.Size = new System.Drawing.Size(74, 24);
            this.checkBoxPiece.TabIndex = 5;
            this.checkBoxPiece.Text = "Piece";
            this.checkBoxPiece.UseVisualStyleBackColor = true;
            // 
            // checkBoxGasket
            // 
            this.checkBoxGasket.AutoSize = true;
            this.checkBoxGasket.Checked = true;
            this.checkBoxGasket.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxGasket.Location = new System.Drawing.Point(83, 3);
            this.checkBoxGasket.Name = "checkBoxGasket";
            this.checkBoxGasket.Size = new System.Drawing.Size(87, 24);
            this.checkBoxGasket.TabIndex = 6;
            this.checkBoxGasket.Text = "Gasket";
            this.checkBoxGasket.UseVisualStyleBackColor = true;
            // 
            // checkBoxGlazing
            // 
            this.checkBoxGlazing.AutoSize = true;
            this.checkBoxGlazing.Checked = true;
            this.checkBoxGlazing.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxGlazing.Location = new System.Drawing.Point(176, 3);
            this.checkBoxGlazing.Name = "checkBoxGlazing";
            this.checkBoxGlazing.Size = new System.Drawing.Size(89, 24);
            this.checkBoxGlazing.TabIndex = 7;
            this.checkBoxGlazing.Text = "Glazing";
            this.checkBoxGlazing.UseVisualStyleBackColor = true;
            // 
            // checkBoxCustomer
            // 
            this.checkBoxCustomer.AutoSize = true;
            this.checkBoxCustomer.Checked = true;
            this.checkBoxCustomer.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxCustomer.Location = new System.Drawing.Point(271, 3);
            this.checkBoxCustomer.Name = "checkBoxCustomer";
            this.checkBoxCustomer.Size = new System.Drawing.Size(104, 24);
            this.checkBoxCustomer.TabIndex = 8;
            this.checkBoxCustomer.Text = "Customer";
            this.checkBoxCustomer.UseVisualStyleBackColor = true;
            // 
            // checkBoxSubUnit
            // 
            this.checkBoxSubUnit.AutoSize = true;
            this.checkBoxSubUnit.Checked = true;
            this.checkBoxSubUnit.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxSubUnit.Location = new System.Drawing.Point(381, 3);
            this.checkBoxSubUnit.Name = "checkBoxSubUnit";
            this.checkBoxSubUnit.Size = new System.Drawing.Size(93, 24);
            this.checkBoxSubUnit.TabIndex = 9;
            this.checkBoxSubUnit.Text = "SubUnit";
            this.checkBoxSubUnit.UseVisualStyleBackColor = true;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.checkBoxPiece);
            this.flowLayoutPanel1.Controls.Add(this.checkBoxGasket);
            this.flowLayoutPanel1.Controls.Add(this.checkBoxGlazing);
            this.flowLayoutPanel1.Controls.Add(this.checkBoxCustomer);
            this.flowLayoutPanel1.Controls.Add(this.checkBoxSubUnit);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(12, 409);
            this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(520, 35);
            this.flowLayoutPanel1.TabIndex = 10;
            // 
            // ShowHideSheetSets
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(544, 525);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Controls.Add(this.checkBoxFloorPlans);
            this.Controls.Add(this.checkBoxElevations);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonStart);
            this.Controls.Add(this.checkedListBox1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShowHideSheetSets";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Select Set(s)";
            this.Load += new System.EventHandler(this.ShowHideSheetSets_Load);
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckedListBox checkedListBox1;
        private System.Windows.Forms.Button buttonStart;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.CheckBox checkBoxElevations;
        private System.Windows.Forms.CheckBox checkBoxFloorPlans;
        private System.Windows.Forms.CheckBox checkBoxPiece;
        private System.Windows.Forms.CheckBox checkBoxGasket;
        private System.Windows.Forms.CheckBox checkBoxGlazing;
        private System.Windows.Forms.CheckBox checkBoxCustomer;
        private System.Windows.Forms.CheckBox checkBoxSubUnit;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
    }
}