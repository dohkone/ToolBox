namespace ImageKeeper.Core.Models;

public sealed class NodeCounts
{
    public int Total { get; init; }
    public int Selected { get; init; }
    public int Unselected => Total - Selected;
}
