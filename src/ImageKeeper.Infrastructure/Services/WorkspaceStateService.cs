using System;
using System.Collections.Generic;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.Infrastructure.Services;

public sealed class WorkspaceStateService : IWorkspaceStateService
{
	private readonly Dictionary<Guid, RootCardState> _states = new Dictionary<Guid, RootCardState>();

	public RootCardState GetOrCreateCardState(Guid rootNodeId)
	{
		if (_states.TryGetValue(rootNodeId, out RootCardState value))
		{
			return value;
		}
		value = new RootCardState
		{
			RootNodeId = rootNodeId
		};
		_states[rootNodeId] = value;
		return value;
	}

	public void SetCollapsed(Guid rootNodeId, bool isCollapsed)
	{
		GetOrCreateCardState(rootNodeId).IsCollapsed = isCollapsed;
	}

	public void SetSelectedNode(Guid rootNodeId, Guid? selectedNodeId)
	{
		GetOrCreateCardState(rootNodeId).SelectedNodeId = selectedNodeId;
	}
}
