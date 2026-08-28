using System;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class UpnpSettingsForm : Form
    {
        private readonly CheckBox chkUpnpEnabled;
        private readonly Label lblNote;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        public bool UpnpEnabled { get; private set; }

        public UpnpSettingsForm(bool currentEnabled)
        {
            Text = "UPnP Port Forwarding";
            Width = 450;
            Height = 180;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            chkUpnpEnabled = new CheckBox
            {
                Text = "Enable UPnP port forwarding",
                Left = 16,
                Top = 16,
                Width = 400,
                Checked = currentEnabled
            };

            lblNote = new Label
            {
                Text = "Note: Some routers do not support UPnP. If enabled, the server port will be automatically opened when the server starts and closed when it stops.",
                Left = 16,
                Top = 48,
                Width = 400,
                Height = 60
            };

            btnOk = new Button { Text = "OK", Left = 250, Top = 110, Width = 80, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 340, Top = 110, Width = 80, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(chkUpnpEnabled);
            Controls.Add(lblNote);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            btnOk.Click += BtnOk_Click;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            UpnpEnabled = chkUpnpEnabled.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}