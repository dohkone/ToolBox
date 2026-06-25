namespace ImageKeeper.Core.Models;

public sealed class ImageItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public DateTime LastWriteTime { get; init; }
    public bool IsSelected { get; set; }
    public bool IsMoved { get; set; }
    public string ThumbnailCacheKey => $"{FilePath}|{LastWriteTime.Ticks}";
}
