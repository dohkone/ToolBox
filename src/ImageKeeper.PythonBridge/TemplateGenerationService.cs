using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class TemplateGenerationService : ITemplateGenerationService
{
	private sealed class TemplateGenerationPayload
	{
		public string? Mode { get; init; }

		public string? OutputDirectory { get; init; }

		public string[]? Prompts { get; init; }

		public TemplateGenerationItemPayload[]? Results { get; init; }
	}

	private sealed class TemplateGenerationItemPayload
	{
		public int Index { get; init; }

		public string? Prompt { get; init; }

		public string? FileName { get; init; }

		[JsonPropertyName("image_path")]
		public string? ImagePath { get; init; }
	}

	private readonly string _pythonExePath;

	private readonly string _scriptPath;

	private readonly object _processSyncRoot = new object();

	private Process? _currentProcess;

	public TemplateGenerationService(string pythonExePath, string scriptPath)
	{
		_pythonExePath = pythonExePath;
		_scriptPath = scriptPath;
	}

	public async Task<TemplateGenerateResult> GenerateAsync(TemplateGenerateRequest request, CancellationToken cancellationToken = default(CancellationToken))
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
		processStartInfo.ArgumentList.Add("--template-path");
		processStartInfo.ArgumentList.Add(request.TemplatePath);
		processStartInfo.ArgumentList.Add("--output-dir");
		processStartInfo.ArgumentList.Add(request.OutputDirectory);
		processStartInfo.ArgumentList.Add("--image2-script");
		processStartInfo.ArgumentList.Add(request.Image2ScriptPath);
		processStartInfo.ArgumentList.Add("--image-type");
		processStartInfo.ArgumentList.Add(request.ImageType switch
		{
			ImageTemplateType.SceneImage => "scene",
			ImageTemplateType.CompareImage => "compare",
			_ => "main"
		});
		if (request.ImageType == ImageTemplateType.MainImage && IsImage2Script(request.Image2ScriptPath))
		{
			string textureReferencePath = ResolveTextureReferencePath();
			if (!File.Exists(textureReferencePath))
			{
				throw new InvalidOperationException("Texture reference image not found: " + textureReferencePath);
			}
			processStartInfo.ArgumentList.Add("--texture-reference");
			processStartInfo.ArgumentList.Add(textureReferencePath);
		}
		processStartInfo.ArgumentList.Add("--count");
		processStartInfo.ArgumentList.Add(request.Count.ToString());
		processStartInfo.ArgumentList.Add("--concurrency");
		processStartInfo.ArgumentList.Add(request.Concurrency.ToString());
		if (request.UniqueScene)
		{
			processStartInfo.ArgumentList.Add("--unique-scene");
		}
		if (request.PromptsOnly)
		{
			processStartInfo.ArgumentList.Add("--prompts-only");
		}
		if (request.Seed.HasValue)
		{
			processStartInfo.ArgumentList.Add("--seed");
			processStartInfo.ArgumentList.Add(request.Seed.Value.ToString());
		}
		using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("无法启动模板生图脚本。");
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
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(text) ? stdout : text);
		}
		if (string.IsNullOrWhiteSpace(stdout))
		{
			throw new InvalidOperationException("模板生图脚本没有返回结果。");
		}
		JsonSerializerOptions options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		};
		TemplateGenerationPayload templateGenerationPayload = JsonSerializer.Deserialize<TemplateGenerationPayload>(stdout, options) ?? throw new InvalidOperationException("模板生图返回结果无法解析。");
		return new TemplateGenerateResult
		{
			Success = true,
			Mode = (templateGenerationPayload.Mode ?? string.Empty),
			OutputDirectory = (templateGenerationPayload.OutputDirectory ?? request.OutputDirectory),
			Prompts = (templateGenerationPayload.Prompts ?? Array.Empty<string>()),
			Items = (templateGenerationPayload.Results?.Select((TemplateGenerationItemPayload item) => new TemplateGenerateItem
			{
				Index = item.Index,
				Prompt = (item.Prompt ?? string.Empty),
				FileName = (item.FileName ?? string.Empty),
				ImagePath = (item.ImagePath ?? string.Empty)
			}).ToArray() ?? Array.Empty<TemplateGenerateItem>())
		};
	}

	private static bool IsImage2Script(string scriptPath)
	{
		return scriptPath.Contains("image2-generate", StringComparison.OrdinalIgnoreCase);
	}

	private static string ResolveTextureReferencePath()
	{
		string userTexturePath = ResolveUserTextureReferencePath();
		string bundledTexturePath = ResolveBundledTextureReferencePath();
		if (File.Exists(bundledTexturePath))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(userTexturePath) ?? string.Empty);
			FileInfo bundledFile = new FileInfo(bundledTexturePath);
			FileInfo userFile = new FileInfo(userTexturePath);
			if (!userFile.Exists || userFile.Length != bundledFile.Length || userFile.LastWriteTimeUtc < bundledFile.LastWriteTimeUtc)
			{
				File.Copy(bundledTexturePath, userTexturePath, overwrite: true);
			}
		}
		return userTexturePath;
	}

	private static string ResolveUserTextureReferencePath()
	{
		string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string root = string.IsNullOrWhiteSpace(localAppData) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".toolbox") : Path.Combine(localAppData, "ToolBox");
		return Path.Combine(root, "assets", "textures", "texure.jpg");
	}

	private static string ResolveBundledTextureReferencePath()
	{
		return Path.Combine(AppContext.BaseDirectory, "assets", "textures", "texure.jpg");
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
