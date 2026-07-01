namespace ImageKeeper.Core.Models;

public sealed class MiaoshouPublishProgressEvent
{
    public string Type { get; init; } = string.Empty;
    public string CardPath { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public string Elapsed { get; init; } = string.Empty;
}
