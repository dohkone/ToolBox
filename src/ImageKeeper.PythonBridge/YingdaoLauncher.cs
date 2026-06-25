using System.Diagnostics;
using System.Text.Json;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class YingdaoLauncher : IYingdaoLauncher
{
    private readonly string _nodeExePath;
    private readonly string _scriptPath;
    private readonly string _configPath;

    public YingdaoLauncher(string nodeExePath, string scriptPath, string configPath)
    {
        _nodeExePath = nodeExePath;
        _scriptPath = scriptPath;
        _configPath = configPath;
    }

    public async Task<string> LaunchMiaoshouAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_scriptPath))
        {
            throw new FileNotFoundException("Yingdao start script not found.", _scriptPath);
        }

        if (!File.Exists(_configPath))
        {
            throw new FileNotFoundException("Yingdao config not found.", _configPath);
        }

        var psi = new ProcessStartInfo
        {
            FileName = _nodeExePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        psi.ArgumentList.Add(_scriptPath);
        psi.ArgumentList.Add("--config");
        psi.ArgumentList.Add(_configPath);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start Yingdao launcher.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return "妙手自动上架已启动。";
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            if (doc.RootElement.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString() ?? "妙手自动上架已启动。";
            }
        }
        catch (JsonException)
        {
            // Keep the raw stdout as a human-readable success message.
        }

        return stdout;
    }
}
