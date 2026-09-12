using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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

        // Server process watcher timer
        private System.Windows.Forms.Timer? processWatcherTimer;
        
        // Player list refresh timer
        private System.Windows.Forms.Timer? playerRefreshTimer;

        private StatusWebsiteHost? statusWebsiteHost;
        private string cachedPublicIp = "...";
        private PerformanceGraphsPanel? performanceGraphsPanel;
        private RconClient? rconClient;
        private bool rconConnected = false;

        // servers storage
        private readonly List<ServerInfo> servers = new List<ServerInfo>();
        private int SelectedIndex => listBoxServers.SelectedIndex;
        // Designer-created buttons (declared in Form1.Designer.cs)

        // root folder under program directory where servers are stored
        private string ServersRoot => Path.Combine(AppContext.BaseDirectory, "servers");

        private record ServerInfo(string Name, string IP, int Port, string Version, string ServerSoftware = "Paper")
        {
            public int RamMB { get; set; } = 2048;
            public string PropertiesPath { get; set; } = string.Empty;
            public bool EulaAccepted { get; set; } = false;
            public bool Running { get; set; } = false;
            public List<string> Players { get; } = new List<string>();

            // path on disk for this server instance (optional)
            public string FolderPath { get; set; } = string.Empty;

            public bool PostShutdownEnabled { get; set; }
            public string PostShutdownScriptType { get; set; } = "ps1";
            public string PostShutdownScriptFile { get; set; } = string.Empty;

            // UPnP port forwarding
            public bool UpnpEnabled { get; set; } = false;

            // RCON configuration
            public bool RconEnabled { get; set; } = false;
            public int RconPort { get; set; } = 25575;
            public string RconPassword { get; set; } = string.Empty;

            // process instance when running (may be detached)
            [JsonIgnore]
            public Process? ProcessInstance { get; set; }
        }

        // Backup world (world, nether, end) into a single zip file
        private async void backupWorldToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0) { MessageBox.Show("Select a server first.", "Backup world", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var s = servers[SelectedIndex];
            if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath)) { MessageBox.Show("Server folder not found.", "Backup world", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            // Show backup warning if not disabled
            if (!AppSettings.NeverShowBackupWarningAgain)
            {
                using var dlg = new BackupWarningDialog();
                dlg.ShowDialog(this);

                // Update the setting if user checked "never show again"
                if (dlg.NeverShowAgain)
                {
                    AppSettings.NeverShowBackupWarningAgain = true;
                }
            }

            using var sfd = new SaveFileDialog() { Filter = "Zip Archive|*.zip", FileName = MakeSafeFileNameForZip(s.Name + "-world.zip") };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            // candidate world directories to include
            var candidates = new[] { "world", "world_nether", "world_the_end", "DIM-1", "DIM1" };
            var dirs = candidates.Select(d => Path.Combine(s.FolderPath, d)).Where(Directory.Exists).ToList();
            if (dirs.Count == 0)
            {
                MessageBox.Show("No world folders found (expected e.g. 'world', 'world_nether', 'world_the_end').", "Backup world", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var cts = new CancellationTokenSource();
            using var dlg2 = new ProgressDialog(cts, "Creating world backup...");
            var progress = new Progress<int>(pct => dlg2.SetProgress(pct));
            try
            {
                dlg2.Show(this);
                await Task.Run(() => CreateZipFromDirectories(s.FolderPath, dirs, sfd.FileName, progress, cts.Token));
                MessageBox.Show("World backup completed.", "Backup world", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("World backup cancelled.", "Backup world", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"World backup failed: {ex.Message}", "Backup world", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (dlg2.Visible) dlg2.Close();
            }
        }

        // Backup entire server folder into a zip
        private async void backupServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0) { MessageBox.Show("Select a server first.", "Backup server", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var s = servers[SelectedIndex];
            if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath)) { MessageBox.Show("Server folder not found.", "Backup server", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            // Show backup warning if not disabled
            if (!AppSettings.NeverShowBackupWarningAgain)
            {
                using var dlg = new BackupWarningDialog();
                dlg.ShowDialog(this);

                // Update the setting if user checked "never show again"
                if (dlg.NeverShowAgain)
                {
                    AppSettings.NeverShowBackupWarningAgain = true;
                }
            }

            using var sfd = new SaveFileDialog() { Filter = "Zip Archive|*.zip", FileName = MakeSafeFileNameForZip(s.Name + "-server.zip") };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            var cts = new CancellationTokenSource();
            using var dlg2 = new ProgressDialog(cts, "Creating server backup...");
            var progress = new Progress<int>(pct => dlg2.SetProgress(pct));
            try
            {
                dlg2.Show(this);
                // include all files under server folder
                await Task.Run(() => CreateZipFromDirectories(s.FolderPath, new List<string> { s.FolderPath }, sfd.FileName, progress, cts.Token));
                MessageBox.Show("Server backup completed.", "Backup server", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Server backup cancelled.", "Backup server", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Server backup failed: {ex.Message}", "Backup server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (dlg2.Visible) dlg2.Close();
            }
        }

        // Helper: make a simple safe filename
        private string MakeSafeFileNameForZip(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
            if (string.IsNullOrWhiteSpace(name)) name = "backup.zip";
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) name += ".zip";
            return name;
        }

        // Create zip from one or more directories. If directories contains the root itself, include whole tree.
        private void CreateZipFromDirectories(string sourceRoot, System.Collections.Generic.List<string> directories, string destinationZip, IProgress<int> progress, CancellationToken ct)
        {
            // collect files
            var allFiles = new System.Collections.Generic.List<string>();
            foreach (var dir in directories)
            {
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    allFiles.AddRange(files);
                }
                else if (File.Exists(dir))
                {
                    allFiles.Add(dir);
                }
            }

            if (allFiles.Count == 0)
                throw new InvalidOperationException("No files found to include in the archive.");

            // ensure destination directory
            var destDir = Path.GetDirectoryName(destinationZip);
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

            // create temp zip then move (to avoid partial files when cancelled)
            var tmp = destinationZip + ".tmp" + Guid.NewGuid().ToString("N");
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.ReadWrite))
                using (var za = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
                {
                    for (int i = 0; i < allFiles.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var file = allFiles[i];
                        // compute relative path to sourceRoot; if directories contained full root, use that
                        string entryName = Path.GetRelativePath(sourceRoot, file).Replace('\\', '/');
                        if (string.IsNullOrEmpty(entryName) || entryName == ".") entryName = Path.GetFileName(file);

                        var entry = za.CreateEntry(entryName, CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        fileStream.CopyTo(entryStream);

                        var pct = (int)(((i + 1) * 100L) / allFiles.Count);
                        progress?.Report(pct);
                    }
                }

                // move tmp to destination (overwrite)
                if (File.Exists(destinationZip)) File.Delete(destinationZip);
                File.Move(tmp, destinationZip);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        // DTO used for persisting server configuration
        private class ServerConfig
        {
            public string Name { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public string ServerSoftware { get; set; } = "Paper";
            public int Port { get; set; }
            public int RamMB { get; set; }
            public string PropertiesFileName { get; set; } = string.Empty;
            public bool EulaAccepted { get; set; }
            public bool PostShutdownEnabled { get; set; }
            public string PostShutdownScriptType { get; set; } = "ps1";
            public string PostShutdownScriptFile { get; set; } = string.Empty;
            public bool UpnpEnabled { get; set; } = false;
            public bool RconEnabled { get; set; } = false;
            public int RconPort { get; set; } = 25575;
            public string RconPassword { get; set; } = string.Empty;
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

            // Start the process watcher timer
            InitializeProcessWatcher();
            
            // Start the player refresh timer
            InitializePlayerRefreshTimer();

            _ = RefreshPublicIpAsync();
            TryStartStatusWebsiteFromSettings();

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
                                                        cfg.Version,
                                                        cfg.ServerSoftware ?? "Paper")
                                {
                                    RamMB = cfg.RamMB,
                                    PropertiesPath = string.IsNullOrEmpty(cfg.PropertiesFileName) ? string.Empty : Path.Combine(dir, cfg.PropertiesFileName),
                                    EulaAccepted = cfg.EulaAccepted,
                                    FolderPath = dir,
                                    PostShutdownEnabled = cfg.PostShutdownEnabled,
                                    PostShutdownScriptType = string.IsNullOrEmpty(cfg.PostShutdownScriptType) ? "ps1" : cfg.PostShutdownScriptType,
                                    PostShutdownScriptFile = cfg.PostShutdownScriptFile ?? string.Empty,
                                    UpnpEnabled = cfg.UpnpEnabled,
                                    RconEnabled = cfg.RconEnabled,
                                    RconPort = cfg.RconPort,
                                    RconPassword = cfg.RconPassword ?? string.Empty
                                };
                                servers.Add(si);
                                continue;
                            }
                        }




                        // fallback: no config.json ? infer from folder name
                        var folderName = Path.GetFileName(dir);
                        var fallback = new ServerInfo(folderName, "127.0.0.1", 25565, "N/A", "Paper")
                        {
                            FolderPath = dir
                        };
                        // if server.properties exists try to detect port (best-effort)
                        var props = Path.Combine(dir, "server.properties");
                        if (File.Exists(props))
                        {
                            if (TryReadPortFromProperties(props, out var p))
                                fallback = fallback with { Port = p };
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
                lblServerSoftwareValue.Text = "N/A";
                lblIPValue.Text = "N/A";
                lblPortValue.Text = "N/A";
                lblStatusValue.Text = "Stopped";
                listBoxPlayers.Items.Clear();
                if (lblRconValue != null) lblRconValue.Text = "Disabled";

                // keep Start disabled when nothing is selected
                btnStartServer.Enabled = false;
                if (btnDeleteServer != null) btnDeleteServer.Enabled = false;
                if (btnEditProperties != null) btnEditProperties.Enabled = false;
                return;
            }

            var s = servers[SelectedIndex];
            label1.Text = s.Name;
            lblVersionValue.Text = s.Version;
            lblServerSoftwareValue.Text = s.ServerSoftware;
            lblPortValue.Text = s.Port.ToString();

            // Show RCON status
            if (lblRconValue != null)
            {
                if (s.RconEnabled)
                {
                    if (rconConnected && rconClient != null && rconClient.IsAuthenticated)
                    {
                        lblRconValue.Text = $"Connected (Port {s.RconPort})";
                        lblRconValue.ForeColor = Color.Green;
                    }
                    else
                    {
                        lblRconValue.Text = $"Enabled (Port {s.RconPort}) - Not Connected";
                        lblRconValue.ForeColor = Color.Orange;
                    }
                }
                else
                {
                    lblRconValue.Text = "Disabled";
                    lblRconValue.ForeColor = Color.Gray;
                }
            }

            // Show LAN (private) IP immediately and fetch public IP asynchronously
            var lan = GetLocalIPv4Address();
            lblIPValue.Text = $"IP LAN: {lan}\r\nIP: ...";
            _ = FetchAndSetPublicIpAsync(lan);

            // Show Running or Stopped based on server state and process
            if (s.Running && s.ProcessInstance != null && !s.ProcessInstance.HasExited)
            {
                lblStatusValue.Text = "Running";
                
                // Show Stop button when server is running (with or without RCON)
                btnStartServer.Text = "Stop";
                btnStartServer.Enabled = true;
            }
            else
            {
                lblStatusValue.Text = "Stopped";
                s.Running = false; // ensure Running flag matches reality
                // Enable Start button when server is stopped
                btnStartServer.Text = "Start";
                btnStartServer.Enabled = true;
            }

            listBoxPlayers.Items.Clear();
            foreach (var p in s.Players)
                listBoxPlayers.Items.Add(p);

            if (btnDeleteServer != null) btnDeleteServer.Enabled = true;
            if (btnEditProperties != null) btnEditProperties.Enabled = true;
        }

        private void listBoxServers_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSelectedServerInfo();
        }

        private void listBoxPlayers_Click(object sender, EventArgs e)
        {
            if (listBoxPlayers.SelectedIndex < 0) return;
            if (SelectedIndex < 0 || SelectedIndex >= servers.Count) return;
            
            var playerName = listBoxPlayers.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(playerName)) return;
            
            var s = servers[SelectedIndex];
            if (!rconConnected || rconClient == null || !rconClient.IsAuthenticated)
            {
                MessageBox.Show("RCON is not connected. Cannot fetch player information.", "RCON Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // Show player info form
            using var playerInfoForm = new PlayerInfoForm(playerName, rconClient);
            playerInfoForm.ShowDialog(this);
        }

        // START/STOP SERVER: toggle between starting and gracefully stopping via RCON
        private async void btnStartServer_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0) { MessageBox.Show("Select a server first."); return; }
            var s = servers[SelectedIndex];

            // Check if server is running - then perform stop
            if (s.Running && s.ProcessInstance != null && !s.ProcessInstance.HasExited)
            {
                // If RCON is connected, perform graceful stop
                if (rconConnected && rconClient != null && rconClient.IsAuthenticated)
                {
                    await PerformGracefulStopAsync(s);
                }
                else
                {
                    // Fallback to process-based stop (less graceful but functional)
                    await PerformProcessStopAsync(s);
                }
                return;
            }

            // Otherwise, start the server
            if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath))
            {
                MessageBox.Show("Server folder not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            s = SyncPortFromProperties(s);

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
                    var jarPath = Path.Combine(s.FolderPath, "server.jar");

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
                    _ = SendDiscordWebhookAsync(s, true);

                    // Open UPnP port if enabled
                    if (s.UpnpEnabled)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var success = await UpnpService.Instance.OpenPortAsync(s.Port, $"Minecraft Server - {s.Name}");
                                if (!success)
                                {
                                    // Show warning but don't block server start
                                    this.Invoke(() =>
                                    {
                                        MessageBox.Show($"Failed to open port {s.Port} via UPnP. The router may not support UPnP or UPnP may be disabled.", "UPnP Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                this.Invoke(() =>
                                {
                                    MessageBox.Show($"UPnP error: {ex.Message}", "UPnP Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                });
                            }
                        });
                    }

                    // Try to connect to RCON if enabled
                    if (s.RconEnabled)
                    {
                        _ = Task.Run(async () => await TryConnectRconAsync(s));
                    }
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

        // Process-based server shutdown (fallback when RCON is not available)
        private async Task PerformProcessStopAsync(ServerInfo s, bool showWarning = true)
        {
            var proc = s.ProcessInstance;
            if (proc == null || proc.HasExited)
            {
                MessageBox.Show("Server process is not running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Update UI to show stopping state
            btnStartServer.Enabled = false;
            btnStartServer.Text = "Stopping...";
            lblStatusValue.Text = "Stopping...";

            try
            {
                // Show warning about force kill since RCON is not available (only for button click)
                if (showWarning)
                {
                    var result = MessageBox.Show(
                        "RCON is not connected. The server will be forcefully terminated. This may cause data loss. Continue?",
                        "Warning",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes)
                    {
                        // User chose not to force kill, revert UI state
                        btnStartServer.Enabled = true;
                        btnStartServer.Text = "Stop";
                        lblStatusValue.Text = "Running";
                        return;
                    }
                }

                try
                {
                    proc.Kill(entireProcessTree: true);
                    await proc.WaitForExitAsync();
                    HandleServerProcessExited(s);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to stop server: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // Revert UI state on error
                    btnStartServer.Enabled = true;
                    btnStartServer.Text = "Stop";
                    lblStatusValue.Text = "Running";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during process stop: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Revert UI state on error
                btnStartServer.Enabled = true;
                btnStartServer.Text = "Stop";
                lblStatusValue.Text = "Running";
            }
        }

        // Graceful RCON-based server shutdown
        private async Task PerformGracefulStopAsync(ServerInfo s)
        {
            if (rconClient == null || !rconClient.IsAuthenticated)
            {
                MessageBox.Show("RCON is not connected. Cannot perform graceful shutdown.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var proc = s.ProcessInstance;
            if (proc == null || proc.HasExited)
            {
                MessageBox.Show("Server process is not running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Update UI to show stopping state
            btnStartServer.Enabled = false;
            btnStartServer.Text = "Stopping...";
            lblStatusValue.Text = "Stopping...";

            try
            {
                // Send the RCON "stop" command
                await rconClient.SendCommandAsync("stop");

                // Wait for the process to exit naturally with a 30-second timeout
                var timeoutTask = Task.Delay(30000); // 30 seconds
                var exitTask = proc.WaitForExitAsync();

                var completedTask = await Task.WhenAny(exitTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Process didn't exit within 30 seconds, ask user about force kill
                    var result = MessageBox.Show(
                        "Server is taking too long to stop. Force kill?",
                        "Timeout",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        try
                        {
                            proc.Kill(entireProcessTree: true);
                            await proc.WaitForExitAsync();
                            HandleServerProcessExited(s);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to force kill server: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            // Revert UI state on error
                            btnStartServer.Enabled = true;
                            btnStartServer.Text = "Stop";
                            lblStatusValue.Text = "Running";
                        }
                    }
                    else
                    {
                        // User chose not to force kill, revert UI state
                        btnStartServer.Enabled = true;
                        btnStartServer.Text = "Stop";
                        lblStatusValue.Text = "Running";
                        return;
                    }
                }

                // Process has exited, clean up
                HandleServerProcessExited(s);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during graceful shutdown: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Revert UI state on error
                btnStartServer.Enabled = true;
                btnStartServer.Text = "Stop";
                lblStatusValue.Text = "Running";
            }
        }

        // Try to connect to RCON when server starts
        private async Task TryConnectRconAsync(ServerInfo s)
        {
            if (!s.RconEnabled || string.IsNullOrEmpty(s.RconPassword))
            {
                System.Diagnostics.Debug.WriteLine($"[RCON] RCON not enabled for server {s.Name}");
                this.Invoke(() =>
                {
                    LoadSelectedServerInfo();
                });
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[RCON] Attempting to connect to {s.Name} at 127.0.0.1:{s.RconPort}");

            // Retry connection multiple times since server might not be ready immediately
            const int maxRetries = 10;
            const int retryDelayMs = 2000; // 2 seconds between retries

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[RCON] Connection attempt {attempt + 1}/{maxRetries}...");

                    // Use localhost since we're running the server locally
                    rconClient = new RconClient("127.0.0.1", s.RconPort, s.RconPassword);
                    bool connected = await rconClient.ConnectAsync();

                    if (connected)
                    {
                        rconConnected = true;
                        System.Diagnostics.Debug.WriteLine($"[RCON] ✅ Successfully connected and authenticated to {s.Name}");

                        // Update UI on the main thread
                        this.Invoke(() =>
                        {
                            LoadSelectedServerInfo();
                        });

                        // Fetch players after successful connection
                        _ = FetchPlayersFromRconAsync(s);
                        return; // Success, exit the retry loop
                    }
                    else
                    {
                        rconConnected = false;
                        System.Diagnostics.Debug.WriteLine($"[RCON] ⚠️ Connection returned false on attempt {attempt + 1}");
                        rconClient?.Dispose();
                        rconClient = null;
                    }
                }
                catch (Exception ex)
                {
                    // Log the error for debugging
                    System.Diagnostics.Debug.WriteLine($"[RCON] ❌ Connection attempt {attempt + 1} failed: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[RCON] Exception details: {ex.GetType().Name}");
                    rconConnected = false;
                    rconClient?.Dispose();
                    rconClient = null;
                }

                // Wait before retrying (except on the last attempt)
                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(retryDelayMs);
                }
            }

            // All connection attempts failed
            System.Diagnostics.Debug.WriteLine($"[RCON] ❌ Failed to connect to {s.Name} after {maxRetries} attempts");
            System.Diagnostics.Debug.WriteLine($"[RCON] Please check:");
            System.Diagnostics.Debug.WriteLine($"[RCON] 1. Is the Minecraft server running?");
            System.Diagnostics.Debug.WriteLine($"[RCON] 2. Is 'enable-rcon=true' in server.properties?");
            System.Diagnostics.Debug.WriteLine($"[RCON] 3. Does 'rcon.port={s.RconPort}' match in server.properties?");
            System.Diagnostics.Debug.WriteLine($"[RCON] 4. Is 'rcon.password' set to '{s.RconPassword}' in server.properties?");
        }

        // Fetch players from RCON using the 'list' command
        private async Task FetchPlayersFromRconAsync(ServerInfo s)
        {
            if (!rconConnected || rconClient == null || !rconClient.IsAuthenticated)
            {
                System.Diagnostics.Debug.WriteLine("[RCON] RCON not connected or not authenticated. Skipping player fetch.");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[RCON] Sending 'list' command to server {s.Name}...");
                var response = await rconClient.SendCommandAsync("list");

                System.Diagnostics.Debug.WriteLine($"[RCON] Server response: '{response}'");

                // Check if response contains an error
                if (response.Contains("Error executing", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"[RCON] ⚠️ Server returned an error: {response}");
                    System.Diagnostics.Debug.WriteLine("[RCON] This usually means:");
                    System.Diagnostics.Debug.WriteLine("[RCON] 1. RCON is not properly enabled in server.properties");
                    System.Diagnostics.Debug.WriteLine("[RCON] 2. The Minecraft server may be in a broken state");
                    System.Diagnostics.Debug.WriteLine("[RCON] 3. Try restarting the Minecraft server and reconnecting");
                    return;
                }

                if (!string.IsNullOrEmpty(response))
                {
                    // Parse the response. Expected format: "There are X out of Y max players online: player1, player2, ..."
                    var players = ParsePlayerList(response);

                    this.Invoke(() =>
                    {
                        s.Players.Clear();
                        s.Players.AddRange(players);
                        LoadSelectedServerInfo();
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[RCON] ⚠️ Server returned empty response for 'list' command");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RCON] ❌ Failed to fetch players from RCON: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[RCON] Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[RCON] Stack trace: {ex.StackTrace}");
            }
        }

        // Parse player list from RCON response
        private List<string> ParsePlayerList(string response)
        {
            var players = new List<string>();
            
            // RCON 'list' command response format: "There are X out of Y max players online: player1, player2, ..."
            // Or: "There are 0/20 players online:"
            
            try
            {
                // Find the colon that separates the count from the player list
                var colonIndex = response.IndexOf(':');
                if (colonIndex >= 0 && colonIndex < response.Length - 1)
                {
                    var playerPart = response.Substring(colonIndex + 1).Trim();
                    
                    if (!string.IsNullOrEmpty(playerPart))
                    {
                        // Split by comma and trim each player name
                        var playerNames = playerPart.Split(',');
                        foreach (var name in playerNames)
                        {
                            var trimmedName = name.Trim();
                            if (!string.IsNullOrEmpty(trimmedName))
                            {
                                players.Add(trimmedName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse player list: {ex.Message}");
            }
            
            return players;
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

            // Use the new process-based stop method
            await PerformProcessStopAsync(s);
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
                // Get all network interfaces and filter to only physical/real adapters
                var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                        && IsPhysicalNetworkAdapter(nic))
                    .ToList();

                // Priority 1: Look for 192.168.x.x addresses (most common private range)
                foreach (var nic in nics)
                {
                    var ip = GetIPFromInterface(nic, "192.168.");
                    if (ip != null) return ip;
                }

                // Priority 2: Look for 10.x.x.x addresses (Class A private range)
                foreach (var nic in nics)
                {
                    var ip = GetIPFromInterface(nic, "10.");
                    if (ip != null) return ip;
                }

                // Priority 3: Look for 172.16-31.x.x addresses (Class B private range, excluding Sandbox 172.20)
                foreach (var nic in nics)
                {
                    var props = nic.GetIPProperties();
                    foreach (var addr in props.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var ip = addr.Address.ToString();
                            if (ip.StartsWith("172."))
                            {
                                // Only accept 172.16-172.19 and 172.32-223 (skip 172.20-31 for Sandbox/Docker)
                                var parts = ip.Split('.');
                                if (int.TryParse(parts[1], out int secondOctet))
                                {
                                    if ((secondOctet >= 16 && secondOctet <= 19) || secondOctet >= 32)
                                    {
                                        return ip;
                                    }
                                }
                            }
                        }
                    }
                }

                // Fallback: Use Dns method but with additional filtering
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        var s = ip.ToString();
                        // Skip link-local, loopback, and virtual network ranges
                        if (!s.StartsWith("169.254.") && !s.StartsWith("127.") && !IsVirtualNetworkIP(s))
                            return s;
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private bool IsPhysicalNetworkAdapter(System.Net.NetworkInformation.NetworkInterface nic)
        {
            // Exclude virtual adapters by name
            var name = nic.Name.ToLower();
            return !name.Contains("hyper-v")
                && !name.Contains("docker")
                && !name.Contains("vitual")
                && !name.Contains("vpn")
                && !name.Contains("tunnel")
                && !name.Contains("tap")
                && !name.Contains("tun")
                && !name.Contains("loopback")
                && !name.Contains("pseudo")
                && !name.Contains("isatap")
                && !name.Contains("6to4")
                && !name.Contains("teredo")
                && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback
                && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Ppp
                && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Slip;
        }

        private string? GetIPFromInterface(System.Net.NetworkInformation.NetworkInterface nic, string prefix)
        {
            try
            {
                var props = nic.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var ip = addr.Address.ToString();
                        if (ip.StartsWith(prefix) && !IPAddress.IsLoopback(addr.Address))
                            return ip;
                    }
                }
            }
            catch { }
            return null;
        }

        private bool IsVirtualNetworkIP(string ipAddress)
        {
            // Check for known virtual network ranges
            if (ipAddress.StartsWith("172.20.") || ipAddress.StartsWith("172.21.") || 
                ipAddress.StartsWith("172.22.") || ipAddress.StartsWith("172.23.") ||
                ipAddress.StartsWith("172.24.") || ipAddress.StartsWith("172.25.") ||
                ipAddress.StartsWith("172.26.") || ipAddress.StartsWith("172.27.") ||
                ipAddress.StartsWith("172.28.") || ipAddress.StartsWith("172.29.") ||
                ipAddress.StartsWith("172.30.") || ipAddress.StartsWith("172.31."))  // Windows Sandbox uses 172.20-172.31
                return true;
            if (ipAddress.StartsWith("127.")) // Loopback
                return true;
            if (ipAddress.StartsWith("169.254.")) // APIPA/Link-local
                return true;
            return false;
        }

        private async Task FetchAndSetPublicIpAsync(string lanIp)
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(5);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.5 (https://github.com/JaatrovyKnedlicek/MC-Server-Manager-Windows)");
                var ip = (await http.GetStringAsync("https://api.ipify.org")).Trim();
                if (string.IsNullOrEmpty(ip)) ip = "N/A";
                cachedPublicIp = ip;
                if (!IsHandleCreated || IsDisposed) return;
                BeginInvoke(() => lblIPValue.Text = $"IP LAN: {lanIp}\r\nIP: {ip}");
            }
            catch
            {
                cachedPublicIp = "N/A";
                try { if (IsHandleCreated && !IsDisposed) BeginInvoke(() => lblIPValue.Text = $"IP LAN: {lanIp}\r\nIP: N/A"); } catch { }
            }
        }



        private void btnDeleteServer_Click(object sender, EventArgs e) => DeleteSelectedServer();
        private void btnEditProperties_Click(object sender, EventArgs e) => _ = EditSelectedServerPropertiesAsync();

        private async Task EditSelectedServerPropertiesAsync()
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

            // Check if server is running
            bool serverRunning = s.Running && s.ProcessInstance != null && !s.ProcessInstance.HasExited;
            string tempFilePath = null;
            bool changesApplied = false;

            try
            {
                if (!File.Exists(propsPath))
                {
                    // create a simple default file
                    File.WriteAllText(propsPath, "# server.properties\r\n# Generated by MC Server Manager\r\n");
                }

                // remember path
                s.PropertiesPath = propsPath;

                // If server is running, create a temporary copy to edit
                if (serverRunning)
                {
                    tempFilePath = Path.Combine(Path.GetTempPath(), $"server_properties_{Guid.NewGuid()}.tmp");
                    File.Copy(propsPath, tempFilePath, true);
                    
                    // Show info message about editing temp file
                    MessageBox.Show(
                        "The server is currently running, so server.properties is locked. A temporary copy will be edited. Changes will be applied when you save - you may need to restart the server for changes to take effect.",
                        "Server Running",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                // Use the temp file if server is running, otherwise use the original file
                string fileToEdit = serverRunning ? tempFilePath : propsPath;

                using var dlg = new ServerPropertiesForm(fileToEdit);
                if (dlg.ShowDialog(this) == DialogResult.OK && serverRunning)
                {
                    // If server is running and user saved changes, copy back to original
                    // This might fail if server still has the file locked, so we handle it gracefully
                    try
                    {
                        File.Copy(tempFilePath, propsPath, true);
                        MessageBox.Show(
                            "Properties saved successfully. Changes will take effect when the server is restarted.",
                            "Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        changesApplied = true;
                    }
                    catch (IOException)
                    {
                        // If we can't copy back now, save the temp file location and offer to apply later
                        var result = MessageBox.Show(
                            $"Could not apply changes to server.properties because the file is still in use by the running server.\n\nThe changes have been saved to a temporary file:\n{tempFilePath}\n\nWould you like to stop the server now to apply the changes?",
                            "File Locked",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (result == DialogResult.Yes)
                        {
                            // Stop the server using the appropriate method
                            if (rconConnected && rconClient != null && rconClient.IsAuthenticated)
                            {
                                await PerformGracefulStopAsync(s);
                            }
                            else
                            {
                                await PerformProcessStopAsync(s, false);
                            }

                            // Try to copy the file again after server stops
                            await Task.Delay(2000); // Wait for server to fully stop
                            
                            try
                            {
                                File.Copy(tempFilePath, propsPath, true);
                                MessageBox.Show("Changes applied successfully after server stopped.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                changesApplied = true;
                            }
                            catch (IOException)
                            {
                                MessageBox.Show(
                                    $"Still could not apply changes. The modified file is saved at:\n{tempFilePath}\n\nYou can manually copy this file to replace server.properties when the server is stopped.",
                                    "Manual Action Required",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                $"Changes saved to temporary file:\n{tempFilePath}\n\nYou can manually copy this file to replace server.properties when the server is stopped.",
                                "Changes Saved to Temp File",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open server.properties: {ex.Message}", "Edit Properties", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Clean up temp file only if changes were successfully applied
                if (changesApplied && tempFilePath != null && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Check if any servers are still running
            var runningServers = servers.Where(s => s.Running && s.ProcessInstance != null && !s.ProcessInstance.HasExited).ToList();

            if (runningServers.Count > 0)
            {
                // If the user has never disabled the warning, show it
                if (!AppSettings.NeverShowStopWarningAgain)
                {
                    using var dlg = new StopWarningDialog();
                    dlg.ShowDialog(this);

                    // Update the setting if user checked "never show again"
                    if (dlg.NeverShowAgain)
                    {
                        AppSettings.NeverShowStopWarningAgain = true;
                    }
                }
                // Always allow closing - servers will continue running unaffected
            }

            if (consoleAllocated)
            {
                try { FreeConsole(); } catch { }
                consoleAllocated = false;
            }

            try { statusWebsiteHost?.Stop(); } catch { }
            
            // Close all UPnP ports when application closes
            _ = Task.Run(async () =>
            {
                try
                {
                    await UpnpService.Instance.CloseAllPortsAsync();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            });
            
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
                    var destJar = Path.Combine(folder, "server.jar");
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
                ServerSoftware = dlg.ServerSoftware,
                Port = port,
                RamMB = dlg.ServerRamMB,
                PropertiesFileName = string.Empty, // no server.properties
                EulaAccepted = dlg.EulaAccepted,
                RconEnabled = false,
                RconPort = 25575,
                RconPassword = string.Empty
            };

            var configJson = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            try { File.WriteAllText(Path.Combine(folder, "config.json"), configJson); } catch { }

            // add to in-memory list and update UI
            var s = new ServerInfo(name, ip, port, version, dlg.ServerSoftware)
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
            MessageBox.Show("Minecraft Server Manager 3\nVersion: 3.5\n© Ján Repka 2026", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void label1_Click(object sender, EventArgs e) { }

        // Add this method inside the Form1 class (e.g. above CreateUniqueServerFolder)
        private int MapMinecraftToJavaMajor(string mcVersion)
        {
            // Try parse as Version
            if (!Version.TryParse(mcVersion, out var v))
            {
                // fallback: choose latest (Java 25)
                return 25;
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

            // non-1.x versions (new versioning scheme)
            if (v.Major >= 26)
            {
                // 26.1 and above -> Java 25
                if (v.Major == 26 && v.Minor >= 1)
                    return 25;
                // 26.0 -> Java 21 (assuming 26.0 still uses Java 21)
                if (v.Major == 26 && v.Minor == 0)
                    return 21;
                // 27+ -> Java 25 (future-proof)
                if (v.Major >= 27)
                    return 25;
            }
            
            // 2.x - 25.x versions -> Java 21
            if (v.Major >= 2 && v.Major <= 25)
                return 21;
            
            // fallback for unknown future versions
            return 25;
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
        private async void serverPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
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

            // Check if server is running
            bool serverRunning = s.Running && s.ProcessInstance != null && !s.ProcessInstance.HasExited;
            string tempFilePath = null;
            bool changesApplied = false;

            try
            {
                if (!File.Exists(propsPath))
                {
                    // create a simple default file
                    File.WriteAllText(propsPath, "# server.properties\n# Generated by MC Server Manager\n");
                }

                // remember path
                s.PropertiesPath = propsPath;

                // If server is running, create a temporary copy to edit
                if (serverRunning)
                {
                    tempFilePath = Path.Combine(Path.GetTempPath(), $"server_properties_{Guid.NewGuid()}.tmp");
                    File.Copy(propsPath, tempFilePath, true);
                    
                    // Show info message about editing temp file
                    MessageBox.Show(
                        "The server is currently running, so server.properties is locked. A temporary copy will be opened in Notepad. After you save and close Notepad, you'll be prompted to apply the changes.",
                        "Server Running",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                // Use the temp file if server is running, otherwise use the original file
                string fileToEdit = serverRunning ? tempFilePath : propsPath;

                var psi = new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{fileToEdit}\"",
                    UseShellExecute = true
                };
                
                var notepadProcess = Process.Start(psi);
                
                if (notepadProcess != null)
                {
                    // Wait for Notepad to close
                    await notepadProcess.WaitForExitAsync();
                    
                    // If server was running, prompt to apply changes
                    if (serverRunning)
                    {
                        var result = MessageBox.Show(
                            "Notepad has been closed. Would you like to apply the changes to server.properties now?",
                            "Apply Changes",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            // Try to copy the changes back
                            try
                            {
                                File.Copy(tempFilePath, propsPath, true);
                                MessageBox.Show(
                                    "Changes applied successfully. Changes will take effect when the server is restarted.",
                                    "Success",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                                changesApplied = true;
                            }
                            catch (IOException)
                            {
                                // If we can't copy back now, offer to stop the server
                                var stopResult = MessageBox.Show(
                                    $"Could not apply changes to server.properties because the file is still in use by the running server.\n\nThe modified file is saved at:\n{tempFilePath}\n\nWould you like to stop the server now to apply the changes?",
                                    "File Locked",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Warning);

                                if (stopResult == DialogResult.Yes)
                                {
                                    // Stop the server using the appropriate method
                                    if (rconConnected && rconClient != null && rconClient.IsAuthenticated)
                                    {
                                        await PerformGracefulStopAsync(s);
                                    }
                                    else
                                    {
                                        await PerformProcessStopAsync(s, false);
                                    }

                                    // Try to copy the file again after server stops
                                    await Task.Delay(2000); // Wait for server to fully stop
                                    
                                    try
                                    {
                                        File.Copy(tempFilePath, propsPath, true);
                                        MessageBox.Show("Changes applied successfully after server stopped.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                        changesApplied = true;
                                    }
                                    catch (IOException)
                                    {
                                        MessageBox.Show(
                                            $"Still could not apply changes. The modified file is saved at:\n{tempFilePath}\n\nYou can manually copy this file to replace server.properties when the server is stopped.",
                                            "Manual Action Required",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Warning);
                                    }
                                }
                                else
                                {
                                    MessageBox.Show(
                                        $"Changes saved to temporary file:\n{tempFilePath}\n\nYou can manually copy this file to replace server.properties when the server is stopped.",
                                        "Changes Saved to Temp File",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Information);
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                $"Changes saved to temporary file:\n{tempFilePath}\n\nYou can manually copy this file to replace server.properties when the server is stopped.",
                                "Changes Saved to Temp File",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open server.properties: {ex.Message}", "Edit Properties", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Clean up temp file only if changes were successfully applied
                if (changesApplied && tempFilePath != null && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        // Opens the RAM editor from Tools menu
        private void serverEditRamToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0) { MessageBox.Show("Select a server first.", "Edit RAM", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var s = servers[SelectedIndex];
            if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath)) { MessageBox.Show("Server folder not found.", "Edit RAM", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            using var dlg = new EditRamForm(s.RamMB);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                s.RamMB = dlg.SelectedRamMB;
                try
                {
                    SaveServerConfig(s);
                    if (!string.IsNullOrEmpty(s.FolderPath) && Directory.Exists(s.FolderPath))
                        UpdateStartCmdRamValues(s.FolderPath, s.RamMB);
                }
                catch { }

                PopulateServerList();
                LoadSelectedServerInfo();
            }
        }

        private void openServerFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0 || SelectedIndex >= servers.Count)
            {
                MessageBox.Show("Select a server first.", "Open Server Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var path = servers[SelectedIndex].FolderPath;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show("Server folder not found.", "Open Server Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cleanLogsFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0 || SelectedIndex >= servers.Count)
            {
                MessageBox.Show("Select a server first.", "Clean Logs Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var s = servers[SelectedIndex];
            if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath))
            {
                MessageBox.Show("Server folder not found.", "Clean Logs Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var logsPath = Path.Combine(s.FolderPath, "logs");
            if (!Directory.Exists(logsPath))
            {
                MessageBox.Show("Logs folder not found.", "Clean Logs Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string[] gzLogs;
            try
            {
                gzLogs = Directory.GetFiles(logsPath, "*.log.gz", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read logs folder: {ex.Message}", "Clean Logs Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (gzLogs.Length == 0)
            {
                MessageBox.Show("No compressed log files (.log.gz) to delete.", "Clean Logs Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete {gzLogs.Length} compressed log file(s) from '{s.Name}'?",
                "Clean Logs Folder",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            var deleted = 0;
            var failed = 0;
            foreach (var file in gzLogs)
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch
                {
                    failed++;
                }
            }

            if (failed == 0)
                MessageBox.Show($"Deleted {deleted} log file(s).", "Clean Logs Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show($"Deleted {deleted} log file(s). {failed} could not be deleted.", "Clean Logs Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Open plugins folder for selected server (or servers root if none selected)
        private void openPluginsFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path;
            if (SelectedIndex >= 0 && SelectedIndex < servers.Count && !string.IsNullOrEmpty(servers[SelectedIndex].FolderPath))
            {
                path = Path.Combine(servers[SelectedIndex].FolderPath, "plugins");
            }
            else
            {
                path = ServersRoot;
            }

            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Open router settings page in default browser; tries to detect default gateway and falls back to 192.168.1.1
        private void openRouterSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string gateway = GetDefaultGateway() ?? "192.168.1.1";
            var url = $"http://{gateway}/";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open router settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Kill server process from Tools menu
        private void killServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0)
            {
                MessageBox.Show("Select a server first.", "Kill Server", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var s = servers[SelectedIndex];

            if (!s.Running || s.ProcessInstance == null)
            {
                MessageBox.Show("Server is not running or not tracked by the manager.", "Kill Server", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show($"Force kill server '{s.Name}'? This will forcefully terminate the process.", "Kill Server", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                var proc = s.ProcessInstance;
                if (!proc.HasExited)
                {
                    proc.Kill(true);
                    proc.WaitForExit(3000);
                    HandleServerProcessExited(s);
                    MessageBox.Show("Server process killed.", "Kill Server", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to kill server process: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Check port availability from Tools menu
        private async void checkPortAvailabilityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0)
            {
                MessageBox.Show("Select a server first.", "Check Port Availability", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var s = servers[SelectedIndex];
            var port = s.Port;

            // Sync port from properties if available
            var propsPath = s.PropertiesPath;
            if (string.IsNullOrEmpty(propsPath) && !string.IsNullOrEmpty(s.FolderPath))
            {
                propsPath = Path.Combine(s.FolderPath, "server.properties");
            }

            if (!string.IsNullOrEmpty(propsPath) && File.Exists(propsPath) && TryReadPortFromProperties(propsPath, out var propsPort))
            {
                port = propsPort;
            }

            var resultsForm = new PortCheckResultsForm(s.Name);
            resultsForm.Show(this);

            try
            {
                await CheckPortAvailabilityAsync(s, port, resultsForm);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Port check failed: {ex.Message}", "Check Port Availability", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Server icon settings from Tools menu
        private void serverIconToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0)
            {
                MessageBox.Show("Select a server first.", "Server Icon", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var s = servers[SelectedIndex];
            if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath))
            {
                MessageBox.Show("Server folder not found.", "Server Icon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var dlg = new ServerIconForm(s.Name, s.FolderPath);
            dlg.ShowDialog(this);
        }

        // UPnP settings from Tools menu
        private void upnpSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0)
            {
                MessageBox.Show("Select a server first.", "UPnP Port Forwarding", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var s = servers[SelectedIndex];
            if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath))
            {
                MessageBox.Show("Server folder not found.", "UPnP Port Forwarding", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var dlg = new UpnpSettingsForm(s.UpnpEnabled);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                s.UpnpEnabled = dlg.UpnpEnabled;
                try
                {
                    SaveServerConfig(s);
                }
                catch { }

                PopulateServerList();
                LoadSelectedServerInfo();
            }
        }

        private void performanceGraphsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (performanceGraphsToolStripMenuItem.Checked)
            {
                // Show performance graphs
                if (performanceGraphsPanel == null)
                {
                    performanceGraphsPanel = new PerformanceGraphsPanel
                    {
                        Dock = DockStyle.Bottom,
                        Height = 320
                    };
                    Controls.Add(performanceGraphsPanel);
                    performanceGraphsPanel.BringToFront();
                }
                else
                {
                    performanceGraphsPanel.Visible = true;
                }
            }
            else
            {
                // Hide performance graphs
                if (performanceGraphsPanel != null)
                {
                    performanceGraphsPanel.Visible = false;
                }
            }
        }

        private void rconConsoleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0) { MessageBox.Show("Select a server first.", "RCON Console", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var s = servers[SelectedIndex];

            if (!s.RconEnabled)
            {
                MessageBox.Show("RCON is not enabled for this server. Please enable RCON in server settings first.", "RCON Console", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(s.RconPassword))
            {
                MessageBox.Show("RCON password is not set. Please configure RCON password in server settings.", "RCON Console", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var rconForm = new RconConsoleForm(s.IP, s.RconPort, s.RconPassword);
            rconForm.ShowDialog(this);
        }

        private void rconSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0) { MessageBox.Show("Select a server first.", "RCON Settings", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var s = servers[SelectedIndex];

            using var dlg = new RconSettingsForm(s.RconEnabled, s.RconPort, s.RconPassword);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                s.RconEnabled = dlg.RconEnabled;
                s.RconPort = dlg.RconPort;
                s.RconPassword = dlg.RconPassword;

                try
                {
                    SaveServerConfig(s);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save RCON settings: {ex.Message}", "RCON Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Check if server port is open and available to the public
        private async Task CheckPortAvailabilityAsync(ServerInfo server, int port, PortCheckResultsForm resultsForm)
        {
            try
            {
                var lanIp = GetLocalIPv4Address();
                var publicIp = cachedPublicIp;

                // Add initial items to ListView with "Checking" status
                resultsForm.AddLocalPortCheck(port, "127.0.0.1");
                resultsForm.AddPublicPortCheck(port, publicIp);

                var statusWebsiteEnabled = AppSettings.StatusWebsiteEnabled;
                var statusWebsitePort = AppSettings.StatusWebsitePort;

                if (statusWebsiteEnabled)
                {
                    resultsForm.AddStatusWebsiteCheck(statusWebsitePort, publicIp);
                }

                // Step 1: Check local port availability
                await Task.Delay(100);
                var localPortOpen = await IsPortAvailableLocallyAsync(port);
                resultsForm.UpdateLocalPortResult(localPortOpen);

                // Step 2: Check public port availability
                await Task.Delay(100);
                var publicPortOpen = await IsPortAvailablePubliclyAsync(publicIp, port);
                resultsForm.UpdatePublicPortResult(publicPortOpen);

                // Step 3: Check status website if enabled
                if (statusWebsiteEnabled)
                {
                    await Task.Delay(100);
                    var statusWebsiteOpen = await IsPortAvailablePubliclyAsync(publicIp, statusWebsitePort);
                    resultsForm.UpdateStatusWebsiteResult(statusWebsiteOpen);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't crash
                System.Diagnostics.Debug.WriteLine($"Port check error: {ex.Message}");
            }
        }

        // Check if port is available locally
        private async Task<bool> IsPortAvailableLocallyAsync(int port)
        {
            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync("127.0.0.1", port);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    return false;
                }

                await connectTask;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Check if port is available publicly using external service
        private async Task<bool> IsPortAvailablePubliclyAsync(string publicIp, int port)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.2 (https://github.com/JaatrovyKnedlicek/MC-Server-Manager-Windows)");
                var response = await httpClient.GetAsync($"https://api.mcsrvstat.us/2/{publicIp}:{port}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return content.Contains("\"online\":true");
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Attempt to find the first IPv4 default gateway on active interfaces
        private string? GetDefaultGateway()
        {
            try
            {
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                        continue;
                    var props = nic.GetIPProperties();
                    foreach (var ga in props.GatewayAddresses)
                    {
                        var addr = ga.Address;
                        if (addr != null && addr.AddressFamily == AddressFamily.InterNetwork && !addr.ToString().Equals("0.0.0.0"))
                            return addr.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Updates the start.cmd file with new RAM values (Xms and Xmx)
        /// </summary>
        private void UpdateStartCmdRamValues(string serverFolderPath, int ramMB)
        {
            try
            {
                var startCmdPath = Path.Combine(serverFolderPath, "start.cmd");
                if (!File.Exists(startCmdPath))
                    return;

                // Read the current start.cmd content
                var content = File.ReadAllText(startCmdPath);

                // Convert RAM MB to appropriate format (G for GB, M for MB)
                string ramArg = (ramMB % 1024 == 0) ? $"{ramMB / 1024}G" : $"{ramMB}M";

                // Replace -Xms and -Xmx values using regex
                // Pattern: -Xms<number>[GM] and -Xmx<number>[GM]
                content = System.Text.RegularExpressions.Regex.Replace(content, @"-Xms\d+[GM]", $"-Xms{ramArg}");
                content = System.Text.RegularExpressions.Regex.Replace(content, @"-Xmx\d+[GM]", $"-Xmx{ramArg}");

                // Write the updated content back
                File.WriteAllText(startCmdPath, content);
            }
            catch
            {
                // Silently ignore errors updating start.cmd
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Clean up RCON client when form closes
            rconClient?.Dispose();
            rconClient = null;
            rconConnected = false;
        }

        /// <summary>
        /// Initialize and start the background process watcher timer
        /// </summary>
        private void InitializeProcessWatcher()
        {
            processWatcherTimer = new System.Windows.Forms.Timer();
            processWatcherTimer.Interval = 1000; // Check every 1 second
            processWatcherTimer.Tick += ProcessWatcherTimer_Tick;
            processWatcherTimer.Start();
        }

        /// <summary>
        /// Initialize and start the player list refresh timer
        /// </summary>
        private void InitializePlayerRefreshTimer()
        {
            playerRefreshTimer = new System.Windows.Forms.Timer();
            playerRefreshTimer.Interval = 5000; // Refresh every 5 seconds
            playerRefreshTimer.Tick += PlayerRefreshTimer_Tick;
            playerRefreshTimer.Start();
        }

        /// <summary>
        /// Timer tick handler that monitors running server processes
        /// </summary>
        private void ProcessWatcherTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                foreach (var server in servers)
                {
                    try
                    {
                        if (server.Running && server.ProcessInstance != null && server.ProcessInstance.HasExited)
                            HandleServerProcessExited(server);
                    }
                    catch
                    {
                        // ignore a single server watcher failure
                    }
                }
            }
            catch
            {
                // Silently ignore any errors in the watcher
            }
        }

        /// <summary>
        /// Timer tick handler that refreshes player list from RCON when connected
        /// </summary>
        private void PlayerRefreshTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Only refresh if we have a selected server and RCON is connected
                if (SelectedIndex >= 0 && SelectedIndex < servers.Count)
                {
                    var s = servers[SelectedIndex];
                    if (rconConnected && rconClient != null && rconClient.IsAuthenticated && s.Running)
                    {
                        _ = FetchPlayersFromRconAsync(s);
                    }
                }
            }
            catch
            {
                // Silently ignore any errors in the player refresh
            }
        }

        private void statusWebsiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var dlg = new StatusWebsiteForm(AppSettings.StatusWebsiteEnabled, AppSettings.StatusWebsitePort, GetLocalIPv4Address(), cachedPublicIp);
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            if (dlg.WebsiteEnabled)
            {
                try
                {
                    StartStatusWebsite(dlg.Port);
                    AppSettings.StatusWebsitePort = dlg.Port;
                    AppSettings.StatusWebsiteEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not start the status website on port {dlg.Port}: {ex.Message}", "Status Website", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                StopStatusWebsite();
                AppSettings.StatusWebsitePort = dlg.Port;
                AppSettings.StatusWebsiteEnabled = false;
            }
        }

        private void discordWebhookToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var dlg = new DiscordWebhookForm(AppSettings.DiscordWebhookEnabled, AppSettings.DiscordWebhookUri);
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            AppSettings.DiscordWebhookEnabled = dlg.WebhookEnabled;
            AppSettings.DiscordWebhookUri = dlg.WebhookUri;
        }

        private static async Task SendDiscordWebhookAsync(ServerInfo server, bool online)
        {
            if (!AppSettings.DiscordWebhookEnabled || string.IsNullOrWhiteSpace(AppSettings.DiscordWebhookUri))
                return;

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.5 (https://github.com/JaatrovyKnedlicek/MC-Server-Manager-Windows)");
                var payload = new
                {
                    username = "MC Server Manager",
                    embeds = new[]
                    {
                        new
                        {
                            title = $"{server.Name} is {(online ? "online" : "offline")}",
                            description = online ? "The Minecraft server is now online." : "The Minecraft server is now offline.",
                            color = online ? 5763719 : 15548997
                        }
                    }
                };
                using var response = await http.PostAsJsonAsync(AppSettings.DiscordWebhookUri, payload);
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                // Webhook failures must not affect server management.
            }
        }

        private void TryStartStatusWebsiteFromSettings()
        {
            if (!AppSettings.StatusWebsiteEnabled)
                return;

            try
            {
                StartStatusWebsite(AppSettings.StatusWebsitePort);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Status website could not start on port {AppSettings.StatusWebsitePort}: {ex.Message}", "Status Website", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StartStatusWebsite(int port)
        {
            statusWebsiteHost ??= new StatusWebsiteHost(GetStatusWebsiteServers);
            statusWebsiteHost.Start(port);
        }

        private void StopStatusWebsite()
        {
            statusWebsiteHost?.Stop();
        }

        private IReadOnlyList<StatusWebsiteServerInfo> GetStatusWebsiteServers()
        {
            if (IsHandleCreated && InvokeRequired)
                return (IReadOnlyList<StatusWebsiteServerInfo>)Invoke(GetStatusWebsiteServers);

            var lan = GetLocalIPv4Address();
            var list = new List<StatusWebsiteServerInfo>(servers.Count);
            foreach (var s in servers)
            {
                var port = s.Port;
                var propsPath = ResolvePropertiesPath(s);
                if (!string.IsNullOrEmpty(propsPath) && TryReadPortFromProperties(propsPath, out var propsPort))
                    port = propsPort;

                var publicIp = string.IsNullOrWhiteSpace(cachedPublicIp) || cachedPublicIp == "..." ? "N/A" : cachedPublicIp;
                var lanIp = string.IsNullOrWhiteSpace(lan) ? "N/A" : lan;
                var online = s.Running && s.ProcessInstance != null && !s.ProcessInstance.HasExited;

                list.Add(new StatusWebsiteServerInfo
                {
                    Name = s.Name,
                    Motd = ReadMotd(propsPath),
                    Version = string.IsNullOrWhiteSpace(s.Version) ? "N/A" : s.Version,
                    PublicAddress = $"{publicIp}:{port}",
                    LanAddress = $"{lanIp}:{port}",
                    Online = online
                });
            }

            return list;
        }

        private static string? ResolvePropertiesPath(ServerInfo s)
        {
            if (!string.IsNullOrEmpty(s.PropertiesPath) && File.Exists(s.PropertiesPath))
                return s.PropertiesPath;
            if (string.IsNullOrEmpty(s.FolderPath))
                return null;
            var path = Path.Combine(s.FolderPath, "server.properties");
            return File.Exists(path) ? path : null;
        }

        private static string ReadMotd(string? propertiesPath)
        {
            var motd = ReadServerProperty(propertiesPath, "motd");
            motd = UnescapeServerProperty(motd);
            motd = StripMinecraftFormatting(motd);
            return string.IsNullOrWhiteSpace(motd) ? "A Minecraft Server" : motd.Trim();
        }

        private static string ReadServerProperty(string? propertiesPath, string key)
        {
            if (string.IsNullOrEmpty(propertiesPath) || !File.Exists(propertiesPath))
                return string.Empty;

            var prefix = key + "=";
            try
            {
                foreach (var line in File.ReadAllLines(propertiesPath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith('#') || !trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    return trimmed[prefix.Length..];
                }
            }
            catch
            {
                // ignore unreadable properties files
            }

            return string.Empty;
        }

        private static string UnescapeServerProperty(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            value = value.Replace("\\n", " ").Replace("\\u00A7", "§").Replace("\\u00a7", "§");
            return value;
        }

        private static string StripMinecraftFormatting(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var sb = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if ((text[i] == '§' || text[i] == '&') && i + 1 < text.Length)
                {
                    i++;
                    continue;
                }
                sb.Append(text[i]);
            }
            return sb.ToString();
        }

        private async Task RefreshPublicIpAsync()
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(5);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.5 (https://github.com/JaatrovyKnedlicek/MC-Server-Manager-Windows)");
                var ip = (await http.GetStringAsync("https://api.ipify.org")).Trim();
                if (!string.IsNullOrEmpty(ip))
                    cachedPublicIp = ip;
            }
            catch
            {
                if (cachedPublicIp == "...")
                    cachedPublicIp = "N/A";
            }
        }

        private void postShutdownActionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedIndex < 0)
            {
                MessageBox.Show("Select a server first.", "Post-Shutdown Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var s = servers[SelectedIndex];
            if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath))
            {
                MessageBox.Show("Server folder not found.", "Post-Shutdown Actions", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var dlg = new PostShutdownActionsForm(s.PostShutdownEnabled, s.PostShutdownScriptType, s.PostShutdownScriptFile, s.FolderPath);
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            s.PostShutdownEnabled = dlg.ActionsEnabled;
            s.PostShutdownScriptType = dlg.ScriptType;
            s.PostShutdownScriptFile = dlg.ScriptFileName;
            try { SaveServerConfig(s); } catch { }
        }

        private ServerInfo SyncPortFromProperties(ServerInfo s)
        {
            var propsPath = s.PropertiesPath;
            if (string.IsNullOrEmpty(propsPath) || !File.Exists(propsPath))
            {
                if (string.IsNullOrEmpty(s.FolderPath))
                    return s;
                propsPath = Path.Combine(s.FolderPath, "server.properties");
            }

            if (!File.Exists(propsPath) || !TryReadPortFromProperties(propsPath, out var port) || port == s.Port)
                return s;

            var updated = s with { Port = port };
            for (int i = 0; i < servers.Count; i++)
            {
                if (ReferenceEquals(servers[i], s))
                {
                    servers[i] = updated;
                    break;
                }
            }

            try { SaveServerConfig(updated); } catch { }
            return updated;
        }

        private static bool TryReadPortFromProperties(string propertiesPath, out int port)
        {
            port = 0;
            try
            {
                foreach (var line in File.ReadAllLines(propertiesPath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith('#') || !trimmed.StartsWith("server-port="))
                        continue;

                    var value = trimmed.Substring("server-port=".Length).Trim();
                    if (int.TryParse(value, out port) && port is > 0 and <= 65535)
                        return true;
                }
            }
            catch
            {
                // ignore unreadable properties files
            }

            return false;
        }

        private void SaveServerConfig(ServerInfo s)
        {
            if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath))
                return;

            var cfg = new ServerConfig
            {
                Name = s.Name,
                Version = s.Version,
                ServerSoftware = s.ServerSoftware,
                Port = s.Port,
                RamMB = s.RamMB,
                PropertiesFileName = string.IsNullOrEmpty(s.PropertiesPath) ? string.Empty : Path.GetFileName(s.PropertiesPath),
                EulaAccepted = s.EulaAccepted,
                PostShutdownEnabled = s.PostShutdownEnabled,
                PostShutdownScriptType = s.PostShutdownScriptType,
                PostShutdownScriptFile = s.PostShutdownScriptFile,
                UpnpEnabled = s.UpnpEnabled,
                RconEnabled = s.RconEnabled,
                RconPort = s.RconPort,
                RconPassword = s.RconPassword
            };
            var configJson = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(s.FolderPath, "config.json"), configJson);
        }

        private void HandleServerProcessExited(ServerInfo s)
        {
            var wasRunning = s.Running || s.ProcessInstance != null;
            s.Running = false;
            s.ProcessInstance = null;
            s.Players.Clear();

            // Clean up RCON connection
            rconConnected = false;
            rconClient?.Dispose();
            rconClient = null;

            if (wasRunning)
            {
                RunPostShutdownAction(s);
                _ = SendDiscordWebhookAsync(s, false);

                // Close UPnP port if it was opened
                if (s.UpnpEnabled)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await UpnpService.Instance.ClosePortAsync(s.Port);
                        }
                        catch
                        {
                            // Ignore errors when closing port
                        }
                    });
                }
            }

            if (SelectedIndex >= 0 && SelectedIndex < servers.Count && ReferenceEquals(servers[SelectedIndex], s))
                LoadSelectedServerInfo();
        }

        private void RunPostShutdownAction(ServerInfo s)
        {
            if (!s.PostShutdownEnabled)
                return;
            if (string.IsNullOrEmpty(s.FolderPath) || string.IsNullOrEmpty(s.PostShutdownScriptFile))
                return;

            var scriptPath = Path.IsPathRooted(s.PostShutdownScriptFile)
                ? s.PostShutdownScriptFile
                : Path.Combine(s.FolderPath, s.PostShutdownScriptFile);

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"Post-shutdown script was not found:\n{scriptPath}", "Post-Shutdown Actions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var type = (s.PostShutdownScriptType ?? "ps1").Trim().ToLowerInvariant();
                ProcessStartInfo psi;
                switch (type)
                {
                    case "ps1":
                        psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                            WorkingDirectory = s.FolderPath,
                            UseShellExecute = true
                        };
                        break;
                    case "py":
                        if (!TryStartPython(scriptPath, s.FolderPath))
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = scriptPath,
                                WorkingDirectory = s.FolderPath,
                                UseShellExecute = true
                            });
                        return;
                    default:
                        psi = new ProcessStartInfo
                        {
                            FileName = scriptPath,
                            WorkingDirectory = s.FolderPath,
                            UseShellExecute = true
                        };
                        break;
                }

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to run post-shutdown script: {ex.Message}", "Post-Shutdown Actions", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool TryStartPython(string scriptPath, string workingDirectory)
        {
            foreach (var exe in new[] { "py", "python", "python3" })
            {
                try
                {
                    var args = exe == "py" ? $"-3 \"{scriptPath}\"" : $"\"{scriptPath}\"";
                    var started = Process.Start(new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = args,
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = true
                    });
                    if (started != null)
                        return true;
                }
                catch
                {
                    // try the next interpreter name
                }
            }

            return false;
        }

        private void toolsToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}
