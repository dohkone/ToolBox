using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface IImageWorkspaceService
{
    NodeCounts CalculateCounts(FolderNode node);
    NodeCounts CalculateCounts(IEnumerable<FolderNode> nodes);
    void SetSelectionState(FolderNode node, bool isSelected);
    void InvertSelection(FolderNode node);
}
