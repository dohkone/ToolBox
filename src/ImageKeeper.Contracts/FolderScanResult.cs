namespace ImageKeeper.Contracts;

public sealed class FolderScanResult
{
    public string RootFolder { get; init; } = string.Empty;
    public int FolderCount { get; init; }
    public int ImageCount { get; init; }
}
