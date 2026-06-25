namespace ImageKeeper.Contracts;

public sealed class ProductSheetResult
{
    public bool Success { get; init; }
    public string OutputPath { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
