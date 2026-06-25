namespace ImageKeeper.Core.Models;

public sealed class WorkspaceTab
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "新标签页";
    public string RootFolder { get; set; } = string.Empty;
    public string BackupFolder { get; set; } = string.Empty;
    public bool IsScanning { get; set; }
    public bool IsBusy { get; set; }
    public bool HasSelectionChanges { get; set; }
    public Guid? SelectedNodeId { get; set; }
    public Guid? SelectedImageId { get; set; }
    public List<FolderNode> RootNodes { get; } = [];
}
