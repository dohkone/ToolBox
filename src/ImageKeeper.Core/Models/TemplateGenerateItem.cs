namespace ImageKeeper.Core.Models;

public sealed class TemplateGenerateItem
{
    public int Index { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ImagePath { get; init; } = string.Empty;
}
