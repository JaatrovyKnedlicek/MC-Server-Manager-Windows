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
        private int stepIndex = 0; // 0..3 (0: software selection, 1: download/RAM, 2: installation, 3: summary)

        public string ServerName => txtName.Text.Trim();
        public string ServerSoftware => cmbServerSoftware.SelectedItem?.ToString() ?? string.Empty;
        public string ServerVersion => cmbVersions.SelectedItem?.ToString() ?? string.Empty;
        public int ServerRamMB => (int)numRam.Value;
        public bool EulaAccepted => chkEulaAccept.Checked;

        // map of version -> download url
        private System.Collections.Generic.Dictionary<string, string> versionUrls = new();

        // path of downloaded jar (in the server folder)
        public string DownloadedJarPath { get; private set; } = string.Empty;

        // folder created for this server during step 0 -> step 1 transition
        public string ServerFolderPath { get; private set; } = string.Empty;

        // path to NeoForge win_args.txt (discovered after installation)
        private string NeoForgeWinArgsPath { get; set; } = string.Empty;

        // path to Forge win_args.txt (discovered after installation)
        private string ForgeWinArgsPath { get; set; } = string.Empty;

        public NewServerWizardForm()
        {
            InitializeComponent();
            InitializeServerSoftwareDropdown();
            LoadServerVersions();
            UpdateRamLimitsAndPresets();
            UpdateStep();
        }

        private void InitializeServerSoftwareDropdown()
        {
            cmbServerSoftware.Items.Clear();
            cmbServerSoftware.Items.Add("Paper");
            cmbServerSoftware.Items.Add("Purpur");
            cmbServerSoftware.Items.Add("Folia");
            cmbServerSoftware.Items.Add("Arclight");
            cmbServerSoftware.Items.Add("Mohist");
            cmbServerSoftware.Items.Add("Fabric");
            cmbServerSoftware.Items.Add("Forge");
            cmbServerSoftware.Items.Add("NeoForge");
            cmbServerSoftware.Items.Add("Spigot");
            cmbServerSoftware.Items.Add("Vanilla");
            cmbServerSoftware.SelectedIndex = 0;
        }

        private void cmbServerSoftware_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Reload versions when server software changes
            LoadServerVersions();
        }

        private async void LoadServerVersions()
        {
            // Show loading indicator
            loadingPanel.Visible = true;
            loadingPanel.BringToFront();
            cmbServerSoftware.Enabled = false;
            cmbVersions.Enabled = false;
            lblName.Enabled = false;
            txtName.Enabled = false;
            lblPaperVersion.Enabled = false;

            try
            {
                var software = cmbServerSoftware.SelectedItem?.ToString();
                if (software == "Purpur")
                {
                    await LoadPurpurVersionsAsync();
                }
                else if (software == "Fabric")
                {
                    await LoadFabricVersionsAsync();
                }
                else if (software == "Forge")
                {
                    await LoadForgeVersionsAsync();
                }
                else if (software == "NeoForge")
                {
                    await LoadNeoForgeVersionsAsync();
                }
                else if (software == "Spigot")
                {
                    await LoadSpigotVersionsAsync();
                }
                else if (software == "Vanilla")
                {
                    await LoadVanillaVersionsAsync();
                }
                else if (software == "Folia")
                {
                    await LoadFoliaVersionsAsync();
                }
                else if (software == "Arclight")
                {
                    await LoadArclightVersionsAsync();
                }
                else if (software == "Mohist")
                {
                    await LoadMohistVersionsAsync();
                }
                else
                {
                    await LoadPaperVersionsAsync();
                }
            }
            finally
            {
                // Hide loading indicator
                loadingPanel.Visible = false;
                loadingPanel.SendToBack();
                cmbServerSoftware.Enabled = true;
                cmbVersions.Enabled = true;
                lblName.Enabled = true;
                txtName.Enabled = true;
                lblPaperVersion.Enabled = true;
            }
        }

        private async Task LoadPaperVersionsAsync()
        {
            await LoadPaperLikeVersionsAsync("paper", "PaperMC", "MC_Server_Manager_3.paper-versions.json");
        }

        private async Task LoadFoliaVersionsAsync()
        {
            await LoadPaperLikeVersionsAsync("folia", "Folia", null);
        }

        private async Task LoadArclightVersionsAsync()
        {
            versionUrls.Clear();
            string latestVersion = "1.20.1";

            // Arclight is a Forge-based server mod. Load versions from their GitHub releases
            // or use a fallback list of known versions with their respective build URLs
            var arclightVersions = new Dictionary<string, string>
            {
                // Format: { "mc_version", "download_url_to_jar" }
                // Arclight builds are typically available from GitHub or CurseForge
                { "1.20.1", "" },
                { "1.20.4", "" },
                { "1.19.2", "" },
                { "1.18.2", "" }
            };

            foreach (var kvp in arclightVersions)
            {
                versionUrls[kvp.Key] = kvp.Value;
            }

            // Show informational message about Arclight API status
            var message = "Arclight - Minecraft Server Software\n\n" +
                "Status: Automatic version downloads are currently unavailable (no public API).\n\n" +
                "To use Arclight:\n" +
                "1. Download from CurseForge or GitHub: https://github.com/IzzelAliz/Arclight\n" +
                "2. Select the version from the dropdown\n" +
                "3. When prompted for download, manually provide the JAR location\n\n" +
                "Supported versions: 1.20.1, 1.20.4, 1.19.2, 1.18.2";

            MessageBox.Show(message, "Arclight Server Software", MessageBoxButtons.OK, MessageBoxIcon.Information);
            PopulateVersionDropdown(latestVersion);
        }

        private async Task LoadMohistVersionsAsync()
        {
            versionUrls.Clear();
            string latestVersion = "1.20.1";

            // Note: Mohist's official API (mohistmc.com/api/) is currently unavailable.
            // These are known supported versions. Users must download JARs manually or
            // obtain builds from: https://github.com/MohistMC/Mohist/releases
            // 
            // To enable automatic downloads, provide actual JAR URLs in the dictionary below.
            var mohistVersions = new Dictionary<string, string>
            {
                // Format: { "mc_version", "download_url_to_jar" }
                // Example: { "1.20.1", "https://example.com/builds/mohist-1.20.1.jar" }
                { "1.20.1", "" },
                { "1.20.4", "" },
                { "1.19.2", "" },
                { "1.18.2", "" }
            };

            foreach (var kvp in mohistVersions)
            {
                versionUrls[kvp.Key] = kvp.Value;
            }

            // Show informational message about Mohist API status
            var message = "Mohist - Minecraft Server Software\n\n" +
                "Status: Automatic version downloads are currently unavailable (API offline).\n\n" +
                "To use Mohist:\n" +
                "1. Download the JAR from: https://github.com/MohistMC/Mohist/releases\n" +
                "2. Select the version from the dropdown\n" +
                "3. When prompted for download, manually provide the JAR location\n\n" +
                "Supported versions: 1.20.1, 1.20.4, 1.19.2, 1.18.2";

            MessageBox.Show(message, "Mohist Server Software", MessageBoxButtons.OK, MessageBoxIcon.Information);
            PopulateVersionDropdown(latestVersion);
        }

        private async Task LoadPaperLikeVersionsAsync(string projectName, string apiDisplayName, string embeddedResourceName)
        {
            versionUrls.Clear();
            string latestVersion = null;
            string errorMessage = null;
            string errorCode = null;

            // Try to fetch from PaperMC Fill v3 API first
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.5");
                var response = await http.GetAsync($"https://fill.papermc.io/v3/projects/{projectName}");
                
                if (!response.IsSuccessStatusCode)
                {
                    errorCode = $"HTTP {(int)response.StatusCode}";
                    errorMessage = response.ReasonPhrase ?? "Unknown HTTP error";
                    System.Diagnostics.Debug.WriteLine($"{apiDisplayName} API returned error status: {errorCode} - {errorMessage}");
                }
                else
                {
                    var apiResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(apiResponse);

                    // Fill v3 API has "versions" as an object mapping version groups to version strings
                    if (doc.RootElement.TryGetProperty("versions", out var versionsObj))
                    {
                        // Flatten all version strings from all groups
                        var allVersions = new System.Collections.Generic.List<string>();
                        
                        foreach (var versionGroup in versionsObj.EnumerateObject())
                        {
                            foreach (var version in versionGroup.Value.EnumerateArray())
                            {
                                var versionString = version.GetString();
                                if (!string.IsNullOrEmpty(versionString) && !allVersions.Contains(versionString))
                                {
                                    allVersions.Add(versionString);
                                }
                            }
                        }

                        // Get the latest version (first one after sorting)
                        if (allVersions.Count > 0)
                        {
                            // Sort versions to get the latest
                            allVersions.Sort((a, b) => CompareMinecraftVersions(a, b));
                            allVersions.Reverse();
                            latestVersion = allVersions[0];
                        }

                        // Build download URLs for each version using Fill v3 API format
                        foreach (var versionString in allVersions)
                        {
                            // Use the Fill v3 API to get the latest build for each version
                            try
                            {
                                var buildResponse = await http.GetAsync($"https://fill.papermc.io/v3/projects/{projectName}/versions/{versionString}/builds/latest");
                                if (buildResponse.IsSuccessStatusCode)
                                {
                                    var buildData = await buildResponse.Content.ReadAsStringAsync();
                                    using var buildDoc = JsonDocument.Parse(buildData);
                                    
                                    // Extract download URL from the response
                                    if (buildDoc.RootElement.TryGetProperty("downloads", out var downloads) &&
                                        downloads.TryGetProperty("server:default", out var serverDownload) &&
                                        serverDownload.TryGetProperty("url", out var urlElement))
                                    {
                                        var downloadUrl = urlElement.GetString();
                                        if (!string.IsNullOrEmpty(downloadUrl))
                                        {
                                            versionUrls[versionString] = downloadUrl;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to get build URL for version {versionString}: {ex.Message}");
                                // Skip this version if we can't get its download URL
                            }
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errorCode = "NETWORK_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                errorCode = "TIMEOUT_ERROR";
                errorMessage = "Request timed out. The API did not respond within the expected time.";
                System.Diagnostics.Debug.WriteLine($"Request timed out: {ex.Message}");
            }
            catch (Exception ex)
            {
                errorCode = "GENERAL_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Failed to fetch versions from {apiDisplayName} API: {ex.Message}");
            }

            // Show error message if API failed
            if (errorCode != null || versionUrls.Count == 0)
            {
                var apiName = apiDisplayName;
                // If no error code but no versions, set a specific error code
                if (errorCode == null && versionUrls.Count == 0)
                {
                    errorCode = "NO_DATA";
                    errorMessage = "API returned no version data";
                }

                var message = $"Failed to fetch the newest versions from the {apiName} API.\n\nError Code: {errorCode}\nError: {errorMessage}\n\nShowing locally stored versions instead.";

                MessageBox.Show(
                    message,
                    "API Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                // Fall back to embedded JSON when available
                if (!string.IsNullOrEmpty(embeddedResourceName))
                {
                    LoadVersionsFromEmbeddedJson(embeddedResourceName, ref latestVersion);
                }
            }

            // Populate combo with all versions
            PopulateVersionDropdown(latestVersion);
        }

        private async Task LoadPurpurVersionsAsync()
        {
            versionUrls.Clear();
            string latestVersion = null;
            string errorMessage = null;
            string errorCode = null;

            // Try to fetch from Purpur API first
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.5");
                var response = await http.GetAsync("https://api.purpurmc.org/v2/purpur");
                
                if (!response.IsSuccessStatusCode)
                {
                    errorCode = $"HTTP {(int)response.StatusCode}";
                    errorMessage = response.ReasonPhrase ?? "Unknown HTTP error";
                    System.Diagnostics.Debug.WriteLine($"Purpur API returned error status: {errorCode} - {errorMessage}");
                }
                else
                {
                    var apiResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(apiResponse);

                    // Purpur API returns a JSON object with "versions" array
                    if (doc.RootElement.TryGetProperty("versions", out var versions))
                    {
                        // Get the latest version
                        if (versions.GetArrayLength() > 0)
                        {
                            latestVersion = versions[0].GetString();
                        }

                        // Build download URLs for each version using Purpur API format
                        foreach (var version in versions.EnumerateArray())
                        {
                            var versionString = version.GetString();
                            if (!string.IsNullOrEmpty(versionString))
                            {
                                // Use the Purpur API latest download format
                                var downloadUrl = $"https://api.purpurmc.org/v2/purpur/{versionString}/latest/download";
                                versionUrls[versionString] = downloadUrl;
                            }
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errorCode = "NETWORK_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                errorCode = "TIMEOUT_ERROR";
                errorMessage = "Request timed out. The API did not respond within the expected time.";
                System.Diagnostics.Debug.WriteLine($"Request timed out: {ex.Message}");
            }
            catch (Exception ex)
            {
                errorCode = "GENERAL_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Failed to fetch versions from Purpur API: {ex.Message}");
            }

            // Show error message if API failed
            if (errorCode != null || versionUrls.Count == 0)
            {
                var apiName = ServerSoftware == "Purpur" ? "Purpur" : (ServerSoftware == "Fabric" ? "Fabric" : (ServerSoftware == "Forge" ? "Forge" : (ServerSoftware == "NeoForge" ? "NeoForge" : (ServerSoftware == "Spigot" ? "Spigot" : (ServerSoftware == "Vanilla" ? "Mojang" : "PaperMC")))));
                // If no error code but no versions, set a specific error code
                if (errorCode == null && versionUrls.Count == 0)
                {
                    errorCode = "NO_DATA";
                    errorMessage = "API returned no version data";
                }
                
                var message = $"Failed to fetch the newest versions from the {apiName} API.\n\nError Code: {errorCode}\nError: {errorMessage}\n\nShowing locally stored versions instead.";
                
                MessageBox.Show(
                    message,
                    "API Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                
                // Fall back to embedded JSON
                LoadVersionsFromEmbeddedJson(ref latestVersion);
            }

            // Populate combo with all versions
            PopulateVersionDropdown(latestVersion);
        }

        private async Task LoadSpigotVersionsAsync()
        {
            versionUrls.Clear();
            string latestVersion = null;
            string errorMessage = null;
            string errorCode = null;

            // Try to fetch from Spigot versions API
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.5");
                var response = await http.GetAsync("https://hub.spigotmc.org/versions/");
                
                if (!response.IsSuccessStatusCode)
                {
                    errorCode = $"HTTP {(int)response.StatusCode}";
                    errorMessage = response.ReasonPhrase ?? "Unknown HTTP error";
                    System.Diagnostics.Debug.WriteLine($"Spigot API returned error status: {errorCode} - {errorMessage}");
                }
                else
                {
                    var htmlContent = await response.Content.ReadAsStringAsync();
                    
                    // Parse HTML to extract version links (pattern: href="1.21.11.json")
                    var allVersions = new System.Collections.Generic.List<string>();
                    var versionPattern = new System.Text.RegularExpressions.Regex(@"href=""(\d+\.\d+(?:\.\d+)?(?:-[a-zA-Z0-9]+)?)\.json""");
                    
                    foreach (System.Text.RegularExpressions.Match match in versionPattern.Matches(htmlContent))
                    {
                        var versionString = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(versionString) && !allVersions.Contains(versionString))
                        {
                            allVersions.Add(versionString);
                        }
                    }

                    // Get the latest version (first one after sorting)
                    if (allVersions.Count > 0)
                    {
                        // Sort versions to get the latest
                        allVersions.Sort((a, b) => CompareMinecraftVersions(a, b));
                        allVersions.Reverse();
                        latestVersion = allVersions[0];
                    }

                    // Build download URLs for each version using GetBukkit CDN
                    foreach (var versionString in allVersions)
                    {
                        // Use the GetBukkit CDN for Spigot downloads
                        var downloadUrl = $"https://cdn.getbukkit.org/spigot/spigot-{versionString}.jar";
                        versionUrls[versionString] = downloadUrl;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errorCode = "NETWORK_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                errorCode = "TIMEOUT_ERROR";
                errorMessage = "Request timed out. The API did not respond within the expected time.";
                System.Diagnostics.Debug.WriteLine($"Request timed out: {ex.Message}");
            }
            catch (Exception ex)
            {
                errorCode = "GENERAL_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Failed to fetch versions from Spigot API: {ex.Message}");
            }

            // Show error message if API failed
            if (errorCode != null || versionUrls.Count == 0)
            {
                var apiName = ServerSoftware == "Purpur" ? "Purpur" : (ServerSoftware == "Fabric" ? "Fabric" : (ServerSoftware == "Forge" ? "Forge" : (ServerSoftware == "NeoForge" ? "NeoForge" : (ServerSoftware == "Spigot" ? "Spigot" : (ServerSoftware == "Vanilla" ? "Mojang" : "PaperMC")))));
                // If no error code but no versions, set a specific error code
                if (errorCode == null && versionUrls.Count == 0)
                {
                    errorCode = "NO_DATA";
                    errorMessage = "API returned no version data";
                }
                
                var message = $"Failed to fetch the newest versions from the {apiName} API.\n\nError Code: {errorCode}\nError: {errorMessage}\n\nShowing locally stored versions instead.";
                
                MessageBox.Show(
                    message,
                    "API Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                
                // Fall back to embedded JSON
                LoadVersionsFromEmbeddedJson(ref latestVersion);
            }

            // Populate combo with all versions
            PopulateVersionDropdown(latestVersion);
        }

        private async Task LoadVanillaVersionsAsync()
        {
            versionUrls.Clear();
            string latestVersion = null;
            string errorMessage = null;
            string errorCode = null;

            // Try to fetch from Mojang version manifest API
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.5");
                var response = await http.GetAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json");
                
                if (!response.IsSuccessStatusCode)
                {
                    errorCode = $"HTTP {(int)response.StatusCode}";
                    errorMessage = response.ReasonPhrase ?? "Unknown HTTP error";
                    System.Diagnostics.Debug.WriteLine($"Mojang API returned error status: {errorCode} - {errorMessage}");
                }
                else
                {
                    var apiResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(apiResponse);

                    // Get the latest release version
                    if (doc.RootElement.TryGetProperty("latest", out var latest) &&
                        latest.TryGetProperty("release", out var latestRelease))
                    {
                        latestVersion = latestRelease.GetString();
                    }

                    // Get all versions
                    if (doc.RootElement.TryGetProperty("versions", out var versions))
                    {
                        var allVersions = new System.Collections.Generic.List<string>();
                        
                        // Collect all release versions (filter out snapshots for cleaner list)
                        foreach (var version in versions.EnumerateArray())
                        {
                            if (version.TryGetProperty("type", out var type) && 
                                type.GetString() == "release" &&
                                version.TryGetProperty("id", out var id))
                            {
                                var versionString = id.GetString();
                                if (!string.IsNullOrEmpty(versionString) && !allVersions.Contains(versionString))
                                {
                                    allVersions.Add(versionString);
                                }
                            }
                        }

                        // Sort versions to get the latest
                        if (allVersions.Count > 0)
                        {
                            allVersions.Sort((a, b) => CompareMinecraftVersions(a, b));
                            allVersions.Reverse();
                            // Update latestVersion to be the first in sorted list
                            if (allVersions.Count > 0)
                            {
                                latestVersion = allVersions[0];
                            }
                        }

                        // Build download URLs for each version by fetching version metadata
                        foreach (var versionString in allVersions)
                        {
                            try
                            {
                                // Find the version in the manifest to get its metadata URL
                                string versionUrl = null;
                                foreach (var version in versions.EnumerateArray())
                                {
                                    if (version.TryGetProperty("id", out var id) && 
                                        id.GetString() == versionString &&
                                        version.TryGetProperty("url", out var url))
                                    {
                                        versionUrl = url.GetString();
                                        break;
                                    }
                                }

                                if (!string.IsNullOrEmpty(versionUrl))
                                {
                                    // Fetch version metadata to get server download URL
                                    var versionResponse = await http.GetAsync(versionUrl);
                                    if (versionResponse.IsSuccessStatusCode)
                                    {
                                        var versionData = await versionResponse.Content.ReadAsStringAsync();
                                        using var versionDoc = JsonDocument.Parse(versionData);
                                        
                                        // Extract server download URL
                                        if (versionDoc.RootElement.TryGetProperty("downloads", out var downloads) &&
                                            downloads.TryGetProperty("server", out var server) &&
                                            server.TryGetProperty("url", out var serverUrl))
                                        {
                                            var downloadUrl = serverUrl.GetString();
                                            if (!string.IsNullOrEmpty(downloadUrl))
                                            {
                                                versionUrls[versionString] = downloadUrl;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to get download URL for vanilla version {versionString}: {ex.Message}");
                                // Skip this version if we can't get its download URL
                            }
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errorCode = "NETWORK_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                errorCode = "TIMEOUT_ERROR";
                errorMessage = "Request timed out. The API did not respond within the expected time.";
                System.Diagnostics.Debug.WriteLine($"Request timed out: {ex.Message}");
            }
            catch (Exception ex)
            {
                errorCode = "GENERAL_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Failed to fetch versions from Mojang API: {ex.Message}");
            }

            // Show error message if API failed
            if (errorCode != null || versionUrls.Count == 0)
            {
                var apiName = ServerSoftware == "Purpur" ? "Purpur" : (ServerSoftware == "Fabric" ? "Fabric" : (ServerSoftware == "Forge" ? "Forge" : (ServerSoftware == "NeoForge" ? "NeoForge" : (ServerSoftware == "Spigot" ? "Spigot" : (ServerSoftware == "Vanilla" ? "Mojang" : "PaperMC")))));
                // If no error code but no versions, set a specific error code
                if (errorCode == null && versionUrls.Count == 0)
                {
                    errorCode = "NO_DATA";
                    errorMessage = "API returned no version data";
                }
                
                var message = $"Failed to fetch the newest versions from the {apiName} API.\n\nError Code: {errorCode}\nError: {errorMessage}\n\nShowing locally stored versions instead.";
                
                MessageBox.Show(
                    message,
                    "API Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                
                // Fall back to embedded JSON
                LoadVersionsFromEmbeddedJson(ref latestVersion);
            }

            // Populate combo with all versions
            PopulateVersionDropdown(latestVersion);
        }

        private async Task LoadNeoForgeVersionsAsync()
        {
            versionUrls.Clear();
            string latestVersion = null;
            string errorMessage = null;
            string errorCode = null;

            // Try to fetch from NeoForge Maven metadata
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.5");
                
                // Fetch Maven metadata XML
                var metadataResponse = await http.GetAsync("https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml");
                
                if (!metadataResponse.IsSuccessStatusCode)
                {
                    errorCode = $"HTTP {(int)metadataResponse.StatusCode}";
                    errorMessage = metadataResponse.ReasonPhrase ?? "Unknown HTTP error";
                    System.Diagnostics.Debug.WriteLine($"NeoForge Maven metadata returned error status: {errorCode} - {errorMessage}");
                }
                else
                {
                    var metadataXml = await metadataResponse.Content.ReadAsStringAsync();
                    
                    // Parse XML to extract versions
                    var allVersions = new System.Collections.Generic.List<string>();
                    var versionToMinecraftMap = new System.Collections.Generic.Dictionary<string, string>();
                    
                    // Simple XML parsing to extract version elements
                    var versionStartTag = "<version>";
                    var versionEndTag = "</version>";
                    var index = 0;
                    
                    while ((index = metadataXml.IndexOf(versionStartTag, index)) != -1)
                    {
                        var startIndex = index + versionStartTag.Length;
                        var endIndex = metadataXml.IndexOf(versionEndTag, startIndex);
                        if (endIndex != -1)
                        {
                            var version = metadataXml.Substring(startIndex, endIndex - startIndex).Trim();
                            if (!string.IsNullOrEmpty(version) && !allVersions.Contains(version))
                            {
                                allVersions.Add(version);
                            }
                            index = endIndex + versionEndTag.Length;
                        }
                        else
                        {
                            break;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"NeoForge Maven metadata returned {allVersions.Count} total versions");

                    // Map NeoForge versions to Minecraft versions
                    // NeoForge version pattern: major.minor.patch (e.g., 21.4.89 -> Minecraft 1.21.4)
                    var minecraftVersions = new System.Collections.Generic.SortedSet<string>(System.Collections.Generic.Comparer<string>.Create((a, b) => CompareMinecraftVersions(a, b)));
                    
                    foreach (var neoforgeVersion in allVersions)
                    {
                        // Skip beta versions and extract stable ones
                        if (!neoforgeVersion.Contains("-beta") && !neoforgeVersion.Contains("-alpha"))
                        {
                            var parts = neoforgeVersion.Split('.');
                            if (parts.Length >= 2)
                            {
                                var major = parts[0];
                                var minor = parts[1];
                                
                                // Map NeoForge major.minor to Minecraft version
                                // 20.x -> 1.20.x, 21.x -> 1.21.x (older versions)
                                // 26.x -> 26.x (newer versions without 1. prefix)
                                string minecraftVersion;
                                if (int.TryParse(major, out int majorNum) && majorNum >= 26)
                                {
                                    // New versioning scheme: 26.x -> 26.x
                                    minecraftVersion = $"{major}.{minor}";
                                }
                                else
                                {
                                    // Old versioning scheme: 20.x -> 1.20.x
                                    minecraftVersion = $"1.{major}.{minor}";
                                }
                                
                                if (!versionToMinecraftMap.ContainsKey(minecraftVersion))
                                {
                                    versionToMinecraftMap[minecraftVersion] = neoforgeVersion;
                                    minecraftVersions.Add(minecraftVersion);
                                }
                                else
                                {
                                    // Keep the latest NeoForge version for this Minecraft version
                                    var existingNeoForgeVersion = versionToMinecraftMap[minecraftVersion];
                                    if (CompareNeoForgeVersions(neoforgeVersion, existingNeoForgeVersion) > 0)
                                    {
                                        versionToMinecraftMap[minecraftVersion] = neoforgeVersion;
                                    }
                                }
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"NeoForge mapped to {minecraftVersions.Count} Minecraft versions");

                    // Get the latest version
                    if (minecraftVersions.Count > 0)
                    {
                        var sortedVersions = minecraftVersions.ToList();
                        sortedVersions.Sort((a, b) => CompareMinecraftVersions(a, b));
                        sortedVersions.Reverse();
                        latestVersion = sortedVersions[0];
                        System.Diagnostics.Debug.WriteLine($"Latest NeoForge Minecraft version: {latestVersion}");
                        
                        // Limit to latest 50 versions
                        if (sortedVersions.Count > 50)
                        {
                            sortedVersions = sortedVersions.Take(50).ToList();
                            System.Diagnostics.Debug.WriteLine($"Limited to latest 50 NeoForge versions for dropdown");
                        }

                        // Build download URLs for each Minecraft version
                        foreach (var mcVersion in sortedVersions)
                        {
                            if (versionToMinecraftMap.TryGetValue(mcVersion, out var neoforgeVersion))
                            {
                                // Build Maven download URL
                                var downloadUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoforgeVersion}/neoforge-{neoforgeVersion}-installer.jar";
                                versionUrls[mcVersion] = downloadUrl;
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Built {versionUrls.Count} NeoForge download URLs");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errorCode = "NETWORK_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                errorCode = "TIMEOUT_ERROR";
                errorMessage = "Request timed out. The API did not respond within the expected time.";
                System.Diagnostics.Debug.WriteLine($"Request timed out: {ex.Message}");
            }
            catch (Exception ex)
            {
                errorCode = "GENERAL_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Failed to fetch versions from NeoForge Maven: {ex.Message}");
            }

            // Show error message if API failed
            if (errorCode != null || versionUrls.Count == 0)
            {
                var apiName = "NeoForge";
                // If no error code but no versions, set a specific error code
                if (errorCode == null && versionUrls.Count == 0)
                {
                    errorCode = "NO_DATA";
                    errorMessage = "API returned no version data";
                }
                
                var message = $"Failed to fetch the newest versions from the {apiName} API.\n\nError Code: {errorCode}\nError: {errorMessage}\n\nShowing locally stored versions instead.";
                
                MessageBox.Show(
                    message,
                    "API Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                
                // Fall back to embedded JSON
                LoadVersionsFromEmbeddedJson(ref latestVersion);
            }

            // Populate combo with all versions
            PopulateVersionDropdown(latestVersion);
        }

        private async Task LoadForgeVersionsAsync()
        {
            versionUrls.Clear();
            string latestVersion = null;
            string errorMessage = null;
            string errorCode = null;

            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.5");

                // Fetch Forge Maven metadata for available versions
                var metadataResponse = await http.GetAsync("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml");

                if (!metadataResponse.IsSuccessStatusCode)
                {
                    errorCode = $"HTTP {(int)metadataResponse.StatusCode}";
                    errorMessage = metadataResponse.ReasonPhrase ?? "Unknown HTTP error";
                    System.Diagnostics.Debug.WriteLine($"Forge Maven metadata returned error status: {errorCode} - {errorMessage}");
                }
                else
                {
                    var xmlContent = await metadataResponse.Content.ReadAsStringAsync();
                    var doc = new System.Xml.XmlDocument();
                    doc.LoadXml(xmlContent);

                    var allVersions = new System.Collections.Generic.List<string>();
                    var versionNodes = doc.GetElementsByTagName("version");

                    foreach (System.Xml.XmlNode node in versionNodes)
                    {
                        var version = node.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(version))
                        {
                            allVersions.Add(version);
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Forge Maven metadata returned {allVersions.Count} total versions");

                    // Map Forge versions to Minecraft versions
                    // Forge format: MCVERSION-forge-FORGEVERSION (e.g., 1.20.1-47.3.0)
                    var minecraftVersions = new System.Collections.Generic.SortedSet<string>(System.Collections.Generic.Comparer<string>.Create((a, b) => CompareMinecraftVersions(a, b)));
                    var versionToForgeMap = new System.Collections.Generic.Dictionary<string, string>();

                    foreach (var forgeVersion in allVersions)
                    {
                        // Skip non-installer versions and pre-releases
                        if (!forgeVersion.Contains("-") || forgeVersion.Contains("-beta") || forgeVersion.Contains("-alpha"))
                            continue;

                        // Parse format: 1.20.1-47.3.0
                        var parts = forgeVersion.Split('-');
                        if (parts.Length >= 2)
                        {
                            var mcVersion = parts[0]; // e.g., "1.20.1"

                            // Validate it's a proper Minecraft version
                            if (Version.TryParse(mcVersion, out var mcVer) && mcVer.Major == 1)
                            {
                                if (!versionToForgeMap.ContainsKey(mcVersion))
                                {
                                    versionToForgeMap[mcVersion] = forgeVersion;
                                    minecraftVersions.Add(mcVersion);
                                }
                                else
                                {
                                    // Keep the latest Forge version for this Minecraft version
                                    var existingForgeVersion = versionToForgeMap[mcVersion];
                                    if (CompareForgeVersions(forgeVersion, existingForgeVersion) > 0)
                                    {
                                        versionToForgeMap[mcVersion] = forgeVersion;
                                    }
                                }
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Forge mapped to {minecraftVersions.Count} Minecraft versions");

                    if (minecraftVersions.Count > 0)
                    {
                        var sortedVersions = minecraftVersions.ToList();
                        sortedVersions.Sort((a, b) => CompareMinecraftVersions(a, b));
                        sortedVersions.Reverse(); // newest first
                        latestVersion = sortedVersions[0];

                        // Limit to latest 50 versions for dropdown performance
                        if (sortedVersions.Count > 50)
                        {
                            System.Diagnostics.Debug.WriteLine($"Limited to latest 50 Forge versions for dropdown");
                            sortedVersions = sortedVersions.Take(50).ToList();
                        }

                        foreach (var mcVersion in sortedVersions)
                        {
                            if (versionToForgeMap.TryGetValue(mcVersion, out var forgeVersion))
                            {
                                // Build Maven download URL for installer
                                var downloadUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{forgeVersion}/forge-{forgeVersion}-installer.jar";
                                versionUrls[mcVersion] = downloadUrl;
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"Built {versionUrls.Count} Forge download URLs");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errorCode = "NETWORK_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                errorCode = "GENERAL_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Failed to fetch versions from Forge Maven: {ex.Message}");
            }

            if (errorCode != null && versionUrls.Count == 0)
            {
                var apiName = "Forge";
                var message = $"Failed to fetch the newest versions from the {apiName} API.\n\nError Code: {errorCode}\nError: {errorMessage}\n\nShowing locally stored versions instead.";

                MessageBox.Show(
                    message,
                    "API Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            // Populate combo with all versions
            PopulateVersionDropdown(latestVersion);
        }

        private int CompareForgeVersions(string a, string b)
        {
            // Format: MCVERSION-forge-FORGEVERSION
            // Extract just the forge version part (after the '-forge-' part)
            var forgePartA = GetForgeVersionPart(a);
            var forgePartB = GetForgeVersionPart(b);

            var partsA = forgePartA.Split('.');
            var partsB = forgePartB.Split('.');

            for (int i = 0; i < Math.Max(partsA.Length, partsB.Length); i++)
            {
                var partA = i < partsA.Length && int.TryParse(partsA[i], out var numA) ? numA : 0;
                var partB = i < partsB.Length && int.TryParse(partsB[i], out var numB) ? numB : 0;

                if (partA != partB)
                {
                    return partA.CompareTo(partB);
                }
            }

            return 0;
        }

        private string GetForgeVersionPart(string fullVersion)
        {
            // Format: 1.20.1-47.3.0
            var parts = fullVersion.Split('-');
            return parts.Length >= 2 ? parts[1] : fullVersion;
        }

        private int CompareNeoForgeVersions(string a, string b)
        {
            var partsA = a.Split('.');
            var partsB = b.Split('.');
            
            for (int i = 0; i < Math.Max(partsA.Length, partsB.Length); i++)
            {
                var partA = i < partsA.Length ? int.Parse(partsA[i]) : 0;
                var partB = i < partsB.Length ? int.Parse(partsB[i]) : 0;
                
                if (partA != partB)
                {
                    return partA.CompareTo(partB);
                }
            }
            
            return 0;
        }

        private async Task LoadFabricVersionsAsync()
        {
            versionUrls.Clear();
            string latestVersion = null;
            string errorMessage = null;
            string errorCode = null;

            // Try to fetch from Fabric Meta API first
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.5");
                
                // First, fetch all game versions
                var gameVersionsResponse = await http.GetAsync("https://meta.fabricmc.net/v2/versions/game");
                
                if (!gameVersionsResponse.IsSuccessStatusCode)
                {
                    errorCode = $"HTTP {(int)gameVersionsResponse.StatusCode}";
                    errorMessage = gameVersionsResponse.ReasonPhrase ?? "Unknown HTTP error";
                    System.Diagnostics.Debug.WriteLine($"Fabric Meta API returned error status: {errorCode} - {errorMessage}");
                }
                else
                {
                    var gameVersionsJson = await gameVersionsResponse.Content.ReadAsStringAsync();
                    using var gameVersionsDoc = JsonDocument.Parse(gameVersionsJson);

                    // Fabric API returns an array of game version objects
                    var allVersions = new System.Collections.Generic.List<string>();
                    
                    foreach (var versionObj in gameVersionsDoc.RootElement.EnumerateArray())
                    {
                        if (versionObj.TryGetProperty("version", out var versionElement))
                        {
                            var versionString = versionElement.GetString();
                            // Only include stable release versions to exclude snapshots and preview builds
                            if (!string.IsNullOrEmpty(versionString) && 
                                versionObj.TryGetProperty("stable", out var stableElement) &&
                                stableElement.GetBoolean() &&
                                !allVersions.Contains(versionString))
                            {
                                allVersions.Add(versionString);
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Fabric API returned {allVersions.Count} stable versions (filtered out snapshots)");

                    // Get the latest version (first one after sorting)
                    if (allVersions.Count > 0)
                    {
                        // Sort versions to get the latest
                        allVersions.Sort((a, b) => CompareMinecraftVersions(a, b));
                        allVersions.Reverse();
                        latestVersion = allVersions[0];
                        System.Diagnostics.Debug.WriteLine($"Latest Fabric version: {latestVersion}");
                        
                        // Limit to latest 50 stable versions to avoid excessive dropdown items
                        if (allVersions.Count > 50)
                        {
                            allVersions = allVersions.Take(50).ToList();
                            System.Diagnostics.Debug.WriteLine($"Limited to latest 50 stable versions for dropdown");
                        }
                    }

                    // Second, fetch the latest loader version (single request)
                    string latestLoaderVersion = null;
                    try
                    {
                        var loaderResponse = await http.GetAsync("https://meta.fabricmc.net/v2/versions/loader");
                        if (loaderResponse.IsSuccessStatusCode)
                        {
                            var loaderJson = await loaderResponse.Content.ReadAsStringAsync();
                            using var loaderDoc = JsonDocument.Parse(loaderJson);
                            
                            // Get the first (latest) loader version
                            if (loaderDoc.RootElement.GetArrayLength() > 0)
                            {
                                var firstLoader = loaderDoc.RootElement[0];
                                if (firstLoader.TryGetProperty("version", out var loaderVersionElement))
                                {
                                    latestLoaderVersion = loaderVersionElement.GetString();
                                    System.Diagnostics.Debug.WriteLine($"Latest Fabric loader version: {latestLoaderVersion}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to get latest loader version: {ex.Message}");
                    }

                    // Third, fetch the latest installer version (single request)
                    string latestInstallerVersion = null;
                    try
                    {
                        var installerResponse = await http.GetAsync("https://meta.fabricmc.net/v2/versions/installer");
                        if (installerResponse.IsSuccessStatusCode)
                        {
                            var installerJson = await installerResponse.Content.ReadAsStringAsync();
                            using var installerDoc = JsonDocument.Parse(installerJson);
                            
                            // Get the first (latest) installer version
                            if (installerDoc.RootElement.GetArrayLength() > 0)
                            {
                                var firstInstaller = installerDoc.RootElement[0];
                                if (firstInstaller.TryGetProperty("version", out var installerVersionElement))
                                {
                                    latestInstallerVersion = installerVersionElement.GetString();
                                    System.Diagnostics.Debug.WriteLine($"Latest Fabric installer version: {latestInstallerVersion}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to get latest installer version: {ex.Message}");
                    }

                    // Build download URLs using the latest loader and installer versions for all game versions
                    if (!string.IsNullOrEmpty(latestLoaderVersion) && !string.IsNullOrEmpty(latestInstallerVersion))
                    {
                        foreach (var versionString in allVersions)
                        {
                            // Use the Fabric server launcher download URL format with the latest loader and installer versions
                            var downloadUrl = $"https://meta.fabricmc.net/v2/versions/loader/{Uri.EscapeDataString(versionString)}/{latestLoaderVersion}/{latestInstallerVersion}/server/jar";
                            versionUrls[versionString] = downloadUrl;
                        }
                        System.Diagnostics.Debug.WriteLine($"Built {versionUrls.Count} download URLs using loader version {latestLoaderVersion} and installer version {latestInstallerVersion}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Could not get loader or installer version, no download URLs built");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                errorCode = "NETWORK_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                errorCode = "TIMEOUT_ERROR";
                errorMessage = "Request timed out. The API did not respond within the expected time.";
                System.Diagnostics.Debug.WriteLine($"Request timed out: {ex.Message}");
            }
            catch (Exception ex)
            {
                errorCode = "GENERAL_ERROR";
                errorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Failed to fetch versions from Fabric Meta API: {ex.Message}");
            }

            // Show error message if API failed
            if (errorCode != null || versionUrls.Count == 0)
            {
                var apiName = "Fabric";
                // If no error code but no versions, set a specific error code
                if (errorCode == null && versionUrls.Count == 0)
                {
                    errorCode = "NO_DATA";
                    errorMessage = "API returned no version data";
                }
                
                var message = $"Failed to fetch the newest versions from the {apiName} API.\n\nError Code: {errorCode}\nError: {errorMessage}\n\nShowing locally stored versions instead.";
                
                MessageBox.Show(
                    message,
                    "API Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                
                // Fall back to embedded JSON
                LoadVersionsFromEmbeddedJson(ref latestVersion);
            }

            // Populate combo with all versions
            PopulateVersionDropdown(latestVersion);
        }

        private void LoadVersionsFromEmbeddedJson(ref string latestVersion)
            => LoadVersionsFromEmbeddedJson("MC_Server_Manager_3.paper-versions.json", ref latestVersion);

        private void LoadVersionsFromEmbeddedJson(string resourceName, ref string latestVersion)
        {
            versionUrls.Clear();

            // Read from embedded resource
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var doc = JsonDocument.Parse(stream);

                    if (doc.RootElement.TryGetProperty("latest", out var latestEl))
                        latestVersion = latestEl.GetString();

                    if (doc.RootElement.TryGetProperty("versions", out var versions))
                    {
                        foreach (var prop in versions.EnumerateObject())
                        {
                            var key = prop.Name;
                            var value = prop.Value.GetString() ?? string.Empty;
                            versionUrls[key] = value;
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Embedded resource '{resourceName}' not found.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load embedded {resourceName}: {ex.Message}");
                versionUrls.Clear();
            }
        }

        private void PopulateVersionDropdown(string latestVersion)
        {
            // fallback if empty
            if (versionUrls.Count == 0)
            {
                versionUrls["1.20.2"] = "https://fill-data.papermc.io/v1/objects/ba340a835ac40b8563aa7eda1cd6479a11a7623409c89a2c35cd9d7490ed17a7/paper-1.20.2-318.jar";
            }

            // populate combo with all versions
            cmbVersions.Items.Clear();

            var keys = versionUrls.Keys.ToList();

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
            panelStep4.Visible = stepIndex == 3;

            btnBack.Enabled = stepIndex > 0;
            btnNext.Visible = stepIndex < 3;
            btnFinish.Visible = stepIndex == 3;

            if (stepIndex == 3)
            {
                lblSummary.Text =
                    $"Name: {ServerName}\r\n" +
                    $"Server Software: {ServerSoftware}\r\n" +
                    $"Version: {ServerVersion}\r\n" +
                    $"RAM: {ServerRamMB} MB\r\n" +
                    $"\r\nNote: The server JAR and a JDK were downloaded. A start.cmd was generated to launch the server using the bundled JDK.";
            }
        }

        // Map Minecraft version string to Java major according to your rules.
        private int MapMinecraftToJavaMajor(string mcVersion)
        {
            // For NeoForge, versions like "26.2" or "21.4" have different Java requirements
            // 26.x (1.26) -> Java 25
            // 21.x (1.21) through 25.x -> Java 21
            // 20.x (1.20) and below -> varies

            if (string.IsNullOrEmpty(mcVersion))
                return 25; // fallback

            // Try parse as Version
            if (!Version.TryParse(mcVersion, out var v))
            {
                // fallback: choose latest (Java 25)
                return 25;
            }

            // Handle new NeoForge versioning (26.x, 27.x etc. without 1. prefix)
            if (v.Major >= 26)
            {
                // 26.0 -> Java 21
                if (v.Major == 26 && v.Minor == 0)
                    return 21;
                // 26.1+ -> Java 25
                if (v.Major == 26 && v.Minor >= 1)
                    return 25;
                // 27+ -> Java 25
                if (v.Major >= 27)
                    return 25;
            }

            // Handle intermediate NeoForge versions (21-25 without 1. prefix)
            if (v.Major >= 21 && v.Major <= 25)
                return 21;

            // Handle classic Minecraft versioning (1.x.x)
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

            // Handle 2-25 range versions
            if (v.Major >= 2 && v.Major <= 25)
                return 21;

            // fallback for unknown future versions
            return 25;
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

        private bool ServerSoftwareRequiresInstallation(string software)
        {
            return software == "NeoForge" || software == "Forge";
        }

        private async Task<bool> RunServerInstallationAsync(string serverFolderPath, string downloadedJarPath, string serverSoftware)
        {
            if (!ServerSoftwareRequiresInstallation(serverSoftware))
            {
                return true; // No installation needed
            }

            try
            {
                // For Forge/NeoForge, the downloaded jar is the installer
                var installerName = serverSoftware == "Forge" ? "Forge" : "NeoForge";
                var installFlag = serverSoftware == "Forge" ? "--installServer" : "--installServer";

                lblInstallationProgress.Text = $"Running {installerName} installer...\r\nThe installer will open in a separate window.";
                progressBarInstallation.Value = 0;
                progressBarInstallation.Style = ProgressBarStyle.Marquee;

                var javaMajor = MapMinecraftToJavaMajor(ServerVersion);
                var jdkInstallFolder = Path.Combine(AppContext.BaseDirectory, "jdks", $"temurin-{javaMajor}");
                var javaExe = Path.Combine(jdkInstallFolder, "bin", "java.exe");

                if (!File.Exists(javaExe))
                {
                    MessageBox.Show($"Java executable not found at {javaExe}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = $"-jar \"{downloadedJarPath}\" {installFlag}",
                    WorkingDirectory = serverFolderPath,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        MessageBox.Show($"Failed to start {installerName} installer process", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    // Wait for the installation to complete
                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode != 0)
                    {
                        MessageBox.Show($"{installerName} installation failed with exit code {process.ExitCode}.\r\nPlease check the installer window for details.", "Installation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    // Find the win_args.txt file created by the installer (for Forge/NeoForge)
                    lblInstallationProgress.Text = $"Locating {installerName} configuration files...";
                    var winArgsPath = FindInstallerWinArgsPath(serverFolderPath, serverSoftware);

                    if (string.IsNullOrEmpty(winArgsPath))
                    {
                        MessageBox.Show($"{installerName} installation completed, but configuration files were not found.\r\nThe server may not be properly configured.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }

                    // Store the path for later use in start.cmd generation
                    if (serverSoftware == "Forge")
                    {
                        ForgeWinArgsPath = winArgsPath;
                    }
                    else if (serverSoftware == "NeoForge")
                    {
                        NeoForgeWinArgsPath = winArgsPath;
                    }

                    lblInstallationProgress.Text = $"{installerName} installation completed successfully!";
                    progressBarInstallation.Value = 100;
                    progressBarInstallation.Style = ProgressBarStyle.Continuous;
                    await Task.Delay(1500); // Show completion message briefly
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running server installation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                progressBarInstallation.Style = ProgressBarStyle.Continuous;
                return false;
            }
        }

        private string FindInstallerWinArgsPath(string serverFolderPath, string serverSoftware)
        {
            // This finds win_args.txt for both Forge and NeoForge
            try
            {
                string librariesPath;
                if (serverSoftware == "Forge")
                {
                    // Forge uses minecraftforge path
                    librariesPath = Path.Combine(serverFolderPath, "libraries", "net", "minecraftforge", "forge");
                }
                else
                {
                    // NeoForge uses neoforged path
                    librariesPath = Path.Combine(serverFolderPath, "libraries", "net", "neoforged", "neoforge");
                }

                if (!Directory.Exists(librariesPath))
                    return string.Empty;

                // Look for win_args.txt in any version folder
                var winArgsFile = Directory.GetFiles(librariesPath, "win_args.txt", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(winArgsFile))
                {
                    // Return the relative path from the server folder
                    var relativePath = Path.GetRelativePath(serverFolderPath, winArgsFile);
                    return relativePath;
                }
            }
            catch { }

            return string.Empty;
        }

        private string FindNeoForgeWinArgsPath(string serverFolderPath)
        {
            try
            {
                var librariesPath = Path.Combine(serverFolderPath, "libraries", "net", "neoforged", "neoforge");
                if (!Directory.Exists(librariesPath))
                    return string.Empty;

                // Look for win_args.txt in any version folder
                var winArgsFile = Directory.GetFiles(librariesPath, "win_args.txt", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(winArgsFile))
                {
                    // Return the relative path from the server folder
                    var relativePath = Path.GetRelativePath(serverFolderPath, winArgsFile);
                    return relativePath;
                }
            }
            catch { }
            return string.Empty;
        }

        // When moving from step 0 to step 1 we create folder and download server JAR and mapped JDK (with progress dialog).
        private async void btnNext_Click(object? sender, EventArgs e)
        {
            if (stepIndex == 0)
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Please enter a server name.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (cmbServerSoftware.SelectedItem == null)
                {
                    MessageBox.Show("Please select a server software.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (cmbVersions.SelectedItem == null)
                {
                    MessageBox.Show("Please select a version.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var version = cmbVersions.SelectedItem!.ToString()!;
                if (!versionUrls.TryGetValue(version, out var downloadUrl) || string.IsNullOrWhiteSpace(downloadUrl))
                {
                    MessageBox.Show("Download URL not available for selected version.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                // download server jar into this folder as server.jar (generic name)
                var destJar = Path.Combine(ServerFolderPath, "server.jar");
                var ctsServer = new CancellationTokenSource();
                using (var progressDlg = new ProgressDialog(ctsServer, $"Downloading {ServerSoftware} {version}..."))
                {
                    var progress = new Progress<int>(percent => progressDlg.SetProgress(percent));
                    try
                    {
                        progressDlg.Show(this);
                        DownloadedJarPath = await DownloadServerJarAsync(downloadUrl, destJar, progress, ctsServer.Token);
                        if (string.IsNullOrEmpty(DownloadedJarPath) || !File.Exists(DownloadedJarPath))
                        {
                            MessageBox.Show($"{ServerSoftware} download failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            try { Directory.Delete(ServerFolderPath, true); } catch { }
                            ServerFolderPath = string.Empty;
                            return;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        MessageBox.Show($"{ServerSoftware} download cancelled.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        try { Directory.Delete(ServerFolderPath, true); } catch { }
                        ServerFolderPath = string.Empty;
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to download {ServerSoftware}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    using (var progressDlg = new ProgressDialog(ctsJdk, $"Downloading JDK {javaMajor}..."))
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
            else if (stepIndex == 1)
            {
                // Check if this server software needs installation (only NeoForge)
                if (ServerSoftwareRequiresInstallation(ServerSoftware))
                {
                    // Transition to step 2 (installation) and run the installation
                    stepIndex = 2;
                    UpdateStep();
                    btnNext.Enabled = false;
                    btnBack.Enabled = false;

                    var success = await RunServerInstallationAsync(ServerFolderPath, DownloadedJarPath, ServerSoftware);

                    btnNext.Enabled = true;
                    btnBack.Enabled = true;

                    if (!success)
                    {
                        // Stay on installation step
                        return;
                    }
                    // Installation succeeded, move to next step
                    stepIndex = 3;
                    UpdateStep();
                    return;
                }
                // No installation needed for non-NeoForge servers, skip to step 3 (summary)
                stepIndex = 3;
                UpdateStep();
                return;
            }

            stepIndex = Math.Min(3, stepIndex + 1);
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
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.4 (https://github.com/JaatrovyKnedlicek/MC-Server-Manager-Windows)");
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
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MCServerManager/3.4 (https://github.com/JaatrovyKnedlicek/MC-Server-Manager-Windows)");
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

        private async Task<string> DownloadServerJarAsync(string url, string destination, IProgress<int> progress, CancellationToken ct)
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

                if (ServerSoftware == "NeoForge" || ServerSoftware == "Forge")
                {
                    // For Forge/NeoForge, we need to use the generated argument files
                    // and also create user_jvm_args.txt with memory settings
                    string winArgsPath = ServerSoftware == "Forge" ? ForgeWinArgsPath : NeoForgeWinArgsPath;
                    string softwareName = ServerSoftware == "Forge" ? "Forge" : "NeoForge";

                    if (string.IsNullOrEmpty(winArgsPath))
                    {
                        // Fallback in case path wasn't found during installation
                        MessageBox.Show($"{softwareName} win_args.txt path not found. Please reinstall the server.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Use bundled Java if available, otherwise fall back to system java
                    if (File.Exists(javaExe))
                    {
                        // Escape backslashes for batch file
                        winArgsPath = winArgsPath.Replace("\\", "\\");
                        startCmd = $"@echo off\r\n\"{javaExe}\" @user_jvm_args.txt @{winArgsPath} --nogui %*\r\npause\r\n";
                    }
                    else
                    {
                        MessageBox.Show($"Bundled JDK not found for Java {javaMajor}, start.cmd will use system java.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        // Escape backslashes for batch file
                        winArgsPath = winArgsPath.Replace("\\", "\\");
                        startCmd = $"@echo off\r\njava @user_jvm_args.txt @{winArgsPath} --nogui %*\r\npause\r\n";
                    }

                    // Also create user_jvm_args.txt with JVM memory arguments
                    string userJvmArgs = $"-Xms{ramArg}\r\n-Xmx{ramArg}\r\n";
                    if (!string.IsNullOrEmpty(ServerFolderPath))
                    {
                        File.WriteAllText(Path.Combine(ServerFolderPath, "user_jvm_args.txt"), userJvmArgs);
                    }
                }
                else if (File.Exists(javaExe))
                {
                    startCmd = $"@echo off\r\n\"{javaExe}\" -Xms{ramArg} -Xmx{ramArg} -jar \"%~dp0\\server.jar\" --nogui\r\n";
                }
                else
                {
                    MessageBox.Show($"Bundled JDK not found for Java {javaMajor}, start.cmd will use system java.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    startCmd = $"@echo off\r\njava -Xms{ramArg} -Xmx{ramArg} -jar \"%~dp0\\server.jar\" --nogui\r\n";
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