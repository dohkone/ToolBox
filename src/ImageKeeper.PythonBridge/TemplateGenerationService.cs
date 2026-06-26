using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class TemplateGenerationService : ITemplateGenerationService
{
    private readonly string _pythonExePath;
    private readonly string _scriptPath;

    public TemplateGenerationService(string pythonExePath, string scriptPath)
    {
        _pythonExePath = pythonExePath;
        _scriptPath = scriptPath;
    }

    public async Task<TemplateGenerateResult> GenerateAsync(TemplateGenerateRequest request, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonExePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["PYTHONUTF8"] = "1";

        startInfo.ArgumentList.Add(_scriptPath);
        startInfo.ArgumentList.Add("--template-path");
        startInfo.ArgumentList.Add(request.TemplatePath);
        startInfo.ArgumentList.Add("--output-dir");
        startInfo.ArgumentList.Add(request.OutputDirectory);
        startInfo.ArgumentList.Add("--image2-script");
        startInfo.ArgumentList.Add(request.Image2ScriptPath);
        startInfo.ArgumentList.Add("--count");
        startInfo.ArgumentList.Add(request.Count.ToString());
        startInfo.ArgumentList.Add("--concurrency");
        startInfo.ArgumentList.Add(request.Concurrency.ToString());

        if (request.UniqueScene)
        {
            startInfo.ArgumentList.Add("--unique-scene");
        }

        if (request.PromptsOnly)
        {
            startInfo.ArgumentList.Add("--prompts-only");
        }

        if (request.Seed.HasValue)
        {
            startInfo.ArgumentList.Add("--seed");
            startInfo.ArgumentList.Add(request.Seed.Value.ToString());
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动模板生图脚本。");
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
            throw new InvalidOperationException("模板生图脚本没有返回结果。");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var payload = JsonSerializer.Deserialize<TemplateGenerationPayload>(stdout, options)
            ?? throw new InvalidOperationException("模板生图返回结果无法解析。");

        return new TemplateGenerateResult
        {
            Success = true,
            Mode = payload.Mode ?? string.Empty,
            OutputDirectory = payload.OutputDirectory ?? request.OutputDirectory,
            Prompts = payload.Prompts ?? Array.Empty<string>(),
            Items = payload.Results?
                .Select(item => new TemplateGenerateItem
                {
                    Index = item.Index,
                    Prompt = item.Prompt ?? string.Empty,
                    FileName = item.FileName ?? string.Empty,
                    ImagePath = item.ImagePath ?? string.Empty
                })
                .ToArray() ?? Array.Empty<TemplateGenerateItem>()
        };
    }

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
}
