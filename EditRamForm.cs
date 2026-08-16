using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class EditRamForm : Form
    {
        private NumericUpDown numRam;
        private ComboBox cmbRamPresets;
        private Label lblTotalRam;
        private Label lblRamInfo;
        private Button btnOk;
        private Button btnCancel;

        public int SelectedRamMB { get; private set; }

        public EditRamForm(int currentRamMB)
        {
            Text = "Edit RAM";
            Width = 360;
            Height = 200;
            StartPosition = FormStartPosition.CenterParent;

            // Create controls with sensible base sizes; positions adjusted after presets are populated.
            numRam = new NumericUpDown { Left = 10, Top = 34, Width = 100 };
            cmbRamPresets = new ComboBox { Left = 120, Top = 30, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            lblTotalRam = new Label { Left = 10, Top = 72, Width = 320 };
            lblRamInfo = new Label { Left = 10, Top = 94, Width = 320 };

            btnOk = new Button { Text = "OK", Left = 170, Top = 130, Width = 80, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Left = 260, Top = 130, Width = 80, DialogResult = DialogResult.Cancel };

            Controls.Add(new Label { Text = "RAM (MB):", Left = 10, Top = 10, Width = 200 });
            Controls.Add(numRam);
            Controls.Add(cmbRamPresets);
            Controls.Add(lblTotalRam);
            Controls.Add(lblRamInfo);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            btnOk.Click += BtnOk_Click;
            cmbRamPresets.SelectedIndexChanged += CmbRamPresets_SelectedIndexChanged;

            // Populate presets and limits first, then adjust layout to avoid overlap.
            UpdateRamLimitsAndPresets();

            // adjust control sizes and ensure combo is placed right of numeric control with a larger gap
            numRam.Width = 72;
            cmbRamPresets.Width = 180;
            // move combo further to the right to avoid overlap with its dropdown button
            cmbRamPresets.Left = numRam.Left + numRam.Width + 24;
            // align vertically with numeric control
            cmbRamPresets.Top = numRam.Top;

            // set initial value
            numRam.Value = Math.Min(numRam.Maximum, Math.Max(numRam.Minimum, currentRamMB));
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            SelectedRamMB = (int)numRam.Value;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CmbRamPresets_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (int.TryParse(cmbRamPresets.SelectedItem?.ToString(), out var val))
            {
                if (val < numRam.Minimum) val = (int)numRam.Minimum;
                if (val > numRam.Maximum) val = (int)numRam.Maximum;
                numRam.Value = val;
            }
        }

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

        private static int GetTotalPhysicalMemoryMB()
        {
            try
            {
                var mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>();
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

            numRam.Minimum = 128;
            numRam.Maximum = maxAssignable;

            lblTotalRam.Text = $"System RAM: {totalMB} MB ({totalMB / 1024.0:F1} GB)";
            lblRamInfo.Text = $"Max assignable: {maxAssignable} MB.";

            cmbRamPresets.Items.Clear();
            int[] presets = new[] { 512, 1024, 2048, 3072, 4096, 6144, 8192, 12288, 16384 };
            foreach (var p in presets)
                if (p <= maxAssignable) cmbRamPresets.Items.Add(p.ToString());
            if (cmbRamPresets.Items.Count == 0) cmbRamPresets.Items.Add(numRam.Minimum.ToString());

            var defaultPresetIndex = cmbRamPresets.Items.Cast<string>().ToList().FindIndex(x => x == "2048");
            cmbRamPresets.SelectedIndex = defaultPresetIndex >= 0 ? defaultPresetIndex : 0;

            if (int.TryParse(cmbRamPresets.SelectedItem?.ToString(), out var val))
                numRam.Value = Math.Min((decimal)numRam.Maximum, Math.Max((decimal)numRam.Minimum, val));
        }
    }
}
