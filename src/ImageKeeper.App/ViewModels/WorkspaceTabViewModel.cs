using System.Collections.ObjectModel;
using System.IO;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.App.ViewModels;

public sealed class WorkspaceTabViewModel : ViewModelBase
{
    private readonly IImageWorkspaceService _imageWorkspaceService;
    private readonly IWorkspaceStateService _workspaceStateService;
    private readonly IProductSheetService? _productSheetService;
    private readonly Func<bool>? _isAutoPublishBusyProvider;
    private readonly Func<Func<Task>, Task>? _runExclusiveAsync;
    private readonly RelayCommand _activateCommand;
    private readonly RelayCommand _closeCommand;
    private string _rootFolder;
    private string _backupFolder;
    private string _title;
    private bool _isSelected;

    public WorkspaceTabViewModel(
        string rootFolder,
        string backupFolder,
        IImageWorkspaceService imageWorkspaceService,
        IWorkspaceStateService workspaceStateService,
        IProductSheetService? productSheetService = null,
        Func<bool>? isAutoPublishBusyProvider = null,
        Func<Func<Task>, Task>? runExclusiveAsync = null)
    {
        _rootFolder = rootFolder;
        _backupFolder = backupFolder;
        _imageWorkspaceService = imageWorkspaceService;
        _workspaceStateService = workspaceStateService;
        _productSheetService = productSheetService;
        _isAutoPublishBusyProvider = isAutoPublishBusyProvider;
        _runExclusiveAsync = runExclusiveAsync;
        _title = BuildTitle(rootFolder);
        _activateCommand = new RelayCommand(_ => RequestedActivate?.Invoke(this));
        _closeCommand = new RelayCommand(_ => RequestedClose?.Invoke(this));
    }

    public event Action<WorkspaceTabViewModel>? RequestedActivate;

    public event Action<WorkspaceTabViewModel>? RequestedClose;

    public event Action<PreviewRequest?>? PreviewRequested;

    public event Action<string>? StatusChanged;

    public event Action<WorkspaceTabViewModel>? SelectionContextChanged;

    public ObservableCollection<RootCardViewModel> RootCards { get; } = [];

    public RelayCommand ActivateCommand => _activateCommand;

    public RelayCommand CloseCommand => _closeCommand;

    public string RootFolder
    {
        get => _rootFolder;
        private set
        {
            if (!SetProperty(ref _rootFolder, value))
            {
                return;
            }

            OnPropertyChanged(nameof(LoadedFolderText));
            OnPropertyChanged(nameof(IsPlaceholder));
        }
    }

    public string BackupFolder
    {
        get => _backupFolder;
        private set
        {
            if (!SetProperty(ref _backupFolder, value))
            {
                return;
            }

            foreach (var card in RootCards)
            {
                card.SetBackupFolder(value);
            }
        }
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool HasRootCards => RootCards.Count > 0;

    public bool IsPlaceholder => string.IsNullOrWhiteSpace(RootFolder);

    public RootCardViewModel? ActiveCard { get; private set; }

    public string LoadedFolderText => string.IsNullOrWhiteSpace(RootFolder)
        ? "当前文件夹：未选择"
        : $"当前父目录：{RootFolder}";

    public NodeCounts GetTabCounts()
    {
        return _imageWorkspaceService.CalculateCounts(RootCards.Select(card => card.RootNode.Model));
    }

    public void SetRootFolder(string rootFolder)
    {
        RootFolder = rootFolder;
        Title = BuildTitle(rootFolder);
    }

    public void SetRootNodes(IEnumerable<FolderNode> nodes)
    {
        foreach (var existingCard in RootCards)
        {
            existingCard.PreviewRequested -= OnCardPreviewRequested;
            existingCard.StatusChanged -= OnCardStatusChanged;
            existingCard.Activated -= OnCardActivated;
            existingCard.SelectionContextChanged -= OnCardSelectionContextChanged;
        }

        RootCards.Clear();
        ActiveCard = null;

        foreach (var node in nodes)
        {
            var card = new RootCardViewModel(
                node,
                _imageWorkspaceService,
                _workspaceStateService,
                BackupFolder,
                _productSheetService,
                _isAutoPublishBusyProvider,
                _runExclusiveAsync);
            card.PreviewRequested += OnCardPreviewRequested;
            card.StatusChanged += OnCardStatusChanged;
            card.Activated += OnCardActivated;
            card.SelectionContextChanged += OnCardSelectionContextChanged;
            RootCards.Add(card);
        }

        if (RootCards.Count > 0)
        {
            SetActiveCard(RootCards[0]);
        }

        OnPropertyChanged(nameof(HasRootCards));
    }

    public void SetBackupFolder(string backupFolder)
    {
        BackupFolder = backupFolder;
    }

    public void RestoreDefaultSelection()
    {
        if (RootCards.Count == 0)
        {
            return;
        }

        foreach (var card in RootCards)
        {
            card.RestoreOrSelectDefaultNode(activateCard: false);
        }

        var preferredCard = RootCards.FirstOrDefault(card => card.GetFirstNodeWithImages() is not null)
            ?? RootCards[0];

        SetActiveCard(preferredCard);
    }

    public string GetSelectionSummaryText()
    {
        if (ActiveCard?.SelectedNode is not null)
        {
            return ActiveCard.SelectionSummaryText;
        }

        var counts = GetTabCounts();
        return $"图片 {counts.Total} 张，已选中 {counts.Selected} 张，未选中 {counts.Unselected} 张";
    }

    public string GetCurrentFolderText()
    {
        if (ActiveCard?.SelectedNode is not null)
        {
            return $"当前文件夹：{ActiveCard.CurrentFolderPath}";
        }

        return string.IsNullOrWhiteSpace(RootFolder)
            ? "当前文件夹：未选择"
            : $"当前父目录：{RootFolder}";
    }

    public void InvertSelection()
    {
        ActiveCard?.InvertSelectionFromToolbar();
    }

    public void NotifyAutoPublishStateChanged()
    {
        foreach (var card in RootCards)
        {
            card.NotifyAutoPublishStateChanged();
        }
    }

    private void OnCardPreviewRequested(PreviewRequest? request)
    {
        PreviewRequested?.Invoke(request);
    }

    private void OnCardStatusChanged(string message)
    {
        StatusChanged?.Invoke(message);
    }

    private void OnCardActivated(RootCardViewModel card)
    {
        SetActiveCard(card);
    }

    private void OnCardSelectionContextChanged(RootCardViewModel card)
    {
        if (ActiveCard == card)
        {
            SelectionContextChanged?.Invoke(this);
        }
    }

    private void SetActiveCard(RootCardViewModel? card)
    {
        if (ActiveCard == card)
        {
            return;
        }

        if (ActiveCard is not null)
        {
            ActiveCard.IsActive = false;
        }

        ActiveCard = card;

        if (ActiveCard is not null)
        {
            ActiveCard.IsActive = true;
        }

        SelectionContextChanged?.Invoke(this);
    }

    private static string BuildTitle(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder))
        {
            return "新标签页";
        }

        var name = Path.GetFileName(rootFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? rootFolder : name;
    }
}
