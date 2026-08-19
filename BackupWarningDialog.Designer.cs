using System;
using System.Drawing;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    partial class BackupWarningDialog
    {
        private TableLayoutPanel mainPanel;
        private Label titleLabel;
        private Label messageLabel;
        private CheckBox neverShowCheckBox;
        private FlowLayoutPanel buttonPanel;
        private Button okBtn;

        private void InitializeComponent()
        {
            this.mainPanel = new TableLayoutPanel();
            this.titleLabel = new Label();
            this.messageLabel = new Label();
            this.neverShowCheckBox = new CheckBox();
            this.buttonPanel = new FlowLayoutPanel();
            this.okBtn = new Button();
            this.SuspendLayout();
            // 
            // Form
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(530, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Backup Information";
            this.ShowIcon = true;
            // 
            // mainPanel
            // 
            this.mainPanel.Dock = DockStyle.Fill;
            this.mainPanel.Padding = new Padding(15);
            this.mainPanel.RowCount = 4;
            this.mainPanel.ColumnCount = 1;
            this.mainPanel.AutoSize = false;
            this.mainPanel.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            this.mainPanel.RowStyles.Clear();
            this.mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // title
            this.mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // message (fills)
            this.mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // checkbox
            this.mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // buttons
            // 
            // titleLabel
            // 
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new Font(this.Font.FontFamily, 14F, FontStyle.Bold);
            this.titleLabel.Text = "ℹ Backup Information";
            this.titleLabel.Dock = DockStyle.Top;
            this.titleLabel.Margin = new Padding(0, 0, 0, 10);
            // 
            // messageLabel
            // 
            this.messageLabel.AutoSize = true;
            this.messageLabel.Text = "Before backing up your world or server:\r\n\r\n" +
                                     "• Use the server console to run the 'save-all' command to save all data\r\n" +
                                     "• Or gracefully stop the server using the 'stop' command\r\n" +
                                     "• Wait for the server to complete saving\r\n\r\n" +
                                     "This ensures no data loss and creates a clean backup of your files.";
            this.messageLabel.Dock = DockStyle.Fill;
            this.messageLabel.Margin = new Padding(0, 10, 0, 15);
            this.messageLabel.MaximumSize = new Size(470, 0);
            // 
            // neverShowCheckBox
            // 
            this.neverShowCheckBox.Text = "Never show this message again";
            this.neverShowCheckBox.AutoSize = true;
            this.neverShowCheckBox.Dock = DockStyle.Left;
            this.neverShowCheckBox.Margin = new Padding(0, 10, 0, 15);
            this.neverShowCheckBox.CheckedChanged += new EventHandler(this.neverShowCheckBox_CheckedChanged);
            // 
            // buttonPanel
            // 
            this.buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            this.buttonPanel.Dock = DockStyle.Fill;
            this.buttonPanel.Padding = new Padding(0);
            this.buttonPanel.AutoSize = true;
            this.buttonPanel.WrapContents = false;
            // 
            // okBtn
            // 
            this.okBtn.Text = "OK";
            this.okBtn.Size = new Size(90, 32);
            this.okBtn.Margin = new Padding(0, 10, 0, 10);
            this.okBtn.DialogResult = DialogResult.OK;
            // 
            // assemble
            // 
            this.buttonPanel.Controls.Add(this.okBtn);
            this.mainPanel.Controls.Add(this.titleLabel, 0, 0);
            this.mainPanel.Controls.Add(this.messageLabel, 0, 1);
            this.mainPanel.Controls.Add(this.neverShowCheckBox, 0, 2);
            this.mainPanel.Controls.Add(this.buttonPanel, 0, 3);
            this.Controls.Add(this.mainPanel);
            this.AcceptButton = this.okBtn;
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
