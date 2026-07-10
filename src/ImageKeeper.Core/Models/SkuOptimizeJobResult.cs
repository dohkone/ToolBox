namespace ImageKeeper.Core.Models;

public sealed class SkuOptimizeJobResult
{
	public int Index { get; init; }

	public string SourceImage { get; init; } = string.Empty;

	public string Status { get; init; } = string.Empty;

	public string ImagePath { get; init; } = string.Empty;

	public string Error { get; init; } = string.Empty;

	public int Attempts { get; init; }
}
