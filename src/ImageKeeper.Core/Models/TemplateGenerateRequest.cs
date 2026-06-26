namespace ImageKeeper.Core.Models;

public sealed class TemplateGenerateRequest
{
    public string TemplatePath { get; init; } = string.Empty;
    public string OutputDirectory { get; init; } = string.Empty;
    public string Image2ScriptPath { get; init; } = string.Empty;
    public int Count { get; init; }
    public int Concurrency { get; init; }
    public int? Seed { get; init; }
    public bool UniqueScene { get; init; }
    public bool PromptsOnly { get; init; }
}
