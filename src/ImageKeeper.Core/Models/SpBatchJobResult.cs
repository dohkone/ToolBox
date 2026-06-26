namespace ImageKeeper.Core.Models;

public sealed class SpBatchJobResult
{
    public int Index { get; init; }
    public string SourceImage { get; init; } = string.Empty;
    public string SourceCopyPath { get; init; } = string.Empty;
    public string SpDirectory { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ImagePath { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public int Attempts { get; init; }
}
