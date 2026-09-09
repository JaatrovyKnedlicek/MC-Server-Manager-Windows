using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class RconConsoleForm : Form
    {
        private readonly string _host;
        private readonly int _rconPort;
        private readonly string _rconPassword;
        private RconClient? _rconClient;

        private RichTextBox txtConsole;
        private TextBox txtCommand;
        private Button btnSend;
        private Button btnConnect;
        private Button btnDisconnect;
        private Label lblStatus;
        private Label lblConnectionInfo;

        public RconConsoleForm(string host, int rconPort, string rconPassword)
        {
            _host = host;
            _rconPort = rconPort;
            _rconPassword = rconPassword;

            Text = "RCON Console";
            Width = 800;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(600, 400);

            InitializeComponents();
            UpdateConnectionStatus(false);
        }

        private void InitializeComponents()
        {
            // Status label at the top
            lblStatus = new Label
            {
                Text = "Status: Disconnected",
                Left = 16,
                Top = 16,
                Width = 200,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };

            lblConnectionInfo = new Label
            {
                Text = $"Server: {_host}:{_rconPort}",
                Left = 230,
                Top = 16,
                Width = 300,
                ForeColor = Color.Gray
            };

            // Console output area
            txtConsole = new RichTextBox
            {
                Left = 16,
                Top = 45,
                Width = ClientSize.Width - 32,
                Height = ClientSize.Height - 130,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Consolas", 10),
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Command input area
            var lblCommand = new Label
            {
                Text = "Command:",
                Left = 16,
                Top = ClientSize.Height - 75,
                Width = 70,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            txtCommand = new TextBox
            {
                Left = 90,
                Top = ClientSize.Height - 75,
                Width = ClientSize.Width - 200,
                Height = 25,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            txtCommand.KeyDown += TxtCommand_KeyDown;

            // Buttons
            btnConnect = new Button
            {
                Text = "Connect",
                Left = ClientSize.Width - 180,
                Top = 16,
                Width = 80,
                Height = 25,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnConnect.Click += BtnConnect_Click;

            btnDisconnect = new Button
            {
                Text = "Disconnect",
                Left = ClientSize.Width - 90,
                Top = 16,
                Width = 80,
                Height = 25,
                Enabled = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnDisconnect.Click += BtnDisconnect_Click;

            btnSend = new Button
            {
                Text = "Send",
                Left = ClientSize.Width - 90,
                Top = ClientSize.Height - 75,
                Width = 80,
                Height = 25,
                Enabled = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnSend.Click += BtnSend_Click;

            // Add controls
            Controls.Add(lblStatus);
            Controls.Add(lblConnectionInfo);
            Controls.Add(txtConsole);
            Controls.Add(lblCommand);
            Controls.Add(txtCommand);
            Controls.Add(btnConnect);
            Controls.Add(btnDisconnect);
            Controls.Add(btnSend);
        }

        private async void BtnConnect_Click(object? sender, EventArgs e)
        {
            btnConnect.Enabled = false;
            lblStatus.Text = "Status: Connecting...";
            lblStatus.ForeColor = Color.Orange;

            try
            {
                _rconClient = new RconClient(_host, _rconPort, _rconPassword);
                bool connected = await _rconClient.ConnectAsync();

                if (connected)
                {
                    UpdateConnectionStatus(true);
                    AppendToConsole($"Connected to {_host}:{_rconPort}", Color.Green);
                    
                    // Send initial commands to get server info
                    await SendCommandAsync("list");
                }
                else
                {
                    UpdateConnectionStatus(false);
                    AppendToConsole("Failed to connect. Check RCON port and password.", Color.Red);
                    _rconClient?.Dispose();
                    _rconClient = null;
                }
            }
            catch (Exception ex)
            {
                UpdateConnectionStatus(false);
                AppendToConsole($"Connection error: {ex.Message}", Color.Red);
                _rconClient?.Dispose();
                _rconClient = null;
            }
            finally
            {
                btnConnect.Enabled = true;
            }
        }

        private void BtnDisconnect_Click(object? sender, EventArgs e)
        {
            _rconClient?.Dispose();
            _rconClient = null;
            UpdateConnectionStatus(false);
            AppendToConsole("Disconnected from server.", Color.Yellow);
        }

        private async void BtnSend_Click(object? sender, EventArgs e)
        {
            await SendCommandAsync(txtCommand.Text);
            txtCommand.Clear();
            txtCommand.Focus();
        }

        private void TxtCommand_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && btnSend.Enabled)
            {
                BtnSend_Click(sender, e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private async Task SendCommandAsync(string command)
        {
            if (_rconClient == null || !_rconClient.IsAuthenticated)
            {
                AppendToConsole("Not connected to server.", Color.Red);
                return;
            }

            if (string.IsNullOrWhiteSpace(command))
                return;

            AppendToConsole($"> {command}", Color.White);

            try
            {
                var response = await _rconClient.SendCommandAsync(command);
                if (!string.IsNullOrEmpty(response))
                {
                    AppendToConsole(response, Color.Lime);
                }
                else
                {
                    AppendToConsole("(No response)", Color.Gray);
                }
            }
            catch (Exception ex)
            {
                AppendToConsole($"Error sending command: {ex.Message}", Color.Red);
                UpdateConnectionStatus(false);
            }
        }

        private void AppendToConsole(string text, Color color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendToConsole(text, color)));
                return;
            }

            int originalSelectionStart = txtConsole.SelectionStart;
            int originalSelectionLength = txtConsole.SelectionLength;

            txtConsole.SelectionStart = txtConsole.TextLength;
            txtConsole.SelectionLength = 0;
            txtConsole.SelectionColor = color;
            txtConsole.AppendText(text + Environment.NewLine);
            txtConsole.SelectionColor = txtConsole.ForeColor;

            // Scroll to bottom
            txtConsole.SelectionStart = txtConsole.TextLength;
            txtConsole.ScrollToCaret();

            // Restore original selection
            txtConsole.SelectionStart = originalSelectionStart;
            txtConsole.SelectionLength = originalSelectionLength;
        }

        private void UpdateConnectionStatus(bool connected)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateConnectionStatus(connected)));
                return;
            }

            if (connected)
            {
                lblStatus.Text = "Status: Connected";
                lblStatus.ForeColor = Color.Green;
                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;
                btnSend.Enabled = true;
                txtCommand.Enabled = true;
            }
            else
            {
                lblStatus.Text = "Status: Disconnected";
                lblStatus.ForeColor = Color.Red;
                btnConnect.Enabled = true;
                btnDisconnect.Enabled = false;
                btnSend.Enabled = false;
                txtCommand.Enabled = false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _rconClient?.Dispose();
            base.OnFormClosing(e);
        }
    }
}