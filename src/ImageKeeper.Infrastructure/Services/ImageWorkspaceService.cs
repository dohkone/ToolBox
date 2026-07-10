using System.Collections.Generic;
using System.Linq;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.Infrastructure.Services;

public sealed class ImageWorkspaceService : IImageWorkspaceService
{
	public NodeCounts CalculateCounts(FolderNode node)
	{
		int count = node.Images.Count;
		int selected = node.Images.Count((ImageItem i) => i.IsSelected);
		return new NodeCounts
		{
			Total = count,
			Selected = selected
		};
	}

	public NodeCounts CalculateCounts(IEnumerable<FolderNode> nodes)
	{
		int num = 0;
		int num2 = 0;
		foreach (FolderNode node in nodes)
		{
			NodeCounts nodeCounts = CalculateCountsRecursive(node);
			num += nodeCounts.Total;
			num2 += nodeCounts.Selected;
		}
		return new NodeCounts
		{
			Total = num,
			Selected = num2
		};
	}

	public void SetSelectionState(FolderNode node, bool isSelected)
	{
		foreach (ImageItem image in node.Images)
		{
			image.IsSelected = isSelected;
		}
	}

	public void InvertSelection(FolderNode node)
	{
		foreach (ImageItem image in node.Images)
		{
			image.IsSelected = !image.IsSelected;
		}
	}

	private static NodeCounts CalculateCountsRecursive(FolderNode node)
	{
		int num = node.Images.Count;
		int num2 = node.Images.Count((ImageItem i) => i.IsSelected);
		foreach (FolderNode child in node.Children)
		{
			NodeCounts nodeCounts = CalculateCountsRecursive(child);
			num += nodeCounts.Total;
			num2 += nodeCounts.Selected;
		}
		return new NodeCounts
		{
			Total = num,
			Selected = num2
		};
	}
}
