using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public partial class Form1 : Form
    {
        private bool serverRunning = false;

        // Console management (kept for legacy, but UI button hidden)
        private bool consoleAllocated = false;
        private bool consoleVisible = false;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // Console input forwarding (still present but not used for detached windows)
        private CancellationTokenSource? consoleInputCts;
        private Task? consoleInputTask;

        // servers storage
        private readonly List<ServerInfo> servers = new List<ServerInfo>();
        private int SelectedIndex => listBoxServers.SelectedIndex;
        // Designer-created buttons (declared in Form1.Designer.cs)

        // root folder under program directory where servers are stored
        private string ServersRoot => Path.Combine(AppContext.BaseDirectory, "servers");

        private record ServerInfo(string Name, string IP, int Port, string Version)
        {
            public int RamMB { get; set; } = 2048;
            public string PropertiesPath { get; set; } = string.Empty;
            public bool EulaAccepted { get; set; } = false;
            public bool Running { get; set; } = false;
            public List<string> Players { get; } = new List<string>();

            // path on disk for this server instance (optional)
            public string FolderPath { get; set; } = string.Empty;

            // process instance when running (may be detached)
            [JsonIgnore]
            public Process? ProcessInstance { get; set; }
        }

        // DTO used for persisting server configuration
        private class ServerConfig
        {
            public string Name { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public int Port { get; set; }
            public int RamMB { get; set; }
            public string PropertiesFileName { get; set; } = string.Empty;
            public bool EulaAccepted { get; set; }
        }

        public Form1()
        {
            InitializeComponent();

            // Designer adds Delete and Edit Properties buttons; no runtime creation needed

            // ensure servers folder exists
            Directory.CreateDirectory(ServersRoot);

            // initial UI state
            lblStatusValue.Text = "Stopped";
            listBoxPlayers.Items.Clear();

            // load saved servers from disk (if any)
            LoadServersFromDisk();

            // (Stop and Console buttons removed from UI)

            // Do NOT allocate console by default anymore
        }

        private void LoadServersFromDisk()
        {
            servers.Clear();
            listBoxServers.Items.Clear();

            try
            {
                if (!Directory.Exists(ServersRoot))
                    Directory.CreateDirectory(ServersRoot);

                foreach (var dir in Directory.GetDirectories(ServersRoot))
                {
                    try
                    {
                        var configPath = Path.Combine(dir, "config.json");
                        if (File.Exists(configPath))
                        {
                            var json = File.ReadAllText(configPath);
                            var cfg = JsonSerializer.Deserialize<ServerConfig>(json);
                            if (cfg != null)
                            {
                                var si = new ServerInfo(cfg.Name,
                                                        "127.0.0.1",
                                                        cfg.Port,
                                                        cfg.Version)
                                {
                                    RamMB = cfg.RamMB,
                                    PropertiesPath = string.IsNullOrEmpty(cfg.PropertiesFileName) ? string.Empty : Path.Combine(dir, cfg.PropertiesFileName),
                                    EulaAccepted = cfg.EulaAccepted,
                                    FolderPath = dir
                                };
                                servers.Add(si);
                                continue;
                            }
                        }




                        // fallback: no config.json — infer from folder name
                        var folderName = Path.GetFileName(dir);
                        var fallback = new ServerInfo(folderName, "127.0.0.1", 25565, "N/A")
                        {
                            FolderPath = dir
                        };
                        // if server.properties exists try to detect port (best-effort)
                        var props = Path.Combine(dir, "server.properties");
                        if (File.Exists(props))
                        {
                            try
                            {
                                foreach (var line in File.ReadAllLines(props))
                                {
                                    var trimmed = line.Trim();
                                    if (trimmed.StartsWith("server-port="))
                                    {
                                        var parts = trimmed.Split('=', 2);
                                        if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var p))
                                            fallback = fallback with { Port = p };
                                    }
                                }
                            }
                            catch { /* ignore */ }
                            fallback.PropertiesPath = props;
                        }

                        servers.Add(fallback);
                    }
                    catch
                    {
                        // ignore problematic server folder and continue
                    }
                }
            }
            catch
            {
                // ignore loading errors - leave servers empty
            }

            // populate listbox
            PopulateServerList();
        }

        private void PopulateServerList()
        {
            listBoxServers.Items.Clear();
            foreach (var s in servers)
                listBoxServers.Items.Add(s.Name);
            if (listBoxServers.Items.Count > 0)
                listBoxServers.SelectedIndex = 0;
        }

        private void LoadSelectedServerInfo()
        {
            if (SelectedIndex < 0 || SelectedIndex >= servers.Count)
            {
                label1.Text = "Select a server from the left";
                lblVersionValue.Text = "N/A";
                lblIPValue.Text = "N/A";
                lblPortValue.Text = "N/A";
                lblStatusValue.Text = "Stopped";
                listBoxPlayers.Items.Clear();

                // keep Start disabled when nothing is selected
                btnStartServer.Enabled = false;
                if (btnDeleteServer != null) btnDeleteServer.Enabled = false;
                if (btnEditProperties != null) btnEditProperties.Enabled = false;
                return;
            }

            var s = servers[SelectedIndex];
            label1.Text = s.Name;
            lblVersionValue.Text = s.Version;
            lblPortValue.Text = s.Port.ToString();

            // Show LAN (private) IP immediately and fetch public IP asynchronously
            var lan = GetLocalIPv4Address();
            lblIPValue.Text = $"IP LAN: {lan}\r\nIP: ...";
            _ = FetchAndSetPublicIpAsync(lan);

            // Always show "Stopped" for now; do not display "Running"
            lblStatusValue.Text = "Stopped";

            listBoxPlayers.Items.Clear();
            foreach (var p in s.Players)
                listBoxPlayers.Items.Add(p);

            // Always allow Start when a server is selected.
            btnStartServer.Enabled = true;
            if (btnDeleteServer != null) btnDeleteServer.Enabled = true;
            if (btnEditProperties != null) btnEditProperties.Enabled = true;
        }

        private void listBoxServers_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSelectedServerInfo();
        }

        // START SERVER: launch server via start.cmd in a new separate window (UseShellExecute=true).
        private void btnStartServer_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0) { MessageBox.Show("Select a server first."); return; }
            var s = servers[SelectedIndex];

            // removed the early return that prevented starting when already running

            if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath))
            {
                MessageBox.Show("Server folder not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var startCmdPath = Path.Combine(s.FolderPath, "start.cmd");
            Process? launched = null;

            try
            {
                if (File.Exists(startCmdPath))
                {
                    // Start the .cmd file with shell execute so it opens in its own window
                    var psi = new ProcessStartInfo
                    {
                        FileName = startCmdPath,
                        WorkingDirectory = s.FolderPath,
                        UseShellExecute = true,      // important to open a separate window
                        CreateNoWindow = false
                    };

                    launched = Process.Start(psi);
                }
                else
                {
                    // fallback: launch java directly in a new window (uses bundled java if available)
                    var javaMajor = MapMinecraftToJavaMajor(s.Version);
                    var bundledJava = Path.Combine(AppContext.BaseDirectory, "jdks", $"temurin-{javaMajor}", "bin", "java.exe");
                    string javaToUse = File.Exists(bundledJava) ? bundledJava : "java";

                    string ramArg = (s.RamMB % 1024 == 0) ? $"{s.RamMB / 1024}G" : $"{s.RamMB}M";
                    var jarPath = Path.Combine(s.FolderPath, "paper.jar");

                    var psi = new ProcessStartInfo
                    {
                        FileName = javaToUse,
                        Arguments = $"-Xms{ramArg} -Xmx{ramArg} -jar \"{jarPath}\" --nogui",
                        WorkingDirectory = s.FolderPath,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };

                    launched = Process.Start(psi);
                }

                if (launched != null)
                {
                    // track the launched process so we can optionally kill it later
                    s.ProcessInstance = launched;
                    s.Running = true;
                    s.Players.Clear();
                    LoadSelectedServerInfo();
                }
                else
                {
                    MessageBox.Show("Failed to launch server process.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start server: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // STOP SERVER: if we have a tracked Process instance, try graceful stop via stdin if possible,
        // otherwise kill the process.
        private async void btnStopServer_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0) { MessageBox.Show("Select a server first."); return; }
            var s = servers[SelectedIndex];

            if (!s.Running || s.ProcessInstance == null)
            {
                MessageBox.Show("Server is not running or not tracked by the manager.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var proc = s.ProcessInstance;
            try
            {
                // If we launched with RedirectStandardInput (not the usual case for UseShellExecute),
                // attempt graceful "stop" command.
                if (!proc.HasExited && proc.StartInfo != null && proc.StartInfo.RedirectStandardInput)
                {
                    try
                    {
                        await proc.StandardInput.WriteLineAsync("stop");
                        await proc.StandardInput.FlushAsync();
                        // give it some time to exit normally
                        if (!proc.WaitForExit(5000))
                        {
                            try { proc.Kill(true); } catch { }
                            proc.WaitForExit(5000);
                        }
                    }
                    catch
                    {
                        // if that fails fallback to kill
                        try { proc.Kill(true); } catch { }
                    }
                }
                else
                {
                    // We don't have stdin available (detached window). Kill the process tree.
                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill(entireProcessTree: true);
                            proc.WaitForExit(5000);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to terminate server process: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while stopping server: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                s.Running = false;
                s.ProcessInstance = null;
                s.Players.Clear();
                LoadSelectedServerInfo();
            }
        }

        // Console allocation methods kept but not used by default.
        private void CreateConsoleHidden()
        {
            if (consoleAllocated) return;
            if (!AllocConsole())
            {
                consoleAllocated = false;
                return;
            }

            consoleAllocated = true;
            var h = GetConsoleWindow();
            if (h != IntPtr.Zero)
            {
                ShowWindow(h, SW_HIDE);
                consoleVisible = false;
            }

            // the input forwarding loop is no longer necessary for detached windows,
            // but methods remain in case you re-enable inline console.
        }

        private void ToggleConsoleVisibility()
        {
            if (!consoleAllocated)
            {
                CreateConsoleHidden();
                if (!consoleAllocated) return;
            }

            var h = GetConsoleWindow();
            if (h == IntPtr.Zero) return;

            if (consoleVisible)
            {
                ShowWindow(h, SW_HIDE);
                consoleVisible = false;
            }
            else
            {
                ShowWindow(h, SW_SHOW);
                consoleVisible = true;
            }
        }

        private string GetLocalIPv4Address()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        var s = ip.ToString();
                        if (!s.StartsWith("169.254."))
                            return s;
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private async Task FetchAndSetPublicIpAsync(string lanIp)
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(5);
                var ip = (await http.GetStringAsync("https://api.ipify.org")).Trim();
                if (string.IsNullOrEmpty(ip)) ip = "N/A";
                if (!IsHandleCreated || IsDisposed) return;
                BeginInvoke(() => lblIPValue.Text = $"IP LAN: {lanIp}\r\nIP: {ip}");
            }
            catch
            {
                try { if (IsHandleCreated && !IsDisposed) BeginInvoke(() => lblIPValue.Text = $"IP LAN: {lanIp}\r\nIP: N/A"); } catch { }
            }
        }



        private void btnDeleteServer_Click(object sender, EventArgs e) => DeleteSelectedServer();
        private void btnEditProperties_Click(object sender, EventArgs e) => EditSelectedServerProperties();

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Attempt to stop any tracked processes (best-effort).
            foreach (var s in servers.ToList())
            {
                try
                {
                    if (s.ProcessInstance != null && !s.ProcessInstance.HasExited)
                    {
                        try { s.ProcessInstance.Kill(true); } catch { }
                    }
                }
                catch { }
            }

            if (consoleAllocated)
            {
                try { FreeConsole(); } catch { }
                consoleAllocated = false;
            }
            base.OnFormClosing(e);
        }

        // File menu: new server wizard etc. (unchanged)
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var dlg = new NewServerWizardForm();
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            var name = dlg.ServerName;
            var ip = "127.0.0.1"; // local only
            var port = 25565; // default port (port selection removed)
            var version = dlg.ServerVersion;

            // Use wizard-created folder if available, otherwise create one here.
            string folder;
            if (!string.IsNullOrEmpty(dlg.ServerFolderPath) && Directory.Exists(dlg.ServerFolderPath))
            {
                folder = dlg.ServerFolderPath;
            }
            else
            {
                var safeName = MakeSafeFolderName(name);
                folder = Path.Combine(ServersRoot, safeName);
                var suffix = 1;
                while (Directory.Exists(folder))
                {
                    folder = Path.Combine(ServersRoot, $"{safeName}-{suffix++}");
                }
                Directory.CreateDirectory(folder);
            }

            // move downloaded jar into folder (if any)
            try
            {
                if (!string.IsNullOrEmpty(dlg.DownloadedJarPath) && File.Exists(dlg.DownloadedJarPath))
                {
                    var destJar = Path.Combine(folder, "paper.jar");
                    var srcFull = Path.GetFullPath(dlg.DownloadedJarPath);
                    var destFull = Path.GetFullPath(destJar);

                    if (!string.Equals(srcFull, destFull, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(dlg.DownloadedJarPath, destJar, overwrite: true);
                    }
                }
            }
            catch
            {
                // ignore jar copy failures for now
            }

            // write eula.txt if accepted
            if (dlg.EulaAccepted)
            {
                try { File.WriteAllText(Path.Combine(folder, "eula.txt"), "eula=true"); } catch { }
            }

            // write config.json for this server
            var cfg = new ServerConfig
            {
                Name = name,
                Version = version,
                Port = port,
                RamMB = dlg.ServerRamMB,
                PropertiesFileName = string.Empty, // no server.properties
                EulaAccepted = dlg.EulaAccepted
            };

            var configJson = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            try { File.WriteAllText(Path.Combine(folder, "config.json"), configJson); } catch { }

            // add to in-memory list and update UI
            var s = new ServerInfo(name, ip, port, version)
            {
                RamMB = dlg.ServerRamMB,
                PropertiesPath = string.Empty,
                EulaAccepted = dlg.EulaAccepted,
                FolderPath = folder
            };

            servers.Add(s);
            PopulateServerList();
            listBoxServers.SelectedIndex = Math.Max(0, listBoxServers.Items.Count - 1);
        }

        private string MakeSafeFolderName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '-');
            // trim and fallback
            name = name.Trim();
            if (string.IsNullOrEmpty(name))
                name = "server";
            return name;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) =>
            MessageBox.Show("Open action not implemented yet.", "Open", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void saveToolStripMenuItem_Click(object sender, EventArgs e) =>
            MessageBox.Show("Save action not implemented yet.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Close();

        private void undoToolStripMenuItem_Click(object sender, EventArgs e) =>
            MessageBox.Show("Undo not implemented.", "Undo", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void redoToolStripMenuItem_Click(object sender, EventArgs e) =>
            MessageBox.Show("Redo not implemented.", "Redo", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void cutToolStripMenuItem_Click(object sender, EventArgs e) =>
            MessageBox.Show("Cut not implemented.", "Cut", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void copyToolStripMenuItem_Click(object sender, EventArgs e) =>
            MessageBox.Show("Copy not implemented.", "Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e) =>
            MessageBox.Show("Paste not implemented.", "Paste", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void statusBarToolStripMenuItem_Click(object sender, EventArgs e) =>
            MessageBox.Show("Toggle Status Bar - not implemented yet.", "View", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e) =>
            MessageBox.Show("Minecraft Server Manager 3.0.0-indev\nVersion: 3.0.0-indev\n© Ján Repka 2025", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void label1_Click(object sender, EventArgs e) { }

        // Add this method inside the Form1 class (e.g. above CreateUniqueServerFolder)
        private int MapMinecraftToJavaMajor(string mcVersion)
        {
            // Try parse as Version
            if (!Version.TryParse(mcVersion, out var v))
            {
                // fallback: choose latest (Java 21)
                return 21;
            }

            // compare using Version, treat missing fields as 0
            if (v.Major == 1)
            {
                var minor = v.Minor;
                // 1.8 .. 1.11 -> Java 8
                if (minor >= 8 && minor <= 11)
                    return 8;
                // 1.12 .. 1.16.4 -> Java 11
                if (new Version(1, 12) <= v && v <= new Version(1, 16, 4))
                    return 11;
                // 1.16.5 -> Java 16
                if (v.Major == 1 && v.Minor == 16 && v.Build == 5)
                    return 16;
                // 1.17.1 and above -> Java 21
                if (v >= new Version(1, 17, 1))
                    return 21;

                // fallback
                return 21;
            }

            // non-1.x versions (future-proof) -> Java 21
            if (v.Major >= 2) return 21;
            return 21;
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        // Add these methods inside the Form1 class

        private void DeleteSelectedServer()
        {
            if (SelectedIndex < 0) { MessageBox.Show("Select a server first.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var s = servers[SelectedIndex];
            var name = s.Name;

            var confirm = MessageBox.Show(
                $"Delete server '{name}'? This will remove the server from the list and delete its folder on disk (if present).",
                "Delete Server",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            // If running, attempt to stop/kill first (best-effort)
            if (s.Running)
            {
                var runChoice = MessageBox.Show(
                    "Server is running. Stop it now and continue deletion?",
                    "Server Running",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);

                if (runChoice == DialogResult.Cancel) return;
                if (runChoice == DialogResult.Yes)
                {
                    try
                    {
                        if (s.ProcessInstance != null && !s.ProcessInstance.HasExited)
                        {
                            try { s.ProcessInstance.Kill(true); } catch { }
                            s.ProcessInstance.WaitForExit(3000);
                        }
                    }
                    catch { /* ignore stop errors */ }
                }
                else
                {
                    // user chose No -> cancel deletion
                    return;
                }
            }

            // Delete folder on disk (if present)
            if (!string.IsNullOrEmpty(s.FolderPath) && Directory.Exists(s.FolderPath))
            {
                try
                {
                    Directory.Delete(s.FolderPath, recursive: true);
                }
                catch (Exception ex)
                {
                    var keep = MessageBox.Show($"Failed to delete server folder: {ex.Message}\r\nRemove from list anyway?",
                                               "Delete Error",
                                               MessageBoxButtons.YesNo,
                                               MessageBoxIcon.Warning);
                    if (keep != DialogResult.Yes) return;
                }
            }

            // Remove from in-memory list and update UI
            servers.RemoveAt(SelectedIndex);
            PopulateServerList();
            MessageBox.Show($"Server '{name}' deleted.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e) => DeleteSelectedServer();

        // Opens the selected server's server.properties in Notepad (creates file if missing)
        private void EditSelectedServerProperties()
        {
            if (SelectedIndex < 0) { MessageBox.Show("Select a server first.", "Edit Properties", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var s = servers[SelectedIndex];

            // determine properties path
            string propsPath = s.PropertiesPath;
            if (string.IsNullOrEmpty(propsPath))
            {
                if (!string.IsNullOrEmpty(s.FolderPath) && Directory.Exists(s.FolderPath))
                    propsPath = Path.Combine(s.FolderPath, "server.properties");
            }

            if (string.IsNullOrEmpty(propsPath))
            {
                MessageBox.Show("Server folder not available to create or open server.properties.", "Edit Properties", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                if (!File.Exists(propsPath))
                {
                    // create a simple default file
                    File.WriteAllText(propsPath, "# server.properties\n# Generated by MC Server Manager\n");
                }

                // remember path
                s.PropertiesPath = propsPath;

                var psi = new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{propsPath}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open server.properties: {ex.Message}", "Edit Properties", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
