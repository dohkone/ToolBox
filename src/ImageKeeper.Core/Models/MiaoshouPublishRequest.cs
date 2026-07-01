namespace ImageKeeper.Core.Models;

public sealed class MiaoshouPublishRequest
{
    public string ManifestPath { get; init; } = string.Empty;
    public string ResultPath { get; init; } = string.Empty;
    public string ConfigPath { get; init; } = string.Empty;
    public string EventsPath { get; init; } = string.Empty;
    public string LogPath { get; init; } = string.Empty;
    public Func<MiaoshouPublishProgressEvent, Task>? ProgressHandler { get; init; }
}
