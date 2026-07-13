using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class SkuOptimizeService : ISkuOptimizeService
{
	private sealed class SkuOptimizePayload
	{
		[JsonPropertyName("input_dir")]
		public string? InputDirectory { get; init; }

		[JsonPropertyName("output_dir")]
		public string? OutputDirectory { get; init; }

		[JsonPropertyName("result_root")]
		public string? ResultRoot { get; init; }

		public int Concurrency { get; init; }

		[JsonPropertyName("length_multiplier")]
		public double LengthMultiplier { get; init; }

		[JsonPropertyName("diameter_multiplier")]
		public double DiameterMultiplier { get; init; }

		public SkuOptimizeJobPayload[]? Results { get; init; }
	}

	private sealed class SkuOptimizeJobPayload
	{
		public int Index { get; init; }

		[JsonPropertyName("source_image")]
		public string? SourceImage { get; init; }

		public string? Status { get; init; }

		[JsonPropertyName("image_path")]
		public string? ImagePath { get; init; }

		public string? Error { get; init; }

		public int Attempts { get; init; }
	}

	private readonly string _pythonExePath;

	private readonly string _scriptPath;

	private readonly object _processSyncRoot = new object();

	private Process? _currentProcess;

	public SkuOptimizeService(string pythonExePath, string scriptPath)
	{
		_pythonExePath = pythonExePath;
		_scriptPath = scriptPath;
	}

	public async Task<SkuOptimizeResult> GenerateAsync(SkuOptimizeRequest request, CancellationToken cancellationToken = default(CancellationToken))
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
		processStartInfo.ArgumentList.Add(_scriptPath);
		processStartInfo.ArgumentList.Add("--input-dir");
		processStartInfo.ArgumentList.Add(request.InputDirectory);
		processStartInfo.ArgumentList.Add("--output-dir");
		processStartInfo.ArgumentList.Add(request.OutputDirectory);
		processStartInfo.ArgumentList.Add("--image2-script");
		processStartInfo.ArgumentList.Add(request.Image2ScriptPath);
		processStartInfo.ArgumentList.Add("--concurrency");
		processStartInfo.ArgumentList.Add(request.Concurrency.ToString());
		processStartInfo.ArgumentList.Add("--length-multiplier");
		processStartInfo.ArgumentList.Add(request.LengthMultiplier.ToString(CultureInfo.InvariantCulture));
		processStartInfo.ArgumentList.Add("--diameter-multiplier");
		processStartInfo.ArgumentList.Add(request.DiameterMultiplier.ToString(CultureInfo.InvariantCulture));
		if (request.Overwrite)
		{
			processStartInfo.ArgumentList.Add("--overwrite");
		}
		using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("无法启动 SKU 图优化脚本。");
		RegisterRunningProcess(process, cancellationToken);
		Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
		try
		{
			await process.WaitForExitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			TryKillProcessTree(process);
			throw;
		}
		finally
		{
			ClearRunningProcess(process);
		}
		string stdout = (await stdoutTask).Trim();
		string text = (await stderrTask).Trim();
		if (string.IsNullOrWhiteSpace(stdout))
		{
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(text) ? "SKU 图优化脚本没有返回结果。" : text);
		}
		JsonSerializerOptions options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		};
		SkuOptimizePayload skuOptimizePayload;
		try
		{
			skuOptimizePayload = JsonSerializer.Deserialize<SkuOptimizePayload>(stdout, options);
		}
		catch (JsonException ex2)
		{
			throw new InvalidOperationException("SKU 图优化结果解析失败：" + ex2.Message);
		}
		if (skuOptimizePayload == null)
		{
			throw new InvalidOperationException("SKU 图优化返回结果为空。");
		}
		if (process.ExitCode != 0)
		{
			SkuOptimizeJobPayload[]? results = skuOptimizePayload.Results;
			if (results == null || results.Length == 0)
			{
				throw new InvalidOperationException(string.IsNullOrWhiteSpace(text) ? "SKU 图优化执行失败。" : text);
			}
		}
		return new SkuOptimizeResult
		{
			Success = (process.ExitCode == 0 && (skuOptimizePayload.Results?.All((SkuOptimizeJobPayload item) => !string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase)) ?? true)),
			InputDirectory = (skuOptimizePayload.InputDirectory ?? request.InputDirectory),
			OutputDirectory = (skuOptimizePayload.OutputDirectory ?? request.OutputDirectory),
			ResultRoot = (skuOptimizePayload.ResultRoot ?? string.Empty),
			Concurrency = skuOptimizePayload.Concurrency,
			LengthMultiplier = skuOptimizePayload.LengthMultiplier,
			DiameterMultiplier = skuOptimizePayload.DiameterMultiplier,
			Results = (skuOptimizePayload.Results?.Select((SkuOptimizeJobPayload item) => new SkuOptimizeJobResult
			{
				Index = item.Index,
				SourceImage = (item.SourceImage ?? string.Empty),
				Status = (item.Status ?? string.Empty),
				ImagePath = (item.ImagePath ?? string.Empty),
				Error = (item.Error ?? string.Empty),
				Attempts = item.Attempts
			}).ToArray() ?? Array.Empty<SkuOptimizeJobResult>())
		};
	}

	public void CancelCurrentRun()
	{
		lock (_processSyncRoot)
		{
			if (_currentProcess != null)
			{
				TryKillProcessTree(_currentProcess);
			}
		}
	}

	private void RegisterRunningProcess(Process process, CancellationToken cancellationToken)
	{
		lock (_processSyncRoot)
		{
			_currentProcess = process;
		}
		cancellationToken.Register(delegate
		{
			TryKillProcessTree(process);
		});
	}

	private void ClearRunningProcess(Process process)
	{
		lock (_processSyncRoot)
		{
			if (_currentProcess == process)
			{
				_currentProcess = null;
			}
		}
	}

	private static void TryKillProcessTree(Process process)
	{
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
		catch
		{
		}
	}
}
