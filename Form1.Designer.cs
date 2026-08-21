using System;
using System.Drawing;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            newToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            toolsToolStripMenuItem = new ToolStripMenuItem();
            openServerFolderToolStripMenuItem = new ToolStripMenuItem();
            openPluginsFolderToolStripMenuItem = new ToolStripMenuItem();
            cleanLogsFolderToolStripMenuItem = new ToolStripMenuItem();
            openRouterSettingsToolStripMenuItem = new ToolStripMenuItem();
            serverPropertiesToolStripMenuItem = new ToolStripMenuItem();
            editRamToolStripMenuItem = new ToolStripMenuItem();
            backupWorldToolStripMenuItem = new ToolStripMenuItem();
            backupServerToolStripMenuItem = new ToolStripMenuItem();
            postShutdownActionsToolStripMenuItem = new ToolStripMenuItem();
            killServerToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            statusBarToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            lblServersTitle = new Label();
            listBoxServers = new ListBox();
            label1 = new Label();
            btnStartServer = new Button();
            groupBoxInfo = new GroupBox();
            lblStatusTitle = new Label();
            lblStatusValue = new Label();
            labelVersionTitle = new Label();
            lblVersionValue = new Label();
            labelIPTitle = new Label();
            lblIPValue = new Label();
            labelPortTitle = new Label();
            lblPortValue = new Label();
            labelPlayersTitle = new Label();
            listBoxPlayers = new ListBox();
            label2 = new Label();
            btnDeleteServer = new Button();
            btnEditProperties = new Button();
            menuStrip1.SuspendLayout();
            groupBoxInfo.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, toolsToolStripMenuItem, editToolStripMenuItem, viewToolStripMenuItem, helpToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1088, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { newToolStripMenuItem, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // newToolStripMenuItem
            // 
            newToolStripMenuItem.Name = "newToolStripMenuItem";
            newToolStripMenuItem.Size = new Size(132, 22);
            newToolStripMenuItem.Text = "New server";
            newToolStripMenuItem.Click += newToolStripMenuItem_Click;
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(132, 22);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openServerFolderToolStripMenuItem, openPluginsFolderToolStripMenuItem, cleanLogsFolderToolStripMenuItem, openRouterSettingsToolStripMenuItem, serverPropertiesToolStripMenuItem, editRamToolStripMenuItem, backupWorldToolStripMenuItem, backupServerToolStripMenuItem, postShutdownActionsToolStripMenuItem, killServerToolStripMenuItem });
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            toolsToolStripMenuItem.Size = new Size(47, 20);
            toolsToolStripMenuItem.Text = "Tools";
            // 
            // openServerFolderToolStripMenuItem
            // 
            openServerFolderToolStripMenuItem.Name = "openServerFolderToolStripMenuItem";
            openServerFolderToolStripMenuItem.Size = new Size(186, 22);
            openServerFolderToolStripMenuItem.Text = "Open Server Folder";
            openServerFolderToolStripMenuItem.Click += openServerFolderToolStripMenuItem_Click;
            // 
            // openPluginsFolderToolStripMenuItem
            // 
            openPluginsFolderToolStripMenuItem.Name = "openPluginsFolderToolStripMenuItem";
            openPluginsFolderToolStripMenuItem.Size = new Size(186, 22);
            openPluginsFolderToolStripMenuItem.Text = "Open Plugins Folder";
            openPluginsFolderToolStripMenuItem.Click += openPluginsFolderToolStripMenuItem_Click;
            // 
            // cleanLogsFolderToolStripMenuItem
            // 
            cleanLogsFolderToolStripMenuItem.Name = "cleanLogsFolderToolStripMenuItem";
            cleanLogsFolderToolStripMenuItem.Size = new Size(186, 22);
            cleanLogsFolderToolStripMenuItem.Text = "Clean Logs Folder";
            cleanLogsFolderToolStripMenuItem.Click += cleanLogsFolderToolStripMenuItem_Click;
            // 
            // openRouterSettingsToolStripMenuItem
            // 
            openRouterSettingsToolStripMenuItem.Name = "openRouterSettingsToolStripMenuItem";
            openRouterSettingsToolStripMenuItem.Size = new Size(186, 22);
            openRouterSettingsToolStripMenuItem.Text = "Open Router Settings";
            openRouterSettingsToolStripMenuItem.Click += openRouterSettingsToolStripMenuItem_Click;
            // 
            // serverPropertiesToolStripMenuItem
            // 
            serverPropertiesToolStripMenuItem.Name = "serverPropertiesToolStripMenuItem";
            serverPropertiesToolStripMenuItem.Size = new Size(186, 22);
            serverPropertiesToolStripMenuItem.Text = "Server Properties";
            serverPropertiesToolStripMenuItem.Click += serverPropertiesToolStripMenuItem_Click;
            // 
            // editRamToolStripMenuItem
            // 
            editRamToolStripMenuItem.Name = "editRamToolStripMenuItem";
            editRamToolStripMenuItem.Size = new Size(186, 22);
            editRamToolStripMenuItem.Text = "RAM Usage Settings";
            editRamToolStripMenuItem.Click += serverEditRamToolStripMenuItem_Click;
            // 
            // backupWorldToolStripMenuItem
            // 
            backupWorldToolStripMenuItem.Name = "backupWorldToolStripMenuItem";
            backupWorldToolStripMenuItem.Size = new Size(186, 22);
            backupWorldToolStripMenuItem.Text = "Backup world";
            backupWorldToolStripMenuItem.Click += backupWorldToolStripMenuItem_Click;
            // 
            // backupServerToolStripMenuItem
            // 
            backupServerToolStripMenuItem.Name = "backupServerToolStripMenuItem";
            backupServerToolStripMenuItem.Size = new Size(186, 22);
            backupServerToolStripMenuItem.Text = "Backup server";
            backupServerToolStripMenuItem.Click += backupServerToolStripMenuItem_Click;
            // 
            // postShutdownActionsToolStripMenuItem
            // 
            postShutdownActionsToolStripMenuItem.Name = "postShutdownActionsToolStripMenuItem";
            postShutdownActionsToolStripMenuItem.Size = new Size(186, 22);
            postShutdownActionsToolStripMenuItem.Text = "Post-Shutdown Actions";
            postShutdownActionsToolStripMenuItem.Click += postShutdownActionsToolStripMenuItem_Click;
            // 
            // killServerToolStripMenuItem
            // 
            killServerToolStripMenuItem.Name = "killServerToolStripMenuItem";
            killServerToolStripMenuItem.Size = new Size(186, 22);
            killServerToolStripMenuItem.Text = "Kill Server Process";
            killServerToolStripMenuItem.Click += killServerToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(39, 20);
            editToolStripMenuItem.Text = "Edit";
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { statusBarToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(44, 20);
            viewToolStripMenuItem.Text = "View";
            // 
            // statusBarToolStripMenuItem
            // 
            statusBarToolStripMenuItem.Checked = true;
            statusBarToolStripMenuItem.CheckOnClick = true;
            statusBarToolStripMenuItem.CheckState = CheckState.Checked;
            statusBarToolStripMenuItem.Name = "statusBarToolStripMenuItem";
            statusBarToolStripMenuItem.Size = new Size(180, 22);
            statusBarToolStripMenuItem.Text = "Status Bar";
            statusBarToolStripMenuItem.Click += statusBarToolStripMenuItem_Click;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(44, 20);
            helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(180, 22);
            aboutToolStripMenuItem.Text = "About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // lblServersTitle
            // 
            lblServersTitle.AutoSize = true;
            lblServersTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblServersTitle.Location = new Point(12, 28);
            lblServersTitle.Name = "lblServersTitle";
            lblServersTitle.Size = new Size(50, 15);
            lblServersTitle.TabIndex = 0;
            lblServersTitle.Text = "Servers";
            // 
            // listBoxServers
            // 
            listBoxServers.FormattingEnabled = true;
            listBoxServers.Location = new Point(12, 48);
            listBoxServers.Name = "listBoxServers";
            listBoxServers.Size = new Size(200, 409);
            listBoxServers.TabIndex = 1;
            listBoxServers.SelectedIndexChanged += listBoxServers_SelectedIndexChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            label1.Location = new Point(228, 36);
            label1.Name = "label1";
            label1.Size = new Size(255, 25);
            label1.TabIndex = 2;
            label1.Text = "Select a server from the left";
            label1.Click += label1_Click;
            // 
            // btnStartServer
            // 
            btnStartServer.Location = new Point(665, 37);
            btnStartServer.Name = "btnStartServer";
            btnStartServer.Size = new Size(90, 30);
            btnStartServer.TabIndex = 3;
            btnStartServer.Text = "Start";
            btnStartServer.UseVisualStyleBackColor = true;
            btnStartServer.Click += btnStartServer_Click;
            // 
            // groupBoxInfo
            // 
            groupBoxInfo.Controls.Add(lblStatusTitle);
            groupBoxInfo.Controls.Add(lblStatusValue);
            groupBoxInfo.Controls.Add(labelVersionTitle);
            groupBoxInfo.Controls.Add(lblVersionValue);
            groupBoxInfo.Controls.Add(labelIPTitle);
            groupBoxInfo.Controls.Add(lblIPValue);
            groupBoxInfo.Controls.Add(labelPortTitle);
            groupBoxInfo.Controls.Add(lblPortValue);
            groupBoxInfo.Controls.Add(labelPlayersTitle);
            groupBoxInfo.Controls.Add(listBoxPlayers);
            groupBoxInfo.Location = new Point(228, 90);
            groupBoxInfo.Name = "groupBoxInfo";
            groupBoxInfo.Size = new Size(840, 420);
            groupBoxInfo.TabIndex = 6;
            groupBoxInfo.TabStop = false;
            groupBoxInfo.Text = "Server Info";
            // 
            // lblStatusTitle
            // 
            lblStatusTitle.AutoSize = true;
            lblStatusTitle.Location = new Point(12, 22);
            lblStatusTitle.Name = "lblStatusTitle";
            lblStatusTitle.Size = new Size(42, 15);
            lblStatusTitle.TabIndex = 0;
            lblStatusTitle.Text = "Status:";
            // 
            // lblStatusValue
            // 
            lblStatusValue.AutoSize = true;
            lblStatusValue.Location = new Point(82, 22);
            lblStatusValue.Name = "lblStatusValue";
            lblStatusValue.Size = new Size(51, 15);
            lblStatusValue.TabIndex = 1;
            lblStatusValue.Text = "Stopped";
            // 
            // labelVersionTitle
            // 
            labelVersionTitle.AutoSize = true;
            labelVersionTitle.Location = new Point(12, 48);
            labelVersionTitle.Name = "labelVersionTitle";
            labelVersionTitle.Size = new Size(48, 15);
            labelVersionTitle.TabIndex = 2;
            labelVersionTitle.Text = "Version:";
            // 
            // lblVersionValue
            // 
            lblVersionValue.AutoSize = true;
            lblVersionValue.Location = new Point(82, 48);
            lblVersionValue.Name = "lblVersionValue";
            lblVersionValue.Size = new Size(29, 15);
            lblVersionValue.TabIndex = 3;
            lblVersionValue.Text = "N/A";
            // 
            // labelIPTitle
            // 
            labelIPTitle.AutoSize = true;
            labelIPTitle.Location = new Point(12, 81);
            labelIPTitle.Name = "labelIPTitle";
            labelIPTitle.Size = new Size(20, 15);
            labelIPTitle.TabIndex = 4;
            labelIPTitle.Text = "IP:";
            // 
            // lblIPValue
            // 
            lblIPValue.AutoSize = true;
            lblIPValue.Location = new Point(82, 81);
            lblIPValue.Name = "lblIPValue";
            lblIPValue.Size = new Size(29, 15);
            lblIPValue.TabIndex = 5;
            lblIPValue.Text = "N/A";
            // 
            // labelPortTitle
            // 
            labelPortTitle.AutoSize = true;
            labelPortTitle.Location = new Point(12, 114);
            labelPortTitle.Name = "labelPortTitle";
            labelPortTitle.Size = new Size(32, 15);
            labelPortTitle.TabIndex = 6;
            labelPortTitle.Text = "Port:";
            // 
            // lblPortValue
            // 
            lblPortValue.AutoSize = true;
            lblPortValue.Location = new Point(82, 114);
            lblPortValue.Name = "lblPortValue";
            lblPortValue.Size = new Size(37, 15);
            lblPortValue.TabIndex = 5;
            lblPortValue.Text = "25565";
            // 
            // labelPlayersTitle
            // 
            labelPlayersTitle.AutoSize = true;
            labelPlayersTitle.Location = new Point(12, 147);
            labelPlayersTitle.Name = "labelPlayersTitle";
            labelPlayersTitle.Size = new Size(47, 15);
            labelPlayersTitle.TabIndex = 6;
            labelPlayersTitle.Text = "Players:";
            labelPlayersTitle.Visible = false;
            // 
            // listBoxPlayers
            // 
            listBoxPlayers.FormattingEnabled = true;
            listBoxPlayers.Location = new Point(12, 165);
            listBoxPlayers.Name = "listBoxPlayers";
            listBoxPlayers.Size = new Size(200, 139);
            listBoxPlayers.TabIndex = 7;
            listBoxPlayers.Visible = false;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(227, 516);
            label2.Name = "label2";
            label2.Size = new Size(416, 15);
            label2.TabIndex = 7;
            label2.Text = "InDev version - may contiains bugs, errors and unfinished or missing features.\r\n";
            label2.Visible = false;
            label2.Click += label2_Click;
            // 
            // btnDeleteServer
            // 
            btnDeleteServer.Location = new Point(761, 37);
            btnDeleteServer.Name = "btnDeleteServer";
            btnDeleteServer.Size = new Size(90, 30);
            btnDeleteServer.TabIndex = 7;
            btnDeleteServer.Text = "Delete";
            btnDeleteServer.UseVisualStyleBackColor = true;
            btnDeleteServer.Click += btnDeleteServer_Click;
            // 
            // btnEditProperties
            // 
            btnEditProperties.Location = new Point(857, 36);
            btnEditProperties.Name = "btnEditProperties";
            btnEditProperties.Size = new Size(110, 30);
            btnEditProperties.TabIndex = 8;
            btnEditProperties.Text = "Edit Properties";
            btnEditProperties.UseVisualStyleBackColor = true;
            btnEditProperties.Click += btnEditProperties_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1088, 610);
            Controls.Add(label2);
            Controls.Add(menuStrip1);
            Controls.Add(lblServersTitle);
            Controls.Add(listBoxServers);
            Controls.Add(label1);
            Controls.Add(btnStartServer);
            Controls.Add(btnDeleteServer);
            Controls.Add(btnEditProperties);
            Controls.Add(groupBoxInfo);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            Text = "Minecraft Server Manager 3";
            Load += Form1_Load;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            groupBoxInfo.ResumeLayout(false);
            groupBoxInfo.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private Label lblServersTitle;
        private ListBox listBoxServers;
        private Button btnStartServer;

        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem newToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem statusBarToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem serverPropertiesToolStripMenuItem;
        private ToolStripMenuItem editRamToolStripMenuItem;

        private GroupBox groupBoxInfo;
        private Label lblStatusTitle;
        private Label lblStatusValue;
        private Label labelVersionTitle;
        private Label lblVersionValue;
        private Label labelIPTitle;
        private Label lblIPValue;
        private Label labelPortTitle;
        private Label lblPortValue;
        private Label labelPlayersTitle;
        private ListBox listBoxPlayers;
        private Label label2;
        private Button btnDeleteServer;
        private Button btnEditProperties;
        private ToolStripMenuItem toolsToolStripMenuItem;
        private ToolStripMenuItem openServerFolderToolStripMenuItem;
        private ToolStripMenuItem openPluginsFolderToolStripMenuItem;
        private ToolStripMenuItem cleanLogsFolderToolStripMenuItem;
        private ToolStripMenuItem openRouterSettingsToolStripMenuItem;
        private ToolStripMenuItem backupWorldToolStripMenuItem;
        private ToolStripMenuItem backupServerToolStripMenuItem;
        private ToolStripMenuItem postShutdownActionsToolStripMenuItem;
        private ToolStripMenuItem killServerToolStripMenuItem;
    }
}
