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
            Text = "EcomTool 更新",
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
            Text = "正在准备下载更新..."
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
            Text = "取消"
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
            string installerPath = Path.Combine(folder, InstallerName);

            if (!File.Exists(installerPath) || new FileInfo(installerPath).Length != ExpectedSize)
            {
                DownloadFile(installerPath);
            }

            SetStatus("下载完成，正在启动安装程序...", 100);
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
                "更新包下载失败：" + Environment.NewLine + ex.Message + Environment.NewLine + Environment.NewLine +
                "可以稍后重试，或手动打开下载页面。" + Environment.NewLine + ReleaseUrl,
                "操作失败",
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
        string tempPath = installerPath + ".download";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(InstallerUrl);
        request.UserAgent = "EcomTool-Tiny-Updater";
        request.Timeout = 30 * 60 * 1000;
        request.ReadWriteTimeout = 30 * 60 * 1000;
        request.AllowAutoRedirect = true;

        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (Stream input = response.GetResponseStream())
        using (FileStream output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
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
                SetStatus("正在下载完整安装包... " + percent + "%", percent);
            }
        }

        if (ExpectedSize > 0 && new FileInfo(tempPath).Length != ExpectedSize)
        {
            throw new InvalidOperationException("下载文件大小不完整，请重新尝试。");
        }

        if (File.Exists(installerPath))
        {
            File.Delete(installerPath);
        }
        File.Move(tempPath, installerPath);
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
