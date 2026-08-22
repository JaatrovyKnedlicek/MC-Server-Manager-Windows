using System;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class StatusWebsiteForm : Form
    {
        private readonly record struct PortPreset(int Port, string Label)
        {
            public override string ToString() => Label;
        }

        private static readonly PortPreset[] DefaultPorts =
        {
            new(80, "80 — HTTP (may require administrator)"),
            new(8000, "8000 — Common development HTTP"),
            new(8080, "8080 — Alternate HTTP"),
            new(8081, "8081 — Alternate HTTP"),
            new(8888, "8888 — Alternate HTTP"),
            new(3000, "3000 — Node.js / frontend apps"),
            new(5000, "5000 — Flask / ASP.NET development")
        };

        private readonly string _lanIp;
        private readonly string _publicIp;
        private readonly CheckBox chkEnabled;
        private readonly NumericUpDown numPort;
        private readonly ComboBox cmbPresets;
        private readonly Label lblLan;
        private readonly Label lblPublic;
        private readonly Button btnOk;
        private readonly Button btnCancel;
        private bool _updatingPort;

        public bool WebsiteEnabled { get; private set; }
        public int Port { get; private set; }

        public StatusWebsiteForm(bool enabled, int port, string lanIp, string publicIp)
        {
            _lanIp = string.IsNullOrWhiteSpace(lanIp) || lanIp == "..." ? "N/A" : lanIp;
            _publicIp = string.IsNullOrWhiteSpace(publicIp) || publicIp == "..." ? "N/A" : publicIp;

            Text = "Status Website";
            Width = 500;
            Height = 260;
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
                Width = 450,
                Checked = enabled
            };

            var lblPort = new Label { Text = "Port:", Left = 16, Top = 54, Width = 40 };
            numPort = new NumericUpDown
            {
                Left = 62,
                Top = 50,
                Width = 80,
                Minimum = 1,
                Maximum = 65535,
                ThousandsSeparator = false
            };
            cmbPresets = new ComboBox
            {
                Left = 150,
                Top = 50,
                Width = 318,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DisplayMember = nameof(PortPreset.Label)
            };
            foreach (var preset in DefaultPorts)
                cmbPresets.Items.Add(preset);

            var selected = port is > 0 and <= 65535 ? port : 8080;
            numPort.Value = selected;

            lblLan = new Label { Left = 16, Top = 96, Width = 452, AutoEllipsis = true };
            lblPublic = new Label { Left = 16, Top = 118, Width = 452, AutoEllipsis = true };

            btnOk = new Button { Text = "OK", Left = 296, Top = 178, Width = 80, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 388, Top = 178, Width = 80, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(chkEnabled);
            Controls.Add(lblPort);
            Controls.Add(numPort);
            Controls.Add(cmbPresets);
            Controls.Add(lblLan);
            Controls.Add(lblPublic);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            chkEnabled.CheckedChanged += (_, _) => UpdateEnabledState();
            numPort.ValueChanged += (_, _) =>
            {
                SyncPresetFromPort();
                UpdateAddressLabels();
            };
            cmbPresets.SelectedIndexChanged += CmbPresets_SelectedIndexChanged;
            btnOk.Click += BtnOk_Click;

            SyncPresetFromPort();
            UpdateAddressLabels();
            UpdateEnabledState();
        }

        private void CmbPresets_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingPort)
                return;
            if (cmbPresets.SelectedItem is PortPreset preset)
            {
                _updatingPort = true;
                numPort.Value = preset.Port;
                _updatingPort = false;
                UpdateAddressLabels();
            }
        }

        private void SyncPresetFromPort()
        {
            var port = (int)numPort.Value;
            _updatingPort = true;
            var match = -1;
            for (var i = 0; i < cmbPresets.Items.Count; i++)
            {
                if (cmbPresets.Items[i] is PortPreset preset && preset.Port == port)
                {
                    match = i;
                    break;
                }
            }
            cmbPresets.SelectedIndex = match;
            _updatingPort = false;
        }

        private void UpdateAddressLabels()
        {
            var port = (int)numPort.Value;
            lblLan.Text = $"LAN:    {FormatAddress(_lanIp, port)}";
            lblPublic.Text = $"Public: {FormatAddress(_publicIp, port)}";
        }

        private static string FormatAddress(string ip, int port)
        {
            if (string.IsNullOrWhiteSpace(ip) || ip == "N/A")
                return $"N/A:{port}";
            return $"http://{ip}:{port}/";
        }

        private void UpdateEnabledState()
        {
            var on = chkEnabled.Checked;
            numPort.Enabled = on;
            cmbPresets.Enabled = on;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            WebsiteEnabled = chkEnabled.Checked;
            Port = (int)numPort.Value;
        }
    }
}
