using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using ImageKeeper.App.Utilities;
using ImageKeeper.Core;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;
using Forms = System.Windows.Forms;
using Media = System.Windows.Media;

namespace ImageKeeper.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IFolderScanService _folderScanService;
    private readonly IImageWorkspaceService _imageWorkspaceService;
    private readonly IWorkspaceStateService _workspaceStateService;
    private readonly IProductSheetService _productSheetService;
    private readonly SemaphoreSlim _autoPublishLock = new(1, 1);
    private readonly AsyncRelayCommand _chooseFolderCommand;
    private readonly AsyncRelayCommand _selectBackupFolderCommand;
    private readonly RelayCommand _invertSelectionCommand;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _rebuildSizeIndexCommand;
    private readonly AsyncRelayCommand _generateProductSheetCommand;
    private readonly AsyncRelayCommand _addTabCommand;
    private CancellationTokenSource? _previewCancellationTokenSource;
    private WorkspaceTabViewModel? _selectedTab;
    private bool _isBusy;
    private bool _isRecursive = true;
    private bool _isScanProgressIndeterminate = true;
    private double _scanProgressValue;
    private string _statusMessage = "请选择文件夹开始筛选";
    private string _loadedFolderText = "当前文件夹：未选择";
    private string _selectionSummaryText = "图片 0 张，已选中 0 张，未选中 0 张";
    private string _backupFolderText = $"备份目录：{WorkspaceDefaults.DefaultBackupFolder}";
    private string _loadingTitle = "正在加载图片资源...";
    private string _loadingDetail = "请稍候，目录和图片较多时会需要更多时间。";
    private string _scanProgressText = "等待加载";
    private string _previewTitle = "未选中图片";
    private string _previewMeta = "点击左侧缩略图后，这里显示完整预览。";
    private Media.ImageSource? _previewImageSource;
    private string _backupFolder = WorkspaceDefaults.DefaultBackupFolder;
    private bool _isAutoPublishRunning;

    public MainWindowViewModel(
        IFolderScanService folderScanService,
        IImageWorkspaceService imageWorkspaceService,
        IWorkspaceStateService workspaceStateService,
        IProductSheetService productSheetService)
    {
        _folderScanService = folderScanService;
        _imageWorkspaceService = imageWorkspaceService;
        _workspaceStateService = workspaceStateService;
        _productSheetService = productSheetService;

        _chooseFolderCommand = new AsyncRelayCommand(_ => ChooseFolderAsync(), _ => !IsBusy);
        _selectBackupFolderCommand = new AsyncRelayCommand(_ => SelectBackupFolderAsync(), _ => !IsBusy);
        _invertSelectionCommand = new RelayCommand(_ => SelectedTab?.InvertSelection(), _ => !IsBusy && SelectedTab?.ActiveCard is not null);
        _refreshCommand = new AsyncRelayCommand(_ => RefreshCurrentTabAsync(), _ => !IsBusy && SelectedTab is not null && Directory.Exists(SelectedTab.RootFolder));
        _rebuildSizeIndexCommand = new AsyncRelayCommand(_ => RebuildSizeIndexAsync(), _ => !IsBusy);
        _generateProductSheetCommand = new AsyncRelayCommand(_ => GenerateProductSheetAsync(), _ => !IsBusy && SelectedTab?.RootCards.Count > 0);
        _addTabCommand = new AsyncRelayCommand(_ => ChooseFolderAsync(), _ => !IsBusy);
    }

    public ObservableCollection<WorkspaceTabViewModel> WorkspaceTabs { get; } = [];

    public ICommand ChooseFolderCommand => _chooseFolderCommand;

    public ICommand SelectBackupFolderCommand => _selectBackupFolderCommand;

    public ICommand InvertSelectionCommand => _invertSelectionCommand;

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand RebuildSizeIndexCommand => _rebuildSizeIndexCommand;

    public ICommand GenerateProductSheetCommand => _generateProductSheetCommand;

    public ICommand AddTabCommand => _addTabCommand;

    public WorkspaceTabViewModel? SelectedTab
    {
        get => _selectedTab;
        private set
        {
            if (_selectedTab == value)
            {
                return;
            }

            if (_selectedTab is not null)
            {
                _selectedTab.IsSelected = false;
                _selectedTab.SelectionContextChanged -= OnTabSelectionContextChanged;
                _selectedTab.StatusChanged -= OnTabStatusChanged;
                _selectedTab.PreviewRequested -= OnPreviewRequested;
            }

            _selectedTab = value;

            if (_selectedTab is not null)
            {
                _selectedTab.IsSelected = true;
                _selectedTab.SelectionContextChanged += OnTabSelectionContextChanged;
                _selectedTab.StatusChanged += OnTabStatusChanged;
                _selectedTab.PreviewRequested += OnPreviewRequested;
                UpdateSummaryForTab(_selectedTab);
            }
            else
            {
                ResetSummary();
                ClearPreview();
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasTabs));
            OnPropertyChanged(nameof(HasRootCards));
            _invertSelectionCommand.RaiseCanExecuteChanged();
            _refreshCommand.RaiseCanExecuteChanged();
            _generateProductSheetCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasTabs => WorkspaceTabs.Count > 0;

    public bool HasRootCards => SelectedTab?.HasRootCards ?? false;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsLoadingVisible));
            _chooseFolderCommand.RaiseCanExecuteChanged();
            _selectBackupFolderCommand.RaiseCanExecuteChanged();
            _invertSelectionCommand.RaiseCanExecuteChanged();
            _refreshCommand.RaiseCanExecuteChanged();
            _rebuildSizeIndexCommand.RaiseCanExecuteChanged();
            _generateProductSheetCommand.RaiseCanExecuteChanged();
            _addTabCommand.RaiseCanExecuteChanged();
            NotifyAutoPublishStateChanged();
        }
    }

    public bool IsRecursive
    {
        get => _isRecursive;
        set => SetProperty(ref _isRecursive, value);
    }

    public bool IsLoadingVisible => IsBusy;

    public string BackupFolder
    {
        get => _backupFolder;
        set
        {
            if (!SetProperty(ref _backupFolder, value))
            {
                return;
            }

            BackupFolderText = $"备份目录：{value}";
            foreach (var tab in WorkspaceTabs)
            {
                tab.SetBackupFolder(value);
            }
        }
    }

    public bool IsScanProgressIndeterminate
    {
        get => _isScanProgressIndeterminate;
        private set => SetProperty(ref _isScanProgressIndeterminate, value);
    }

    public double ScanProgressValue
    {
        get => _scanProgressValue;
        private set => SetProperty(ref _scanProgressValue, value);
    }

    public string ScanProgressText
    {
        get => _scanProgressText;
        private set => SetProperty(ref _scanProgressText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LoadedFolderText
    {
        get => _loadedFolderText;
        private set => SetProperty(ref _loadedFolderText, value);
    }

    public string SelectionSummaryText
    {
        get => _selectionSummaryText;
        private set => SetProperty(ref _selectionSummaryText, value);
    }

    public string BackupFolderText
    {
        get => _backupFolderText;
        private set => SetProperty(ref _backupFolderText, value);
    }

    public string LoadingTitle
    {
        get => _loadingTitle;
        private set => SetProperty(ref _loadingTitle, value);
    }

    public string LoadingDetail
    {
        get => _loadingDetail;
        private set => SetProperty(ref _loadingDetail, value);
    }

    public string PreviewTitle
    {
        get => _previewTitle;
        private set => SetProperty(ref _previewTitle, value);
    }

    public string PreviewMeta
    {
        get => _previewMeta;
        private set => SetProperty(ref _previewMeta, value);
    }

    public Media.ImageSource? PreviewImageSource
    {
        get => _previewImageSource;
        private set
        {
            if (!SetProperty(ref _previewImageSource, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasPreviewImage));
        }
    }

    public bool HasPreviewImage => PreviewImageSource is not null;

    public async Task InitializeAsync()
    {
        await Task.Yield();
        ResetSummary();
    }

    public async Task LoadFolderAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            StatusMessage = $"目录不存在：{folderPath}";
            return;
        }

        var existingTab = WorkspaceTabs.FirstOrDefault(tab =>
            string.Equals(tab.RootFolder, folderPath, StringComparison.OrdinalIgnoreCase));

        if (existingTab is not null)
        {
            SelectedTab = existingTab;
            await ReloadTabAsync(existingTab);
            return;
        }

        var tab = CreateTab(folderPath);
        WorkspaceTabs.Add(tab);
        OnPropertyChanged(nameof(HasTabs));
        SelectedTab = tab;
        await ReloadTabAsync(tab);
    }

    private WorkspaceTabViewModel CreateTab(string folderPath)
    {
        var tab = new WorkspaceTabViewModel(
            folderPath,
            BackupFolder,
            _imageWorkspaceService,
            _workspaceStateService,
            _productSheetService,
            () => _isAutoPublishRunning || IsBusy,
            RunAutoPublishExclusiveAsync);
        tab.RequestedActivate += OnTabRequestedActivate;
        tab.RequestedClose += OnTabRequestedClose;
        return tab;
    }

    private async Task RunAutoPublishExclusiveAsync(Func<Task> action)
    {
        if (_isAutoPublishRunning)
        {
            StatusMessage = "已有自动上架任务执行中，请稍后再试。";
            NotifyAutoPublishStateChanged();
            return;
        }

        await _autoPublishLock.WaitAsync();
        try
        {
            _isAutoPublishRunning = true;
            NotifyAutoPublishStateChanged();
            await action();
        }
        finally
        {
            _isAutoPublishRunning = false;
            NotifyAutoPublishStateChanged();
            _autoPublishLock.Release();
        }
    }

    private void NotifyAutoPublishStateChanged()
    {
        foreach (var tab in WorkspaceTabs)
        {
            tab.NotifyAutoPublishStateChanged();
        }
    }

    private async Task ReloadTabAsync(WorkspaceTabViewModel tab)
    {
        IsBusy = true;
        ScanProgressValue = 0;
        IsScanProgressIndeterminate = true;
        LoadingTitle = "正在准备扫描...";
        LoadingDetail = "正在读取目录结构，请稍候。";
        ScanProgressText = "读取目录结构中";
        StatusMessage = "正在扫描图片...";

        try
        {
            if (SelectedTab != tab)
            {
                SelectedTab = tab;
            }

            tab.SetRootNodes([]);
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(HasRootCards));
            ClearPreview();
            UpdateSummaryForTab(tab);

            var progress = new Progress<FolderScanProgress>(OnFolderScanProgress);
            var nodes = await _folderScanService.ScanAsync(tab.RootFolder, IsRecursive, progress);

            LoadingTitle = "正在生成界面...";
            var folderCount = CountFolders(nodes);
            var imageCount = CountImages(nodes);
            LoadingDetail = $"共扫描文件夹 {folderCount} 个，图片 {imageCount} 张，正在渲染界面。";
            ScanProgressText = "正在生成界面";

            tab.SetRootNodes(nodes);
            tab.RestoreDefaultSelection();
            UpdateSummaryForTab(tab);
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(HasRootCards));
            _generateProductSheetCommand.RaiseCanExecuteChanged();

            if (tab.RootCards.Count > 0)
            {
                ScanProgressValue = 100;
                IsScanProgressIndeterminate = false;
                ScanProgressText = "加载完成";
                StatusMessage = $"当前目录：{tab.ActiveCard?.CurrentFolderTitle ?? tab.Title}，共 {tab.ActiveCard?.TotalCount ?? 0} 张，已选中 {tab.ActiveCard?.SelectedCount ?? 0} 张，未选中 {tab.ActiveCard?.UnselectedCount ?? 0} 张";
            }
            else
            {
                StatusMessage = "请选择文件夹开始筛选";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"扫描文件夹失败：{ex.Message}";
            LoadingDetail = StatusMessage;
        }
        finally
        {
            LoadingTitle = "加载完成";
            if (string.IsNullOrWhiteSpace(LoadingDetail))
            {
                LoadingDetail = "请稍候，目录和图片较多时会需要更多时间。";
            }

            IsBusy = false;
        }
    }

    private async Task RefreshCurrentTabAsync()
    {
        if (SelectedTab is null)
        {
            return;
        }

        await ReloadTabAsync(SelectedTab);
    }

    private async Task ChooseFolderAsync()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择要加载的父目录",
            InitialDirectory = SelectedTab is not null && Directory.Exists(SelectedTab.RootFolder)
                ? SelectedTab.RootFolder
                : WorkspaceDefaults.DefaultOpenFolder,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        await LoadFolderAsync(dialog.SelectedPath);
    }

    private async Task SelectBackupFolderAsync()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择备份目录",
            InitialDirectory = Directory.Exists(BackupFolder) ? BackupFolder : WorkspaceDefaults.DefaultBackupFolder,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        BackupFolder = dialog.SelectedPath;
        StatusMessage = $"备份目录已切换到：{BackupFolder}";
        await Task.CompletedTask;
    }

    private async Task RebuildSizeIndexAsync()
    {
        IsBusy = true;
        LoadingTitle = "正在重建尺寸索引";
        LoadingDetail = "脚本执行完成后会自动返回当前界面。";
        ScanProgressValue = 0;
        IsScanProgressIndeterminate = true;
        StatusMessage = "正在执行尺寸索引重建...";

        try
        {
            await _productSheetService.RebuildSizeIndexAsync();
            StatusMessage = "尺寸索引重建完成。";
            LoadingDetail = StatusMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = $"重建尺寸索引失败：{ex.Message}";
            LoadingDetail = StatusMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GenerateProductSheetAsync()
    {
        var firstCard = SelectedTab?.RootCards.FirstOrDefault();
        if (firstCard is null)
        {
            return;
        }

        IsBusy = true;
        LoadingTitle = "正在生成商品表";
        LoadingDetail = $"正在处理：{firstCard.RootFolderPath}";
        ScanProgressValue = 0;
        IsScanProgressIndeterminate = true;
        StatusMessage = $"开始处理：{firstCard.RootFolderPath}";

        try
        {
            var result = await _productSheetService.GenerateAsync(firstCard.RootFolderPath);
            StatusMessage = $"商品表任务状态：{result.Status}";
            LoadingDetail = StatusMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成商品表失败：{ex.Message}";
            LoadingDetail = StatusMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnFolderScanProgress(FolderScanProgress progress)
    {
        LoadingTitle = progress.Stage;
        IsScanProgressIndeterminate = progress.TotalFolders <= 0;
        ScanProgressValue = progress.Percent;

        var folderText = progress.TotalFolders > 0
            ? $"已扫描文件夹 {progress.ProcessedFolders}/{progress.TotalFolders} 个"
            : $"已发现文件夹 {progress.ProcessedFolders} 个";

        LoadingDetail = $"{folderText}，已发现图片 {progress.ImageCount} 张";
        ScanProgressText = progress.TotalFolders > 0
            ? $"{progress.Percent:0}%"
            : "读取目录结构中";
    }

    private void OnPreviewRequested(PreviewRequest? request)
    {
        _ = LoadPreviewAsync(request);
    }

    private async Task LoadPreviewAsync(PreviewRequest? request)
    {
        _previewCancellationTokenSource?.Cancel();
        _previewCancellationTokenSource?.Dispose();
        _previewCancellationTokenSource = new CancellationTokenSource();
        var token = _previewCancellationTokenSource.Token;

        if (request is null)
        {
            ClearPreview();
            return;
        }

        PreviewTitle = request.FileName;
        PreviewMeta = $"大小：{FormatFileSize(request.FileSize)}{Environment.NewLine}完整路径：{request.FilePath}";
        PreviewImageSource = null;

        try
        {
            var bitmap = await Task.Run(() => ImageBitmapLoader.LoadFromFile(request.FilePath, decodePixelWidth: 1800), token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            PreviewImageSource = bitmap;
        }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            PreviewTitle = "预览失败";
            PreviewMeta = ex.Message;
            PreviewImageSource = null;
        }
    }

    private void ClearPreview()
    {
        PreviewImageSource = null;
        PreviewTitle = "未选中图片";
        PreviewMeta = "点击当前目录中的图片，在这里查看完整预览。";
    }

    private void ResetSummary()
    {
        LoadedFolderText = "当前文件夹：未选择";
        SelectionSummaryText = "图片 0 张，已选中 0 张，未选中 0 张";
        BackupFolderText = $"备份目录：{BackupFolder}";
        StatusMessage = "请选择文件夹开始筛选";
    }

    private void UpdateSummaryForTab(WorkspaceTabViewModel tab)
    {
        LoadedFolderText = tab.GetCurrentFolderText();
        SelectionSummaryText = tab.GetSelectionSummaryText();
        BackupFolderText = $"备份目录：{BackupFolder}";
    }

    private void OnTabRequestedActivate(WorkspaceTabViewModel tab)
    {
        SelectedTab = tab;
    }

    private void OnTabRequestedClose(WorkspaceTabViewModel tab)
    {
        if (!WorkspaceTabs.Contains(tab))
        {
            return;
        }

        tab.RequestedActivate -= OnTabRequestedActivate;
        tab.RequestedClose -= OnTabRequestedClose;
        tab.SelectionContextChanged -= OnTabSelectionContextChanged;
        tab.StatusChanged -= OnTabStatusChanged;
        tab.PreviewRequested -= OnPreviewRequested;

        var index = WorkspaceTabs.IndexOf(tab);
        WorkspaceTabs.Remove(tab);
        OnPropertyChanged(nameof(HasTabs));

        if (WorkspaceTabs.Count == 0)
        {
            SelectedTab = null;
            return;
        }

        var nextIndex = Math.Min(index, WorkspaceTabs.Count - 1);
        SelectedTab = WorkspaceTabs[nextIndex];
    }

    private void OnTabSelectionContextChanged(WorkspaceTabViewModel tab)
    {
        if (SelectedTab == tab)
        {
            UpdateSummaryForTab(tab);
            OnPropertyChanged(nameof(SelectedTab));
        }
    }

    private void OnTabStatusChanged(string message)
    {
        StatusMessage = message;
        if (SelectedTab is not null)
        {
            UpdateSummaryForTab(SelectedTab);
        }
    }

    private static int CountFolders(IEnumerable<FolderNode> nodes)
    {
        return nodes.Sum(CountFolders);
    }

    private static int CountFolders(FolderNode node)
    {
        return 1 + node.Children.Sum(CountFolders);
    }

    private static int CountImages(IEnumerable<FolderNode> nodes)
    {
        return nodes.Sum(CountImages);
    }

    private static int CountImages(FolderNode node)
    {
        return node.Images.Count + node.Children.Sum(CountImages);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}
