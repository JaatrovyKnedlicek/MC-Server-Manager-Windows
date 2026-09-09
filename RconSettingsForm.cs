using System;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class RconSettingsForm : Form
    {
        private readonly CheckBox chkEnabled;
        private readonly Label lblPort;
        private readonly NumericUpDown numPort;
        private readonly Label lblPassword;
        private readonly TextBox txtPassword;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        public bool RconEnabled { get; private set; }
        public int RconPort { get; private set; }
        public string RconPassword { get; private set; } = string.Empty;

        public RconSettingsForm(bool enabled, int port, string password)
        {
            Text = "RCON Settings";
            Width = 400;
            Height = 220;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            chkEnabled = new CheckBox
            {
                Text = "Enable RCON",
                Left = 16,
                Top = 16,
                Width = 350,
                Checked = enabled
            };

            lblPort = new Label
            {
                Text = "RCON Port:",
                Left = 16,
                Top = 50,
                Width = 80
            };

            numPort = new NumericUpDown
            {
                Left = 100,
                Top = 46,
                Width = 80,
                Minimum = 1,
                Maximum = 65535,
                Value = port > 0 && port <= 65535 ? port : 25575
            };

            lblPassword = new Label
            {
                Text = "RCON Password:",
                Left = 16,
                Top = 86,
                Width = 100
            };

            txtPassword = new TextBox
            {
                Left = 120,
                Top = 82,
                Width = 240,
                Text = password ?? string.Empty,
                UseSystemPasswordChar = true
            };

            var lblHint = new Label
            {
                Text = "RCON allows remote server management and command execution.",
                Left = 16,
                Top = 118,
                Width = 350,
                ForeColor = SystemColors.GrayText
            };

            btnOk = new Button
            {
                Text = "OK",
                Left = 200,
                Top = 150,
                Width = 80,
                DialogResult = DialogResult.OK
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Left = 290,
                Top = 150,
                Width = 80,
                DialogResult = DialogResult.Cancel
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(chkEnabled);
            Controls.Add(lblPort);
            Controls.Add(numPort);
            Controls.Add(lblPassword);
            Controls.Add(txtPassword);
            Controls.Add(lblHint);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            chkEnabled.CheckedChanged += (_, _) => UpdateEnabledState();
            btnOk.Click += BtnOk_Click;
            UpdateEnabledState();
        }

        private void UpdateEnabledState()
        {
            var enabled = chkEnabled.Checked;
            lblPort.Enabled = enabled;
            numPort.Enabled = enabled;
            lblPassword.Enabled = enabled;
            txtPassword.Enabled = enabled;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            RconEnabled = chkEnabled.Checked;
            RconPort = (int)numPort.Value;
            RconPassword = txtPassword.Text;

            if (RconEnabled)
            {
                if (string.IsNullOrWhiteSpace(RconPassword))
                {
                    MessageBox.Show("RCON password is required when RCON is enabled.", "RCON Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
            }
        }
    }
}