using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class SpBatchService : ISpBatchService
{
	private sealed class SpBatchPayload
	{
		public string? Mode { get; init; }

		[JsonPropertyName("input_dir")]
		public string? InputDirectory { get; init; }

		[JsonPropertyName("output_dir")]
		public string? OutputDirectory { get; init; }

		[JsonPropertyName("dated_root")]
		public string? DatedRoot { get; init; }

		public int Concurrency { get; init; }

		public int Retries { get; init; }

		[JsonPropertyName("prepare_only")]
		public bool PrepareOnly { get; init; }

		[JsonPropertyName("color_count")]
		public int? ColorCount { get; init; }

		[JsonPropertyName("selected_colors")]
		public string[]? SelectedColors { get; init; }

		[JsonPropertyName("prepared_bundles")]
		public SpBatchBundlePayload[]? PreparedBundles { get; init; }

		public SpBatchJobPayload[]? Results { get; init; }

		public SpBatchPlanJobPayload[]? Jobs { get; init; }
	}

	private sealed class SpBatchBundlePayload
	{
		[JsonPropertyName("source_image")]
		public string? SourceImage { get; init; }

		[JsonPropertyName("sp_dir")]
		public string? SpDir { get; init; }

		[JsonPropertyName("main_dir")]
		public string? MainDir { get; init; }

		[JsonPropertyName("sku_dir")]
		public string? SkuDir { get; init; }

		[JsonPropertyName("detail_dir")]
		public string? DetailDir { get; init; }

		[JsonPropertyName("source_copy_path")]
		public string? SourceCopyPath { get; init; }
	}

	private sealed class SpBatchJobPayload
	{
		public int Index { get; init; }

		[JsonPropertyName("source_image")]
		public string? SourceImage { get; init; }

		[JsonPropertyName("source_copy_path")]
		public string? SourceCopyPath { get; init; }

		[JsonPropertyName("sp_dir")]
		public string? SpDir { get; init; }

		public string? Color { get; init; }

		public string? Stage { get; init; }

		public string? Status { get; init; }

		[JsonPropertyName("image_path")]
		public string? ImagePath { get; init; }

		public string? Error { get; init; }

		public int Attempts { get; init; }
	}

	private sealed class SpBatchPlanJobPayload
	{
		public int Index { get; init; }

		[JsonPropertyName("source_image")]
		public string? SourceImage { get; init; }

		[JsonPropertyName("sp_dir")]
		public string? SpDir { get; init; }

		public string? Color { get; init; }

		public string? Stage { get; init; }

		[JsonPropertyName("output_path")]
		public string? OutputPath { get; init; }
	}

	private readonly string _pythonExePath;

	private readonly string _scriptPath;

	private readonly object _processSyncRoot = new object();

	private Process? _currentProcess;

	public SpBatchService(string pythonExePath, string scriptPath)
	{
		_pythonExePath = pythonExePath;
		_scriptPath = scriptPath;
	}

	public async Task<SpBatchResult> GenerateAsync(SpBatchRequest request, CancellationToken cancellationToken = default(CancellationToken))
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
		processStartInfo.ArgumentList.Add("--request");
		processStartInfo.ArgumentList.Add(BuildRequestText(request));
		processStartInfo.ArgumentList.Add("--input-dir");
		processStartInfo.ArgumentList.Add(request.InputDirectory);
		processStartInfo.ArgumentList.Add("--output-dir");
		processStartInfo.ArgumentList.Add(request.OutputDirectory);
		processStartInfo.ArgumentList.Add("--image2-script");
		processStartInfo.ArgumentList.Add(request.Image2ScriptPath);
		processStartInfo.ArgumentList.Add("--material");
		processStartInfo.ArgumentList.Add(request.Material);
		string? colorTemplatePath = ColorTemplateFileHelper.Write(request.ColorTemplateColors, request.SelectedColors);
		if (!string.IsNullOrWhiteSpace(colorTemplatePath))
		{
			processStartInfo.ArgumentList.Add("--color-template");
			processStartInfo.ArgumentList.Add(colorTemplatePath);
		}
		processStartInfo.ArgumentList.Add("--concurrency");
		processStartInfo.ArgumentList.Add(request.Concurrency.ToString());
		processStartInfo.ArgumentList.Add("--retries");
		processStartInfo.ArgumentList.Add(request.Retries.ToString());
		if (request.Overwrite)
		{
			processStartInfo.ArgumentList.Add("--overwrite");
		}
		if (request.Mode == SpBatchMode.DryRun)
		{
			processStartInfo.ArgumentList.Add("--dry-run");
		}
		else if (request.Mode == SpBatchMode.PrepareOnly)
		{
			processStartInfo.ArgumentList.Add("--prepare-only");
		}
		else if (request.Mode == SpBatchMode.GenerateMaster)
		{
			processStartInfo.ArgumentList.Add("--master-only");
		}
		else if (request.Mode == SpBatchMode.GenerateColors)
		{
			processStartInfo.ArgumentList.Add("--recolor-only");
		}
		using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("无法启动 SP 批处理脚本。");
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
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(text) ? "SP 批处理脚本没有返回结果。" : text);
		}
		JsonSerializerOptions options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		};
		SpBatchPayload spBatchPayload;
		try
		{
			spBatchPayload = JsonSerializer.Deserialize<SpBatchPayload>(stdout, options);
		}
		catch (JsonException ex2)
		{
			throw new InvalidOperationException("SP 批处理结果解析失败：" + ex2.Message);
		}
		if (spBatchPayload == null)
		{
			throw new InvalidOperationException("SP 批处理返回结果为空。");
		}
		if (process.ExitCode != 0)
		{
			SpBatchJobPayload[]? results = spBatchPayload.Results;
			if (results == null || results.Length == 0)
			{
				throw new InvalidOperationException(string.IsNullOrWhiteSpace(text) ? "SP 批处理执行失败。" : text);
			}
		}
		return new SpBatchResult
		{
			Success = (process.ExitCode == 0 && (spBatchPayload.Results?.All((SpBatchJobPayload item) => !string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase)) ?? true)),
			Mode = (spBatchPayload.Mode ?? string.Empty),
			InputDirectory = (spBatchPayload.InputDirectory ?? request.InputDirectory),
			OutputDirectory = (spBatchPayload.OutputDirectory ?? request.OutputDirectory),
			DatedRoot = (spBatchPayload.DatedRoot ?? string.Empty),
			Concurrency = spBatchPayload.Concurrency,
			Retries = spBatchPayload.Retries,
			PrepareOnly = spBatchPayload.PrepareOnly,
			ColorCount = spBatchPayload.ColorCount,
			SelectedColors = (spBatchPayload.SelectedColors ?? Array.Empty<string>()),
			PreparedBundles = (spBatchPayload.PreparedBundles?.Select((SpBatchBundlePayload item) => new SpBatchBundle
			{
				SourceImage = (item.SourceImage ?? string.Empty),
				SpDirectory = (item.SpDir ?? string.Empty),
				MainDirectory = (item.MainDir ?? string.Empty),
				SkuDirectory = (item.SkuDir ?? string.Empty),
				DetailDirectory = (item.DetailDir ?? string.Empty),
				SourceCopyPath = (item.SourceCopyPath ?? string.Empty)
			}).ToArray() ?? Array.Empty<SpBatchBundle>()),
			Results = BuildJobResults(spBatchPayload)
		};
	}

	private static string BuildRequestText(SpBatchRequest request)
	{
		string value = request.Mode switch
		{
			SpBatchMode.DryRun => "只出计划", 
			SpBatchMode.PrepareOnly => "只建结构", 
			SpBatchMode.GenerateMaster => "只生成 SKU 母图", 
			SpBatchMode.GenerateColors => "基于 SKU 母图生成其他颜色", 
			_ => "正式生成", 
		};
		return $"基于 {request.InputDirectory} 的图片，输出到 {request.OutputDirectory}，并发 {request.Concurrency}，重试 {request.Retries}，{value}";
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

	private static IReadOnlyList<SpBatchJobResult> BuildJobResults(SpBatchPayload payload)
	{
		SpBatchJobPayload[] results = payload.Results;
		if (results != null && results.Length > 0)
		{
			return payload.Results.Select((SpBatchJobPayload item) => new SpBatchJobResult
			{
				Index = item.Index,
				SourceImage = (item.SourceImage ?? string.Empty),
				SourceCopyPath = (item.SourceCopyPath ?? string.Empty),
				SpDirectory = (item.SpDir ?? string.Empty),
				Color = (item.Color ?? string.Empty),
				Stage = (item.Stage ?? string.Empty),
				Status = (item.Status ?? string.Empty),
				ImagePath = (item.ImagePath ?? string.Empty),
				Error = (item.Error ?? string.Empty),
				Attempts = item.Attempts
			}).ToArray();
		}
		SpBatchPlanJobPayload[] jobs = payload.Jobs;
		if (jobs != null && jobs.Length > 0)
		{
			return payload.Jobs.Select((SpBatchPlanJobPayload item) => new SpBatchJobResult
			{
				Index = item.Index,
				SourceImage = (item.SourceImage ?? string.Empty),
				SpDirectory = (item.SpDir ?? string.Empty),
				Color = (item.Color ?? string.Empty),
				Stage = (item.Stage ?? string.Empty),
				Status = "planned",
				ImagePath = (item.OutputPath ?? string.Empty)
			}).ToArray();
		}
		return Array.Empty<SpBatchJobResult>();
	}
}
