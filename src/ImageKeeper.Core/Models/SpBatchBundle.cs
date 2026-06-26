namespace ImageKeeper.Core.Models;

public sealed class SpBatchBundle
{
    public string SourceImage { get; init; } = string.Empty;
    public string SpDirectory { get; init; } = string.Empty;
    public string MainDirectory { get; init; } = string.Empty;
    public string SkuDirectory { get; init; } = string.Empty;
    public string DetailDirectory { get; init; } = string.Empty;
    public string SourceCopyPath { get; init; } = string.Empty;
}
