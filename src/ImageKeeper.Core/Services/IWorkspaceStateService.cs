using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface IWorkspaceStateService
{
    RootCardState GetOrCreateCardState(Guid rootNodeId);
    void SetCollapsed(Guid rootNodeId, bool isCollapsed);
    void SetSelectedNode(Guid rootNodeId, Guid? selectedNodeId);
}
