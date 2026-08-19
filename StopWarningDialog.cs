using System;
using System.Windows.Forms;

namespace MC_Server_Manager_3
{
    public partial class StopWarningDialog : Form
    {
        public bool NeverShowAgain { get; private set; }

        public StopWarningDialog()
        {
            InitializeComponent();
            NeverShowAgain = false;
        }

        private void neverShowCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            NeverShowAgain = neverShowCheckBox.Checked;
        }

        private void StopWarningDialog_Load(object sender, EventArgs e)
        {

        }

        private void messageLabel_Click(object sender, EventArgs e)
        {

        }

        private void StopWarningDialog_Load_1(object sender, EventArgs e)
        {

        }
    }
}
