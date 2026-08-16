using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public class ServerPropertiesForm : Form
    {
        private readonly string filePath;
        private DataGridView dgv;
        private Button btnSave;
        private Button btnCancel;
        private Button btnAdd;
        private Button btnRemove;

        public ServerPropertiesForm(string path)
        {
            filePath = path ?? throw new ArgumentNullException(nameof(path));
            Text = "Server Properties";
            Width = 600;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;

            InitializeComponents();
            LoadProperties();
        }

        private void InitializeComponents()
        {
            dgv = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 380,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = "Key" });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Value" });

            btnAdd = new Button { Text = "Add", Width = 80, Left = 10, Top = 390 };
            btnRemove = new Button { Text = "Remove", Width = 80, Left = 100, Top = 390 };
            btnSave = new Button { Text = "Save", Width = 80, Left = 400, Top = 390, DialogResult = DialogResult.OK };
            btnCancel = new Button { Text = "Cancel", Width = 80, Left = 490, Top = 390, DialogResult = DialogResult.Cancel };

            btnAdd.Click += (s, e) => dgv.Rows.Add("", "");
            btnRemove.Click += (s, e) => { if (dgv.SelectedRows.Count > 0) dgv.Rows.RemoveAt(dgv.SelectedRows[0].Index); };
            btnSave.Click += BtnSave_Click;

            Controls.Add(dgv);
            Controls.Add(btnAdd);
            Controls.Add(btnRemove);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
        }

        private void LoadProperties()
        {
            dgv.Rows.Clear();
            if (!File.Exists(filePath)) return;
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.TrimStart().StartsWith("#")) continue; // skip comments
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line.Substring(0, idx).Trim();
                var val = line.Substring(idx + 1).Trim();
                dgv.Rows.Add(key, val);
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                var lines = new List<string>();
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.IsNewRow) continue;
                    var key = Convert.ToString(row.Cells[0].Value)?.Trim();
                    var val = Convert.ToString(row.Cells[1].Value)?.Trim();
                    if (string.IsNullOrEmpty(key)) continue;
                    lines.Add($"{key}={val}");
                }

                File.WriteAllLines(filePath, lines);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save properties: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
