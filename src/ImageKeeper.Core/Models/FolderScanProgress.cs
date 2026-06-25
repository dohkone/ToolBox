namespace ImageKeeper.Core.Models;

public sealed class FolderScanProgress
{
    public string Stage { get; init; } = string.Empty;
    public string CurrentFolder { get; init; } = string.Empty;
    public int ProcessedFolders { get; init; }
    public int TotalFolders { get; init; }
    public int ImageCount { get; init; }
    public int SkippedFolders { get; init; }
    public double Percent => TotalFolders <= 0 ? 0 : Math.Min(100, ProcessedFolders * 100.0 / TotalFolders);
}
