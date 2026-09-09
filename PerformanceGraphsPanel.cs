using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class PerformanceGraphsPanel : Panel
    {
        private PerformanceCounter? cpuCounter;
        private PerformanceCounter? ramCounter;
        private PerformanceCounter? ramAvailableCounter;
        private PerformanceCounter? diskCounter;
        private PerformanceCounter? diskReadCounter;
        private PerformanceCounter? diskWriteCounter;
        private Queue<float> cpuHistory = new Queue<float>(120); // 2 minutes of data at 1 sample/sec
        private Queue<float> ramHistory = new Queue<float>(120);
        private Queue<float> networkDownloadHistory = new Queue<float>(120);
        private Queue<float> networkUploadHistory = new Queue<float>(120);
        private Queue<float> diskUsageHistory = new Queue<float>(120);
        private Queue<float> diskReadHistory = new Queue<float>(120);
        private Queue<float> diskWriteHistory = new Queue<float>(120);

        private float maxNetworkBandwidth = 10f; // 10 Mbps default
        private long lastNetworkBytesReceived = 0;
        private long lastNetworkBytesSent = 0;
        private DateTime lastNetworkCheck = DateTime.Now;

        private long totalRamMB = 0;
        private float currentRamUsedMB = 0;
        private float currentDownloadMbps = 0;
        private float currentUploadMbps = 0;
        private float currentDiskUsagePercent = 0;
        private float currentDiskReadMBps = 0;
        private float currentDiskWriteMBps = 0;
        private long lastDiskReadBytes = 0;
        private long lastDiskWriteBytes = 0;
        private DateTime lastDiskCheck = DateTime.Now;

        private System.Windows.Forms.Timer? updateTimer;
        private readonly object lockObject = new object();

        public PerformanceGraphsPanel()
        {
            DoubleBuffered = true;
            BackColor = SystemColors.Control;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);

            InitializePerformanceCounters();
            InitializeTimer();
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                // CPU usage counter
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                cpuCounter.NextValue(); // First call returns garbage
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize CPU counter: {ex.Message}");
                cpuCounter = null;
            }

            try
            {
                // RAM usage counter (percentage)
                ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use", "", true);
                ramCounter.NextValue();

                // RAM available in MB
                ramAvailableCounter = new PerformanceCounter("Memory", "Available MBytes", "", true);
                ramAvailableCounter.NextValue();

                // Calculate total RAM
                try
                {
                    var totalRam = new PerformanceCounter("Memory", "Committed Bytes", "", true);
                    var availableRam = ramAvailableCounter.NextValue();
                    var committedRam = totalRam.NextValue() / (1024 * 1024);
                    totalRamMB = (long)(availableRam + committedRam);
                    totalRam.Dispose();
                }
                catch
                {
                    totalRamMB = 16000; // fallback default
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize RAM counter: {ex.Message}");
                ramCounter = null;
                ramAvailableCounter = null;
            }

            try
            {
                // Disk usage counter (percentage)
                diskCounter = new PerformanceCounter("LogicalDisk", "% Free Space", "_Total", true);
                diskCounter.NextValue();

                // Disk read counter (bytes/sec)
                diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
                diskReadCounter.NextValue();

                // Disk write counter (bytes/sec)
                diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
                diskWriteCounter.NextValue();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize disk counter: {ex.Message}");
                diskCounter = null;
                diskReadCounter = null;
                diskWriteCounter = null;
            }
        }

        private void InitializeTimer()
        {
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 1000; // Update every second
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            CollectPerformanceData();
            Invalidate();
        }

        private void CollectPerformanceData()
        {
            lock (lockObject)
            {
                // CPU
                if (cpuCounter != null)
                {
                    try
                    {
                        float cpuUsage = cpuCounter.NextValue();
                        cpuHistory.Enqueue(cpuUsage);
                        if (cpuHistory.Count > 120)
                            cpuHistory.Dequeue();
                    }
                    catch { }
                }

                // RAM
                if (ramCounter != null && ramAvailableCounter != null)
                {
                    try
                    {
                        float ramUsage = ramCounter.NextValue();
                        ramHistory.Enqueue(ramUsage);
                        if (ramHistory.Count > 120)
                            ramHistory.Dequeue();

                        // Calculate used RAM in MB
                        float availableMB = ramAvailableCounter.NextValue();
                        currentRamUsedMB = totalRamMB - availableMB;
                    }
                    catch { }
                }

                // Network (upload and download separately)
                try
                {
                    long totalBytesReceived = 0;
                    long totalBytesSent = 0;

                    foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (nic.OperationalStatus != OperationalStatus.Up)
                            continue;

                        var stats = nic.GetIPStatistics();
                        totalBytesReceived += stats.BytesReceived;
                        totalBytesSent += stats.BytesSent;
                    }

                    var now = DateTime.Now;
                    var elapsed = (now - lastNetworkCheck).TotalSeconds;

                    if (elapsed > 0 && lastNetworkBytesReceived > 0)
                    {
                        // Download speed (bytes received per second -> Mbps)
                        float downloadBytesPerSec = (float)((totalBytesReceived - lastNetworkBytesReceived) / elapsed);
                        float downloadMbps = (downloadBytesPerSec * 8) / (1024 * 1024); // Convert bytes/sec to Mbps
                        currentDownloadMbps = downloadMbps;

                        // Upload speed (bytes sent per second -> Mbps)
                        float uploadBytesPerSec = (float)((totalBytesSent - lastNetworkBytesSent) / elapsed);
                        float uploadMbps = (uploadBytesPerSec * 8) / (1024 * 1024); // Convert bytes/sec to Mbps
                        currentUploadMbps = uploadMbps;

                        // Update max bandwidth if exceeded
                        float maxSpeed = Math.Max(downloadMbps, uploadMbps);
                        if (maxSpeed > maxNetworkBandwidth)
                            maxNetworkBandwidth = maxSpeed * 1.2f;

                        // Store average download for graph
                        float avgMbps = (downloadMbps + uploadMbps) / 2f;
                        float networkUsagePercent = (avgMbps / maxNetworkBandwidth) * 100f;
                        networkDownloadHistory.Enqueue(Math.Min(downloadMbps / maxNetworkBandwidth * 100f, 100));
                        networkUploadHistory.Enqueue(Math.Min(uploadMbps / maxNetworkBandwidth * 100f, 100));

                        if (networkDownloadHistory.Count > 120)
                            networkDownloadHistory.Dequeue();
                        if (networkUploadHistory.Count > 120)
                            networkUploadHistory.Dequeue();
                    }

                    lastNetworkBytesReceived = totalBytesReceived;
                    lastNetworkBytesSent = totalBytesSent;
                    lastNetworkCheck = now;
                }
                catch { }

                // Disk (usage, read, write)
                try
                {
                    if (diskCounter != null)
                    {
                        float freeSpacePercent = diskCounter.NextValue();
                        currentDiskUsagePercent = 100f - freeSpacePercent;
                        diskUsageHistory.Enqueue(currentDiskUsagePercent);
                        if (diskUsageHistory.Count > 120)
                            diskUsageHistory.Dequeue();
                    }

                    if (diskReadCounter != null && diskWriteCounter != null)
                    {
                        var now = DateTime.Now;
                        var elapsed = (now - lastDiskCheck).TotalSeconds;

                        if (elapsed > 0 && lastDiskReadBytes > 0)
                        {
                            long currentReadBytes = (long)diskReadCounter.NextValue();
                            long currentWriteBytes = (long)diskWriteCounter.NextValue();

                            // Calculate MB/s
                            float readBytesPerSec = (float)((currentReadBytes - lastDiskReadBytes) / elapsed);
                            float writeBytesPerSec = (float)((currentWriteBytes - lastDiskWriteBytes) / elapsed);

                            currentDiskReadMBps = readBytesPerSec / (1024 * 1024);
                            currentDiskWriteMBps = writeBytesPerSec / (1024 * 1024);

                            // Store for graph (normalize to reasonable scale, e.g., 100 MB/s max)
                            float maxDiskSpeed = 100f; // 100 MB/s max for graph scaling
                            diskReadHistory.Enqueue(Math.Min(currentDiskReadMBps / maxDiskSpeed * 100f, 100));
                            diskWriteHistory.Enqueue(Math.Min(currentDiskWriteMBps / maxDiskSpeed * 100f, 100));

                            if (diskReadHistory.Count > 120)
                                diskReadHistory.Dequeue();
                            if (diskWriteHistory.Count > 120)
                                diskWriteHistory.Dequeue();

                            lastDiskReadBytes = currentReadBytes;
                            lastDiskWriteBytes = currentWriteBytes;
                        }
                        else
                        {
                            // First call - initialize
                            lastDiskReadBytes = (long)diskReadCounter.NextValue();
                            lastDiskWriteBytes = (long)diskWriteCounter.NextValue();
                        }
                        lastDiskCheck = now;
                    }
                }
                catch { }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);

            lock (lockObject)
            {
                int graphWidth = 300; // Fixed width instead of full width
                int graphHeight = Height / 4;
                int xOffset = 10;

                DrawCpuGraph(e.Graphics, cpuHistory, xOffset, 0, graphWidth, graphHeight, Color.FromArgb(0, 120, 255));
                DrawRamGraph(e.Graphics, ramHistory, xOffset, graphHeight, graphWidth, graphHeight, Color.FromArgb(0, 200, 0));
                DrawNetworkGraph(e.Graphics, networkDownloadHistory, networkUploadHistory, xOffset, graphHeight * 2, graphWidth, graphHeight);
                DrawDiskGraph(e.Graphics, diskUsageHistory, diskReadHistory, diskWriteHistory, xOffset, graphHeight * 3, graphWidth, graphHeight);
            }
        }

        private void DrawCpuGraph(Graphics g, Queue<float> data, int offsetX, int offsetY, int width, int height, Color color)
        {
            if (data.Count < 2)
                return;

            // Draw background
            using (var brush = new SolidBrush(SystemColors.Window))
            {
                g.FillRectangle(brush, offsetX, offsetY, width, height);
            }

            // Draw border
            using (var pen = new Pen(Color.Gray))
            {
                g.DrawRectangle(pen, offsetX, offsetY, width - 1, height - 1);
            }

            // Draw grid lines
            using (var gridPen = new Pen(Color.LightGray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = offsetY + (height * i / 4);
                    g.DrawLine(gridPen, offsetX, y, offsetX + width, y);
                }
            }

            // Draw data line
            float[] dataArray = data.ToArray();
            if (dataArray.Length < 2) return;

            using (var pen = new Pen(color, 2f))
            {
                for (int i = 0; i < dataArray.Length - 1; i++)
                {
                    float x1 = offsetX + (float)(width - 1) * i / (dataArray.Length - 1);
                    float y1 = offsetY + height - (dataArray[i] / 100f * height);

                    float x2 = offsetX + (float)(width - 1) * (i + 1) / (dataArray.Length - 1);
                    float y2 = offsetY + height - (dataArray[i + 1] / 100f * height);

                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }

            // Draw label and current value
            if (dataArray.Length > 0)
            {
                float currentValue = dataArray[dataArray.Length - 1];
                string text = $"CPU: {currentValue:F1}%";

                using (var font = new Font("Arial", 9, FontStyle.Bold))
                using (var brush = new SolidBrush(color))
                {
                    g.DrawString(text, font, brush, offsetX + 5, offsetY + 3);
                }
            }
        }

        private void DrawRamGraph(Graphics g, Queue<float> data, int offsetX, int offsetY, int width, int height, Color color)
        {
            if (data.Count < 2)
                return;

            // Draw background
            using (var brush = new SolidBrush(SystemColors.Window))
            {
                g.FillRectangle(brush, offsetX, offsetY, width, height);
            }

            // Draw border
            using (var pen = new Pen(Color.Gray))
            {
                g.DrawRectangle(pen, offsetX, offsetY, width - 1, height - 1);
            }

            // Draw grid lines
            using (var gridPen = new Pen(Color.LightGray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = offsetY + (height * i / 4);
                    g.DrawLine(gridPen, offsetX, y, offsetX + width, y);
                }
            }

            // Draw data line
            float[] dataArray = data.ToArray();
            if (dataArray.Length < 2) return;

            using (var pen = new Pen(color, 2f))
            {
                for (int i = 0; i < dataArray.Length - 1; i++)
                {
                    float x1 = offsetX + (float)(width - 1) * i / (dataArray.Length - 1);
                    float y1 = offsetY + height - (dataArray[i] / 100f * height);

                    float x2 = offsetX + (float)(width - 1) * (i + 1) / (dataArray.Length - 1);
                    float y2 = offsetY + height - (dataArray[i + 1] / 100f * height);

                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }

            // Draw label and current value with RAM info
            if (dataArray.Length > 0)
            {
                float currentValue = dataArray[dataArray.Length - 1];
                string text = $"RAM: {currentValue:F1}% ({currentRamUsedMB:F0}MB/{totalRamMB}MB)";

                using (var font = new Font("Arial", 9, FontStyle.Bold))
                using (var brush = new SolidBrush(color))
                {
                    g.DrawString(text, font, brush, offsetX + 5, offsetY + 3);
                }
            }
        }

        private void DrawNetworkGraph(Graphics g, Queue<float> downloadData, Queue<float> uploadData, int offsetX, int offsetY, int width, int height)
        {
            if (downloadData.Count < 2 || uploadData.Count < 2)
                return;

            // Draw background
            using (var brush = new SolidBrush(SystemColors.Window))
            {
                g.FillRectangle(brush, offsetX, offsetY, width, height);
            }

            // Draw border
            using (var pen = new Pen(Color.Gray))
            {
                g.DrawRectangle(pen, offsetX, offsetY, width - 1, height - 1);
            }

            // Draw grid lines
            using (var gridPen = new Pen(Color.LightGray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = offsetY + (height * i / 4);
                    g.DrawLine(gridPen, offsetX, y, offsetX + width, y);
                }
            }

            // Draw download line
            float[] downloadArray = downloadData.ToArray();
            if (downloadArray.Length >= 2)
            {
                Color downloadColor = Color.FromArgb(0, 150, 255);
                using (var pen = new Pen(downloadColor, 2f))
                {
                    for (int i = 0; i < downloadArray.Length - 1; i++)
                    {
                        float x1 = offsetX + (float)(width - 1) * i / (downloadArray.Length - 1);
                        float y1 = offsetY + height - (downloadArray[i] / 100f * height);

                        float x2 = offsetX + (float)(width - 1) * (i + 1) / (downloadArray.Length - 1);
                        float y2 = offsetY + height - (downloadArray[i + 1] / 100f * height);

                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
            }

            // Draw upload line
            float[] uploadArray = uploadData.ToArray();
            if (uploadArray.Length >= 2)
            {
                Color uploadColor = Color.FromArgb(255, 100, 0);
                using (var pen = new Pen(uploadColor, 2f))
                {
                    for (int i = 0; i < uploadArray.Length - 1; i++)
                    {
                        float x1 = offsetX + (float)(width - 1) * i / (uploadArray.Length - 1);
                        float y1 = offsetY + height - (uploadArray[i] / 100f * height);

                        float x2 = offsetX + (float)(width - 1) * (i + 1) / (uploadArray.Length - 1);
                        float y2 = offsetY + height - (uploadArray[i + 1] / 100f * height);

                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
            }

            // Draw label and current values
            string text = $"Network - ⬇ {currentDownloadMbps:F2} Mbps | ⬆ {currentUploadMbps:F2} Mbps";

            using (var font = new Font("Arial", 9, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.Black))
            {
                g.DrawString(text, font, brush, offsetX + 5, offsetY + 3);
            }
        }

        private void DrawDiskGraph(Graphics g, Queue<float> usageData, Queue<float> readData, Queue<float> writeData, int offsetX, int offsetY, int width, int height)
        {
            if (usageData.Count < 2 || readData.Count < 2 || writeData.Count < 2)
                return;

            // Draw background
            using (var brush = new SolidBrush(SystemColors.Window))
            {
                g.FillRectangle(brush, offsetX, offsetY, width, height);
            }

            // Draw border
            using (var pen = new Pen(Color.Gray))
            {
                g.DrawRectangle(pen, offsetX, offsetY, width - 1, height - 1);
            }

            // Draw grid lines
            using (var gridPen = new Pen(Color.LightGray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = offsetY + (height * i / 4);
                    g.DrawLine(gridPen, offsetX, y, offsetX + width, y);
                }
            }

            // Draw usage line (disk usage %)
            float[] usageArray = usageData.ToArray();
            if (usageArray.Length >= 2)
            {
                Color usageColor = Color.FromArgb(128, 0, 128);
                using (var pen = new Pen(usageColor, 2f))
                {
                    for (int i = 0; i < usageArray.Length - 1; i++)
                    {
                        float x1 = offsetX + (float)(width - 1) * i / (usageArray.Length - 1);
                        float y1 = offsetY + height - (usageArray[i] / 100f * height);

                        float x2 = offsetX + (float)(width - 1) * (i + 1) / (usageArray.Length - 1);
                        float y2 = offsetY + height - (usageArray[i + 1] / 100f * height);

                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
            }

            // Draw read line
            float[] readArray = readData.ToArray();
            if (readArray.Length >= 2)
            {
                Color readColor = Color.FromArgb(0, 128, 0);
                using (var pen = new Pen(readColor, 2f))
                {
                    for (int i = 0; i < readArray.Length - 1; i++)
                    {
                        float x1 = offsetX + (float)(width - 1) * i / (readArray.Length - 1);
                        float y1 = offsetY + height - (readArray[i] / 100f * height);

                        float x2 = offsetX + (float)(width - 1) * (i + 1) / (readArray.Length - 1);
                        float y2 = offsetY + height - (readArray[i + 1] / 100f * height);

                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
            }

            // Draw write line
            float[] writeArray = writeData.ToArray();
            if (writeArray.Length >= 2)
            {
                Color writeColor = Color.FromArgb(200, 100, 0);
                using (var pen = new Pen(writeColor, 2f))
                {
                    for (int i = 0; i < writeArray.Length - 1; i++)
                    {
                        float x1 = offsetX + (float)(width - 1) * i / (writeArray.Length - 1);
                        float y1 = offsetY + height - (writeArray[i] / 100f * height);

                        float x2 = offsetX + (float)(width - 1) * (i + 1) / (writeArray.Length - 1);
                        float y2 = offsetY + height - (writeArray[i + 1] / 100f * height);

                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
            }

            // Draw label and current values
            string text = $"Disk - 💾 {currentDiskUsagePercent:F1}% | ⬇ {currentDiskReadMBps:F2} MB/s | ⬆ {currentDiskWriteMBps:F2} MB/s";

            using (var font = new Font("Arial", 9, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.Black))
            {
                g.DrawString(text, font, brush, offsetX + 5, offsetY + 3);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                updateTimer?.Stop();
                updateTimer?.Dispose();
                cpuCounter?.Dispose();
                ramCounter?.Dispose();
                ramAvailableCounter?.Dispose();
                diskCounter?.Dispose();
                diskReadCounter?.Dispose();
                diskWriteCounter?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
