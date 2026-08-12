namespace ImageKeeper.Core.Models;

public sealed class AppUserPathsState
{
	public string ReviewRootFolder { get; set; } = string.Empty;

	public string BackupFolder { get; set; } = string.Empty;

	public string TemplateLibraryPath { get; set; } = string.Empty;

	public string GenerationOutputDirectory { get; set; } = string.Empty;

	public string SpBatchInputDirectory { get; set; } = string.Empty;

	public string SpBatchOutputDirectory { get; set; } = string.Empty;

	public string SkuOptimizeOutputDirectory { get; set; } = string.Empty;

	public string ImageGenerationProvider { get; set; } = string.Empty;

	public bool TitleChineseOnly { get; set; }

	public string GenerationMaterial { get; set; } = string.Empty;

	public long SelectedSpBatchColorTemplateGroupId { get; set; }

	public Dictionary<long, string[]> SpBatchSelectedColorNamesByGroupId { get; set; } = new Dictionary<long, string[]>();
}
