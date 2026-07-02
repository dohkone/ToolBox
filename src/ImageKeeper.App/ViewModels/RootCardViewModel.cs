using System.Collections.ObjectModel;
using System.IO;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;
using ImageKeeper.Core.Utilities;
using Media = System.Windows.Media;

namespace ImageKeeper.App.ViewModels;

public sealed class RootCardViewModel : ViewModelBase
{
    private const int ImagePageSize = 60;

    private readonly FolderNodeViewModel _rootNode;
    private readonly IImageWorkspaceService _workspaceService;
    private readonly IWorkspaceStateService _workspaceStateService;
    private readonly RootCardState _cardState;
    private readonly RelayCommand _invertSelectionCommand;
    private readonly RelayCommand _selectAllCommand;
    private readonly RelayCommand _clearSelectionCommand;
    private readonly RelayCommand _collapseCommand;
    private readonly RelayCommand _expandCommand;
    private readonly AsyncRelayCommand _addImagesCommand;
    private readonly AsyncRelayCommand _copyMainToDetailCommand;
    private readonly AsyncRelayCommand _moveSelectedCommand;
    private readonly AsyncRelayCommand _autoPublishCommand;
    private readonly RelayCommand _toggleExpandCommand;
    private readonly RelayCommand _selectNodeCommand;
    private readonly RelayCommand _loadMoreImagesCommand;
    private readonly IProductSheetService? _productSheetService;
    private readonly Func<bool>? _isAutoPublishBusyProvider;
    private readonly Func<Func<Task>, Task>? _runExclusiveAsync;
    private readonly Func<RootCardViewModel, AutoPublishStatus, string, Task>? _setAutoPublishStatusAsync;
    private readonly Func<RootCardViewModel, Task>? _runAutoPublishCardAsync;
    private readonly Action? _batchSelectionChanged;
    private FolderNodeViewModel? _selectedNode;
    private bool _isCollapsed;
    private bool _isActive;
    private bool _isDropTarget;
    private bool _isBatchSelected;
    private int _totalCount;
    private int _selectedCount;
    private int _loadedImageCount;
    private string _backupFolder;
    private AutoPublishStatus _autoPublishStatus = AutoPublishStatus.NotPublished;
    private string _autoPublishLastError = string.Empty;

    public RootCardViewModel(
        FolderNode rootNode,
        IImageWorkspaceService workspaceService,
        IWorkspaceStateService workspaceStateService,
        string backupFolder,
        IProductSheetService? productSheetService = null,
        Func<bool>? isAutoPublishBusyProvider = null,
        Func<Func<Task>, Task>? runExclusiveAsync = null,
        Func<RootCardViewModel, AutoPublishStatus, string, Task>? setAutoPublishStatusAsync = null,
        Func<RootCardViewModel, Task>? runAutoPublishCardAsync = null,
        Action? batchSelectionChanged = null)
    {
        _workspaceService = workspaceService;
        _workspaceStateService = workspaceStateService;
        _backupFolder = backupFolder;
        _productSheetService = productSheetService;
        _isAutoPublishBusyProvider = isAutoPublishBusyProvider;
        _runExclusiveAsync = runExclusiveAsync;
        _setAutoPublishStatusAsync = setAutoPublishStatusAsync;
        _runAutoPublishCardAsync = runAutoPublishCardAsync;
        _batchSelectionChanged = batchSelectionChanged;
        _rootNode = new FolderNodeViewModel(rootNode);
        _cardState = _workspaceStateService.GetOrCreateCardState(rootNode.Id);
        _isCollapsed = _cardState.IsCollapsed;

        _invertSelectionCommand = new RelayCommand(_ => InvertSelection(), _ => SelectedNode is not null && TotalImageCount > 0);
        _selectAllCommand = new RelayCommand(_ => SetSelectionState(true), _ => SelectedNode is not null && TotalImageCount > 0);
        _clearSelectionCommand = new RelayCommand(_ => SetSelectionState(false), _ => SelectedNode is not null && TotalImageCount > 0);
        _collapseCommand = new RelayCommand(_ => SetCollapsed(true));
        _expandCommand = new RelayCommand(_ =>
        {
            SetCollapsed(false);
            Activate();
        });
        _addImagesCommand = new AsyncRelayCommand(_ => AddImagesAsync(), _ => SelectedNode is not null);
        _copyMainToDetailCommand = new AsyncRelayCommand(_ => CopyMainToDetailAsync(), _ => CanCopyMainToDetail());
        _moveSelectedCommand = new AsyncRelayCommand(_ => MoveSelectedAsync(), _ => SelectedNode is not null && SelectedCount > 0);
        _autoPublishCommand = new AsyncRelayCommand(_ => AutoPublishAsync(), _ => CanAutoPublish());
        _toggleExpandCommand = new RelayCommand(node => ToggleExpand(node as FolderNodeViewModel), node => node is FolderNodeViewModel folderNode && folderNode.HasChildren);
        _selectNodeCommand = new RelayCommand(node => SelectNode(node as FolderNodeViewModel), node => node is FolderNodeViewModel);
        _loadMoreImagesCommand = new RelayCommand(_ => LoadMoreImages(), _ => HasMoreImages);

        RebuildVisibleNodes();
        RestoreOrSelectDefaultNode(activateCard: false);
    }

    public event Action<PreviewRequest?>? PreviewRequested;

    public event Action<string>? StatusChanged;

    public event Action<RootCardViewModel>? Activated;

    public event Action<RootCardViewModel>? SelectionContextChanged;

    public string DisplayName => _rootNode.DisplayName;

    public string RootFolderPath => _rootNode.FolderPath;

    public string AutoPublishKeyPath => GetSpRootFolder() ?? RootFolderPath;

    public ObservableCollection<FolderNodeViewModel> VisibleNodes { get; } = [];

    public ObservableCollection<ImageItemViewModel> CurrentImages { get; } = [];

    public RelayCommand InvertSelectionCommand => _invertSelectionCommand;

    public RelayCommand SelectAllCommand => _selectAllCommand;

    public RelayCommand ClearSelectionCommand => _clearSelectionCommand;

    public RelayCommand CollapseCommand => _collapseCommand;

    public RelayCommand ExpandCommand => _expandCommand;

    public AsyncRelayCommand AddImagesCommand => _addImagesCommand;

    public AsyncRelayCommand CopyMainToDetailCommand => _copyMainToDetailCommand;

    public AsyncRelayCommand MoveSelectedCommand => _moveSelectedCommand;

    public AsyncRelayCommand AutoPublishCommand => _autoPublishCommand;

    public RelayCommand ToggleExpandCommand => _toggleExpandCommand;

    public RelayCommand SelectNodeCommand => _selectNodeCommand;

    public RelayCommand LoadMoreImagesCommand => _loadMoreImagesCommand;

    public FolderNodeViewModel RootNode => _rootNode;

    public IReadOnlyList<FolderNodeViewModel> ChildFolders => _rootNode.Children;

    public FolderNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set
        {
            if (!SetProperty(ref _selectedNode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CurrentFolderTitle));
            OnPropertyChanged(nameof(CurrentFolderPath));
            OnPropertyChanged(nameof(CurrentFolderMetaText));
            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(ContentEmptyText));
            OnPropertyChanged(nameof(AutoPublishKeyPath));
            OnPropertyChanged(nameof(CollapsedSummaryText));
            RefreshCurrentImages();
        }
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        private set
        {
            if (!SetProperty(ref _isCollapsed, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CollapsedSummaryText));
            OnPropertyChanged(nameof(CanBatchSelect));

            if (!value)
            {
                IsBatchSelected = false;
            }

            _batchSelectionChanged?.Invoke();
        }
    }

    public bool IsBatchSelected
    {
        get => _isBatchSelected;
        set
        {
            if (!SetProperty(ref _isBatchSelected, value))
            {
                return;
            }

            _batchSelectionChanged?.Invoke();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (!SetProperty(ref _isActive, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CardBorderBrush));
        }
    }

    public bool IsDropTarget
    {
        get => _isDropTarget;
        private set
        {
            if (!SetProperty(ref _isDropTarget, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ContentBorderBrush));
            OnPropertyChanged(nameof(ContentBackgroundBrush));
        }
    }

    public string BackupFolder
    {
        get => _backupFolder;
        private set => SetProperty(ref _backupFolder, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set
        {
            if (SetProperty(ref _totalCount, value))
            {
                OnPropertyChanged(nameof(CountsText));
                OnPropertyChanged(nameof(SelectionSummaryText));
                OnPropertyChanged(nameof(UnselectedCount));
            }
        }
    }

    public int SelectedCount
    {
        get => _selectedCount;
        private set
        {
            if (SetProperty(ref _selectedCount, value))
            {
                OnPropertyChanged(nameof(CountsText));
                OnPropertyChanged(nameof(SelectionSummaryText));
                OnPropertyChanged(nameof(UnselectedCount));
                _moveSelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int UnselectedCount => TotalCount - SelectedCount;

    public int LoadedImageCount
    {
        get => _loadedImageCount;
        private set
        {
            if (!SetProperty(ref _loadedImageCount, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ImageLoadSummaryText));
            OnPropertyChanged(nameof(HasMoreImages));
            _loadMoreImagesCommand.RaiseCanExecuteChanged();
        }
    }

    public int TotalImageCount => SelectedNode?.Model.Images.Count ?? 0;

    public bool HasMoreImages => LoadedImageCount < TotalImageCount;

    public string ImageLoadSummaryText => TotalImageCount == 0
        ? "当前目录没有图片"
        : $"已显示 {LoadedImageCount} / 共 {TotalImageCount} 张";

    public string CurrentFolderTitle => SelectedNode?.DisplayName ?? DisplayName;

    public string CurrentFolderPath => SelectedNode?.FolderPath ?? RootFolderPath;

    public string CurrentFolderHeaderText => string.IsNullOrWhiteSpace(CurrentFolderPath)
        ? CurrentFolderTitle
        : $"{CurrentFolderTitle}  {CurrentFolderPath}";

    public string CurrentFolderMetaText => SelectedNode is null
        ? "从左侧目录选择一个文件夹后，这里显示该目录下的图片。"
        : CurrentFolderPath;

    public string CountsText => $"共 {TotalCount} 张 / 已选中 {SelectedCount} 张 / 未选中 {UnselectedCount} 张";

    public string SelectionSummaryText => SelectedNode is null
        ? "请选择左侧目录中的文件夹。"
        : $"当前目录：{CurrentFolderTitle}，共 {TotalCount} 张，已选中 {SelectedCount} 张，未选中 {UnselectedCount} 张";

    public bool HasImages => CurrentImages.Count > 0;

    public string ContentEmptyText => SelectedNode is null
        ? "从左侧目录选择一个文件夹后，这里会显示该目录下的图片。"
        : "当前文件夹没有图片。可以点击“添加图片”，或直接拖拽图片到这里。";

    public string CollapsedSummaryText => $"{DisplayName}    {AutoPublishKeyPath}";

    public bool IsAutoPublishBusy => _isAutoPublishBusyProvider?.Invoke() ?? false;

    public bool CanBatchSelect => IsCollapsed && !string.IsNullOrWhiteSpace(GetSpRootFolder());

    public AutoPublishStatus AutoPublishStatus
    {
        get => _autoPublishStatus;
        private set
        {
            if (!SetProperty(ref _autoPublishStatus, value))
            {
                return;
            }

            OnPropertyChanged(nameof(AutoPublishStatusText));
            OnPropertyChanged(nameof(AutoPublishStatusForeground));
            OnPropertyChanged(nameof(AutoPublishStatusBackground));
            OnPropertyChanged(nameof(AutoPublishStatusBorderBrush));
        }
    }

    public string AutoPublishLastError
    {
        get => _autoPublishLastError;
        private set => SetProperty(ref _autoPublishLastError, value);
    }

    public string AutoPublishStatusText => AutoPublishStatus switch
    {
        AutoPublishStatus.Publishing => "上架中",
        AutoPublishStatus.Success => "上架成功",
        AutoPublishStatus.Failed => "上架失败",
        _ => "未上架"
    };

    public Media.Brush AutoPublishStatusForeground => AutoPublishStatus switch
    {
        AutoPublishStatus.Publishing => new Media.SolidColorBrush(Media.Color.FromRgb(45, 106, 227)),
        AutoPublishStatus.Success => new Media.SolidColorBrush(Media.Color.FromRgb(103, 194, 58)),
        AutoPublishStatus.Failed => new Media.SolidColorBrush(Media.Color.FromRgb(245, 108, 108)),
        _ => new Media.SolidColorBrush(Media.Color.FromRgb(124, 138, 165))
    };

    public Media.Brush AutoPublishStatusBackground => AutoPublishStatus switch
    {
        AutoPublishStatus.Publishing => new Media.SolidColorBrush(Media.Color.FromRgb(234, 241, 255)),
        AutoPublishStatus.Success => new Media.SolidColorBrush(Media.Color.FromRgb(240, 249, 235)),
        AutoPublishStatus.Failed => new Media.SolidColorBrush(Media.Color.FromRgb(254, 240, 240)),
        _ => new Media.SolidColorBrush(Media.Color.FromRgb(246, 248, 252))
    };

    public Media.Brush AutoPublishStatusBorderBrush => AutoPublishStatus switch
    {
        AutoPublishStatus.Publishing => new Media.SolidColorBrush(Media.Color.FromRgb(159, 190, 255)),
        AutoPublishStatus.Success => new Media.SolidColorBrush(Media.Color.FromRgb(205, 231, 176)),
        AutoPublishStatus.Failed => new Media.SolidColorBrush(Media.Color.FromRgb(252, 196, 196)),
        _ => new Media.SolidColorBrush(Media.Color.FromRgb(217, 225, 236))
    };

    public Media.Brush CardBorderBrush => IsActive
        ? new Media.SolidColorBrush(Media.Color.FromRgb(159, 190, 255))
        : new Media.SolidColorBrush(Media.Color.FromRgb(217, 225, 236));

    public Media.Brush ContentBorderBrush => IsDropTarget
        ? new Media.SolidColorBrush(Media.Color.FromRgb(45, 106, 227))
        : new Media.SolidColorBrush(Media.Color.FromRgb(217, 225, 236));

    public Media.Brush ContentBackgroundBrush => IsDropTarget
        ? new Media.SolidColorBrush(Media.Color.FromRgb(238, 244, 255))
        : new Media.SolidColorBrush(Media.Color.FromRgb(255, 255, 255));

    public void SetBackupFolder(string backupFolder)
    {
        BackupFolder = backupFolder;
    }

    public void NotifyAutoPublishStateChanged()
    {
        OnPropertyChanged(nameof(IsAutoPublishBusy));
        _autoPublishCommand.RaiseCanExecuteChanged();
        _batchSelectionChanged?.Invoke();
    }

    public void ApplyAutoPublishRecord(AutoPublishCardRecord? record)
    {
        AutoPublishStatus = record?.Status ?? AutoPublishStatus.NotPublished;
        AutoPublishLastError = record?.LastError ?? string.Empty;
    }

    public void SetAutoPublishStatus(AutoPublishStatus status, string lastError = "")
    {
        AutoPublishStatus = status;
        AutoPublishLastError = lastError;
    }

    public void InvertSelectionFromToolbar()
    {
        Activate();
        InvertSelection();
    }

    public void Activate()
    {
        Activated?.Invoke(this);
    }

    public void RestoreOrSelectDefaultNode(bool activateCard)
    {
        var targetNode = FindNodeById(_cardState.SelectedNodeId)
            ?? FindFirstNodeWithImages(ChildFolders)
            ?? ChildFolders.FirstOrDefault()
            ?? _rootNode;

        SelectNode(targetNode, activateCard);
    }

    public FolderNodeViewModel? GetFirstNodeWithImages()
    {
        return FindFirstNodeWithImages(ChildFolders) ?? (_rootNode.Model.Images.Count > 0 ? _rootNode : null);
    }

    public bool CanAcceptDrop(IReadOnlyList<string> files)
    {
        return files.Count > 0
            && SelectedNode is not null
            && Directory.Exists(CurrentFolderPath);
    }

    public void SetDropTarget(bool isActive)
    {
        IsDropTarget = isActive;
    }

    private bool CanAutoPublish()
    {
        return _productSheetService is not null
            && _runExclusiveAsync is not null
            && _runAutoPublishCardAsync is not null
            && !IsAutoPublishBusy
            && !string.IsNullOrWhiteSpace(GetSpRootFolder());
    }

    private bool CanCopyMainToDetail()
    {
        var spRootFolder = GetSpRootFolder();
        if (string.IsNullOrWhiteSpace(spRootFolder))
        {
            return false;
        }

        return Directory.Exists(Path.Combine(spRootFolder, "main"));
    }

    public async Task<string> RunAutoPublishInternalAsync()
    {
        var task = await PrepareAutoPublishDataAsync();
        var message = string.IsNullOrWhiteSpace(task.ProductsJsonPath)
            ? "上架 JSON 已生成。"
            : task.ProductsJsonPath;
        StatusChanged?.Invoke($"自动上架准备完成：{Path.GetFileName(task.SpRootFolder)}。{message}");
        return message;
    }

    public async Task<ProductSheetTask> PrepareAutoPublishDataAsync()
    {
        var spRootFolder = GetSpRootFolder();
        if (string.IsNullOrWhiteSpace(spRootFolder) || _productSheetService is null)
        {
            throw new InvalidOperationException("当前卡片未解析到 SP 根目录，无法自动上架。");
        }

        var validationError = ValidateAutoPublishInput(spRootFolder);
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        Activate();
        NotifyAutoPublishStateChanged();

        try
        {
            StatusChanged?.Invoke($"开始准备上架数据：{Path.GetFileName(spRootFolder)}");
            var task = await _productSheetService.GenerateAsync(spRootFolder);
            if (task.Status == "Completed")
            {
                StatusChanged?.Invoke($"上架数据准备完成：{Path.GetFileName(spRootFolder)}");
                return task;
            }

            throw new InvalidOperationException($"自动上架失败：{Path.GetFileName(spRootFolder)}");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"自动上架失败：{ex.Message}");
            throw;
        }
        finally
        {
            NotifyAutoPublishStateChanged();
        }
    }

    private async Task AutoPublishAsync()
    {
        var runAutoPublishCardAsync = _runAutoPublishCardAsync;
        if (_productSheetService is null || _runExclusiveAsync is null || runAutoPublishCardAsync is null)
        {
            StatusChanged?.Invoke("当前卡片未解析到 SP 根目录，无法自动上架。");
            return;
        }

        await runAutoPublishCardAsync(this);
    }

    private string? GetSpRootFolder()
    {
        return SpRootResolver.Resolve(RootFolderPath)
            ?? SpRootResolver.Resolve(CurrentFolderPath);
    }

    private static string? ValidateAutoPublishInput(string spRootFolder)
    {
        var mainFolder = Path.Combine(spRootFolder, "main");
        if (!Directory.Exists(mainFolder))
        {
            return $"自动上架前置校验失败：{mainFolder} 不存在。";
        }

        var sizeImageExists = Directory.EnumerateFiles(mainFolder, "2-*.png", SearchOption.TopDirectoryOnly).Any();
        if (!sizeImageExists)
        {
            return $"自动上架前置校验失败：{mainFolder} 下缺少 2-*.png 尺寸图。";
        }

        return null;
    }

    public async Task AddImageFilesAsync(IReadOnlyList<string> sourceFiles, bool showStatusMessage = true)
    {
        if (SelectedNode is null || sourceFiles.Count == 0)
        {
            return;
        }

        Activate();

        var targetFolder = SelectedNode.FolderPath;
        var copiedFiles = await Task.Run(() =>
        {
            var createdFiles = new List<string>();
            foreach (var sourcePath in sourceFiles)
            {
                var destinationPath = GetUniquePath(targetFolder, Path.GetFileName(sourcePath));
                File.Copy(sourcePath, destinationPath, overwrite: false);
                createdFiles.Add(destinationPath);
            }

            return createdFiles;
        });

        foreach (var filePath in copiedFiles)
        {
            var fileInfo = new FileInfo(filePath);
            var model = new ImageItem
            {
                FilePath = fileInfo.FullName,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length,
                LastWriteTime = fileInfo.LastWriteTime
            };
            SelectedNode.Model.Images.Add(model);
        }

        SelectedNode.Model.Images.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.FileName, right.FileName));
        RefreshCurrentImages(loadAll: true);
        UpdateCounts();

        if (copiedFiles.Count > 0)
        {
            RaisePreviewForPath(copiedFiles[0]);
        }

        if (showStatusMessage)
        {
            StatusChanged?.Invoke($"已向 {SelectedNode.DisplayName} 添加 {copiedFiles.Count} 张图片。");
        }
    }

    private void ToggleExpand(FolderNodeViewModel? node)
    {
        if (node is null || !node.HasChildren)
        {
            return;
        }

        node.IsExpanded = !node.IsExpanded;
        RebuildVisibleNodes();
    }

    private void SelectNode(FolderNodeViewModel? node, bool activateCard = true)
    {
        if (node is null)
        {
            return;
        }

        foreach (var visibleNode in EnumerateAll(_rootNode))
        {
            visibleNode.IsSelected = false;
        }

        node.IsSelected = true;
        ExpandNodeAncestors(node);
        RebuildVisibleNodes();
        SelectedNode = node;
        _workspaceStateService.SetSelectedNode(_rootNode.Id, node.Id);
        UpdateCounts();

        if (activateCard)
        {
            Activate();
        }
    }

    private void InvertSelection()
    {
        if (SelectedNode is null)
        {
            return;
        }

        _workspaceService.InvertSelection(SelectedNode.Model);
        foreach (var image in CurrentImages)
        {
            image.IsSelected = image.Model.IsSelected;
        }

        UpdateCounts();
    }

    private void SetSelectionState(bool isSelected)
    {
        if (SelectedNode is null)
        {
            return;
        }

        _workspaceService.SetSelectionState(SelectedNode.Model, isSelected);
        foreach (var image in CurrentImages)
        {
            image.SyncSelectionStateFromModel();
        }

        UpdateCounts();
    }

    private void SetCollapsed(bool collapsed)
    {
        IsCollapsed = collapsed;
        _workspaceStateService.SetCollapsed(_rootNode.Id, collapsed);
    }

    private async Task AddImagesAsync()
    {
        if (SelectedNode is null)
        {
            return;
        }

        Activate();

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择要添加到当前目录的图片",
            InitialDirectory = SelectedNode.FolderPath,
            Multiselect = true,
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.jfif"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await AddImageFilesAsync(dialog.FileNames, showStatusMessage: true);
    }

    private async Task CopyMainToDetailAsync()
    {
        var spRootFolder = GetSpRootFolder();
        if (string.IsNullOrWhiteSpace(spRootFolder))
        {
            StatusChanged?.Invoke("当前卡片未解析到 SP 根目录，无法复制 main 到 detail。");
            return;
        }

        Activate();

        var mainFolder = Path.Combine(spRootFolder, "main");
        var detailFolder = Path.Combine(spRootFolder, "detail");
        if (!Directory.Exists(mainFolder))
        {
            StatusChanged?.Invoke($"复制失败：{mainFolder} 不存在。");
            return;
        }

        Directory.CreateDirectory(detailFolder);

        var copiedFiles = await Task.Run(() =>
        {
            var result = new List<string>();
            foreach (var sourcePath in Directory.EnumerateFiles(mainFolder, "*", SearchOption.TopDirectoryOnly)
                         .Where(IsSupportedImageFile))
            {
                var destinationPath = Path.Combine(detailFolder, Path.GetFileName(sourcePath));
                File.Copy(sourcePath, destinationPath, overwrite: true);
                result.Add(destinationPath);
            }

            return result;
        });

        var refreshedDetailNode = RefreshNodeImages(detailFolder);
        refreshedDetailNode?.RefreshImageCount();
        UpdateCounts();

        if (SelectedNode is not null
            && string.Equals(
                Path.GetFullPath(SelectedNode.FolderPath),
                Path.GetFullPath(detailFolder),
                StringComparison.OrdinalIgnoreCase))
        {
            RefreshCurrentImages(loadAll: true);
            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(ContentEmptyText));
            OnPropertyChanged(nameof(CurrentFolderMetaText));
            if (CurrentImages.Count > 0)
            {
                RaisePreviewForPath(CurrentImages[0].FilePath);
            }
        }

        StatusChanged?.Invoke($"已复制 main 到 detail，共 {copiedFiles.Count} 张图片，同名文件已覆盖。");
    }

    private async Task MoveSelectedAsync()
    {
        if (SelectedNode is null)
        {
            return;
        }

        Activate();

        var selectedItems = CurrentImages.Where(image => image.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(BackupFolder);

        var movedPaths = await Task.Run(() =>
        {
            var result = new List<ImageItemViewModel>();
            foreach (var item in selectedItems)
            {
                var destinationPath = GetUniquePath(BackupFolder, item.FileName);
                File.Move(item.FilePath, destinationPath);
                result.Add(item);
            }

            return result;
        });

        foreach (var movedItem in movedPaths)
        {
            SelectedNode.Model.Images.Remove(movedItem.Model);
            CurrentImages.Remove(movedItem);
        }

        LoadedImageCount = CurrentImages.Count;
        UpdateCounts();
        PreviewRequested?.Invoke(null);
        StatusChanged?.Invoke($"已移动 {movedPaths.Count} 张图片到备份目录。");
    }

    private void RebuildVisibleNodes()
    {
        VisibleNodes.Clear();
        foreach (var node in EnumerateVisible(_rootNode))
        {
            VisibleNodes.Add(node);
        }
    }

    private IEnumerable<FolderNodeViewModel> EnumerateVisible(FolderNodeViewModel node)
    {
        yield return node;

        if (!node.IsExpanded)
        {
            yield break;
        }

        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateVisible(child))
            {
                yield return descendant;
            }
        }
    }

    private IEnumerable<FolderNodeViewModel> EnumerateAll(FolderNodeViewModel node)
    {
        yield return node;

        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateAll(child))
            {
                yield return descendant;
            }
        }
    }

    private static void ExpandNodeAncestors(FolderNodeViewModel? node)
    {
        var current = node;
        while (current is not null)
        {
            current.IsExpanded = true;
            current = FindParent(current);
        }
    }

    private static FolderNodeViewModel? FindParent(FolderNodeViewModel node)
    {
        return node.Model.Parent is null ? null : FindNode(node, node.Model.Parent.Id);
    }

    private static FolderNodeViewModel? FindNode(FolderNodeViewModel current, Guid targetId)
    {
        if (current.Id == targetId)
        {
            return current;
        }

        foreach (var child in current.Children)
        {
            var match = FindNode(child, targetId);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private FolderNodeViewModel? RefreshNodeImages(string folderPath)
    {
        var targetNode = FindNodeByFolderPath(_rootNode, folderPath);
        if (targetNode is null || !Directory.Exists(folderPath))
        {
            return null;
        }

        targetNode.Model.Images.Clear();
        foreach (var fileInfo in new DirectoryInfo(folderPath)
                     .EnumerateFiles()
                     .Where(file => IsSupportedImageFile(file.FullName))
                     .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
        {
            targetNode.Model.Images.Add(new ImageItem
            {
                FilePath = fileInfo.FullName,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length,
                LastWriteTime = fileInfo.LastWriteTime
            });
        }

        return targetNode;
    }

    private static FolderNodeViewModel? FindNodeByFolderPath(FolderNodeViewModel current, string folderPath)
    {
        var normalizedTarget = Path.GetFullPath(folderPath);
        if (string.Equals(Path.GetFullPath(current.FolderPath), normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        foreach (var child in current.Children)
        {
            var match = FindNodeByFolderPath(child, folderPath);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private FolderNodeViewModel? FindNodeById(Guid? nodeId)
    {
        return nodeId.HasValue ? FindNode(_rootNode, nodeId.Value) : null;
    }

    private static FolderNodeViewModel? FindFirstNodeWithImages(IEnumerable<FolderNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Model.Images.Count > 0)
            {
                return node;
            }

            if (node.Children.Count > 0)
            {
                var childMatch = FindFirstNodeWithImages(node.Children);
                if (childMatch is not null)
                {
                    return childMatch;
                }
            }
        }

        return nodes.FirstOrDefault();
    }

    private void RefreshCurrentImages(bool loadAll = false)
    {
        CurrentImages.Clear();

        if (SelectedNode is null)
        {
            LoadedImageCount = 0;
            UpdateCounts();
            return;
        }

        var images = SelectedNode.Model.Images;
        var targetCount = loadAll ? images.Count : Math.Min(images.Count, ImagePageSize);

        for (var index = 0; index < targetCount; index++)
        {
            CurrentImages.Add(CreateImageViewModel(images[index]));
        }

        LoadedImageCount = CurrentImages.Count;
        UpdateCounts();
    }

    private void LoadMoreImages()
    {
        if (SelectedNode is null || !HasMoreImages)
        {
            return;
        }

        var images = SelectedNode.Model.Images;
        var nextCount = Math.Min(images.Count, LoadedImageCount + ImagePageSize);
        for (var index = LoadedImageCount; index < nextCount; index++)
        {
            CurrentImages.Add(CreateImageViewModel(images[index]));
        }

        LoadedImageCount = CurrentImages.Count;
        OnPropertyChanged(nameof(HasImages));
    }

    private ImageItemViewModel CreateImageViewModel(ImageItem model)
    {
        return new ImageItemViewModel(
            model,
            _ => UpdateCounts(),
            image =>
            {
                Activate();
                PreviewRequested?.Invoke(new PreviewRequest
                {
                    FilePath = image.FilePath,
                    FileName = image.FileName,
                    FolderPath = CurrentFolderPath,
                    FileSize = image.Model.FileSize
                });
            });
    }

    private void RaisePreviewForPath(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        PreviewRequested?.Invoke(new PreviewRequest
        {
            FilePath = fileInfo.FullName,
            FileName = fileInfo.Name,
            FolderPath = CurrentFolderPath,
            FileSize = fileInfo.Exists ? fileInfo.Length : 0
        });
    }

    private void UpdateCounts()
    {
        if (SelectedNode is null)
        {
            TotalCount = 0;
            SelectedCount = 0;
        }
        else
        {
            var counts = _workspaceService.CalculateCounts(SelectedNode.Model);
            TotalCount = counts.Total;
            SelectedCount = counts.Selected;
        }

        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(ContentEmptyText));
        OnPropertyChanged(nameof(TotalImageCount));
        OnPropertyChanged(nameof(ImageLoadSummaryText));
        OnPropertyChanged(nameof(HasMoreImages));
        OnPropertyChanged(nameof(CountsText));
        _invertSelectionCommand.RaiseCanExecuteChanged();
        _selectAllCommand.RaiseCanExecuteChanged();
        _clearSelectionCommand.RaiseCanExecuteChanged();
        _addImagesCommand.RaiseCanExecuteChanged();
        _copyMainToDetailCommand.RaiseCanExecuteChanged();
        _loadMoreImagesCommand.RaiseCanExecuteChanged();
        SelectionContextChanged?.Invoke(this);
    }

    private static string GetUniquePath(string folderPath, string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(folderPath, fileName);
        var index = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(folderPath, $"{baseName} ({index}){extension}");
            index++;
        }

        return candidate;
    }

    private static bool IsSupportedImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp" or ".jfif";
    }
}

