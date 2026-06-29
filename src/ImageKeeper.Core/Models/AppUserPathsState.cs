namespace ImageKeeper.Core.Models;

public sealed class AppUserPathsState
{
    public string ReviewRootFolder { get; set; } = string.Empty;

    public string BackupFolder { get; set; } = string.Empty;

    public string TemplateLibraryPath { get; set; } = string.Empty;

    public string GenerationOutputDirectory { get; set; } = string.Empty;

    public string SpBatchInputDirectory { get; set; } = string.Empty;

    public string SpBatchOutputDirectory { get; set; } = string.Empty;
}
