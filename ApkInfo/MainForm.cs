using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ApkInfo
{
    public partial class MainForm : Form
    {
        public MainForm(string[] args)
        {
            InitializeComponent();

            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(MainForm_DragEnter);
            this.DragDrop += new DragEventHandler(MainForm_DragDrop);
            this.imgIcon.Image = this.Icon.ToBitmap();
            AddAboutMenu();
            //支持命令行
            if (args.Length != 0 && File.Exists(args[0]))
            {
                DecodeApk(args[0]);
            }
        }

        #region 系统菜单操作API
        private const Int32 WM_SYSCOMMAND = 0x112;
        private const Int32 MF_SEPARATOR = 0x800;
        private const Int32 MF_BYPOSITION = 0x400;
        private const Int32 IDM_ABOUT = 999;
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool InsertMenu(IntPtr hMenu, Int32 wPosition, Int32 wFlags, Int32 wIDNewItem, string lpNewItem);
        #endregion

        private void AddAboutMenu()
        {
            IntPtr sysMenuHandle = GetSystemMenu(this.Handle, false);
            InsertMenu(sysMenuHandle, 5, MF_BYPOSITION | MF_SEPARATOR, 0, string.Empty);
            InsertMenu(sysMenuHandle, 6, MF_BYPOSITION, IDM_ABOUT, "关于...");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SYSCOMMAND)
            {
                switch (m.WParam.ToInt32())
                {
                    case IDM_ABOUT:
                        AboutForm.ShowAbout(this);
                        return;
                    default:
                        break;
                }
            }
            base.WndProc(ref m);
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string path = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
            if (File.Exists(path))
            {
                DecodeApk(path);
            }
        }

        private void DecodeApk(string apkPath)
        {
            if (!File.Exists(apkPath))
                return;

            ApkDecoder apkDecoder = new ApkDecoder(apkPath);
            apkDecoder.InfoParsedEvent += new Action<ApkDecoder>(apkDecoder_InfoParsed);
            apkDecoder.AaptNotFoundEvent += new MethodInvoker(apkDecoder_AaptNotFound);
        }

        private void apkDecoder_InfoParsed(ApkDecoder apkDecoder)
        {
            this.Invoke(new Action<ApkDecoder>(SafeInfoParsed), apkDecoder);
        }

        private void apkDecoder_AaptNotFound()
        {
            this.Invoke(new MethodInvoker(ShowAaptNotFoundInfo));
        }

        private void ShowAaptNotFoundInfo()
        {
            MessageBox.Show(this, "解析apk文件所需要的组件aapt.exe遗失，请下载此程序完整组件然后再试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SafeInfoParsed(ApkDecoder apkDecoder)
        {
            txtApplication.Text = apkDecoder.AppName;
            txtVersion.Text = apkDecoder.AppVersion;
            txtVersionCode.Text = apkDecoder.AppVersionCode;
            txtPackage.Text = apkDecoder.PkgName;
            txtIconPath.Text = apkDecoder.IconPath;
            txtMinSdk.Text = apkDecoder.MinSdk;
            txtMinVersion.Text = apkDecoder.MinVersion;
            txtScreenSize.Text = apkDecoder.ScreenSupport;
            txtScreenSolution.Text = apkDecoder.ScreenSolutions;
            txtPermission.Text = apkDecoder.Permissions;
            txtFeature.Text = apkDecoder.Features;
            imgIcon.Image = (apkDecoder.AppIcon != null) ? apkDecoder.AppIcon : this.Icon.ToBitmap();

            txtApkPath.Text = apkDecoder.ApkPath;
            txtApkSize.Text = apkDecoder.ApkSize;

            this.btnPlayStore.Enabled = !string.IsNullOrEmpty(txtPackage.Text);
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            btnOpen.Focus();
        }

        private void btnAppStore_Click(object sender, EventArgs e)
        {
            string url = string.Format("https://play.google.com/store/apps/details?id={0}", txtPackage.Text);
            Process.Start(url);
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Apk文件(*.apk)|*.apk|所有文件(*.*)|*.*";
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                DecodeApk(dlg.FileName);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
