namespace DimensionNotesForm
{
    partial class DimensionNoteForm
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textBoxNotToScale = new System.Windows.Forms.TextBox();
            this.buttonNotToScale = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.buttonDoorFrameOpening = new System.Windows.Forms.Button();
            this.buttonMullionLength = new System.Windows.Forms.Button();
            this.buttonFrameWidth = new System.Windows.Forms.Button();
            this.buttonFrameHeight = new System.Windows.Forms.Button();
            this.buttonRO = new System.Windows.Forms.Button();
            this.buttonRef = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.buttonNotch = new System.Windows.Forms.Button();
            this.buttonGlass = new System.Windows.Forms.Button();
            this.buttonUnitHeight = new System.Windows.Forms.Button();
            this.buttonDLO = new System.Windows.Forms.Button();
            this.buttonUnitWidth = new System.Windows.Forms.Button();
            this.buttonMullionLength2 = new System.Windows.Forms.Button();
            this.buttonMod = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.textBoxCustom = new System.Windows.Forms.TextBox();
            this.buttonApply = new System.Windows.Forms.Button();
            this.buttonReset = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.textBoxNotToScale);
            this.groupBox1.Controls.Add(this.buttonNotToScale);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.buttonDoorFrameOpening);
            this.groupBox1.Controls.Add(this.buttonMullionLength);
            this.groupBox1.Controls.Add(this.buttonFrameWidth);
            this.groupBox1.Controls.Add(this.buttonFrameHeight);
            this.groupBox1.Controls.Add(this.buttonRO);
            this.groupBox1.Controls.Add(this.buttonRef);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(8, 8);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox1.Size = new System.Drawing.Size(209, 314);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Standard Drawings";
            // 
            // textBoxNotToScale
            // 
            this.textBoxNotToScale.Location = new System.Drawing.Point(8, 249);
            this.textBoxNotToScale.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBoxNotToScale.Name = "textBoxNotToScale";
            this.textBoxNotToScale.Size = new System.Drawing.Size(193, 23);
            this.textBoxNotToScale.TabIndex = 8;
            this.textBoxNotToScale.Enter += new System.EventHandler(this.textBoxNotToScale_FocusEnter);
            this.textBoxNotToScale.Leave += new System.EventHandler(this.textBoxNotToScale_FocusLeave);
            // 
            // buttonNotToScale
            // 
            this.buttonNotToScale.Location = new System.Drawing.Point(4, 275);
            this.buttonNotToScale.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonNotToScale.Name = "buttonNotToScale";
            this.buttonNotToScale.Size = new System.Drawing.Size(200, 27);
            this.buttonNotToScale.TabIndex = 9;
            this.buttonNotToScale.Text = "NOT TO SCALE";
            this.buttonNotToScale.UseVisualStyleBackColor = true;
            this.buttonNotToScale.Click += new System.EventHandler(this.buttonNotToScale_Click);
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(5, 211);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(201, 36);
            this.label1.TabIndex = 7;
            this.label1.Text = "Text to override dimension in \"Not To Scale\" dimension:";
            // 
            // buttonDoorFrameOpening
            // 
            this.buttonDoorFrameOpening.Location = new System.Drawing.Point(4, 180);
            this.buttonDoorFrameOpening.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonDoorFrameOpening.Name = "buttonDoorFrameOpening";
            this.buttonDoorFrameOpening.Size = new System.Drawing.Size(200, 27);
            this.buttonDoorFrameOpening.TabIndex = 7;
            this.buttonDoorFrameOpening.Text = "DOOR FRAME OPENING";
            this.buttonDoorFrameOpening.UseVisualStyleBackColor = true;
            this.buttonDoorFrameOpening.Click += new System.EventHandler(this.buttonDoorFrameOpening_Click);
            // 
            // buttonMullionLength
            // 
            this.buttonMullionLength.Location = new System.Drawing.Point(4, 149);
            this.buttonMullionLength.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonMullionLength.Name = "buttonMullionLength";
            this.buttonMullionLength.Size = new System.Drawing.Size(200, 27);
            this.buttonMullionLength.TabIndex = 6;
            this.buttonMullionLength.Text = "MULLION LENGTH";
            this.buttonMullionLength.UseVisualStyleBackColor = true;
            this.buttonMullionLength.Click += new System.EventHandler(this.buttonMullionLength_Click);
            // 
            // buttonFrameWidth
            // 
            this.buttonFrameWidth.Location = new System.Drawing.Point(4, 119);
            this.buttonFrameWidth.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonFrameWidth.Name = "buttonFrameWidth";
            this.buttonFrameWidth.Size = new System.Drawing.Size(200, 27);
            this.buttonFrameWidth.TabIndex = 5;
            this.buttonFrameWidth.Text = "FRAME WIDTH";
            this.buttonFrameWidth.UseVisualStyleBackColor = true;
            this.buttonFrameWidth.Click += new System.EventHandler(this.buttonFrameWidth_Click);
            // 
            // buttonFrameHeight
            // 
            this.buttonFrameHeight.Location = new System.Drawing.Point(4, 88);
            this.buttonFrameHeight.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonFrameHeight.Name = "buttonFrameHeight";
            this.buttonFrameHeight.Size = new System.Drawing.Size(200, 27);
            this.buttonFrameHeight.TabIndex = 4;
            this.buttonFrameHeight.Text = "FRAME HEIGHT";
            this.buttonFrameHeight.UseVisualStyleBackColor = true;
            this.buttonFrameHeight.Click += new System.EventHandler(this.buttonFrameHeight_Click);
            // 
            // buttonRO
            // 
            this.buttonRO.Location = new System.Drawing.Point(4, 57);
            this.buttonRO.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonRO.Name = "buttonRO";
            this.buttonRO.Size = new System.Drawing.Size(200, 27);
            this.buttonRO.TabIndex = 3;
            this.buttonRO.Text = "ROUGH OPENING";
            this.buttonRO.UseVisualStyleBackColor = true;
            this.buttonRO.Click += new System.EventHandler(this.buttonRO_Click);
            // 
            // buttonRef
            // 
            this.buttonRef.Location = new System.Drawing.Point(4, 27);
            this.buttonRef.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonRef.Name = "buttonRef";
            this.buttonRef.Size = new System.Drawing.Size(200, 27);
            this.buttonRef.TabIndex = 2;
            this.buttonRef.Text = "REFERENCE (REF)";
            this.buttonRef.UseVisualStyleBackColor = true;
            this.buttonRef.Click += new System.EventHandler(this.buttonRef_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.buttonNotch);
            this.groupBox2.Controls.Add(this.buttonGlass);
            this.groupBox2.Controls.Add(this.buttonUnitHeight);
            this.groupBox2.Controls.Add(this.buttonDLO);
            this.groupBox2.Controls.Add(this.buttonUnitWidth);
            this.groupBox2.Controls.Add(this.buttonMullionLength2);
            this.groupBox2.Controls.Add(this.buttonMod);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(233, 8);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox2.Size = new System.Drawing.Size(209, 247);
            this.groupBox2.TabIndex = 0;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Unit Drawings";
            // 
            // buttonNotch
            // 
            this.buttonNotch.Location = new System.Drawing.Point(4, 211);
            this.buttonNotch.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonNotch.Name = "buttonNotch";
            this.buttonNotch.Size = new System.Drawing.Size(200, 27);
            this.buttonNotch.TabIndex = 16;
            this.buttonNotch.Text = "NOTCH";
            this.buttonNotch.UseVisualStyleBackColor = true;
            this.buttonNotch.Click += new System.EventHandler(this.buttonNotch_Click);
            // 
            // buttonGlass
            // 
            this.buttonGlass.Location = new System.Drawing.Point(4, 180);
            this.buttonGlass.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonGlass.Name = "buttonGlass";
            this.buttonGlass.Size = new System.Drawing.Size(200, 27);
            this.buttonGlass.TabIndex = 15;
            this.buttonGlass.Text = "GLASS";
            this.buttonGlass.UseVisualStyleBackColor = true;
            this.buttonGlass.Click += new System.EventHandler(this.buttonGlass_Click);
            // 
            // buttonUnitHeight
            // 
            this.buttonUnitHeight.Location = new System.Drawing.Point(4, 27);
            this.buttonUnitHeight.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonUnitHeight.Name = "buttonUnitHeight";
            this.buttonUnitHeight.Size = new System.Drawing.Size(200, 27);
            this.buttonUnitHeight.TabIndex = 10;
            this.buttonUnitHeight.Text = "UNIT HEIGHT";
            this.buttonUnitHeight.UseVisualStyleBackColor = true;
            this.buttonUnitHeight.Click += new System.EventHandler(this.buttonUnitHeight_Click);
            // 
            // buttonDLO
            // 
            this.buttonDLO.Location = new System.Drawing.Point(4, 149);
            this.buttonDLO.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonDLO.Name = "buttonDLO";
            this.buttonDLO.Size = new System.Drawing.Size(200, 27);
            this.buttonDLO.TabIndex = 14;
            this.buttonDLO.Text = "DAYLIGHT OPENING (DLO)";
            this.buttonDLO.UseVisualStyleBackColor = true;
            this.buttonDLO.Click += new System.EventHandler(this.buttonDLO_Click);
            // 
            // buttonUnitWidth
            // 
            this.buttonUnitWidth.Location = new System.Drawing.Point(4, 57);
            this.buttonUnitWidth.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonUnitWidth.Name = "buttonUnitWidth";
            this.buttonUnitWidth.Size = new System.Drawing.Size(200, 27);
            this.buttonUnitWidth.TabIndex = 11;
            this.buttonUnitWidth.Text = "UNIT WIDTH";
            this.buttonUnitWidth.UseVisualStyleBackColor = true;
            this.buttonUnitWidth.Click += new System.EventHandler(this.buttonUnitWidth_Click);
            // 
            // buttonMullionLength2
            // 
            this.buttonMullionLength2.Location = new System.Drawing.Point(4, 119);
            this.buttonMullionLength2.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonMullionLength2.Name = "buttonMullionLength2";
            this.buttonMullionLength2.Size = new System.Drawing.Size(200, 27);
            this.buttonMullionLength2.TabIndex = 13;
            this.buttonMullionLength2.Text = "MULLION LENGTH";
            this.buttonMullionLength2.UseVisualStyleBackColor = true;
            this.buttonMullionLength2.Click += new System.EventHandler(this.buttonMullionLength2_Click);
            // 
            // buttonMod
            // 
            this.buttonMod.Location = new System.Drawing.Point(4, 88);
            this.buttonMod.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonMod.Name = "buttonMod";
            this.buttonMod.Size = new System.Drawing.Size(200, 27);
            this.buttonMod.TabIndex = 12;
            this.buttonMod.Text = "MODULE (MOD)";
            this.buttonMod.UseVisualStyleBackColor = true;
            this.buttonMod.Click += new System.EventHandler(this.buttonMod_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.buttonReset);
            this.groupBox3.Controls.Add(this.textBoxCustom);
            this.groupBox3.Controls.Add(this.buttonApply);
            this.groupBox3.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox3.Location = new System.Drawing.Point(8, 337);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.groupBox3.Size = new System.Drawing.Size(434, 89);
            this.groupBox3.TabIndex = 0;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Custom Text:";
            // 
            // textBoxCustom
            // 
            this.textBoxCustom.Location = new System.Drawing.Point(8, 24);
            this.textBoxCustom.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBoxCustom.Name = "textBoxCustom";
            this.textBoxCustom.Size = new System.Drawing.Size(422, 23);
            this.textBoxCustom.TabIndex = 1;
            // 
            // buttonApply
            // 
            this.buttonApply.Location = new System.Drawing.Point(229, 53);
            this.buttonApply.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonApply.Name = "buttonApply";
            this.buttonApply.Size = new System.Drawing.Size(200, 27);
            this.buttonApply.TabIndex = 17;
            this.buttonApply.Text = "APPLY";
            this.buttonApply.UseVisualStyleBackColor = true;
            this.buttonApply.Click += new System.EventHandler(this.buttonApply_Click);
            // 
            // buttonReset
            // 
            this.buttonReset.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.buttonReset.Location = new System.Drawing.Point(8, 53);
            this.buttonReset.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonReset.Name = "buttonReset";
            this.buttonReset.Size = new System.Drawing.Size(200, 27);
            this.buttonReset.TabIndex = 17;
            this.buttonReset.Text = "RESET OVERRIDES";
            this.buttonReset.UseVisualStyleBackColor = true;
            this.buttonReset.Click += new System.EventHandler(this.buttonReset_Click);
            // 
            // DimensionNoteForm
            // 
            this.AcceptButton = this.buttonApply;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(453, 447);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(469, 486);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(469, 486);
            this.Name = "DimensionNoteForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Dimension Note";
            this.Load += new System.EventHandler(this.DimensionNoteForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox textBoxNotToScale;
        private System.Windows.Forms.Button buttonNotToScale;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonDoorFrameOpening;
        private System.Windows.Forms.Button buttonMullionLength;
        private System.Windows.Forms.Button buttonFrameWidth;
        private System.Windows.Forms.Button buttonFrameHeight;
        private System.Windows.Forms.Button buttonRO;
        private System.Windows.Forms.Button buttonRef;
        private System.Windows.Forms.Button buttonNotch;
        private System.Windows.Forms.Button buttonGlass;
        private System.Windows.Forms.Button buttonUnitHeight;
        private System.Windows.Forms.Button buttonDLO;
        private System.Windows.Forms.Button buttonUnitWidth;
        private System.Windows.Forms.Button buttonMullionLength2;
        private System.Windows.Forms.Button buttonMod;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.TextBox textBoxCustom;
        private System.Windows.Forms.Button buttonApply;
        private System.Windows.Forms.Button buttonReset;
    }
}