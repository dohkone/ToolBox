using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class MiaoshouPublishService : IMiaoshouPublishService
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly string _nodeExePath;

	private readonly string _workingDirectory;

	public MiaoshouPublishService(string nodeExePath, string workingDirectory)
	{
		_nodeExePath = nodeExePath;
		_workingDirectory = workingDirectory;
	}

	public async Task<MiaoshouPublishResult> PublishAsync(MiaoshouPublishRequest request, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!Directory.Exists(_workingDirectory))
		{
			throw new DirectoryNotFoundException("Miaoshou Playwright directory not found: " + _workingDirectory);
		}
		ProcessStartInfo processStartInfo = new ProcessStartInfo
		{
			FileName = _nodeExePath,
			WorkingDirectory = _workingDirectory,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		string text = Path.Combine(_workingDirectory, "node_modules", "ts-node", "dist", "bin.js");
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("ts-node entry not found.", text);
		}
		string text2 = Path.Combine(AppContext.BaseDirectory, "runtime", "playwright-browsers");
		if (Directory.Exists(text2))
		{
			processStartInfo.Environment["PLAYWRIGHT_BROWSERS_PATH"] = text2;
		}
		processStartInfo.ArgumentList.Add(text);
		processStartInfo.ArgumentList.Add("src/open-miaoshou.ts");
		processStartInfo.ArgumentList.Add("--manifest");
		processStartInfo.ArgumentList.Add(request.ManifestPath);
		processStartInfo.ArgumentList.Add("--result");
		processStartInfo.ArgumentList.Add(request.ResultPath);
		processStartInfo.ArgumentList.Add("--config");
		processStartInfo.ArgumentList.Add(request.ConfigPath);
		processStartInfo.ArgumentList.Add("--events");
		processStartInfo.ArgumentList.Add(request.EventsPath);
		processStartInfo.ArgumentList.Add("--log");
		processStartInfo.ArgumentList.Add(request.LogPath);
		using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Unable to start Miaoshou Playwright process.");
		Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
		using CancellationTokenSource resultWaitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		Task<bool> resultTask = WaitForResultFileAsync(request.ResultPath, resultWaitCancellation.Token);
		using CancellationTokenSource eventsCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		Task eventsTask = WatchProgressEventsAsync(request, eventsCancellation.Token);
		Task exitTask = process.WaitForExitAsync(cancellationToken);
		bool flag = await Task.WhenAny(resultTask, exitTask) == resultTask;
		if (flag)
		{
			flag = await resultTask;
		}
		if (flag)
		{
			resultWaitCancellation.Cancel();
			await exitTask;
			eventsCancellation.Cancel();
			await IgnoreCancellationAsync(eventsTask);
			await outputTask;
			await errorTask;
			return await ReadResultAsync(request, cancellationToken);
		}
		resultWaitCancellation.Cancel();
		await exitTask;
		eventsCancellation.Cancel();
		await IgnoreCancellationAsync(eventsTask);
		await outputTask;
		string error = await errorTask;
		if (File.Exists(request.ResultPath))
		{
			return await ReadResultAsync(request, cancellationToken);
		}
		MiaoshouPublishResult miaoshouPublishResult = await TryBuildFallbackResultAsync(request, error, cancellationToken);
		if (miaoshouPublishResult != null)
		{
			return miaoshouPublishResult;
		}
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"Miaoshou Playwright failed with exit code {process.ExitCode}." : error.Trim());
		}
		return new MiaoshouPublishResult
		{
			Status = "success",
			ResultPath = request.ResultPath,
			LogPath = request.LogPath
		};
	}

	private static async Task<MiaoshouPublishResult?> TryBuildFallbackResultAsync(MiaoshouPublishRequest request, string processError, CancellationToken cancellationToken)
	{
		HashSet<string> expectedCardPaths = await ReadManifestCardPathsAsync(request.ManifestPath, cancellationToken);
		Dictionary<string, MiaoshouPublishItemResult> dictionary = await ReadEventResultsAsync(request.EventsPath, cancellationToken);
		if (expectedCardPaths.Count == 0 && dictionary.Count == 0)
		{
			return null;
		}
		List<MiaoshouPublishItemResult> list = new List<MiaoshouPublishItemResult>();
		foreach (string item in expectedCardPaths)
		{
			if (dictionary.TryGetValue(item, out var value))
			{
				list.Add(value);
				continue;
			}
			list.Add(new MiaoshouPublishItemResult
			{
				CardPath = item,
				Label = Path.GetFileName(item),
				Status = "failed",
				Error = "流程异常中断，未收到该卡片的最终上架结果。"
			});
		}
		foreach (KeyValuePair<string, MiaoshouPublishItemResult> item2 in dictionary)
		{
			if (!expectedCardPaths.Contains(item2.Key))
			{
				list.Add(item2.Value);
			}
		}
		int num = list.Count((MiaoshouPublishItemResult item) => string.Equals(item.Status, "success", StringComparison.OrdinalIgnoreCase));
		int num2 = list.Count - num;
		return new MiaoshouPublishResult
		{
			Status = ((num2 > 0) ? "failed" : "success"),
			Error = (string.IsNullOrWhiteSpace(processError) ? "未生成最终结果文件，已根据过程事件补全上架结果。" : processError.Trim()),
			Total = list.Count,
			SuccessCount = num,
			FailedCount = num2,
			Results = list,
			ResultPath = request.ResultPath,
			LogPath = request.LogPath
		};
	}

	private static async Task<bool> WaitForResultFileAsync(string resultPath, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			if (File.Exists(resultPath))
			{
				try
				{
					await using FileStream fileStream = File.Open(resultPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
					if (fileStream.Length > 0)
					{
						return true;
					}
				}
				catch (IOException)
				{
				}
			}
			await Task.Delay(500, cancellationToken);
		}
		return false;
	}

	private static async Task<MiaoshouPublishResult> ReadResultAsync(MiaoshouPublishRequest request, CancellationToken cancellationToken)
	{
		MiaoshouPublishResult? obj = JsonSerializer.Deserialize<MiaoshouPublishResult>(await File.ReadAllTextAsync(request.ResultPath, cancellationToken), JsonOptions) ?? new MiaoshouPublishResult();
		obj.ResultPath = request.ResultPath;
		obj.LogPath = request.LogPath;
		return obj;
	}

	private static async Task<HashSet<string>> ReadManifestCardPathsAsync(string manifestPath, CancellationToken cancellationToken)
	{
		HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (!File.Exists(manifestPath))
		{
			return result;
		}
		HashSet<string> result2;
		await using (FileStream stream = File.OpenRead(manifestPath))
		{
			using JsonDocument jsonDocument = await JsonDocument.ParseAsync(stream, default(JsonDocumentOptions), cancellationToken);
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Array)
			{
				result2 = result;
			}
			else
			{
				foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
				{
					if (item.TryGetProperty("card_folder_path", out var value) && value.ValueKind == JsonValueKind.String)
					{
						string text = value.GetString();
						if (!string.IsNullOrWhiteSpace(text))
						{
							result.Add(text);
						}
					}
				}
				result2 = result;
			}
		}
		return result2;
	}

	private static async Task<Dictionary<string, MiaoshouPublishItemResult>> ReadEventResultsAsync(string eventsPath, CancellationToken cancellationToken)
	{
		Dictionary<string, MiaoshouPublishItemResult> result = new Dictionary<string, MiaoshouPublishItemResult>(StringComparer.OrdinalIgnoreCase);
		if (!File.Exists(eventsPath))
		{
			return result;
		}
		string[] array;
		try
		{
			array = await File.ReadAllLinesAsync(eventsPath, Encoding.UTF8, cancellationToken);
		}
		catch (IOException)
		{
			return result;
		}
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				MiaoshouPublishProgressEvent miaoshouPublishProgressEvent;
				try
				{
					miaoshouPublishProgressEvent = JsonSerializer.Deserialize<MiaoshouPublishProgressEvent>(text, JsonOptions);
				}
				catch (JsonException)
				{
					continue;
				}
				if (miaoshouPublishProgressEvent != null && !string.IsNullOrWhiteSpace(miaoshouPublishProgressEvent.CardPath) && IsProductFinishedEvent(miaoshouPublishProgressEvent.Type))
				{
					result[miaoshouPublishProgressEvent.CardPath] = new MiaoshouPublishItemResult
					{
						CardPath = miaoshouPublishProgressEvent.CardPath,
						Label = miaoshouPublishProgressEvent.Label,
						Status = (string.Equals(miaoshouPublishProgressEvent.Type, "product_success", StringComparison.OrdinalIgnoreCase) ? "success" : "failed"),
						Error = miaoshouPublishProgressEvent.Error,
						Elapsed = miaoshouPublishProgressEvent.Elapsed
					};
				}
			}
		}
		return result;
	}

	private static async Task WatchProgressEventsAsync(MiaoshouPublishRequest request, CancellationToken cancellationToken)
	{
		if (request.ProgressHandler == null || string.IsNullOrWhiteSpace(request.EventsPath))
		{
			return;
		}
		int processedLineCount = 0;
		while (!cancellationToken.IsCancellationRequested)
		{
			if (!File.Exists(request.EventsPath))
			{
				await Task.Delay(300, cancellationToken);
				continue;
			}
			string[] lines;
			try
			{
				lines = await File.ReadAllLinesAsync(request.EventsPath, cancellationToken);
			}
			catch (IOException)
			{
				await Task.Delay(300, cancellationToken);
				continue;
			}
			for (int i = processedLineCount; i < lines.Length; i++)
			{
				await HandleProgressEventLineAsync(lines[i], request.ProgressHandler, cancellationToken);
			}
			processedLineCount = lines.Length;
			await Task.Delay(300, cancellationToken);
		}
	}

	private static async Task HandleProgressEventLineAsync(string line, Func<MiaoshouPublishProgressEvent, Task> progressHandler, CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(line))
		{
			MiaoshouPublishProgressEvent miaoshouPublishProgressEvent;
			try
			{
				miaoshouPublishProgressEvent = JsonSerializer.Deserialize<MiaoshouPublishProgressEvent>(line, JsonOptions);
			}
			catch (JsonException)
			{
				return;
			}
			if (miaoshouPublishProgressEvent != null && !string.IsNullOrWhiteSpace(miaoshouPublishProgressEvent.CardPath) && IsProductFinishedEvent(miaoshouPublishProgressEvent.Type) && !cancellationToken.IsCancellationRequested)
			{
				await progressHandler(miaoshouPublishProgressEvent);
			}
		}
	}

	private static bool IsProductFinishedEvent(string eventType)
	{
		if (!string.Equals(eventType, "product_success", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(eventType, "product_failed", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static async Task IgnoreCancellationAsync(Task task)
	{
		try
		{
			await task;
		}
		catch (OperationCanceledException)
		{
		}
	}
}
