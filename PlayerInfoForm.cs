using System;
using System.Drawing;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public partial class PlayerInfoForm : Form
    {
        private System.ComponentModel.IContainer components = null;
        private readonly string playerName;
        private readonly RconClient rconClient;
        private readonly HttpClient httpClient;
        
        // UI Controls
        private System.Windows.Forms.PictureBox picSkin;
        private System.Windows.Forms.Label lblUsernameTitle;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.Label lblUUIDTitle;
        private System.Windows.Forms.Label lblUUID;
        private System.Windows.Forms.Label lblIPTitle;
        private System.Windows.Forms.Label lblIP;
        private System.Windows.Forms.Label lblOPTitle;
        private System.Windows.Forms.Label lblOP;
        private System.Windows.Forms.Label lblWhitelistTitle;
        private System.Windows.Forms.Label lblWhitelist;
        private System.Windows.Forms.Label lblBannedTitle;
        private System.Windows.Forms.Label lblBanned;
        private System.Windows.Forms.Button btnClose;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                httpClient?.Dispose();
                picSkin?.Image?.Dispose();
            }
            base.Dispose(disposing);
        }

        public PlayerInfoForm(string playerName, RconClient rconClient)
        {
            InitializeComponent();
            
            this.playerName = playerName;
            this.rconClient = rconClient;
            this.httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            
            // Set form title
            Text = $"Player Info: {playerName}";
            
            // Set default placeholder image
            picSkin.Image = CreatePlaceholderImage();
            
            // Set initial values
            lblUsername.Text = playerName;
            lblUUID.Text = "Loading...";
            lblIP.Text = "Loading...";
            lblOP.Text = "Loading...";
            lblWhitelist.Text = "Loading...";
            lblBanned.Text = "Loading...";
            
            // Load player info asynchronously
            _ = LoadPlayerInfoAsync();
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.picSkin = new System.Windows.Forms.PictureBox();
            this.lblUsernameTitle = new System.Windows.Forms.Label();
            this.lblUsername = new System.Windows.Forms.Label();
            this.lblUUIDTitle = new System.Windows.Forms.Label();
            this.lblUUID = new System.Windows.Forms.Label();
            this.lblIPTitle = new System.Windows.Forms.Label();
            this.lblIP = new System.Windows.Forms.Label();
            this.lblOPTitle = new System.Windows.Forms.Label();
            this.lblOP = new System.Windows.Forms.Label();
            this.lblWhitelistTitle = new System.Windows.Forms.Label();
            this.lblWhitelist = new System.Windows.Forms.Label();
            this.lblBannedTitle = new System.Windows.Forms.Label();
            this.lblBanned = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.picSkin)).BeginInit();
            this.SuspendLayout();
            // 
            // picSkin
            // 
            this.picSkin.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picSkin.Location = new System.Drawing.Point(20, 20);
            this.picSkin.Name = "picSkin";
            this.picSkin.Size = new System.Drawing.Size(64, 64);
            this.picSkin.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.picSkin.TabIndex = 0;
            this.picSkin.TabStop = false;
            // 
            // lblUsernameTitle
            // 
            this.lblUsernameTitle.AutoSize = true;
            this.lblUsernameTitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblUsernameTitle.Location = new System.Drawing.Point(100, 20);
            this.lblUsernameTitle.Name = "lblUsernameTitle";
            this.lblUsernameTitle.Size = new System.Drawing.Size(61, 15);
            this.lblUsernameTitle.TabIndex = 1;
            this.lblUsernameTitle.Text = "Username:";
            // 
            // lblUsername
            // 
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new System.Drawing.Point(180, 20);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(0, 15);
            this.lblUsername.TabIndex = 2;
            // 
            // lblUUIDTitle
            // 
            this.lblUUIDTitle.AutoSize = true;
            this.lblUUIDTitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblUUIDTitle.Location = new System.Drawing.Point(100, 45);
            this.lblUUIDTitle.Name = "lblUUIDTitle";
            this.lblUUIDTitle.Size = new System.Drawing.Size(42, 15);
            this.lblUUIDTitle.TabIndex = 3;
            this.lblUUIDTitle.Text = "UUID:";
            // 
            // lblUUID
            // 
            this.lblUUID.AutoSize = true;
            this.lblUUID.Location = new System.Drawing.Point(180, 45);
            this.lblUUID.Name = "lblUUID";
            this.lblUUID.Size = new System.Drawing.Size(0, 15);
            this.lblUUID.TabIndex = 4;
            // 
            // lblIPTitle
            // 
            this.lblIPTitle.AutoSize = true;
            this.lblIPTitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblIPTitle.Location = new System.Drawing.Point(20, 110);
            this.lblIPTitle.Name = "lblIPTitle";
            this.lblIPTitle.Size = new System.Drawing.Size(68, 15);
            this.lblIPTitle.TabIndex = 5;
            this.lblIPTitle.Text = "IP Address:";
            // 
            // lblIP
            // 
            this.lblIP.AutoSize = true;
            this.lblIP.Location = new System.Drawing.Point(120, 110);
            this.lblIP.Name = "lblIP";
            this.lblIP.Size = new System.Drawing.Size(0, 15);
            this.lblIP.TabIndex = 6;
            // 
            // lblOPTitle
            // 
            this.lblOPTitle.AutoSize = true;
            this.lblOPTitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblOPTitle.Location = new System.Drawing.Point(20, 140);
            this.lblOPTitle.Name = "lblOPTitle";
            this.lblOPTitle.Size = new System.Drawing.Size(57, 15);
            this.lblOPTitle.TabIndex = 7;
            this.lblOPTitle.Text = "OP Status:";
            // 
            // lblOP
            // 
            this.lblOP.AutoSize = true;
            this.lblOP.Location = new System.Drawing.Point(120, 140);
            this.lblOP.Name = "lblOP";
            this.lblOP.Size = new System.Drawing.Size(0, 15);
            this.lblOP.TabIndex = 8;
            // 
            // lblWhitelistTitle
            // 
            this.lblWhitelistTitle.AutoSize = true;
            this.lblWhitelistTitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblWhitelistTitle.Location = new System.Drawing.Point(20, 170);
            this.lblWhitelistTitle.Name = "lblWhitelistTitle";
            this.lblWhitelistTitle.Size = new System.Drawing.Size(70, 15);
            this.lblWhitelistTitle.TabIndex = 9;
            this.lblWhitelistTitle.Text = "Whitelisted:";
            // 
            // lblWhitelist
            // 
            this.lblWhitelist.AutoSize = true;
            this.lblWhitelist.Location = new System.Drawing.Point(120, 170);
            this.lblWhitelist.Name = "lblWhitelist";
            this.lblWhitelist.Size = new System.Drawing.Size(0, 15);
            this.lblWhitelist.TabIndex = 10;
            // 
            // lblBannedTitle
            // 
            this.lblBannedTitle.AutoSize = true;
            this.lblBannedTitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblBannedTitle.Location = new System.Drawing.Point(20, 200);
            this.lblBannedTitle.Name = "lblBannedTitle";
            this.lblBannedTitle.Size = new System.Drawing.Size(47, 15);
            this.lblBannedTitle.TabIndex = 11;
            this.lblBannedTitle.Text = "Banned:";
            // 
            // lblBanned
            // 
            this.lblBanned.AutoSize = true;
            this.lblBanned.Location = new System.Drawing.Point(120, 200);
            this.lblBanned.Name = "lblBanned";
            this.lblBanned.Size = new System.Drawing.Size(0, 15);
            this.lblBanned.TabIndex = 12;
            // 
            // btnClose
            // 
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Location = new System.Drawing.Point(380, 320);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(80, 25);
            this.btnClose.TabIndex = 13;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            // 
            // PlayerInfoForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.ClientSize = new System.Drawing.Size(492, 371);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.lblBanned);
            this.Controls.Add(this.lblBannedTitle);
            this.Controls.Add(this.lblWhitelist);
            this.Controls.Add(this.lblWhitelistTitle);
            this.Controls.Add(this.lblOP);
            this.Controls.Add(this.lblOPTitle);
            this.Controls.Add(this.lblIP);
            this.Controls.Add(this.lblIPTitle);
            this.Controls.Add(this.lblUUID);
            this.Controls.Add(this.lblUUIDTitle);
            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.lblUsernameTitle);
            this.Controls.Add(this.picSkin);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PlayerInfoForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "PlayerInfoForm";
            ((System.ComponentModel.ISupportInitialize)(this.picSkin)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private Bitmap CreatePlaceholderImage()
        {
            var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.LightGray);
                g.DrawString("?", new Font("Segoe UI", 24, FontStyle.Bold), Brushes.Gray, 22, 12);
            }
            return bmp;
        }

        private async Task LoadPlayerInfoAsync()
        {
            try
            {
                // Fetch UUID from Minecraft API
                var uuid = await GetPlayerUuidAsync(playerName);
                if (!string.IsNullOrEmpty(uuid))
                {
                    lblUUID.Text = uuid;
                    
                    // Fetch skin
                    _ = LoadPlayerSkinAsync(uuid);
                }
                else
                {
                    lblUUID.Text = "Unable to fetch";
                }
                
                // Fetch in-game info via RCON
                await FetchInGameInfoAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading player info: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<string?> GetPlayerUuidAsync(string username)
        {
            try
            {
                var response = await httpClient.GetStringAsync($"https://api.mojang.com/users/profiles/minecraft/{username}");
                var data = JsonSerializer.Deserialize<JsonElement>(response);
                return data.GetProperty("id").GetString();
            }
            catch
            {
                return null;
            }
        }

        private async Task LoadPlayerSkinAsync(string uuid)
        {
            try
            {
                var response = await httpClient.GetStringAsync($"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");
                var data = JsonSerializer.Deserialize<JsonElement>(response);
                
                // Get skin URL from profile
                foreach (var property in data.GetProperty("properties").EnumerateArray())
                {
                    if (property.GetProperty("name").GetString() == "textures")
                    {
                        var value = property.GetProperty("value").GetString();
                        if (!string.IsNullOrEmpty(value))
                        {
                            // Decode base64 to get JSON with skin URL
                            var decodedBytes = System.Convert.FromBase64String(value);
                            var decodedJson = System.Text.Encoding.UTF8.GetString(decodedBytes);
                            var textureData = JsonSerializer.Deserialize<JsonElement>(decodedJson);
                            
                            if (textureData.TryGetProperty("textures", out var textures))
                            {
                                if (textures.TryGetProperty("SKIN", out var skin))
                                {
                                    var skinUrl = skin.GetProperty("url").GetString();
                                    if (!string.IsNullOrEmpty(skinUrl))
                                    {
                                        await LoadImageAsync(skinUrl);
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }
            catch
            {
                // If skin loading fails, keep placeholder
            }
        }

        private async Task LoadImageAsync(string imageUrl)
        {
            try
            {
                var imageData = await httpClient.GetByteArrayAsync(imageUrl);
                using var ms = new System.IO.MemoryStream(imageData);
                var image = Image.FromStream(ms);
                
                this.Invoke(() =>
                {
                    picSkin.Image?.Dispose();
                    picSkin.Image = image;
                });
            }
            catch
            {
                this.Invoke(() =>
                {
                    picSkin.Image?.Dispose();
                    picSkin.Image = CreatePlaceholderImage();
                });
            }
        }

        private async Task FetchInGameInfoAsync()
        {
            try
            {
                // Get player IP/address (using /msg or other commands)
                // Note: Getting player IP requires server plugins or specific configurations
                // We'll try with basic commands first
                // In reality, getting IP requires server-side plugins or configuration
                this.Invoke(() => lblIP.Text = "Not implemented yet");

                // Check OP status

                var opResponse = await rconClient.SendCommandAsync("op list");
                this.Invoke(() => lblOP.Text = opResponse?.Contains(playerName, StringComparison.OrdinalIgnoreCase) == true ? "Yes" : "No");
                
                // Check whitelist status
                var whitelistResponse = await rconClient.SendCommandAsync("whitelist list");
                this.Invoke(() => lblWhitelist.Text = whitelistResponse?.Contains(playerName, StringComparison.OrdinalIgnoreCase) == true ? "Yes" : "No");
                
                // Check ban status
                var banResponse = await rconClient.SendCommandAsync("banlist");
                this.Invoke(() => lblBanned.Text = banResponse?.Contains(playerName, StringComparison.OrdinalIgnoreCase) == true ? "Yes" : "No");
            }
            catch
            {
                this.Invoke(() =>
                {
                    lblIP.Text = "Error fetching info";
                    lblOP.Text = "Error";
                    lblWhitelist.Text = "Error";
                    lblBanned.Text = "Error";
                });
            }
        }
    }
}