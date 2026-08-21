using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Drawing;
using System.IO;
using System.IO.Compression;
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

        // Server process watcher timer
        private System.Windows.Forms.Timer? processWatcherTimer;

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

            public bool PostShutdownEnabled { get; set; }
            public string PostShutdownScriptType { get; set; } = "ps1";
            public string PostShutdownScriptFile { get; set; } = string.Empty;

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
            public int Port { get; set; }
            public int RamMB { get; set; }
            public string PropertiesFileName { get; set; } = string.Empty;
            public bool EulaAccepted { get; set; }
            public bool PostShutdownEnabled { get; set; }
            public string PostShutdownScriptType { get; set; } = "ps1";
            public string PostShutdownScriptFile { get; set; } = string.Empty;
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
                                    FolderPath = dir,
                                    PostShutdownEnabled = cfg.PostShutdownEnabled,
                                    PostShutdownScriptType = string.IsNullOrEmpty(cfg.PostShutdownScriptType) ? "ps1" : cfg.PostShutdownScriptType,
                                    PostShutdownScriptFile = cfg.PostShutdownScriptFile ?? string.Empty
                                };
                                servers.Add(si);
                                continue;
                            }
                        }




                        // fallback: no config.json ? infer from folder name
                        var folderName = Path.GetFileName(dir);
                        var fallback = new ServerInfo(folderName, "127.0.0.1", 25565, "N/A")
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

            // Show Running or Stopped based on server state and process
            if (s.Running && s.ProcessInstance != null && !s.ProcessInstance.HasExited)
            {
                lblStatusValue.Text = "Running";
                // Disable Start button when server is running
                btnStartServer.Enabled = false;
            }
            else
            {
                lblStatusValue.Text = "Stopped";
                s.Running = false; // ensure Running flag matches reality
                // Enable Start button when server is stopped
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
                HandleServerProcessExited(s);
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
            MessageBox.Show("Minecraft Server Manager 3\nVersion: 3.1\n© Ján Repka 2026", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);

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

        // Opens the selected server's server.properties in an editor window (creates file if missing)
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
                    File.WriteAllText(propsPath, "# server.properties\r\n# Generated by MC Server Manager\r\n");
                }

                            // remember path
                                s.PropertiesPath = propsPath;

                                using var dlg = new ServerPropertiesForm(propsPath);
                                dlg.ShowDialog(this);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Failed to open server.properties: {ex.Message}", "Edit Properties", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }

                        // Opens the selected server's server.properties in Notepad (creates file if missing)
                        private void serverPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
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
                Port = s.Port,
                RamMB = s.RamMB,
                PropertiesFileName = string.IsNullOrEmpty(s.PropertiesPath) ? string.Empty : Path.GetFileName(s.PropertiesPath),
                EulaAccepted = s.EulaAccepted,
                PostShutdownEnabled = s.PostShutdownEnabled,
                PostShutdownScriptType = s.PostShutdownScriptType,
                PostShutdownScriptFile = s.PostShutdownScriptFile
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

            if (wasRunning)
                RunPostShutdownAction(s);

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
    }
}
