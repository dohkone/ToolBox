using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.Infrastructure.Services;

public sealed class ImageWorkspaceService : IImageWorkspaceService
{
    public NodeCounts CalculateCounts(FolderNode node)
    {
        var total = node.Images.Count;
        var selected = node.Images.Count(i => i.IsSelected);
        return new NodeCounts
        {
            Total = total,
            Selected = selected
        };
    }

    public NodeCounts CalculateCounts(IEnumerable<FolderNode> nodes)
    {
        var total = 0;
        var selected = 0;

        foreach (var node in nodes)
        {
            var counts = CalculateCountsRecursive(node);
            total += counts.Total;
            selected += counts.Selected;
        }

        return new NodeCounts
        {
            Total = total,
            Selected = selected
        };
    }

    public void SetSelectionState(FolderNode node, bool isSelected)
    {
        foreach (var image in node.Images)
        {
            image.IsSelected = isSelected;
        }
    }

    public void InvertSelection(FolderNode node)
    {
        foreach (var image in node.Images)
        {
            image.IsSelected = !image.IsSelected;
        }
    }

    private static NodeCounts CalculateCountsRecursive(FolderNode node)
    {
        var total = node.Images.Count;
        var selected = node.Images.Count(i => i.IsSelected);

        foreach (var child in node.Children)
        {
            var childCounts = CalculateCountsRecursive(child);
            total += childCounts.Total;
            selected += childCounts.Selected;
        }

        return new NodeCounts
        {
            Total = total,
            Selected = selected
        };
    }
}
