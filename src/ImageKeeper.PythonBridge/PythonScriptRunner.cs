using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class PythonScriptRunner : IPythonScriptRunner
{
	private readonly string _pythonExePath;

	public PythonScriptRunner(string pythonExePath)
	{
		_pythonExePath = pythonExePath;
	}

	public async Task<int> RunAsync(string scriptPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default(CancellationToken))
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo
		{
			FileName = _pythonExePath,
			WorkingDirectory = PythonProcessHelper.GetWritableWorkingDirectory(),
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
			CreateNoWindow = true
		};
		processStartInfo.Environment["PYTHONIOENCODING"] = "utf-8";
		processStartInfo.Environment["PYTHONUTF8"] = "1";
		processStartInfo.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
		processStartInfo.ArgumentList.Add(scriptPath);
		foreach (string argument in arguments)
		{
			processStartInfo.ArgumentList.Add(argument);
		}
		using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Unable to start python process.");
		Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);
		string standardOutput = await standardOutputTask;
		string text = await standardErrorTask;
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException((!string.IsNullOrWhiteSpace(text)) ? text.Trim() : (string.IsNullOrWhiteSpace(standardOutput) ? $"Python script failed with exit code {process.ExitCode}." : standardOutput.Trim()));
		}
		return process.ExitCode;
	}
}
