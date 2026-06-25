using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.Infrastructure.Services;

public sealed class WorkspaceStateService : IWorkspaceStateService
{
    private readonly Dictionary<Guid, RootCardState> _states = new();

    public RootCardState GetOrCreateCardState(Guid rootNodeId)
    {
        if (_states.TryGetValue(rootNodeId, out var state))
        {
            return state;
        }

        state = new RootCardState
        {
            RootNodeId = rootNodeId
        };
        _states[rootNodeId] = state;
        return state;
    }

    public void SetCollapsed(Guid rootNodeId, bool isCollapsed)
    {
        var state = GetOrCreateCardState(rootNodeId);
        state.IsCollapsed = isCollapsed;
    }

    public void SetSelectedNode(Guid rootNodeId, Guid? selectedNodeId)
    {
        var state = GetOrCreateCardState(rootNodeId);
        state.SelectedNodeId = selectedNodeId;
    }
}
