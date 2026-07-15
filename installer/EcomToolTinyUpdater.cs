using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    private const string InstallerUrl = "__FULL_INSTALLER_URL__";
    private const string InstallerName = "__FULL_INSTALLER_NAME__";
    private const long ExpectedSize = __FULL_INSTALLER_SIZE__;
    private const string ReleaseUrl = "https://github.com/dohkone/ToolBox/releases/latest";

    private static Form _form;
    private static Label _label;
    private static ProgressBar _progress;
    private static Button _cancelButton;
    private static volatile bool _cancelRequested;

    [STAThread]
    private static void Main()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        _form = new Form
        {
            Text = "EcomTool \u66f4\u65b0",
            Width = 420,
            Height = 150,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        _label = new Label
        {
            Left = 18,
            Top = 18,
            Width = 360,
            Height = 24,
            Text = "\u6b63\u5728\u51c6\u5907\u4e0b\u8f7d\u66f4\u65b0..."
        };

        _progress = new ProgressBar
        {
            Left = 18,
            Top = 52,
            Width = 365,
            Height = 18,
            Minimum = 0,
            Maximum = 100
        };

        _cancelButton = new Button
        {
            Left = 305,
            Top = 82,
            Width = 78,
            Height = 28,
            Text = "\u53d6\u6d88"
        };
        _cancelButton.Click += delegate { _cancelRequested = true; _cancelButton.Enabled = false; };

        _form.Controls.Add(_label);
        _form.Controls.Add(_progress);
        _form.Controls.Add(_cancelButton);
        _form.Shown += delegate { new Thread(RunUpdate) { IsBackground = true }.Start(); };

        Application.Run(_form);
    }

    private static void RunUpdate()
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToolBox", "Updates");
            Directory.CreateDirectory(folder);
            string installerPath = GetReusableInstallerPath(folder);

            if (string.IsNullOrEmpty(installerPath))
            {
                installerPath = CreateUniqueFilePath(folder, InstallerName);
                DownloadFile(installerPath);
            }

            SetStatus("\u4e0b\u8f7d\u5b8c\u6210\uff0c\u6b63\u5728\u542f\u52a8\u5b89\u88c5\u7a0b\u5e8f...", 100);
            Process.Start(new ProcessStartInfo { FileName = installerPath, UseShellExecute = true });
            CloseForm();
        }
        catch (OperationCanceledException)
        {
            CloseForm();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "\u66f4\u65b0\u5305\u4e0b\u8f7d\u5931\u8d25\uff1a" + Environment.NewLine + ex.Message + Environment.NewLine + Environment.NewLine +
                "\u53ef\u4ee5\u7a0d\u540e\u91cd\u8bd5\uff0c\u6216\u624b\u52a8\u6253\u5f00\u4e0b\u8f7d\u9875\u9762\u3002" + Environment.NewLine + ReleaseUrl,
                "\u64cd\u4f5c\u5931\u8d25",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            try
            {
                Process.Start(new ProcessStartInfo { FileName = ReleaseUrl, UseShellExecute = true });
            }
            catch
            {
            }
            CloseForm();
        }
    }

    private static void DownloadFile(string installerPath)
    {
        string folder = Path.GetDirectoryName(installerPath);
        if (string.IsNullOrEmpty(folder))
        {
            folder = Path.GetTempPath();
        }
        string tempPath = CreateUniqueFilePath(folder, Path.GetFileNameWithoutExtension(installerPath) + ".download");

        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(InstallerUrl);
            request.UserAgent = "EcomTool-Tiny-Updater";
            request.Timeout = 30 * 60 * 1000;
            request.ReadWriteTimeout = 30 * 60 * 1000;
            request.AllowAutoRedirect = true;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (FileStream output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                long total = response.ContentLength > 0 ? response.ContentLength : ExpectedSize;
                long downloaded = 0;
                byte[] buffer = new byte[1024 * 1024];
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (_cancelRequested)
                    {
                        throw new OperationCanceledException();
                    }

                    output.Write(buffer, 0, read);
                    downloaded += read;

                    int percent = total > 0 ? (int)Math.Min(99, downloaded * 100 / total) : 0;
                    SetStatus("\u6b63\u5728\u4e0b\u8f7d\u5b8c\u6574\u5b89\u88c5\u5305... " + percent + "%", percent);
                }
            }

            if (ExpectedSize > 0 && new FileInfo(tempPath).Length != ExpectedSize)
            {
                throw new InvalidOperationException("\u4e0b\u8f7d\u6587\u4ef6\u5927\u5c0f\u4e0d\u5b8c\u6574\uff0c\u8bf7\u91cd\u65b0\u5c1d\u8bd5\u3002");
            }

            File.Move(tempPath, installerPath);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    private static string GetReusableInstallerPath(string folder)
    {
        string installerPath = Path.Combine(folder, InstallerName);
        if (!File.Exists(installerPath))
        {
            return null;
        }

        try
        {
            if (new FileInfo(installerPath).Length != ExpectedSize)
            {
                return null;
            }

            using (new FileStream(installerPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
            }

            return installerPath;
        }
        catch
        {
            return null;
        }
    }

    private static string CreateUniqueFilePath(string folder, string fileName)
    {
        string safeName = Path.GetFileName(fileName);
        string extension = Path.GetExtension(safeName);
        string stem = Path.GetFileNameWithoutExtension(safeName);
        for (int index = 0; index < 100; index++)
        {
            string suffix = DateTime.Now.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string candidate = Path.Combine(folder, stem + "_" + suffix + extension);
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(folder, stem + "_" + Guid.NewGuid().ToString("N") + extension);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void SetStatus(string text, int percent)
    {
        if (_form == null || _form.IsDisposed)
        {
            return;
        }

        _form.BeginInvoke((Action)delegate
        {
            _label.Text = text;
            _progress.Value = Math.Max(_progress.Minimum, Math.Min(_progress.Maximum, percent));
        });
    }

    private static void CloseForm()
    {
        if (_form == null || _form.IsDisposed)
        {
            return;
        }

        _form.BeginInvoke((Action)delegate { _form.Close(); });
    }
}
