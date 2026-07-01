using System.Diagnostics;
using System.Text.Json;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class MiaoshouPublishService : IMiaoshouPublishService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
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

    public async Task<MiaoshouPublishResult> PublishAsync(
        MiaoshouPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_workingDirectory))
        {
            throw new DirectoryNotFoundException($"Miaoshou Playwright directory not found: {_workingDirectory}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = _nodeExePath,
            WorkingDirectory = _workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var tsNodeEntry = Path.Combine(_workingDirectory, "node_modules", "ts-node", "dist", "bin.js");
        if (!File.Exists(tsNodeEntry))
        {
            throw new FileNotFoundException("ts-node entry not found.", tsNodeEntry);
        }

        var bundledBrowsersPath = Path.Combine(AppContext.BaseDirectory, "runtime", "playwright-browsers");
        if (Directory.Exists(bundledBrowsersPath))
        {
            psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] = bundledBrowsersPath;
        }

        psi.ArgumentList.Add(tsNodeEntry);
        psi.ArgumentList.Add("src/open-miaoshou.ts");
        psi.ArgumentList.Add("--manifest");
        psi.ArgumentList.Add(request.ManifestPath);
        psi.ArgumentList.Add("--result");
        psi.ArgumentList.Add(request.ResultPath);
        psi.ArgumentList.Add("--config");
        psi.ArgumentList.Add(request.ConfigPath);
        psi.ArgumentList.Add("--events");
        psi.ArgumentList.Add(request.EventsPath);
        psi.ArgumentList.Add("--log");
        psi.ArgumentList.Add(request.LogPath);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start Miaoshou Playwright process.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var resultWaitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var resultTask = WaitForResultFileAsync(request.ResultPath, resultWaitCancellation.Token);
        using var eventsCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var eventsTask = WatchProgressEventsAsync(request, eventsCancellation.Token);
        var exitTask = process.WaitForExitAsync(cancellationToken);

        var completedTask = await Task.WhenAny(resultTask, exitTask);
        if (completedTask == resultTask && await resultTask)
        {
            var result = await ReadResultAsync(request, cancellationToken);
            resultWaitCancellation.Cancel();
            eventsCancellation.Cancel();
            await IgnoreCancellationAsync(eventsTask);
            return result;
        }

        resultWaitCancellation.Cancel();
        await exitTask;
        eventsCancellation.Cancel();
        await IgnoreCancellationAsync(eventsTask);
        await outputTask;
        var error = await errorTask;

        if (File.Exists(request.ResultPath))
        {
            return await ReadResultAsync(request, cancellationToken);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? $"Miaoshou Playwright failed with exit code {process.ExitCode}."
                : error.Trim());
        }

        return new MiaoshouPublishResult
        {
            Status = "success",
            ResultPath = request.ResultPath,
            LogPath = request.LogPath
        };
    }

    private static async Task<bool> WaitForResultFileAsync(
        string resultPath,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(resultPath))
            {
                try
                {
                    await using var stream = File.Open(resultPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (stream.Length > 0)
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                    // The writer may still be flushing the result file. Try again shortly.
                }
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
    }

    private static async Task<MiaoshouPublishResult> ReadResultAsync(
        MiaoshouPublishRequest request,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(request.ResultPath, cancellationToken);
        var result = JsonSerializer.Deserialize<MiaoshouPublishResult>(json, JsonOptions) ?? new MiaoshouPublishResult();
        result.ResultPath = request.ResultPath;
        result.LogPath = request.LogPath;
        return result;
    }

    private static async Task WatchProgressEventsAsync(
        MiaoshouPublishRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProgressHandler is null || string.IsNullOrWhiteSpace(request.EventsPath))
        {
            return;
        }

        var processedLineCount = 0;
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

            for (var index = processedLineCount; index < lines.Length; index++)
            {
                await HandleProgressEventLineAsync(lines[index], request.ProgressHandler, cancellationToken);
            }

            processedLineCount = lines.Length;
            await Task.Delay(300, cancellationToken);
        }
    }

    private static async Task HandleProgressEventLineAsync(
        string line,
        Func<MiaoshouPublishProgressEvent, Task> progressHandler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        MiaoshouPublishProgressEvent? progressEvent;
        try
        {
            progressEvent = JsonSerializer.Deserialize<MiaoshouPublishProgressEvent>(line, JsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        if (progressEvent is null
            || string.IsNullOrWhiteSpace(progressEvent.CardPath)
            || !IsProductFinishedEvent(progressEvent.Type)
            || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await progressHandler(progressEvent);
    }

    private static bool IsProductFinishedEvent(string eventType)
    {
        return string.Equals(eventType, "product_success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, "product_failed", StringComparison.OrdinalIgnoreCase);
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
