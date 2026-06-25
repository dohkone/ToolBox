using System.Diagnostics;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class PythonScriptRunner : IPythonScriptRunner
{
    private readonly string _pythonExePath;

    public PythonScriptRunner(string pythonExePath)
    {
        _pythonExePath = pythonExePath;
    }

    public async Task<int> RunAsync(string scriptPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pythonExePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add(scriptPath);
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start python process.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(standardError)
                ? (string.IsNullOrWhiteSpace(standardOutput) ? $"Python script failed with exit code {process.ExitCode}." : standardOutput.Trim())
                : standardError.Trim();
            throw new InvalidOperationException(message);
        }

        return process.ExitCode;
    }
}
