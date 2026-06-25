namespace ImageKeeper.Contracts;

public sealed class ProductSheetRequest
{
    public string SpRootFolder { get; init; } = string.Empty;
    public bool RebuildSizeIndexFirst { get; init; }
}
