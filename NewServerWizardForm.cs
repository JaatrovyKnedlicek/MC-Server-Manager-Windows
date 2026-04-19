using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public partial class NewServerWizardForm : Form
    {
        private int stepIndex = 0; // 0..2

        public string ServerName => txtName.Text.Trim();
        public string ServerVersion => cmbVersions.SelectedItem?.ToString() ?? string.Empty;
        public int ServerRamMB => (int)numRam.Value;
        public bool EulaAccepted => chkEulaAccept.Checked;

        // map of version -> download url loaded from paper-versions.json
        private System.Collections.Generic.Dictionary<string, string> paperVersions = new();

        // path of downloaded jar (in the server folder)
        public string DownloadedJarPath { get; private set; } = string.Empty;

        // folder created for this server during step 0 -> step 1 transition
        public string ServerFolderPath { get; private set; } = string.Empty;

        public NewServerWizardForm()
        {
            InitializeComponent();
            LoadPaperVersions();
            UpdateRamLimitsAndPresets();
            UpdateStep();
        }

        private void LoadPaperVersions()
        {
            paperVersions.Clear();
            string latestVersion = null;

            // Try a few likely locations for the JSON (app output, current dir, project parent)
            var candidatePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "paper-versions.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "paper-versions.json"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "paper-versions.json")
            }.Select(p => Path.GetFullPath(p)).Distinct().ToList();

            string jsonPath = candidatePaths.FirstOrDefault(File.Exists);

            if (jsonPath != null)
            {
                try
                {
                    using var s = File.OpenRead(jsonPath);
                    using var doc = JsonDocument.Parse(s);

                    if (doc.RootElement.TryGetProperty("latest", out var latestEl))
                        latestVersion = latestEl.GetString();

                    if (doc.RootElement.TryGetProperty("versions", out var versions))
                    {
                        foreach (var prop in versions.EnumerateObject())
                        {
                            var key = prop.Name;
                            var value = prop.Value.GetString() ?? string.Empty;
                            paperVersions[key] = value;
                        }
                    }
                }
                catch
                {
                    paperVersions.Clear();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("paper-versions.json not found. Searched: " + string.Join(" ; ", candidatePaths));
            }

            // fallback if empty
            if (paperVersions.Count == 0)
            {
                paperVersions["1.20.2"] = "https://fill-data.papermc.io/v1/objects/ba340a835ac40b8563aa7eda1cd6479a11a7623409c89a2c35cd9d7490ed17a7/paper-1.20.2-318.jar";
            }

            // populate combo with all versions
            cmbVersions.Items.Clear();

            var keys = paperVersions.Keys.ToList();

            // Semantic sort: newest -> oldest
            // If JSON contains "latest", keep it first, then semantic-sort the rest descending.
            if (!string.IsNullOrEmpty(latestVersion) && keys.Contains(latestVersion))
            {
                keys.Remove(latestVersion);
                keys.Sort((a, b) => CompareMinecraftVersions(a, b)); // ascending
                keys.Reverse(); // descending (newest first)
                keys.Insert(0, latestVersion);
            }
            else
            {
                keys.Sort((a, b) => CompareMinecraftVersions(a, b));
                keys.Reverse();
            }

            foreach (var v in keys)
                cmbVersions.Items.Add(v);

            if (!string.IsNullOrEmpty(latestVersion))
            {
                var idx = cmbVersions.Items.IndexOf(latestVersion);
                cmbVersions.SelectedIndex = idx >= 0 ? idx : (cmbVersions.Items.Count > 0 ? 0 : -1);
            }
            else if (cmbVersions.Items.Count > 0)
            {
                cmbVersions.SelectedIndex = 0;
            }
        }

        private static int CompareMinecraftVersions(string a, string b)
        {
            // Compare numeric parts first (1.21.10 > 1.21.9), then handle pre-release (release > prerelease).
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return 0;

            string aMain = a.Split('-', 2)[0];
            string bMain = b.Split('-', 2)[0];

            var aNums = aMain.Split('.').Select(s => { int v; return int.TryParse(s, out v) ? v : 0; }).ToArray();
            var bNums = bMain.Split('.').Select(s => { int v; return int.TryParse(s, out v) ? v : 0; }).ToArray();

            int max = Math.Max(aNums.Length, bNums.Length);
            for (int i = 0; i < max; i++)
            {
                int an = i < aNums.Length ? aNums[i] : 0;
                int bn = i < bNums.Length ? bNums[i] : 0;
                if (an != bn) return an.CompareTo(bn);
            }

            // numeric parts equal → handle prerelease: release (no suffix) > prerelease (has suffix)
            string aPre = a.Contains('-') ? a.Substring(a.IndexOf('-') + 1) : null;
            string bPre = b.Contains('-') ? b.Substring(b.IndexOf('-') + 1) : null;

            bool aHasPre = !string.IsNullOrEmpty(aPre);
            bool bHasPre = !string.IsNullOrEmpty(bPre);

            if (aHasPre == bHasPre) // both release or both prerelease
            {
                if (!aHasPre && !bHasPre) return 0; // both releases and numerically equal
                // both prerelease: do a simple lexical/natural-ish compare
                // try numeric suffix compare (e.g., rc1 vs rc2)
                int cmp = string.Compare(aPre, bPre, StringComparison.OrdinalIgnoreCase);
                return cmp;
            }

            // release is greater than prerelease
            return aHasPre ? -1 : 1;
        }

        // P/Invoke for total physical memory (kept)
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private int GetTotalPhysicalMemoryMB()
        {
            try
            {
                var mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (!GlobalMemoryStatusEx(ref mem)) return 0;
                return (int)(mem.ullTotalPhys / 1024 / 1024);
            }
            catch
            {
                return 0;
            }
        }

        private void UpdateRamLimitsAndPresets()
        {
            var totalMB = GetTotalPhysicalMemoryMB();
            if (totalMB <= 0) totalMB = 4096;
            var maxAssignable = Math.Max(512, totalMB - 512);

            numRam.Minimum = 512;
            numRam.Maximum = maxAssignable;
            if (numRam.Value > numRam.Maximum) numRam.Value = numRam.Maximum;

            lblTotalRam.Text = $"System RAM: {totalMB} MB ({totalMB / 1024.0:F1} GB)";
            lblRamInfo.Text = $"Max assignable: {maxAssignable} MB. Pick a preset or enter custom value.";

            cmbRamPresets.Items.Clear();
            int[] presets = new[] { 512, 1024, 2048, 3072, 4096, 6144, 8192, 12288, 16384 };
            foreach (var p in presets)
                if (p <= maxAssignable) cmbRamPresets.Items.Add(p.ToString());
            if (cmbRamPresets.Items.Count == 0) cmbRamPresets.Items.Add(numRam.Minimum.ToString());

            var defaultPresetIndex = cmbRamPresets.Items.Cast<string>().ToList().FindIndex(x => x == "2048");
            cmbRamPresets.SelectedIndex = defaultPresetIndex >= 0 ? defaultPresetIndex : 0;

            if (int.TryParse(cmbRamPresets.SelectedItem?.ToString(), out var val))
                numRam.Value = Math.Min(numRam.Maximum, Math.Max(numRam.Minimum, val));
        }

        private void cmbRamPresets_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (int.TryParse(cmbRamPresets.SelectedItem?.ToString(), out var val))
            {
                if (val < numRam.Minimum) val = (int)numRam.Minimum;
                if (val > numRam.Maximum) val = (int)numRam.Maximum;
                numRam.Value = val;
            }
        }

        private void UpdateStep()
        {
            panelStep1.Visible = stepIndex == 0;
            panelStep2.Visible = stepIndex == 1;
            panelStep3.Visible = stepIndex == 2;

            btnBack.Enabled = stepIndex > 0;
            btnNext.Visible = stepIndex < 2;
            btnFinish.Visible = stepIndex == 2;

            if (stepIndex == 2)
            {
                lblSummary.Text =
                    $"Name: {ServerName}\r\n" +
                    $"PaperMC Version: {ServerVersion}\r\n" +
                    $"RAM: {ServerRamMB} MB\r\n" +
                    $"\r\nNote: The server JAR and a JDK were downloaded. A start.cmd was generated to launch the server using the bundled JDK.";
            }
        }

        // Map Minecraft version string to Java major according to your rules.
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
                var patch = v.Build; // Build is patch part from Version.TryParse
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

        // helper to create a safe unique folder under app/servers
        private string CreateUniqueServerFolder(string name)
        {
            var safe = name ?? string.Empty;
            foreach (var c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '-');
            safe = safe.Trim();
            if (string.IsNullOrEmpty(safe)) safe = "server";
            var baseDir = Path.Combine(AppContext.BaseDirectory, "servers");
            Directory.CreateDirectory(baseDir);
            var folder = Path.Combine(baseDir, safe);
            var suffix = 1;
            while (Directory.Exists(folder))
            {
                folder = Path.Combine(baseDir, $"{safe}-{suffix++}");
            }
            Directory.CreateDirectory(folder);
            return folder;
        }

        // When moving from step 0 to step 1 we create folder and download PaperMC JAR and mapped JDK (with progress dialog).
        private async void btnNext_Click(object? sender, EventArgs e)
        {
            if (stepIndex == 0)
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Please enter a server name.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (cmbVersions.SelectedItem == null)
                {
                    MessageBox.Show("Please select a PaperMC version.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var version = cmbVersions.SelectedItem!.ToString()!;
                if (!paperVersions.TryGetValue(version, out var paperUrl) || string.IsNullOrWhiteSpace(paperUrl))
                {
                    MessageBox.Show("Download URL not available for selected PaperMC version.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // create server folder now
                try
                {
                    ServerFolderPath = CreateUniqueServerFolder(txtName.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create server folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // download PaperMC into this folder as paper.jar
                var destJar = Path.Combine(ServerFolderPath, "paper.jar");
                var ctsPaper = new CancellationTokenSource();
                using (var progressDlg = new ProgressDialog(ctsPaper, $"Downloading PaperMC {version}..."))
                {
                    var progress = new Progress<int>(percent => progressDlg.SetProgress(percent));
                    try
                    {
                        progressDlg.Show(this);
                        DownloadedJarPath = await DownloadPaperJarAsync(paperUrl, destJar, progress, ctsPaper.Token);
                        if (string.IsNullOrEmpty(DownloadedJarPath) || !File.Exists(DownloadedJarPath))
                        {
                            MessageBox.Show("PaperMC download failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            try { Directory.Delete(ServerFolderPath, true); } catch { }
                            ServerFolderPath = string.Empty;
                            return;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        MessageBox.Show("PaperMC download cancelled.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        try { Directory.Delete(ServerFolderPath, true); } catch { }
                        ServerFolderPath = string.Empty;
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to download PaperMC: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        try { Directory.Delete(ServerFolderPath, true); } catch { }
                        ServerFolderPath = string.Empty;
                        return;
                    }
                    finally
                    {
                        if (progressDlg.Visible) progressDlg.Close();
                    }
                }

                // determine Java major and download/extract Temurin JDK (shared under app/jdks)
                var javaMajor = MapMinecraftToJavaMajor(version);
                var jdksRoot = Path.Combine(AppContext.BaseDirectory, "jdks");
                Directory.CreateDirectory(jdksRoot);
                var jdkInstallFolder = Path.Combine(jdksRoot, $"temurin-{javaMajor}");

                // if not already installed, resolve an Adoptium binary and download/extract
                if (!File.Exists(Path.Combine(jdkInstallFolder, "bin", "java.exe")))
                {
                    // resolve URL via Adoptium assets API
                    string jdkUrl = null;
                    try
                    {
                        jdkUrl = await ResolveTemurinBinaryUrlAsync(javaMajor);
                    }
                    catch
                    {
                        jdkUrl = null;
                    }

                    if (string.IsNullOrEmpty(jdkUrl))
                    {
                        MessageBox.Show($"Unable to resolve Temurin JDK download URL for Java {javaMajor}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        try { Directory.Delete(ServerFolderPath, true); } catch { }
                        ServerFolderPath = string.Empty;
                        return;
                    }

                    var tmpZip = Path.Combine(Path.GetTempPath(), $"temurin-{javaMajor}.zip");
                    var tmpExtract = Path.Combine(Path.GetTempPath(), $"temurin-extract-{Guid.NewGuid():N}");
                    var ctsJdk = new CancellationTokenSource();
                    using (var progressDlg = new ProgressDialog(ctsJdk))
                    {
                        var progress = new Progress<int>(percent => progressDlg.SetProgress(percent));
                        try
                        {
                            progressDlg.Show(this);
                            await DownloadFileWithProgressAsync(jdkUrl, tmpZip, progress, ctsJdk.Token);

                            // clean target if partially present
                            if (Directory.Exists(jdkInstallFolder))
                            {
                                try { Directory.Delete(jdkInstallFolder, true); } catch { }
                            }

                            Directory.CreateDirectory(tmpExtract);
                            ZipFile.ExtractToDirectory(tmpZip, tmpExtract);

                            // locate inner folder that contains bin\java.exe
                            var jdkRoot = FindJdkRootDirectory(tmpExtract);
                            if (jdkRoot == null)
                                throw new InvalidOperationException("Downloaded JDK archive did not contain a valid JDK (no bin\\java.exe found).");

                            // move found jdkRoot to final folder
                            Directory.Move(jdkRoot, jdkInstallFolder);
                        }
                        catch (OperationCanceledException)
                        {
                            MessageBox.Show("JDK download cancelled.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            try { Directory.Delete(ServerFolderPath, true); } catch { }
                            ServerFolderPath = string.Empty;
                            return;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to download or extract JDK: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            try { Directory.Delete(ServerFolderPath, true); } catch { }
                            ServerFolderPath = string.Empty;
                            return;
                        }
                        finally
                        {
                            try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
                            try { if (Directory.Exists(tmpExtract)) Directory.Delete(tmpExtract, true); } catch { }
                            if (progressDlg.Visible) progressDlg.Close();
                        }
                    }
                }
            }

            stepIndex = Math.Min(2, stepIndex + 1);
            UpdateStep();
        }

        private string FormatRamArg(int ramMB)
        {
            if (ramMB % 1024 == 0)
                return $"{ramMB / 1024}G";
            return $"{ramMB}M";
        }

        // Resolve a Temurin (Adoptium) Windows x64 jdk package link for requested feature version
        private async Task<string> ResolveTemurinBinaryUrlAsync(int javaMajor)
        {
            using var http = new HttpClient();
            // Query assets; we ask for jdk windows x64
            var api = $"https://api.adoptium.net/v3/assets/feature_releases/{javaMajor}/ga?architecture=x64&os=windows&image_type=jdk&vendor=adoptium";
            var json = await http.GetStringAsync(api);
            using var doc = JsonDocument.Parse(json);
            // doc is an array of assets; search for a package.link preferring ZIP
            string firstLink = null;
            foreach (var asset in doc.RootElement.EnumerateArray())
            {
                if (asset.TryGetProperty("binaries", out var binaries))
                {
                    foreach (var bin in binaries.EnumerateArray())
                    {
                        if (bin.TryGetProperty("package", out var pkg))
                        {
                            string link = null;
                            string name = null;
                            if (pkg.TryGetProperty("link", out var linkEl))
                                link = linkEl.GetString();
                            if (pkg.TryGetProperty("name", out var nameEl))
                                name = nameEl.GetString();

                            if (string.IsNullOrEmpty(link))
                                continue;

                            if (firstLink == null) firstLink = link;

                            // prefer zip if name or link indicates zip
                            if ((name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ||
                                link.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                return link;
                            }
                        }
                    }
                }
            }
            // fallback to first link found
            return firstLink;
        }

        private async Task DownloadFileWithProgressAsync(string url, string destination, IProgress<int> progress, CancellationToken ct)
        {
            using var http = new HttpClient();
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? -1L;
            var canReport = total > 0;
            var buffer = new byte[81920];

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var fs = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            long totalRead = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fs.WriteAsync(buffer, 0, read, ct);
                totalRead += read;
                if (canReport)
                {
                    var percent = (int)((totalRead * 100L) / total);
                    progress.Report(percent);
                }
                else
                {
                    progress.Report(0);
                }
            }

            progress.Report(100);
        }

        private async Task<string> DownloadPaperJarAsync(string url, string destination, IProgress<int> progress, CancellationToken ct)
        {
            // If file already exists, return it
            if (File.Exists(destination))
            {
                progress.Report(100);
                return destination;
            }

            await DownloadFileWithProgressAsync(url, destination, progress, ct);
            return destination;
        }

        // search recursively for a directory under root that contains bin\java.exe
        private string FindJdkRootDirectory(string root)
        {
            // check root itself first
            var candidate = Path.Combine(root, "bin", "java.exe");
            if (File.Exists(candidate)) return root;

            foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                candidate = Path.Combine(dir, "bin", "java.exe");
                if (File.Exists(candidate)) return dir;
            }

            return null;
        }

        private void btnBack_Click(object? sender, EventArgs e)
        {
            stepIndex = Math.Max(0, stepIndex - 1);
            UpdateStep();
        }

        private void btnCancel_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ServerFolderPath) == false && Directory.Exists(ServerFolderPath) && string.IsNullOrEmpty(DownloadedJarPath))
            {
                try { Directory.Delete(ServerFolderPath, true); } catch { }
                ServerFolderPath = string.Empty;
            }

            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnFinish_Click(object? sender, EventArgs e)
        {
            // final validation
            if (string.IsNullOrWhiteSpace(ServerName))
            {
                MessageBox.Show("Server name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                stepIndex = 0;
                UpdateStep();
                return;
            }

            if (!EulaAccepted)
            {
                var r = MessageBox.Show("You must agree to the Minecraft EULA to run the server. Agree now?", "EULA", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
                chkEulaAccept.Checked = true;
            }

            // Create start.cmd using current RAM and bundled JDK if present
            try
            {
                var javaMajor = MapMinecraftToJavaMajor(ServerVersion);
                var jdkInstallFolder = Path.Combine(AppContext.BaseDirectory, "jdks", $"temurin-{javaMajor}");
                var javaExe = Path.Combine(jdkInstallFolder, "bin", "java.exe");
                var ramArg = FormatRamArg(ServerRamMB);
                string startCmd;

                if (File.Exists(javaExe))
                {
                    startCmd = $"@echo off\r\n\"{javaExe}\" -Xms{ramArg} -Xmx{ramArg} -jar \"%~dp0\\paper.jar\" --nogui\r\n";
                }
                else
                {
                    MessageBox.Show($"Bundled JDK not found for Java {javaMajor}, start.cmd will use system java.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    startCmd = $"@echo off\r\njava -Xms{ramArg} -Xmx{ramArg} -jar \"%~dp0\\paper.jar\" --nogui\r\n";
                }

                if (!string.IsNullOrEmpty(ServerFolderPath))
                {
                    File.WriteAllText(Path.Combine(ServerFolderPath, "start.cmd"), startCmd);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create start.cmd: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}