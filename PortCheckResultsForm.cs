using System;
using System.Drawing;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class PortCheckResultsForm : Form
    {
        private Label lblTitle;
        private Label lblServerName;
        private ListView listViewPorts;
        private Button btnClose;
        private ImageList imageListStatus;

        private string _serverName;

        // ListView item indices for updating
        private ListViewItem? _localPortItem;
        private ListViewItem? _publicPortItem;
        private ListViewItem? _statusWebsiteItem;

        public PortCheckResultsForm(string serverName)
        {
            _serverName = serverName;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Port Check Results";
            this.Size = new Size(600, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Create status icons
            imageListStatus = new ImageList
            {
                ImageSize = new Size(16, 16),
                ColorDepth = ColorDepth.Depth32Bit
            };

            // Yellow dot for checking
            imageListStatus.Images.Add(CreateStatusIcon(Color.Yellow));
            // Green dot for open
            imageListStatus.Images.Add(CreateStatusIcon(Color.Green));
            // Red dot for closed
            imageListStatus.Images.Add(CreateStatusIcon(Color.Red));

            // Title
            lblTitle = new Label
            {
                Text = "Port Availability Check",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(20, 15),
                Size = new Size(540, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Server name
            lblServerName = new Label
            {
                Text = $"Server: {_serverName}",
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Location = new Point(20, 45),
                Size = new Size(540, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ListView for port checks
            listViewPorts = new ListView
            {
                Location = new Point(20, 75),
                Size = new Size(540, 250),
                View = View.Details,
                GridLines = true,
                FullRowSelect = true,
                SmallImageList = imageListStatus
            };

            // Add columns
            listViewPorts.Columns.Add("Status", 60);
            listViewPorts.Columns.Add("Service", 150);
            listViewPorts.Columns.Add("Port", 60);
            listViewPorts.Columns.Add("IP Address", 120);
            listViewPorts.Columns.Add("Details", 150);

            // Close button
            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(480, 335),
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK
            };
            btnClose.Click += (s, e) => this.Close();

            // Add controls to form
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblServerName);
            this.Controls.Add(listViewPorts);
            this.Controls.Add(btnClose);
        }

        private Bitmap CreateStatusIcon(Color color)
        {
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 2, 2, 12, 12);
                }
            }
            return bitmap;
        }

        public void AddLocalPortCheck(int port, string ipAddress)
        {
            if (IsDisposed || listViewPorts.IsDisposed) return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(() => AddLocalPortCheck(port, ipAddress));
                }
                catch { }
                return;
            }

            try
            {
                var item = new ListViewItem("Checking", 0); // Yellow icon
                item.SubItems.Add("Minecraft Game Port");
                item.SubItems.Add(port.ToString());
                item.SubItems.Add(ipAddress);
                item.SubItems.Add("Checking...");

                listViewPorts.Items.Add(item);
                _localPortItem = item;
            }
            catch { }
        }

        public void AddPublicPortCheck(int port, string ipAddress)
        {
            if (IsDisposed || listViewPorts.IsDisposed) return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(() => AddPublicPortCheck(port, ipAddress));
                }
                catch { }
                return;
            }

            try
            {
                var item = new ListViewItem("Checking", 0); // Yellow icon
                item.SubItems.Add("Minecraft Game Port (Public)");
                item.SubItems.Add(port.ToString());
                item.SubItems.Add(ipAddress);
                item.SubItems.Add("Checking...");

                listViewPorts.Items.Add(item);
                _publicPortItem = item;
            }
            catch { }
        }

        public void AddStatusWebsiteCheck(int port, string ipAddress)
        {
            if (IsDisposed || listViewPorts.IsDisposed) return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(() => AddStatusWebsiteCheck(port, ipAddress));
                }
                catch { }
                return;
            }

            try
            {
                var item = new ListViewItem("Checking", 0); // Yellow icon
                item.SubItems.Add("Status Page Web UI");
                item.SubItems.Add(port.ToString());
                item.SubItems.Add(ipAddress);
                item.SubItems.Add("Checking...");

                listViewPorts.Items.Add(item);
                _statusWebsiteItem = item;
            }
            catch { }
        }

        public void UpdateLocalPortResult(bool isOpen)
        {
            if (IsDisposed || listViewPorts.IsDisposed) return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(() => UpdateLocalPortResult(isOpen));
                }
                catch { }
                return;
            }

            try
            {
                if (_localPortItem != null)
                {
                    _localPortItem.ImageIndex = isOpen ? 1 : 2; // Green or Red
                    _localPortItem.Text = isOpen ? "Open" : "Closed";
                    _localPortItem.SubItems[4].Text = isOpen ? "Port is accessible locally" : "Port is not accessible locally";
                }
            }
            catch { }
        }

        public void UpdatePublicPortResult(bool isOpen)
        {
            if (IsDisposed || listViewPorts.IsDisposed) return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(() => UpdatePublicPortResult(isOpen));
                }
                catch { }
                return;
            }

            try
            {
                if (_publicPortItem != null)
                {
                    _publicPortItem.ImageIndex = isOpen ? 1 : 2; // Green or Red
                    _publicPortItem.Text = isOpen ? "Open" : "Closed";
                    _publicPortItem.SubItems[4].Text = isOpen ? "Port is accessible from the internet" : "Port is not accessible from the internet (check port forwarding)";
                }
            }
            catch { }
        }

        public void UpdateStatusWebsiteResult(bool isOpen)
        {
            if (IsDisposed || listViewPorts.IsDisposed) return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(() => UpdateStatusWebsiteResult(isOpen));
                }
                catch { }
                return;
            }

            try
            {
                if (_statusWebsiteItem != null)
                {
                    _statusWebsiteItem.ImageIndex = isOpen ? 1 : 2; // Green or Red
                    _statusWebsiteItem.Text = isOpen ? "Open" : "Closed";
                    _statusWebsiteItem.SubItems[4].Text = isOpen ? "Status website is accessible" : "Status website is not accessible (check port forwarding)";
                }
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    imageListStatus?.Dispose();
                }
                catch { }
            }
            base.Dispose(disposing);
        }
    }
}
