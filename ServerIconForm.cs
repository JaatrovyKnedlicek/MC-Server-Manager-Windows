using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class ServerIconForm : Form
    {
        private Label lblTitle;
        private Label lblServerName;
        private PictureBox pbCurrentIcon;
        private PictureBox pbPreviewIcon;
        private Button btnSelectImage;
        private Button btnSave;
        private Button btnCancel;
        private ProgressBar progressBar;
        private Label lblStatus;
        private GroupBox grpCurrent;
        private GroupBox grpPreview;

        private string serverFolderPath;
        private string? selectedImagePath;
        private Image? currentIcon;
        private Image? previewIcon;

        public ServerIconForm(string serverName, string folderPath)
        {
            serverFolderPath = folderPath;
            InitializeComponent();
            LoadCurrentIcon();
            if (lblServerName != null)
            {
                lblServerName.Text = $"Server: {serverName}";
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Server Icon Settings (server-icon.png)";
            this.Size = new Size(700, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Title
            lblTitle = new Label
            {
                Text = "Set Server Icon",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(20, 15),
                Size = new Size(640, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Server name
            lblServerName = new Label
            {
                Text = "Server: ...",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                Location = new Point(20, 50),
                Size = new Size(640, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Current icon group
            grpCurrent = new GroupBox
            {
                Text = "Current Icon",
                Location = new Point(20, 80),
                Size = new Size(300, 320)
            };

            pbCurrentIcon = new PictureBox
            {
                Location = new Point(10, 20),
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            grpCurrent.Controls.Add(pbCurrentIcon);

            // Preview icon group
            grpPreview = new GroupBox
            {
                Text = "Preview (64x64 PNG - server-icon.png)",
                Location = new Point(340, 80),
                Size = new Size(300, 320)
            };

            pbPreviewIcon = new PictureBox
            {
                Location = new Point(10, 20),
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            grpPreview.Controls.Add(pbPreviewIcon);

            // Select image button
            btnSelectImage = new Button
            {
                Text = "Select Image...",
                Location = new Point(20, 410),
                Size = new Size(150, 30)
            };
            btnSelectImage.Click += btnSelectImage_Click;

            // Save button
            btnSave = new Button
            {
                Text = "Save",
                Location = new Point(480, 410),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK
            };
            btnSave.Click += btnSave_Click;

            // Cancel button
            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(580, 410),
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel
            };

            // Progress bar
            progressBar = new ProgressBar
            {
                Location = new Point(20, 445),
                Size = new Size(640, 20),
                Visible = false
            };

            // Status label
            lblStatus = new Label
            {
                Text = "",
                Location = new Point(180, 415),
                Size = new Size(280, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Add controls to form
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblServerName);
            this.Controls.Add(grpCurrent);
            this.Controls.Add(grpPreview);
            this.Controls.Add(btnSelectImage);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblStatus);
        }

        private void btnSelectImage_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.ico;|All Files|*.*",
                Title = "Select Server Icon"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            selectedImagePath = ofd.FileName;
            ProcessSelectedImage();
        }

        private void btnSave_Click(object? sender, EventArgs e)
        {
            if (previewIcon == null)
            {
                MessageBox.Show("No image to save.", "Save Icon", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                progressBar.Visible = true;
                progressBar.Value = 0;
                lblStatus.Text = "Saving icon...";
                btnSave.Enabled = false;

                var iconPath = Path.Combine(serverFolderPath, "server-icon.png");

                // Dispose current icon to release file lock
                pbCurrentIcon.Image = null;
                currentIcon?.Dispose();
                currentIcon = null;

                // Delete old icon if it exists
                if (File.Exists(iconPath))
                {
                    try
                    {
                        File.Delete(iconPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to delete old icon: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = "Error deleting old icon";
                        return;
                    }
                }

                // Save new icon
                previewIcon.Save(iconPath, ImageFormat.Png);

                // Reload and display the new icon
                currentIcon = new Bitmap(iconPath);
                pbCurrentIcon.Image = currentIcon;

                progressBar.Value = 100;
                lblStatus.Text = "Icon saved successfully!";
                MessageBox.Show("Server icon saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save icon: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error saving icon";
            }
            finally
            {
                btnSave.Enabled = true;
                progressBar.Visible = false;
            }
        }

        private void LoadCurrentIcon()
        {
            try
            {
                var iconPath = Path.Combine(serverFolderPath, "server-icon.png");
                if (File.Exists(iconPath))
                {
                    currentIcon = Image.FromFile(iconPath);
                    pbCurrentIcon.Image = currentIcon;
                }
                else
                {
                    // Show default placeholder
                    pbCurrentIcon.Image = CreatePlaceholderIcon();
                }
            }
            catch
            {
                pbCurrentIcon.Image = CreatePlaceholderIcon();
            }
        }

        private Image CreatePlaceholderIcon()
        {
            var bitmap = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.LightGray);
                using (var brush = new SolidBrush(Color.Gray))
                {
                    g.DrawString("No Icon", new Font("Arial", 8), brush, 5, 25);
                }
            }
            return bitmap;
        }

        private async void ProcessSelectedImage()
        {
            if (string.IsNullOrEmpty(selectedImagePath) || !File.Exists(selectedImagePath))
                return;

            try
            {
                progressBar.Visible = true;
                progressBar.Value = 0;
                lblStatus.Text = "Loading image...";
                btnSave.Enabled = false;
                btnSelectImage.Enabled = false;

                await Task.Run(() =>
                {
                    // Load the image
                    using var originalImage = Image.FromFile(selectedImagePath);

                    // Resize to 64x64
                    using var resizedImage = new Bitmap(64, 64);
                    using (var g = Graphics.FromImage(resizedImage))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(originalImage, 0, 0, 64, 64);
                    }

                    // Convert to PNG
                    previewIcon = new Bitmap(resizedImage);
                });

                // Update UI on main thread
                pbPreviewIcon.Image = previewIcon;
                progressBar.Value = 100;
                lblStatus.Text = "Image processed successfully!";
                btnSave.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error processing image";
            }
            finally
            {
                btnSelectImage.Enabled = true;
                progressBar.Visible = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                currentIcon?.Dispose();
                previewIcon?.Dispose();
                pbCurrentIcon?.Dispose();
                pbPreviewIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
