namespace ImageKeeper.Core.Models;

public sealed class TemplateGenerateResult
{
    public bool Success { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string OutputDirectory { get; init; } = string.Empty;
    public IReadOnlyList<string> Prompts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TemplateGenerateItem> Items { get; init; } = Array.Empty<TemplateGenerateItem>();
}
