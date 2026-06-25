namespace ImageKeeper.App.ViewModels;

public sealed class PreviewRequest
{
    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    public required string FolderPath { get; init; }

    public long FileSize { get; init; }
}
