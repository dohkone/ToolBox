namespace ImageKeeper.Core.Models;

public sealed class TemplateGenerateRequest
{
	public string TemplatePath { get; init; } = string.Empty;

	public string OutputDirectory { get; init; } = string.Empty;

	public string Image2ScriptPath { get; init; } = string.Empty;

	public string Material { get; init; } = "lychee_grain";

	public ImageTemplateType ImageType { get; init; }

	public int Count { get; init; }

	public int Concurrency { get; init; }

	public int? Seed { get; init; }

	public bool UniqueScene { get; init; }

	public bool PromptsOnly { get; init; }

	public IReadOnlyList<ColorTemplateColorRecord> ColorTemplateColors { get; init; } = Array.Empty<ColorTemplateColorRecord>();
}
