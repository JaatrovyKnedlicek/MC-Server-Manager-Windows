using System;
using System.IO;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class PostShutdownActionsForm : Form
    {
        private readonly string _serverFolder;
        private readonly CheckBox chkEnabled;
        private readonly ComboBox cmbType;
        private readonly TextBox txtScript;
        private readonly Button btnUpload;
        private readonly Button btnOk;
        private readonly Button btnCancel;
        private string? _pendingSourcePath;

        public bool ActionsEnabled { get; private set; }
        public string ScriptType { get; private set; } = "ps1";
        public string ScriptFileName { get; private set; } = string.Empty;

        public PostShutdownActionsForm(bool enabled, string scriptType, string scriptFileName, string serverFolder)
        {
            _serverFolder = serverFolder;
            ScriptFileName = scriptFileName ?? string.Empty;
            ScriptType = NormalizeType(scriptType);

            Text = "Post-Shutdown Actions";
            Width = 460;
            Height = 230;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            chkEnabled = new CheckBox
            {
                Text = "Enable post-shutdown actions",
                Left = 16,
                Top = 16,
                Width = 400,
                Checked = enabled
            };

            var lblType = new Label { Text = "Script type:", Left = 16, Top = 50, Width = 80 };
            cmbType = new ComboBox
            {
                Left = 100,
                Top = 46,
                Width = 320,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbType.Items.AddRange(new object[]
            {
                "PowerShell (.ps1)",
                "Batch (.bat)",
                "Command (.cmd)",
                "Python (.py)"
            });
            cmbType.SelectedIndex = TypeToIndex(ScriptType);

            var lblScript = new Label { Text = "Script file:", Left = 16, Top = 86, Width = 80 };
            txtScript = new TextBox
            {
                Left = 100,
                Top = 82,
                Width = 220,
                ReadOnly = true,
                Text = string.IsNullOrEmpty(ScriptFileName) ? "(none)" : ScriptFileName
            };
            btnUpload = new Button { Text = "Upload...", Left = 328, Top = 80, Width = 92 };

            var lblHint = new Label
            {
                Text = "The uploaded script runs after this server's process stops.",
                Left = 16,
                Top = 118,
                Width = 404
            };

            btnOk = new Button { Text = "OK", Left = 248, Top = 150, Width = 80, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 340, Top = 150, Width = 80, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(chkEnabled);
            Controls.Add(lblType);
            Controls.Add(cmbType);
            Controls.Add(lblScript);
            Controls.Add(txtScript);
            Controls.Add(btnUpload);
            Controls.Add(lblHint);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            chkEnabled.CheckedChanged += (_, _) => UpdateEnabledState();
            btnUpload.Click += BtnUpload_Click;
            btnOk.Click += BtnOk_Click;
            UpdateEnabledState();
        }

        private void UpdateEnabledState()
        {
            var on = chkEnabled.Checked;
            cmbType.Enabled = on;
            txtScript.Enabled = on;
            btnUpload.Enabled = on;
        }

        private void BtnUpload_Click(object? sender, EventArgs e)
        {
            var ext = IndexToType(cmbType.SelectedIndex);
            using var ofd = new OpenFileDialog
            {
                Title = "Select post-shutdown script",
                Filter = FilterForType(ext),
                CheckFileExists = true
            };
            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            _pendingSourcePath = ofd.FileName;
            txtScript.Text = Path.GetFileName(ofd.FileName);
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            ScriptType = IndexToType(cmbType.SelectedIndex);
            ActionsEnabled = chkEnabled.Checked;

            if (ActionsEnabled)
            {
                if (string.IsNullOrEmpty(_pendingSourcePath) && string.IsNullOrEmpty(ScriptFileName))
                {
                    MessageBox.Show("Upload a script before enabling post-shutdown actions.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                    return;
                }

                if (!string.IsNullOrEmpty(_pendingSourcePath))
                {
                    try
                    {
                        Directory.CreateDirectory(_serverFolder);
                        var destName = "post-shutdown." + ScriptType;
                        var destPath = Path.Combine(_serverFolder, destName);
                        File.Copy(_pendingSourcePath, destPath, overwrite: true);
                        ScriptFileName = destName;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to upload script: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DialogResult = DialogResult.None;
                        return;
                    }
                }
                else if (!string.Equals(Path.GetExtension(ScriptFileName).TrimStart('.'), ScriptType, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Upload a script that matches the selected type.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                    return;
                }
            }

            DialogResult = DialogResult.OK;
        }

        private static string NormalizeType(string? type)
        {
            return type?.Trim().ToLowerInvariant() switch
            {
                "ps1" or "bat" or "cmd" or "py" => type!.Trim().ToLowerInvariant(),
                _ => "ps1"
            };
        }

        private static int TypeToIndex(string type) => type switch
        {
            "bat" => 1,
            "cmd" => 2,
            "py" => 3,
            _ => 0
        };

        private static string IndexToType(int index) => index switch
        {
            1 => "bat",
            2 => "cmd",
            3 => "py",
            _ => "ps1"
        };

        private static string FilterForType(string type) => type switch
        {
            "bat" => "Batch files (*.bat)|*.bat|All files (*.*)|*.*",
            "cmd" => "Command files (*.cmd)|*.cmd|All files (*.*)|*.*",
            "py" => "Python files (*.py)|*.py|All files (*.*)|*.*",
            _ => "PowerShell files (*.ps1)|*.ps1|All files (*.*)|*.*"
        };
    }
}
