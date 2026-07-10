namespace ImageKeeper.Core.Models;

public sealed class SkuOptimizeRequest
{
    public string InputDirectory { get; init; } = string.Empty;
    public string OutputDirectory { get; init; } = string.Empty;
    public string Image2ScriptPath { get; init; } = string.Empty;
    public int Concurrency { get; init; } = 1;
    public double LengthMultiplier { get; init; } = 2.0;
    public double DiameterMultiplier { get; init; } = 0.67;
    public bool Overwrite { get; init; }
}
