using System;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public partial class BackupWarningDialog : Form
    {
        public bool NeverShowAgain { get; private set; }

        public BackupWarningDialog()
        {
            InitializeComponent();
            NeverShowAgain = false;
        }

        private void neverShowCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            NeverShowAgain = neverShowCheckBox.Checked;
        }
    }
}
