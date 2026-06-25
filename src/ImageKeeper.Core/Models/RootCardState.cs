namespace ImageKeeper.Core.Models;

public sealed class RootCardState
{
    public Guid RootNodeId { get; init; }
    public Guid? SelectedNodeId { get; set; }
    public bool IsCollapsed { get; set; }
}
