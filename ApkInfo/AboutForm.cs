using System.Drawing;
using System.Windows.Forms;

namespace ApkInfo
{
    public partial class AboutForm : Form
    {
        public static bool ShowAbout(IWin32Window owner)
        {
            using (var dlg = new AboutForm())
            {
                return dlg.ShowDialog(owner) == DialogResult.OK;
            }
        }

        public AboutForm()
        {
            InitializeComponent();
            lblTitle.Text += " v" + Application.ProductVersion;
        }

        private void AboutForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                this.DialogResult = DialogResult.Cancel;
            }
        }
    }
}
