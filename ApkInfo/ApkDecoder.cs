using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ApkInfo
{
    public class ApkDecoder
    {
        public event Action<ApkDecoder> InfoParsedEvent;
        public event MethodInvoker AaptNotFoundEvent;
        public static Dictionary<int, string> SdkMap = new Dictionary<int, string> {
            {1, "Android 1.0 / BASE"},
            {2, "Android 1.1 / BASE_1_1"},
            {3, "Android 1.5 / CUPCAKE"},
            {4, "Android 1.6 / DONUT"},
            {5, "Android 2.0 / ECLAIR"},
            {6, "Android 2.0.1 / ECLAIR_0_1"},
            {7, "Android 2.1.x / ECLAIR_MR1"},
            {8, "Android 2.2.x / FROYO"},
            {9, "Android 2.3, 2.3.1, 2.3.2 / GINGERBREAD"},
            {10, "Android 2.3.3, 2.3.4 / GINGERBREAD_MR1"},
            {11, "Android 3.0.x / HONEYCOMB"},
            {12, "Android 3.1.x / HONEYCOMB_MR1"},
            {13, "Android 3.2 / HONEYCOMB_MR2"},
            {14, "Android 4.0, 4.0.1, 4.0.2 / ICE_CREAM_SANDWICH"},
            {15, "Android 4.0.3, 4.0.4 / ICE_CREAM_SANDWICH_MR1"},
            {16, "Android 4.1, 4.1.1 / JELLY_BEAN"},
            {17, "Android 4.2, 4.2.2 / JELLY_BEAN_MR1"},
            {18, "Android 4.3 / JELLY_BEAN_MR2"},
            {19, "Android 4.4 / KITKAT"}
        };

        private string appPath;
        private string apkPath;
        private List<string> infos = new List<string>();

        public ApkDecoder(string apkPath)
        {
            this.appPath = Path.GetDirectoryName(Application.ExecutablePath);
            this.apkPath = apkPath;
            ThreadPool.QueueUserWorkItem(new WaitCallback(Decoder));
        }

        public string ApkPath
        {
            get { return this.apkPath; }
        }

        public string ApkSize
        {
            get { return GetApkSize(this.apkPath); }
        }

        public string AppName { get; private set; }
        public string AppVersion { get; private set; }
        public string AppVersionCode { get; private set; }
        public string PkgName { get; private set; }
        public string IconPath { get; private set; }
        public Image AppIcon;
        public string MinSdk { get; private set; }
        public string MinVersion { get; private set; }
        public string ScreenSupport { get; private set; }
        public string ScreenSolutions { get; private set; }
        public string Permissions { get; private set; }
        public string Features { get; private set; }

        private string GetApkSize(string apkPath)
        {
            string apkSize = "0 M";
            if (!File.Exists(apkPath))
                return apkSize;

            FileInfo fi = new FileInfo(apkPath);
            if (fi.Length >= 1024 * 1024)
            {
                apkSize = string.Format("{0:N2} M", fi.Length / (1024 * 1024f));
            }
            else
            {
                apkSize = string.Format("{0:N2} K", fi.Length / 1024f);
            }
            return apkSize;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetShortPathName(
           [MarshalAs(UnmanagedType.LPTStr)]string path,
           [MarshalAs(UnmanagedType.LPTStr)]StringBuilder short_path,
           int short_len);

        private void Decoder(object state)
        {
            if (!File.Exists(this.apkPath))
                return;
            string aaptPath = Path.Combine(this.appPath, @"tools\aapt.exe");
            if (!File.Exists(aaptPath))
                aaptPath = Path.Combine(this.appPath, @"aapt.exe");
            if (!File.Exists(aaptPath))
            {
                var handler = AaptNotFoundEvent;
                if (handler != null)
                    handler();
                return;
            }

            StringBuilder sb = new StringBuilder(255);
            int result = GetShortPathName(aaptPath, sb, 255);
            if (result != 0)
                aaptPath = sb.ToString();

            var startInfo = new ProcessStartInfo("cmd.exe");
            string dumpFile = Path.GetTempFileName();
            //如此费事做中转，只为处理中文乱码
            string args = string.Format("/k {0} dump badging \"{1}\" > \"{2}\" &exit", aaptPath, this.apkPath, dumpFile);
            startInfo.Arguments = args;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            this.infos.Clear();
            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit(2000);
            }
            if (File.Exists(dumpFile))
            {
                //解析
                using (var sr = new StreamReader(dumpFile, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        this.infos.Add(line);
                    }
                    ParseInfo();
                }
                try
                {
                    File.Delete(dumpFile);
                }
                catch
                {
                }
            }
        }

        private void ParseInfo()
        {
            if (this.infos.Count == 0)
            {
                InvokeParsedHandler();
                return;
            }

            DoParseInfo();
            InvokeParsedHandler();
        }

        private void InvokeParsedHandler()
        {
            var hander = InfoParsedEvent;
            if (hander != null)
                hander(this);
        }

        private void DoParseInfo()
        {
            //解析每个字串
            foreach (var info in this.infos)
            {
                if (string.IsNullOrEmpty(info))
                    continue;

                //application: label='MobileGo™' icon='r/l/icon.png'
                if (info.IndexOf("application:") == 0)
                {
                    this.AppName = GetKeyValue(info, "label=");
                    this.IconPath = GetKeyValue(info, "icon=");
                    GetAppIcon(this.IconPath);
                }

                //package: name='com.wondershare.mobilego' versionCode='4773' versionName='7.5.2.4773'
                if (info.IndexOf("package:") == 0)
                {
                    this.PkgName = GetKeyValue(info, "name=");
                    this.AppVersion = GetKeyValue(info, "versionName=");
                    this.AppVersionCode = GetKeyValue(info, "versionCode=");
                }

                //sdkVersion:'8'
                if (info.IndexOf("sdkVersion:") == 0)
                {
                    this.MinSdk = GetKeyValue(info, "sdkVersion:");
                    this.MinVersion = string.Empty;
                    if (!string.IsNullOrEmpty(this.MinSdk))
                    {
                        int minSdk = 1;
                        if (int.TryParse(this.MinSdk, out minSdk) && minSdk >= 1 && minSdk <= 19)
                        {
                            this.MinVersion = SdkMap[minSdk];
                        }
                    }
                }

                //supports-screens: 'small' 'normal' 'large' 'xlarge'
                if (info.IndexOf("supports-screens:") == 0)
                {
                    this.ScreenSupport = info.Replace("supports-screens:", "").TrimStart().Replace("' '", ", ").Replace("'", "");
                }

                //densities: '120' '160' '213' '240' '320' '480' '640'
                if (info.IndexOf("densities:") == 0)
                {
                    this.ScreenSolutions = info.Replace("densities:", "").TrimStart().Replace("' '", ", ").Replace("'", "");
                }

                //uses-permission:'android.permission.READ_CONTACTS'
                //uses-permission:'android.permission.WRITE_CONTACTS'
                //uses-permission:'android.permission.READ_SMS'
                if (info.IndexOf("uses-permission:") == 0)
                {
                    string permission = info.Substring(info.LastIndexOf('.') + 1).Replace("'", "");
                    this.Permissions += permission + "\r\n";
                }

                //uses-feature:'android.hardware.touchscreen'
                if (info.IndexOf("uses-feature:") == 0)
                {
                    string feature = info.Substring(info.LastIndexOf('.') + 1).Replace("'", "");
                    this.Features += feature + "\r\n";
                }
            }
            if (!string.IsNullOrEmpty(this.Permissions))
            {
                this.Permissions = this.Permissions.Trim();
            }
            if (!string.IsNullOrEmpty(this.Features))
            {
                this.Features = this.Features.Trim();
            }
        }

        private string GetKeyValue(string info, string key)
        {
            if (info.IndexOf(key) != -1)
            {
                int start = info.IndexOf(key) + @key.Length + 1;
                return info.Substring(start, info.IndexOf("'", start) - start);
            }
            return string.Empty;
        }

        private void GetAppIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
                return;
            string unzipPath = Path.Combine(appPath, @"tools\unzip.exe");
            if (!File.Exists(unzipPath))
                unzipPath = Path.Combine(appPath, @"unzip.exe");
            if (!File.Exists(unzipPath))
                return;

            string destPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(iconPath));
            if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }
            var startInfo = new ProcessStartInfo(unzipPath);
            string args = string.Format("-j \"{0}\" \"{1}\" -d \"{2}\"", this.apkPath, iconPath, Path.GetDirectoryName(Path.GetTempPath()));
            startInfo.Arguments = args;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit(2000);
            }

            if (File.Exists(destPath))
            {
                using (var fs = new FileStream(destPath, FileMode.Open, FileAccess.Read))
                {
                    this.AppIcon = Image.FromStream(fs);
                }
                File.Delete(destPath);
            }
        }
    }
}
