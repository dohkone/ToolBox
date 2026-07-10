using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class SkuOptimizeService : ISkuOptimizeService
{
    private readonly string _pythonExePath;
    private readonly string _scriptPath;
    private readonly object _processSyncRoot = new();
    private Process? _currentProcess;

    public SkuOptimizeService(string pythonExePath, string scriptPath)
    {
        _pythonExePath = pythonExePath;
        _scriptPath = scriptPath;
    }

    public async Task<SkuOptimizeResult> GenerateAsync(SkuOptimizeRequest request, CancellationToken cancellationToken = default)
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
        startInfo.ArgumentList.Add("--input-dir");
        startInfo.ArgumentList.Add(request.InputDirectory);
        startInfo.ArgumentList.Add("--output-dir");
        startInfo.ArgumentList.Add(request.OutputDirectory);
        startInfo.ArgumentList.Add("--image2-script");
        startInfo.ArgumentList.Add(request.Image2ScriptPath);
        startInfo.ArgumentList.Add("--concurrency");
        startInfo.ArgumentList.Add(request.Concurrency.ToString());
        startInfo.ArgumentList.Add("--length-multiplier");
        startInfo.ArgumentList.Add(request.LengthMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--diameter-multiplier");
        startInfo.ArgumentList.Add(request.DiameterMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (request.Overwrite)
        {
            startInfo.ArgumentList.Add("--overwrite");
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 SKU 图优化脚本。");
        RegisterRunningProcess(process, cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

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

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "SKU 图优化脚本没有返回结果。" : stderr);
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        SkuOptimizePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SkuOptimizePayload>(stdout, options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"SKU 图优化结果解析失败：{ex.Message}");
        }

        if (payload is null)
        {
            throw new InvalidOperationException("SKU 图优化返回结果为空。");
        }

        if (process.ExitCode != 0 && (payload.Results?.Length ?? 0) == 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "SKU 图优化执行失败。" : stderr);
        }

        return new SkuOptimizeResult
        {
            Success = process.ExitCode == 0 && (payload.Results?.All(item => !string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase)) ?? true),
            InputDirectory = payload.InputDirectory ?? request.InputDirectory,
            OutputDirectory = payload.OutputDirectory ?? request.OutputDirectory,
            ResultRoot = payload.ResultRoot ?? string.Empty,
            Concurrency = payload.Concurrency,
            LengthMultiplier = payload.LengthMultiplier,
            DiameterMultiplier = payload.DiameterMultiplier,
            Results = payload.Results?.Select(item => new SkuOptimizeJobResult
            {
                Index = item.Index,
                SourceImage = item.SourceImage ?? string.Empty,
                Status = item.Status ?? string.Empty,
                ImagePath = item.ImagePath ?? string.Empty,
                Error = item.Error ?? string.Empty,
                Attempts = item.Attempts
            }).ToArray() ?? Array.Empty<SkuOptimizeJobResult>()
        };
    }

    public void CancelCurrentRun()
    {
        lock (_processSyncRoot)
        {
            if (_currentProcess is null)
            {
                return;
            }

            TryKillProcessTree(_currentProcess);
        }
    }

    private void RegisterRunningProcess(Process process, CancellationToken cancellationToken)
    {
        lock (_processSyncRoot)
        {
            _currentProcess = process;
        }

        cancellationToken.Register(() => TryKillProcessTree(process));
    }

    private void ClearRunningProcess(Process process)
    {
        lock (_processSyncRoot)
        {
            if (ReferenceEquals(_currentProcess, process))
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
}
