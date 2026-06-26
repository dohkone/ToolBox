namespace ImageKeeper.Core.Models;

public sealed class SpBatchRequest
{
    public string InputDirectory { get; init; } = string.Empty;
    public string OutputDirectory { get; init; } = string.Empty;
    public string Image2ScriptPath { get; init; } = string.Empty;
    public int Concurrency { get; init; } = 2;
    public int Retries { get; init; } = 4;
    public bool Overwrite { get; init; }
    public SpBatchMode Mode { get; init; } = SpBatchMode.Generate;
    public IReadOnlyList<string> SelectedColors { get; init; } = Array.Empty<string>();
}
