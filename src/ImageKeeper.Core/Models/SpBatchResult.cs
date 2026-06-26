namespace ImageKeeper.Core.Models;

public sealed class SpBatchResult
{
    public bool Success { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string InputDirectory { get; init; } = string.Empty;
    public string OutputDirectory { get; init; } = string.Empty;
    public string DatedRoot { get; init; } = string.Empty;
    public int Concurrency { get; init; }
    public int Retries { get; init; }
    public bool PrepareOnly { get; init; }
    public int? ColorCount { get; init; }
    public IReadOnlyList<string> SelectedColors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SpBatchBundle> PreparedBundles { get; init; } = Array.Empty<SpBatchBundle>();
    public IReadOnlyList<SpBatchJobResult> Results { get; init; } = Array.Empty<SpBatchJobResult>();

    public int SuccessCount => Results.Count(item => string.Equals(item.Status, "generated", StringComparison.OrdinalIgnoreCase));
    public int SkippedCount => Results.Count(item => string.Equals(item.Status, "skipped", StringComparison.OrdinalIgnoreCase));
    public int FailedCount => Results.Count(item => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase));
}
