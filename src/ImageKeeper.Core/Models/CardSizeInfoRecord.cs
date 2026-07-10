namespace ImageKeeper.Core.Models;

public sealed class CardSizeInfoRecord
{
    public string CardPath { get; init; } = string.Empty;
    public string SizeText { get; init; } = string.Empty;
    public string SizeRawInput { get; init; } = string.Empty;
    public string SizeImageHash { get; init; } = string.Empty;
    public string SizeImageLastWriteUtc { get; init; } = string.Empty;
    public DateTimeOffset SizeUpdatedAt { get; init; }
}
