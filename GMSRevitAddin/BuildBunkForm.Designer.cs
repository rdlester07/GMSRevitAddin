namespace BuildBunkForm
{
    partial class BuildBunkForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.buttonNewSet = new System.Windows.Forms.Button();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.UnitNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.UnitAddress = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.UnitOrigin = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.UnitOriginSheet = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.UnitLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.UnitReleased = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.BuildNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.BunkNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.StatusAssembled = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.StatusGlazed = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.StatusBunked = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.StatusQCNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.StatusWeight = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.StatusShipped = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.StatusTruck = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.StatusStructuralTested = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.StatusWeatherTested = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.RevitElementID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.buttonAddtoSet = new System.Windows.Forms.Button();
            this.buttonRemoveFromSet = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxBuildNum = new System.Windows.Forms.TextBox();
            this.textBoxBunkNum = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textBoxAssembled = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textBoxBunked = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textBoxGlazed = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textBoxStruct = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.textBoxTruck = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.textBoxQC = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.textBoxWeight = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.textBoxWeather = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.textBoxShipped = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.buttonApply = new System.Windows.Forms.Button();
            this.label12 = new System.Windows.Forms.Label();
            this.buttonSettings = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // buttonNewSet
            // 
            this.buttonNewSet.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonNewSet.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonNewSet.Location = new System.Drawing.Point(1050, 550);
            this.buttonNewSet.Name = "buttonNewSet";
            this.buttonNewSet.Size = new System.Drawing.Size(284, 46);
            this.buttonNewSet.TabIndex = 3;
            this.buttonNewSet.Text = "New Selection Set";
            this.buttonNewSet.UseVisualStyleBackColor = true;
            this.buttonNewSet.Click += new System.EventHandler(this.buttonNewSet_Click);
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.UnitNumber,
            this.UnitAddress,
            this.UnitOrigin,
            this.UnitOriginSheet,
            this.UnitLevel,
            this.UnitReleased,
            this.BuildNumber,
            this.BunkNumber,
            this.StatusAssembled,
            this.StatusGlazed,
            this.StatusBunked,
            this.StatusQCNumber,
            this.StatusWeight,
            this.StatusShipped,
            this.StatusTruck,
            this.StatusStructuralTested,
            this.StatusWeatherTested,
            this.RevitElementID});
            this.dataGridView1.Location = new System.Drawing.Point(12, 12);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.RowTemplate.Height = 28;
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView1.Size = new System.Drawing.Size(1322, 532);
            this.dataGridView1.TabIndex = 1;
            // 
            // UnitNumber
            // 
            this.UnitNumber.HeaderText = "Unit Number";
            this.UnitNumber.Name = "UnitNumber";
            this.UnitNumber.ReadOnly = true;
            // 
            // UnitAddress
            // 
            this.UnitAddress.HeaderText = "Address";
            this.UnitAddress.Name = "UnitAddress";
            this.UnitAddress.ReadOnly = true;
            // 
            // UnitOrigin
            // 
            this.UnitOrigin.HeaderText = "Origin";
            this.UnitOrigin.Name = "UnitOrigin";
            this.UnitOrigin.ReadOnly = true;
            // 
            // UnitOriginSheet
            // 
            this.UnitOriginSheet.HeaderText = "Origin Sheet";
            this.UnitOriginSheet.Name = "UnitOriginSheet";
            this.UnitOriginSheet.ReadOnly = true;
            // 
            // UnitLevel
            // 
            this.UnitLevel.HeaderText = "Level";
            this.UnitLevel.Name = "UnitLevel";
            this.UnitLevel.ReadOnly = true;
            // 
            // UnitReleased
            // 
            this.UnitReleased.HeaderText = "Released";
            this.UnitReleased.Name = "UnitReleased";
            this.UnitReleased.ReadOnly = true;
            // 
            // BuildNumber
            // 
            this.BuildNumber.HeaderText = "Build Number";
            this.BuildNumber.Name = "BuildNumber";
            this.BuildNumber.ReadOnly = true;
            // 
            // BunkNumber
            // 
            this.BunkNumber.HeaderText = "Bunk Number";
            this.BunkNumber.Name = "BunkNumber";
            this.BunkNumber.ReadOnly = true;
            // 
            // StatusAssembled
            // 
            this.StatusAssembled.HeaderText = "Status - Assembled";
            this.StatusAssembled.Name = "StatusAssembled";
            this.StatusAssembled.ReadOnly = true;
            // 
            // StatusGlazed
            // 
            this.StatusGlazed.HeaderText = "Status - Glazed";
            this.StatusGlazed.Name = "StatusGlazed";
            this.StatusGlazed.ReadOnly = true;
            // 
            // StatusBunked
            // 
            this.StatusBunked.HeaderText = "Status - Bunked";
            this.StatusBunked.Name = "StatusBunked";
            this.StatusBunked.ReadOnly = true;
            // 
            // StatusQCNumber
            // 
            this.StatusQCNumber.HeaderText = "Status - QC Number";
            this.StatusQCNumber.Name = "StatusQCNumber";
            this.StatusQCNumber.ReadOnly = true;
            // 
            // StatusWeight
            // 
            this.StatusWeight.HeaderText = "Status - Weight";
            this.StatusWeight.Name = "StatusWeight";
            this.StatusWeight.ReadOnly = true;
            // 
            // StatusShipped
            // 
            this.StatusShipped.HeaderText = "Status - Shipped";
            this.StatusShipped.Name = "StatusShipped";
            this.StatusShipped.ReadOnly = true;
            // 
            // StatusTruck
            // 
            this.StatusTruck.HeaderText = "Status - Truck";
            this.StatusTruck.Name = "StatusTruck";
            this.StatusTruck.ReadOnly = true;
            // 
            // StatusStructuralTested
            // 
            this.StatusStructuralTested.HeaderText = "Status - Structural Tested";
            this.StatusStructuralTested.Name = "StatusStructuralTested";
            this.StatusStructuralTested.ReadOnly = true;
            // 
            // StatusWeatherTested
            // 
            this.StatusWeatherTested.HeaderText = "Status - Weather Tested";
            this.StatusWeatherTested.Name = "StatusWeatherTested";
            this.StatusWeatherTested.ReadOnly = true;
            // 
            // RevitElementID
            // 
            this.RevitElementID.HeaderText = "ElementID";
            this.RevitElementID.Name = "RevitElementID";
            this.RevitElementID.ReadOnly = true;
            this.RevitElementID.Visible = false;
            // 
            // buttonAddtoSet
            // 
            this.buttonAddtoSet.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonAddtoSet.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonAddtoSet.Location = new System.Drawing.Point(12, 550);
            this.buttonAddtoSet.Name = "buttonAddtoSet";
            this.buttonAddtoSet.Size = new System.Drawing.Size(284, 46);
            this.buttonAddtoSet.TabIndex = 1;
            this.buttonAddtoSet.Text = "Add to Selection Set";
            this.buttonAddtoSet.UseVisualStyleBackColor = true;
            this.buttonAddtoSet.Click += new System.EventHandler(this.buttonAddtoSet_Click);
            // 
            // buttonRemoveFromSet
            // 
            this.buttonRemoveFromSet.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonRemoveFromSet.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonRemoveFromSet.Location = new System.Drawing.Point(302, 550);
            this.buttonRemoveFromSet.Name = "buttonRemoveFromSet";
            this.buttonRemoveFromSet.Size = new System.Drawing.Size(284, 46);
            this.buttonRemoveFromSet.TabIndex = 2;
            this.buttonRemoveFromSet.Text = "Remove From Selection Set";
            this.buttonRemoveFromSet.UseVisualStyleBackColor = true;
            this.buttonRemoveFromSet.Click += new System.EventHandler(this.buttonRemoveFromSet_Click);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 618);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(135, 25);
            this.label1.TabIndex = 4;
            this.label1.Text = "Build Number:";
            // 
            // textBoxBuildNum
            // 
            this.textBoxBuildNum.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxBuildNum.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxBuildNum.Location = new System.Drawing.Point(207, 615);
            this.textBoxBuildNum.Name = "textBoxBuildNum";
            this.textBoxBuildNum.Size = new System.Drawing.Size(140, 30);
            this.textBoxBuildNum.TabIndex = 4;
            // 
            // textBoxBunkNum
            // 
            this.textBoxBunkNum.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxBunkNum.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxBunkNum.Location = new System.Drawing.Point(207, 655);
            this.textBoxBunkNum.Name = "textBoxBunkNum";
            this.textBoxBunkNum.Size = new System.Drawing.Size(140, 30);
            this.textBoxBunkNum.TabIndex = 5;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(12, 658);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(137, 25);
            this.label2.TabIndex = 6;
            this.label2.Text = "Bunk Number:";
            // 
            // textBoxAssembled
            // 
            this.textBoxAssembled.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxAssembled.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxAssembled.Location = new System.Drawing.Point(207, 695);
            this.textBoxAssembled.Name = "textBoxAssembled";
            this.textBoxAssembled.Size = new System.Drawing.Size(140, 30);
            this.textBoxAssembled.TabIndex = 6;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(12, 698);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(189, 25);
            this.label3.TabIndex = 8;
            this.label3.Text = "Status - Assembled:";
            // 
            // textBoxBunked
            // 
            this.textBoxBunked.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxBunked.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxBunked.Location = new System.Drawing.Point(207, 775);
            this.textBoxBunked.Name = "textBoxBunked";
            this.textBoxBunked.Size = new System.Drawing.Size(140, 30);
            this.textBoxBunked.TabIndex = 8;
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(12, 778);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(158, 25);
            this.label4.TabIndex = 10;
            this.label4.Text = "Status - Bunked:";
            // 
            // textBoxGlazed
            // 
            this.textBoxGlazed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxGlazed.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxGlazed.Location = new System.Drawing.Point(207, 735);
            this.textBoxGlazed.Name = "textBoxGlazed";
            this.textBoxGlazed.Size = new System.Drawing.Size(140, 30);
            this.textBoxGlazed.TabIndex = 7;
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(12, 738);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(153, 25);
            this.label5.TabIndex = 12;
            this.label5.Text = "Status - Glazed:";
            // 
            // textBoxStruct
            // 
            this.textBoxStruct.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxStruct.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxStruct.Location = new System.Drawing.Point(660, 735);
            this.textBoxStruct.Name = "textBoxStruct";
            this.textBoxStruct.Size = new System.Drawing.Size(140, 30);
            this.textBoxStruct.TabIndex = 13;
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(12, 818);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(196, 25);
            this.label6.TabIndex = 14;
            this.label6.Text = "Status - QC Number:";
            // 
            // textBoxTruck
            // 
            this.textBoxTruck.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxTruck.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxTruck.Location = new System.Drawing.Point(660, 695);
            this.textBoxTruck.Name = "textBoxTruck";
            this.textBoxTruck.Size = new System.Drawing.Size(140, 30);
            this.textBoxTruck.TabIndex = 12;
            // 
            // label7
            // 
            this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(414, 658);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(164, 25);
            this.label7.TabIndex = 16;
            this.label7.Text = "Status - Shipped:";
            // 
            // textBoxQC
            // 
            this.textBoxQC.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxQC.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxQC.Location = new System.Drawing.Point(207, 815);
            this.textBoxQC.Name = "textBoxQC";
            this.textBoxQC.Size = new System.Drawing.Size(140, 30);
            this.textBoxQC.TabIndex = 9;
            // 
            // label8
            // 
            this.label8.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(414, 738);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(240, 25);
            this.label8.TabIndex = 18;
            this.label8.Text = "Status - Structural Tested:";
            // 
            // textBoxWeight
            // 
            this.textBoxWeight.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxWeight.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxWeight.Location = new System.Drawing.Point(660, 615);
            this.textBoxWeight.Name = "textBoxWeight";
            this.textBoxWeight.Size = new System.Drawing.Size(140, 30);
            this.textBoxWeight.TabIndex = 10;
            // 
            // label9
            // 
            this.label9.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.Location = new System.Drawing.Point(414, 618);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(153, 25);
            this.label9.TabIndex = 20;
            this.label9.Text = "Status - Weight:";
            // 
            // textBoxWeather
            // 
            this.textBoxWeather.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxWeather.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxWeather.Location = new System.Drawing.Point(660, 775);
            this.textBoxWeather.Name = "textBoxWeather";
            this.textBoxWeather.Size = new System.Drawing.Size(140, 30);
            this.textBoxWeather.TabIndex = 14;
            // 
            // label10
            // 
            this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label10.AutoSize = true;
            this.label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label10.Location = new System.Drawing.Point(414, 698);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(141, 25);
            this.label10.TabIndex = 22;
            this.label10.Text = "Status - Truck:";
            // 
            // textBoxShipped
            // 
            this.textBoxShipped.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxShipped.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxShipped.Location = new System.Drawing.Point(660, 655);
            this.textBoxShipped.Name = "textBoxShipped";
            this.textBoxShipped.Size = new System.Drawing.Size(140, 30);
            this.textBoxShipped.TabIndex = 11;
            // 
            // label11
            // 
            this.label11.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label11.AutoSize = true;
            this.label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label11.Location = new System.Drawing.Point(414, 778);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(232, 25);
            this.label11.TabIndex = 24;
            this.label11.Text = "Status - Weather Tested:";
            // 
            // buttonApply
            // 
            this.buttonApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonApply.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonApply.Location = new System.Drawing.Point(1050, 828);
            this.buttonApply.Name = "buttonApply";
            this.buttonApply.Size = new System.Drawing.Size(284, 46);
            this.buttonApply.TabIndex = 0;
            this.buttonApply.Text = "Apply";
            this.buttonApply.UseVisualStyleBackColor = true;
            this.buttonApply.Click += new System.EventHandler(this.buttonApply_Click);
            // 
            // label12
            // 
            this.label12.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(13, 855);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(254, 20);
            this.label12.TabIndex = 25;
            this.label12.Text = "NOTE: Enter - in the field to delete.";
            // 
            // buttonSettings
            // 
            this.buttonSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSettings.BackgroundImage = global::GMSRevitAddin.Properties.Resources.settings_applications;
            this.buttonSettings.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.buttonSettings.FlatAppearance.BorderColor = System.Drawing.SystemColors.Control;
            this.buttonSettings.FlatAppearance.BorderSize = 0;
            this.buttonSettings.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.buttonSettings.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonSettings.Location = new System.Drawing.Point(998, 829);
            this.buttonSettings.Name = "buttonSettings";
            this.buttonSettings.Size = new System.Drawing.Size(46, 46);
            this.buttonSettings.TabIndex = 26;
            this.buttonSettings.TabStop = false;
            this.buttonSettings.UseVisualStyleBackColor = true;
            // 
            // BuildBunkForm
            // 
            this.AcceptButton = this.buttonApply;
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1346, 885);
            this.Controls.Add(this.buttonSettings);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.buttonApply);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textBoxShipped);
            this.Controls.Add(this.textBoxBuildNum);
            this.Controls.Add(this.buttonRemoveFromSet);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.buttonAddtoSet);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.textBoxWeather);
            this.Controls.Add(this.buttonNewSet);
            this.Controls.Add(this.textBoxBunkNum);
            this.Controls.Add(this.textBoxGlazed);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textBoxBunked);
            this.Controls.Add(this.textBoxQC);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.textBoxWeight);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.textBoxStruct);
            this.Controls.Add(this.textBoxAssembled);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.textBoxTruck);
            this.Controls.Add(this.label9);
            this.MinimumSize = new System.Drawing.Size(910, 725);
            this.Name = "BuildBunkForm";
            this.ShowIcon = false;
            this.Text = "Build / Bunk Tool";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BuildBunkForm_FormClosing);
            this.Load += new System.EventHandler(this.BuildBunkForm_Load);
            this.ResizeEnd += new System.EventHandler(this.OnSizeChange);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonNewSet;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button buttonAddtoSet;
        private System.Windows.Forms.Button buttonRemoveFromSet;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxBuildNum;
        private System.Windows.Forms.TextBox textBoxBunkNum;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBoxAssembled;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBoxBunked;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBoxGlazed;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBoxStruct;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textBoxTruck;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox textBoxQC;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox textBoxWeight;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox textBoxWeather;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox textBoxShipped;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Button buttonApply;
        private System.Windows.Forms.DataGridViewTextBoxColumn UnitNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn UnitAddress;
        private System.Windows.Forms.DataGridViewTextBoxColumn UnitOrigin;
        private System.Windows.Forms.DataGridViewTextBoxColumn UnitOriginSheet;
        private System.Windows.Forms.DataGridViewTextBoxColumn UnitLevel;
        private System.Windows.Forms.DataGridViewTextBoxColumn UnitReleased;
        private System.Windows.Forms.DataGridViewTextBoxColumn BuildNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn BunkNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn StatusAssembled;
        private System.Windows.Forms.DataGridViewTextBoxColumn StatusGlazed;
        private System.Windows.Forms.DataGridViewTextBoxColumn StatusBunked;
        private System.Windows.Forms.DataGridViewTextBoxColumn StatusQCNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn StatusWeight;
        private System.Windows.Forms.DataGridViewTextBoxColumn StatusShipped;
        private System.Windows.Forms.DataGridViewTextBoxColumn StatusTruck;
        private System.Windows.Forms.DataGridViewTextBoxColumn StatusStructuralTested;
        private System.Windows.Forms.DataGridViewTextBoxColumn StatusWeatherTested;
        private System.Windows.Forms.DataGridViewTextBoxColumn RevitElementID;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Button buttonSettings;
    }
}