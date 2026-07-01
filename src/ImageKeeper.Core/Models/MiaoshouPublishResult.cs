namespace ImageKeeper.Core.Models;

public sealed class MiaoshouPublishResult
{
    public string Status { get; set; } = string.Empty;
    public int Total { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public string Error { get; set; } = string.Empty;
    public string ResultPath { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
    public List<MiaoshouPublishItemResult> Results { get; set; } = [];
}
