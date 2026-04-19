using System;
using System.Threading;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    // simple progress dialog built in code (no designer)
    public class ProgressDialog : Form
    {
        private ProgressBar progressBar;
        private Label lblPercent;
        private Label lblMessage;
        private Button btnCancel;
        private CancellationTokenSource cts;

        public ProgressDialog(CancellationTokenSource cancellationTokenSource, string initialMessage = "Downloading...")
        {
            cts = cancellationTokenSource;
            Initialize(initialMessage);
        }

        private void Initialize(string initialMessage)
        {
            Text = "Downloading...";
            Width = 420;
            Height = 140;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            lblMessage = new Label()
            {
                Left = 12,
                Top = 10,
                Width = 380,
                Height = 20,
                Text = initialMessage
            };

            progressBar = new ProgressBar()
            {
                Style = ProgressBarStyle.Continuous,
                Width = 340,
                Height = 20,
                Left = 12,
                Top = 36,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            lblPercent = new Label()
            {
                Left = 12,
                Top = 64,
                Width = 200,
                Text = "0 %"
            };

            btnCancel = new Button()
            {
                Text = "Cancel",
                Left = 280,
                Top = 60,
                Width = 75,
                Height = 26
            };
            btnCancel.Click += (s, e) =>
            {
                btnCancel.Enabled = false;
                try { cts?.Cancel(); } catch { }
            };

            Controls.Add(lblMessage);
            Controls.Add(progressBar);
            Controls.Add(lblPercent);
            Controls.Add(btnCancel);
        }

        // Called from any thread via Progress<T> or direct calls
        public void SetProgress(int percent)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<int>(SetProgress), percent); } catch { }
                return;
            }

            if (percent < progressBar.Minimum) percent = progressBar.Minimum;
            if (percent > progressBar.Maximum) percent = progressBar.Maximum;
            progressBar.Value = percent;
            lblPercent.Text = $"{percent} %";
        }

        // Update the descriptive message
        public void SetMessage(string message)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(SetMessage), message); } catch { }
                return;
            }

            lblMessage.Text = message ?? string.Empty;
        }
    }
}