using System;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class StatusWebsiteForm : Form
    {
        private static readonly int[] DefaultPorts = { 80, 8000, 8080, 8081, 8888, 3000, 5000 };

        private readonly CheckBox chkEnabled;
        private readonly ComboBox cmbPort;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        public bool WebsiteEnabled { get; private set; }
        public int Port { get; private set; }

        public StatusWebsiteForm(bool enabled, int port)
        {
            Text = "Status Website";
            Width = 420;
            Height = 200;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            chkEnabled = new CheckBox
            {
                Text = "Enable status website",
                Left = 16,
                Top = 16,
                Width = 370,
                Checked = enabled
            };

            var lblPort = new Label { Text = "Port:", Left = 16, Top = 54, Width = 40 };
            cmbPort = new ComboBox
            {
                Left = 62,
                Top = 50,
                Width = 320,
                DropDownStyle = ComboBoxStyle.DropDown
            };
            foreach (var p in DefaultPorts)
                cmbPort.Items.Add(p.ToString());

            var selected = port is > 0 and <= 65535 ? port : 8080;
            cmbPort.Text = selected.ToString();
            if (!cmbPort.Items.Contains(cmbPort.Text))
                cmbPort.Items.Insert(0, cmbPort.Text);

            var lblHint = new Label
            {
                Text = "Common website ports are listed. You can also type a custom port.",
                Left = 16,
                Top = 86,
                Width = 370
            };

            btnOk = new Button { Text = "OK", Left = 216, Top = 122, Width = 80, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 302, Top = 122, Width = 80, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(chkEnabled);
            Controls.Add(lblPort);
            Controls.Add(cmbPort);
            Controls.Add(lblHint);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            chkEnabled.CheckedChanged += (_, _) => UpdateEnabledState();
            btnOk.Click += BtnOk_Click;
            UpdateEnabledState();
        }

        private void UpdateEnabledState()
        {
            cmbPort.Enabled = chkEnabled.Checked;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            WebsiteEnabled = chkEnabled.Checked;
            if (!WebsiteEnabled)
            {
                Port = ParsePort(cmbPort.Text) ?? 8080;
                return;
            }

            var port = ParsePort(cmbPort.Text);
            if (port == null)
            {
                MessageBox.Show("Enter a valid port between 1 and 65535.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
                return;
            }

            Port = port.Value;
        }

        private static int? ParsePort(string? text)
        {
            if (int.TryParse(text?.Trim(), out var port) && port is > 0 and <= 65535)
                return port;
            return null;
        }
    }
}
