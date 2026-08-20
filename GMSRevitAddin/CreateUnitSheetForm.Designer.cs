namespace CreateUnitSheetForm
{
    partial class CreateUnitSheetForm
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
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxNewUnitNumber = new System.Windows.Forms.TextBox();
            this.labelUnitStatus = new System.Windows.Forms.Label();
            this.checkBoxDuplicate = new System.Windows.Forms.CheckBox();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.labelDuplicateHint = new System.Windows.Forms.Label();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOK = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(20, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(150, 20);
            this.label1.TabIndex = 0;
            this.label1.Text = "New unit number";
            //
            // textBoxNewUnitNumber
            //
            // Single full-width field (rather than the old label-left/field-right grid) so the status
            // line below it can show the placeholder hint or an inline validation error without being
            // squeezed into a narrow right-hand column.
            this.textBoxNewUnitNumber.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.textBoxNewUnitNumber.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxNewUnitNumber.Location = new System.Drawing.Point(20, 50);
            this.textBoxNewUnitNumber.Name = "textBoxNewUnitNumber";
            this.textBoxNewUnitNumber.Size = new System.Drawing.Size(460, 30);
            this.textBoxNewUnitNumber.TabIndex = 1;
            this.textBoxNewUnitNumber.Text = "EXAMPLE: U-001";
            this.textBoxNewUnitNumber.TextChanged += new System.EventHandler(this.textBoxNewUnitNumber_TextChanged);
            this.textBoxNewUnitNumber.Enter += new System.EventHandler(this.textBoxNewUnitNumber_FocusEnter);
            this.textBoxNewUnitNumber.Leave += new System.EventHandler(this.textBoxNewUnitNumber_FocusLeave);
            //
            // labelUnitStatus
            //
            // Doubles as the format hint (muted) and, once the user types something invalid, an inline
            // error (DarkTheme.CurrentErrorText) — replaces the old pattern of a blocking ShowError
            // popup for routine format mistakes. See UpdateUnitNumberValidation.
            this.labelUnitStatus.AutoSize = true;
            this.labelUnitStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelUnitStatus.Location = new System.Drawing.Point(20, 89);
            this.labelUnitStatus.MaximumSize = new System.Drawing.Size(460, 0);
            this.labelUnitStatus.Name = "labelUnitStatus";
            this.labelUnitStatus.Size = new System.Drawing.Size(240, 17);
            this.labelUnitStatus.TabIndex = 2;
            this.labelUnitStatus.Text = "Format: contains \"U-\", no spaces or braces.";
            //
            // checkBoxDuplicate
            //
            // Replaces the old "blank combo box = don't duplicate" convention: whether to duplicate is
            // now an explicit, unambiguous control instead of being inferred from empty text.
            this.checkBoxDuplicate.AutoSize = true;
            this.checkBoxDuplicate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkBoxDuplicate.Location = new System.Drawing.Point(20, 128);
            this.checkBoxDuplicate.Name = "checkBoxDuplicate";
            this.checkBoxDuplicate.Size = new System.Drawing.Size(220, 24);
            this.checkBoxDuplicate.TabIndex = 3;
            this.checkBoxDuplicate.Text = "Duplicate an existing unit";
            this.checkBoxDuplicate.UseVisualStyleBackColor = true;
            this.checkBoxDuplicate.CheckedChanged += new System.EventHandler(this.checkBoxDuplicate_CheckedChanged);
            //
            // comboBox1
            //
            // Editable (DropDown, not DropDownList) so the list can be filtered by typing. The native
            // AutoComplete* settings are deliberately NOT set: their suggestion popup would compete
            // with the filtered dropdown that comboBox1_TextChanged drives. Starts disabled — enabled
            // only while checkBoxDuplicate is checked.
            this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.comboBox1.Enabled = false;
            this.comboBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(20, 160);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(460, 30);
            this.comboBox1.TabIndex = 4;
            this.comboBox1.TextChanged += new System.EventHandler(this.comboBox1_TextChanged);
            this.comboBox1.SelectionChangeCommitted += new System.EventHandler(this.comboBox1_SelectionChangeCommitted);
            this.comboBox1.Validating += new System.ComponentModel.CancelEventHandler(this.comboBox1_Validating);
            //
            // labelDuplicateHint
            //
            this.labelDuplicateHint.AutoSize = true;
            this.labelDuplicateHint.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelDuplicateHint.Location = new System.Drawing.Point(20, 199);
            this.labelDuplicateHint.Name = "labelDuplicateHint";
            this.labelDuplicateHint.Size = new System.Drawing.Size(300, 17);
            this.labelDuplicateHint.TabIndex = 5;
            this.labelDuplicateHint.Text = "Copies its layout and tags onto the new unit.";
            //
            // buttonCancel
            //
            // Lets Cancel close the dialog even when comboBox1 holds a value that fails validation.
            this.buttonCancel.CausesValidation = false;
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonCancel.Location = new System.Drawing.Point(250, 238);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(110, 40);
            this.buttonCancel.TabIndex = 6;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            //
            // buttonOK
            //
            // Starts disabled — UpdateUnitNumberValidation() re-enables it once the unit number field
            // holds a valid, non-placeholder value, so an invalid entry can no longer even reach OK.
            this.buttonOK.Enabled = false;
            this.buttonOK.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonOK.Location = new System.Drawing.Point(370, 238);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(110, 40);
            this.buttonOK.TabIndex = 7;
            this.buttonOK.Text = "Create";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            //
            // CreateUnitSheetForm
            //
            this.AcceptButton = this.buttonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(500, 300);
            this.ControlBox = false;
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.labelDuplicateHint);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.checkBoxDuplicate);
            this.Controls.Add(this.labelUnitStatus);
            this.Controls.Add(this.textBoxNewUnitNumber);
            this.Controls.Add(this.label1);
            this.MaximumSize = new System.Drawing.Size(522, 356);
            this.MinimumSize = new System.Drawing.Size(522, 356);
            this.Name = "CreateUnitSheetForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Create New Unit Sheet";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.CreateUnitSheetForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxNewUnitNumber;
        private System.Windows.Forms.Label labelUnitStatus;
        private System.Windows.Forms.CheckBox checkBoxDuplicate;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Label labelDuplicateHint;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonOK;
    }
}
