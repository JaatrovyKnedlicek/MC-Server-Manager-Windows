namespace MC_Server_Manager_3
{
    partial class NewServerWizardForm
    {
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            panelStep1 = new System.Windows.Forms.Panel();
            lblName = new System.Windows.Forms.Label();
            txtName = new System.Windows.Forms.TextBox();
            lblPaperVersion = new System.Windows.Forms.Label();
            cmbVersions = new System.Windows.Forms.ComboBox();

            panelStep2 = new System.Windows.Forms.Panel();
            lblJarNote = new System.Windows.Forms.Label();

            // RAM controls
            lblRam = new System.Windows.Forms.Label();
            numRam = new System.Windows.Forms.NumericUpDown();
            cmbRamPresets = new System.Windows.Forms.ComboBox();
            lblTotalRam = new System.Windows.Forms.Label();
            lblRamInfo = new System.Windows.Forms.Label();

            // removed server.properties controls per request

            chkEulaAccept = new System.Windows.Forms.CheckBox();

            panelStep3 = new System.Windows.Forms.Panel();
            lblSummary = new System.Windows.Forms.Label();

            btnBack = new System.Windows.Forms.Button();
            btnNext = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            btnFinish = new System.Windows.Forms.Button();

            // panelStep1
            panelStep1.Controls.Add(lblName);
            panelStep1.Controls.Add(txtName);
            panelStep1.Controls.Add(lblPaperVersion);
            panelStep1.Controls.Add(cmbVersions);
            panelStep1.Location = new System.Drawing.Point(12, 12);
            panelStep1.Name = "panelStep1";
            panelStep1.Size = new System.Drawing.Size(520, 120);
            panelStep1.TabIndex = 0;

            // lblName
            lblName.AutoSize = true;
            lblName.Location = new System.Drawing.Point(10, 12);
            lblName.Name = "lblName";
            lblName.Size = new System.Drawing.Size(75, 15);
            lblName.Text = "Server Name:";

            // txtName
            txtName.Location = new System.Drawing.Point(10, 30);
            txtName.Name = "txtName";
            txtName.Size = new System.Drawing.Size(480, 23);

            // lblPaperVersion
            lblPaperVersion.AutoSize = true;
            lblPaperVersion.Location = new System.Drawing.Point(10, 66);
            lblPaperVersion.Name = "lblPaperVersion";
            lblPaperVersion.Size = new System.Drawing.Size(110, 15);
            lblPaperVersion.Text = "PaperMC Version:";

            // cmbVersions
            cmbVersions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbVersions.Location = new System.Drawing.Point(10, 84);
            cmbVersions.Name = "cmbVersions";
            cmbVersions.Size = new System.Drawing.Size(240, 23);

            // panelStep2
            panelStep2.Controls.Add(lblJarNote);
            panelStep2.Controls.Add(lblRam);
            panelStep2.Controls.Add(numRam);
            panelStep2.Controls.Add(cmbRamPresets);
            panelStep2.Controls.Add(lblTotalRam);
            panelStep2.Controls.Add(lblRamInfo);
            panelStep2.Controls.Add(chkEulaAccept);
            panelStep2.Location = new System.Drawing.Point(12, 12);
            panelStep2.Name = "panelStep2";
            panelStep2.Size = new System.Drawing.Size(520, 220);
            panelStep2.TabIndex = 1;
            panelStep2.Visible = false;

            // lblJarNote
            lblJarNote.AutoSize = true;
            lblJarNote.Location = new System.Drawing.Point(10, 10);
            lblJarNote.Name = "lblJarNote";
            lblJarNote.Size = new System.Drawing.Size(360, 15);
            lblJarNote.Text = "The selected PaperMC JAR will be downloaded now (one-time).";

            // lblRam
            lblRam.AutoSize = true;
            lblRam.Location = new System.Drawing.Point(10, 36);
            lblRam.Name = "lblRam";
            lblRam.Size = new System.Drawing.Size(86, 15);
            lblRam.Text = "RAM (MB):";

            // numRam
            numRam.Location = new System.Drawing.Point(10, 54);
            numRam.Maximum = new decimal(new int[] { 65536, 0, 0, 0 });
            numRam.Minimum = new decimal(new int[] { 512, 0, 0, 0 });
            numRam.Name = "numRam";
            numRam.Size = new System.Drawing.Size(120, 23);
            numRam.Value = new decimal(new int[] { 2048, 0, 0, 0 });

            // cmbRamPresets
            cmbRamPresets.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbRamPresets.Location = new System.Drawing.Point(140, 54);
            cmbRamPresets.Name = "cmbRamPresets";
            cmbRamPresets.Size = new System.Drawing.Size(110, 23);
            cmbRamPresets.SelectedIndexChanged += new System.EventHandler(this.cmbRamPresets_SelectedIndexChanged);

            // lblTotalRam
            lblTotalRam.AutoSize = true;
            lblTotalRam.Location = new System.Drawing.Point(10, 84);
            lblTotalRam.Name = "lblTotalRam";
            lblTotalRam.Size = new System.Drawing.Size(120, 15);
            lblTotalRam.Text = "System RAM: calculating...";

            // lblRamInfo
            lblRamInfo.AutoSize = true;
            lblRamInfo.Location = new System.Drawing.Point(10, 104);
            lblRamInfo.Name = "lblRamInfo";
            lblRamInfo.Size = new System.Drawing.Size(320, 15);
            lblRamInfo.Text = "Max assignable will be set based on system memory.";

            // chkEulaAccept
            chkEulaAccept.AutoSize = true;
            chkEulaAccept.Location = new System.Drawing.Point(10, 132);
            chkEulaAccept.Name = "chkEulaAccept";
            chkEulaAccept.Size = new System.Drawing.Size(260, 19);
            chkEulaAccept.Text = "I agree to the Minecraft EULA (eula.txt will be created)";
            chkEulaAccept.UseVisualStyleBackColor = true;

            // panelStep3
            panelStep3.Controls.Add(lblSummary);
            panelStep3.Location = new System.Drawing.Point(12, 12);
            panelStep3.Name = "panelStep3";
            panelStep3.Size = new System.Drawing.Size(520, 220);
            panelStep3.TabIndex = 2;
            panelStep3.Visible = false;

            // lblSummary
            lblSummary.AutoSize = false;
            lblSummary.Location = new System.Drawing.Point(10, 10);
            lblSummary.Name = "lblSummary";
            lblSummary.Size = new System.Drawing.Size(500, 200);
            lblSummary.Text = "";

            // buttons
            btnBack.Location = new System.Drawing.Point(218, 244);
            btnBack.Name = "btnBack";
            btnBack.Size = new System.Drawing.Size(75, 25);
            btnBack.Text = "Back";
            btnBack.UseVisualStyleBackColor = true;
            btnBack.Click += btnBack_Click;

            btnNext.Location = new System.Drawing.Point(299, 244);
            btnNext.Name = "btnNext";
            btnNext.Size = new System.Drawing.Size(75, 25);
            btnNext.Text = "Next >";
            btnNext.UseVisualStyleBackColor = true;
            btnNext.Click += btnNext_Click;

            btnFinish.Location = new System.Drawing.Point(380, 244);
            btnFinish.Name = "btnFinish";
            btnFinish.Size = new System.Drawing.Size(75, 25);
            btnFinish.Text = "Finish";
            btnFinish.UseVisualStyleBackColor = true;
            btnFinish.Click += btnFinish_Click;
            btnFinish.Visible = false;

            btnCancel.Location = new System.Drawing.Point(461, 244);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(75, 25);
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;

            // NewServerWizardForm
            this.ClientSize = new System.Drawing.Size(548, 281);
            this.Controls.Add(panelStep1);
            this.Controls.Add(panelStep2);
            this.Controls.Add(panelStep3);
            this.Controls.Add(btnBack);
            this.Controls.Add(btnNext);
            this.Controls.Add(btnFinish);
            this.Controls.Add(btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NewServerWizardForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "New Server Wizard";
        }

        #endregion

        private System.Windows.Forms.Panel panelStep1;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox txtName;

        private System.Windows.Forms.Label lblPaperVersion;
        private System.Windows.Forms.ComboBox cmbVersions;

        private System.Windows.Forms.Panel panelStep2;
        private System.Windows.Forms.Label lblJarNote;
        private System.Windows.Forms.Label lblRam;
        private System.Windows.Forms.NumericUpDown numRam;
        private System.Windows.Forms.ComboBox cmbRamPresets;
        private System.Windows.Forms.Label lblTotalRam;
        private System.Windows.Forms.Label lblRamInfo;
        // removed server.properties controls
        private System.Windows.Forms.CheckBox chkEulaAccept;

        private System.Windows.Forms.Panel panelStep3;
        private System.Windows.Forms.Label lblSummary;

        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnFinish;
    }
}