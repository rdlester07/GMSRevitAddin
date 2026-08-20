namespace BuildBunkSmallForm
{
    partial class BuildBunkSmallForm
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
            this.labelMainText = new System.Windows.Forms.Label();
            this.labelBuildBunk = new System.Windows.Forms.Label();
            this.textBoxBuildBunkNumber = new System.Windows.Forms.TextBox();
            this.buttonApply = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelMainText
            // 
            this.labelMainText.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelMainText.AutoSize = true;
            this.labelMainText.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelMainText.Location = new System.Drawing.Point(12, 9);
            this.labelMainText.Name = "labelMainText";
            this.labelMainText.Size = new System.Drawing.Size(159, 25);
            this.labelMainText.TabIndex = 0;
            this.labelMainText.Text = "Main Label text...";
            // 
            // labelBuildBunk
            // 
            this.labelBuildBunk.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelBuildBunk.AutoSize = true;
            this.labelBuildBunk.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelBuildBunk.Location = new System.Drawing.Point(12, 58);
            this.labelBuildBunk.Name = "labelBuildBunk";
            this.labelBuildBunk.Size = new System.Drawing.Size(103, 25);
            this.labelBuildBunk.TabIndex = 1;
            this.labelBuildBunk.Text = "_ Number:";
            // 
            // textBoxBuildBunkNumber
            // 
            this.textBoxBuildBunkNumber.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxBuildBunkNumber.Location = new System.Drawing.Point(165, 58);
            this.textBoxBuildBunkNumber.Name = "textBoxBuildBunkNumber";
            this.textBoxBuildBunkNumber.Size = new System.Drawing.Size(110, 30);
            this.textBoxBuildBunkNumber.TabIndex = 1;
            // 
            // buttonApply
            // 
            this.buttonApply.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonApply.Location = new System.Drawing.Point(180, 127);
            this.buttonApply.Name = "buttonApply";
            this.buttonApply.Size = new System.Drawing.Size(95, 40);
            this.buttonApply.TabIndex = 2;
            this.buttonApply.Text = "Apply";
            this.buttonApply.UseVisualStyleBackColor = true;
            this.buttonApply.Click += new System.EventHandler(this.buttonApply_Click);
            // 
            // BuildBunkSmallForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(287, 179);
            this.Controls.Add(this.buttonApply);
            this.Controls.Add(this.textBoxBuildBunkNumber);
            this.Controls.Add(this.labelBuildBunk);
            this.Controls.Add(this.labelMainText);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(309, 235);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(309, 235);
            this.Name = "BuildBunkSmallForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "BuildBunkSmallForm";
            this.Load += new System.EventHandler(this.BuildBunkSmallForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelMainText;
        private System.Windows.Forms.Label labelBuildBunk;
        private System.Windows.Forms.TextBox textBoxBuildBunkNumber;
        private System.Windows.Forms.Button buttonApply;
    }
}