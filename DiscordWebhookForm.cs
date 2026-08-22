using System;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class DiscordWebhookForm : Form
    {
        private readonly CheckBox chkEnabled;
        private readonly TextBox txtUri;

        public bool WebhookEnabled { get; private set; }
        public string WebhookUri { get; private set; } = string.Empty;

        public DiscordWebhookForm(bool enabled, string uri)
        {
            Text = "Discord Webhook Status";
            Width = 560;
            Height = 190;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            chkEnabled = new CheckBox
            {
                Text = "Enable Discord webhook status notifications",
                Left = 16,
                Top = 16,
                Width = 500,
                Checked = enabled
            };

            var lblUri = new Label { Text = "Webhook URI:", Left = 16, Top = 55, Width = 90 };
            txtUri = new TextBox
            {
                Left = 110,
                Top = 51,
                Width = 410,
                Text = uri ?? string.Empty,
                UseSystemPasswordChar = true
            };

            var lblHint = new Label
            {
                Text = "A Discord message is sent when a server starts or stops.",
                Left = 16,
                Top = 84,
                Width = 500
            };

            var btnOk = new Button { Text = "OK", Left = 348, Top = 112, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Left = 440, Top = 112, Width = 80, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            Controls.Add(chkEnabled);
            Controls.Add(lblUri);
            Controls.Add(txtUri);
            Controls.Add(lblHint);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            chkEnabled.CheckedChanged += (_, _) => UpdateEnabledState();
            btnOk.Click += BtnOk_Click;
            UpdateEnabledState();
        }

        private void UpdateEnabledState()
        {
            txtUri.Enabled = chkEnabled.Checked;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            var uri = txtUri.Text.Trim();
            if (chkEnabled.Checked && (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show("Enter a valid HTTPS Discord webhook URI.", "Discord Webhook", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            WebhookEnabled = chkEnabled.Checked;
            WebhookUri = uri;
        }
    }
}