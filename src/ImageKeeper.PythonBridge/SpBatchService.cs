using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class SpBatchService : ISpBatchService
{
    private readonly string _pythonExePath;
    private readonly string _scriptPath;

    public SpBatchService(string pythonExePath, string scriptPath)
    {
        _pythonExePath = pythonExePath;
        _scriptPath = scriptPath;
    }

    public async Task<SpBatchResult> GenerateAsync(SpBatchRequest request, CancellationToken cancellationToken = default)
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
        startInfo.ArgumentList.Add("--request");
        startInfo.ArgumentList.Add(BuildRequestText(request));
        startInfo.ArgumentList.Add("--input-dir");
        startInfo.ArgumentList.Add(request.InputDirectory);
        startInfo.ArgumentList.Add("--output-dir");
        startInfo.ArgumentList.Add(request.OutputDirectory);
        startInfo.ArgumentList.Add("--image2-script");
        startInfo.ArgumentList.Add(request.Image2ScriptPath);
        startInfo.ArgumentList.Add("--concurrency");
        startInfo.ArgumentList.Add(request.Concurrency.ToString());
        startInfo.ArgumentList.Add("--retries");
        startInfo.ArgumentList.Add(request.Retries.ToString());

        if (request.Overwrite)
        {
            startInfo.ArgumentList.Add("--overwrite");
        }

        if (request.Mode == SpBatchMode.DryRun)
        {
            startInfo.ArgumentList.Add("--dry-run");
        }
        else if (request.Mode == SpBatchMode.PrepareOnly)
        {
            startInfo.ArgumentList.Add("--prepare-only");
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 SP 批处理脚本。");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "SP 批处理脚本没有返回结果。" : stderr);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        SpBatchPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SpBatchPayload>(stdout, options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"SP 批处理结果解析失败：{ex.Message}");
        }

        if (payload is null)
        {
            throw new InvalidOperationException("SP 批处理返回结果为空。");
        }

        if (process.ExitCode != 0 && (payload.Results?.Length ?? 0) == 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "SP 批处理执行失败。" : stderr);
        }

        return new SpBatchResult
        {
            Success = process.ExitCode == 0 && (payload.Results?.All(item => !string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase)) ?? true),
            Mode = payload.Mode ?? string.Empty,
            InputDirectory = payload.InputDirectory ?? request.InputDirectory,
            OutputDirectory = payload.OutputDirectory ?? request.OutputDirectory,
            DatedRoot = payload.DatedRoot ?? string.Empty,
            Concurrency = payload.Concurrency,
            Retries = payload.Retries,
            PrepareOnly = payload.PrepareOnly,
            ColorCount = payload.ColorCount,
            SelectedColors = payload.SelectedColors ?? Array.Empty<string>(),
            PreparedBundles = payload.PreparedBundles?
                .Select(item => new SpBatchBundle
                {
                    SourceImage = item.SourceImage ?? string.Empty,
                    SpDirectory = item.SpDir ?? string.Empty,
                    MainDirectory = item.MainDir ?? string.Empty,
                    SkuDirectory = item.SkuDir ?? string.Empty,
                    DetailDirectory = item.DetailDir ?? string.Empty,
                    SourceCopyPath = item.SourceCopyPath ?? string.Empty
                })
                .ToArray() ?? Array.Empty<SpBatchBundle>(),
            Results = BuildJobResults(payload)
        };
    }

    private static string BuildRequestText(SpBatchRequest request)
    {
        var modeText = request.Mode switch
        {
            SpBatchMode.DryRun => "只出计划",
            SpBatchMode.PrepareOnly => "只建结构",
            _ => "正式生成"
        };

        return $"基于 {request.InputDirectory} 的图片，输出到 {request.OutputDirectory}，并发 {request.Concurrency}，重试 {request.Retries}，{modeText}";
    }

    private static IReadOnlyList<SpBatchJobResult> BuildJobResults(SpBatchPayload payload)
    {
        if (payload.Results is { Length: > 0 })
        {
            return payload.Results
                .Select(item => new SpBatchJobResult
                {
                    Index = item.Index,
                    SourceImage = item.SourceImage ?? string.Empty,
                    SourceCopyPath = item.SourceCopyPath ?? string.Empty,
                    SpDirectory = item.SpDir ?? string.Empty,
                    Color = item.Color ?? string.Empty,
                    Status = item.Status ?? string.Empty,
                    ImagePath = item.ImagePath ?? string.Empty,
                    Error = item.Error ?? string.Empty,
                    Attempts = item.Attempts
                })
                .ToArray();
        }

        if (payload.Jobs is { Length: > 0 })
        {
            return payload.Jobs
                .Select(item => new SpBatchJobResult
                {
                    Index = item.Index,
                    SourceImage = item.SourceImage ?? string.Empty,
                    SpDirectory = item.SpDir ?? string.Empty,
                    Color = item.Color ?? string.Empty,
                    Status = "planned",
                    ImagePath = item.OutputPath ?? string.Empty
                })
                .ToArray();
        }

        return Array.Empty<SpBatchJobResult>();
    }

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
        [JsonPropertyName("output_path")]
        public string? OutputPath { get; init; }
    }
}
