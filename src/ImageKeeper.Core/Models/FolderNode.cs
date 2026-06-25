namespace ImageKeeper.Core.Models;

public sealed class FolderNode
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FolderPath { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int Depth { get; init; }
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public bool IsCollapsedCard { get; set; }
    public FolderNode? Parent { get; set; }
    public List<FolderNode> Children { get; } = [];
    public List<ImageItem> Images { get; } = [];
}
