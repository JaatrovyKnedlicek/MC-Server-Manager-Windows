using System;
using System.Drawing;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    partial class StopWarningDialog
    {
        private TableLayoutPanel mainPanel;
        private Label titleLabel;
        private Label messageLabel;
        private CheckBox neverShowCheckBox;
        private FlowLayoutPanel buttonPanel;
        private Button okBtn;

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(StopWarningDialog));
            mainPanel = new TableLayoutPanel();
            titleLabel = new Label();
            messageLabel = new Label();
            neverShowCheckBox = new CheckBox();
            buttonPanel = new FlowLayoutPanel();
            okBtn = new Button();
            mainPanel.SuspendLayout();
            buttonPanel.SuspendLayout();
            SuspendLayout();
            // 
            // mainPanel
            // 
            mainPanel.ColumnCount = 1;
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            mainPanel.Controls.Add(titleLabel, 0, 0);
            mainPanel.Controls.Add(messageLabel, 0, 1);
            mainPanel.Controls.Add(neverShowCheckBox, 0, 2);
            mainPanel.Controls.Add(buttonPanel, 0, 3);
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            mainPanel.Location = new Point(0, 0);
            mainPanel.Name = "mainPanel";
            mainPanel.Padding = new Padding(15);
            mainPanel.RowCount = 4;
            mainPanel.RowStyles.Add(new RowStyle());
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainPanel.RowStyles.Add(new RowStyle());
            mainPanel.RowStyles.Add(new RowStyle());
            mainPanel.Size = new Size(530, 350);
            mainPanel.TabIndex = 0;
            // 
            // titleLabel
            // 
            titleLabel.AutoSize = true;
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            titleLabel.Location = new Point(15, 15);
            titleLabel.Margin = new Padding(0, 0, 0, 10);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(500, 25);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "ℹ Server(s) Still Running";
            // 
            // messageLabel
            // 
            messageLabel.AutoSize = true;
            messageLabel.Dock = DockStyle.Fill;
            messageLabel.Location = new Point(15, 60);
            messageLabel.Margin = new Padding(0, 10, 0, 15);
            messageLabel.MaximumSize = new Size(470, 0);
            messageLabel.Name = "messageLabel";
            messageLabel.Size = new Size(470, 158);
            messageLabel.TabIndex = 1;
            messageLabel.Text = resources.GetString("messageLabel.Text");
            messageLabel.Click += messageLabel_Click;
            // 
            // neverShowCheckBox
            // 
            neverShowCheckBox.AutoSize = true;
            neverShowCheckBox.Dock = DockStyle.Left;
            neverShowCheckBox.Location = new Point(15, 243);
            neverShowCheckBox.Margin = new Padding(0, 10, 0, 15);
            neverShowCheckBox.Name = "neverShowCheckBox";
            neverShowCheckBox.Size = new Size(191, 19);
            neverShowCheckBox.TabIndex = 2;
            neverShowCheckBox.Text = "Never show this message again";
            neverShowCheckBox.CheckedChanged += neverShowCheckBox_CheckedChanged;
            // 
            // buttonPanel
            // 
            buttonPanel.AutoSize = true;
            buttonPanel.Controls.Add(okBtn);
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.Location = new Point(18, 280);
            buttonPanel.Name = "buttonPanel";
            buttonPanel.Size = new Size(494, 52);
            buttonPanel.TabIndex = 3;
            buttonPanel.WrapContents = false;
            // 
            // okBtn
            // 
            okBtn.DialogResult = DialogResult.OK;
            okBtn.Location = new Point(404, 10);
            okBtn.Margin = new Padding(0, 10, 0, 10);
            okBtn.Name = "okBtn";
            okBtn.Size = new Size(90, 32);
            okBtn.TabIndex = 0;
            okBtn.Text = "OK";
            // 
            // StopWarningDialog
            // 
            AcceptButton = okBtn;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(530, 350);
            Controls.Add(mainPanel);
            Name = "StopWarningDialog";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Server Information";
            Load += StopWarningDialog_Load_1;
            mainPanel.ResumeLayout(false);
            mainPanel.PerformLayout();
            buttonPanel.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
