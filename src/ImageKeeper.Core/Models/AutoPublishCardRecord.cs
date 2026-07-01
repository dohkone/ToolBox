namespace ImageKeeper.Core.Models;

public sealed class AutoPublishCardRecord
{
    public long Id { get; init; }
    public string CardFolderPath { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public AutoPublishStatus Status { get; init; } = AutoPublishStatus.NotPublished;
    public string LastError { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? LastRunAt { get; init; }
}
