namespace ImageKeeper.Contracts;

public sealed class ImageOperationResult
{
    public bool Success { get; init; }
    public int ProcessedCount { get; init; }
    public int FailedCount { get; init; }
    public string Message { get; init; } = string.Empty;
}
