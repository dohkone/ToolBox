using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
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
    private const string ReviewWorkspaceSection = "review-workspace";
    private const string ImageGenerateSection = "image-generate";
    private const string TemplateGenerateTab = "template-generate";
    private const string SpBatchTab = "sp-batch";
    private const string DefaultTemplateLibraryPath = @"D:\temu_auto\temp\文生图模板库_Codex.xlsx";
    private const string DefaultImage2ScriptPath = @"D:\new_project\tools\python\image2-generate\scripts\generate_image.py";

    private readonly IFolderScanService _folderScanService;
    private readonly IImageWorkspaceService _imageWorkspaceService;
    private readonly IWorkspaceStateService _workspaceStateService;
    private readonly IProductSheetService _productSheetService;
    private readonly ITemplateGenerationService _templateGenerationService;
    private readonly ISpBatchService _spBatchService;
    private readonly SemaphoreSlim _autoPublishLock = new(1, 1);
    private readonly AsyncRelayCommand _chooseFolderCommand;
    private readonly AsyncRelayCommand _selectBackupFolderCommand;
    private readonly RelayCommand _invertSelectionCommand;
    private readonly AsyncRelayCommand _generateProductSheetCommand;
    private readonly AsyncRelayCommand _addTabCommand;
    private readonly RelayCommand _showReviewWorkspaceCommand;
    private readonly RelayCommand _showImageGenerateCommand;
    private readonly RelayCommand _showTemplateGenerateTabCommand;
    private readonly RelayCommand _showSpBatchTabCommand;
    private readonly AsyncRelayCommand _chooseTemplateLibraryCommand;
    private readonly AsyncRelayCommand _chooseGenerationOutputFolderCommand;
    private readonly RelayCommand _openGenerationOutputFolderCommand;
    private readonly AsyncRelayCommand _runTemplateGenerationCommand;
    private readonly AsyncRelayCommand _chooseSpBatchInputFolderCommand;
    private readonly AsyncRelayCommand _chooseSpBatchOutputFolderCommand;
    private readonly RelayCommand _openSpBatchOutputFolderCommand;
    private readonly AsyncRelayCommand _runSpBatchCommand;
    private CancellationTokenSource? _previewCancellationTokenSource;
    private WorkspaceTabViewModel? _selectedTab;
    private string _selectedSection = ReviewWorkspaceSection;
    private string _selectedImageGenerateTab = TemplateGenerateTab;
    private bool _isBusy;
    private bool _isScanProgressIndeterminate = true;
    private double _scanProgressValue;
    private string _statusMessage = "请选择文件夹开始筛图。";
    private string _loadedFolderText = "当前文件夹：未选择";
    private string _selectionSummaryText = "图片 0 张，已选中 0 张，未选中 0 张";
    private string _backupFolderText = $"备份目录：{WorkspaceDefaults.DefaultBackupFolder}";
    private string _loadingTitle = "正在加载图片资源...";
    private string _loadingDetail = "目录或图片较多时会稍慢一些，请稍候。";
    private string _scanProgressText = "等待加载";
    private string _previewMeta = "大小：-\r\n完整路径：-";
    private Media.ImageSource? _previewImageSource;
    private string _backupFolder = WorkspaceDefaults.DefaultBackupFolder;
    private bool _isAutoPublishRunning;
    private bool _isTemplateGenerating;
    private string _templateLibraryPath = DefaultTemplateLibraryPath;
    private string _generationOutputDirectory = WorkspaceDefaults.DefaultOpenFolder;
    private string _generationCountText = "1";
    private string _generationConcurrencyText = "1";
    private bool _isGenerationUniqueScene = true;
    private bool _isGenerationPromptsOnly = false;
    private string _generationPageDescription = "用于模板生图与 SP 批量处理";
    private string _generationStatusText = "待命";
    private string _generationResultText = "这里会显示本次生成的提示词、输出目录和结果明细。";
    private string _generationResultModeText = "模式：待命";
    private string _generationResultOutputText = $"输出目录：{WorkspaceDefaults.DefaultOpenFolder}";
    private bool _isSpBatchRunning;
    private string _spBatchInputDirectory = WorkspaceDefaults.DefaultOpenFolder;
    private string _spBatchOutputDirectory = WorkspaceDefaults.DefaultSpBatchOutputFolder;
    private string _spBatchConcurrencyText = "2";
    private string _spBatchRetriesText = "4";
    private SpBatchMode _spBatchMode = SpBatchMode.Generate;
    private string _spBatchStatusText = "待命";
    private string _spBatchSummaryText = "用于批量创建日期目录、SP 结构和 6 色 SKU 图。";
    private string _spBatchResultRootText = $"日期目录：{WorkspaceDefaults.DefaultSpBatchOutputFolder}";
    private string _spBatchResultStatsText = "SP 数量：0  总任务：0  成功：0  跳过：0  失败：0";

    public MainWindowViewModel(
        IFolderScanService folderScanService,
        IImageWorkspaceService imageWorkspaceService,
        IWorkspaceStateService workspaceStateService,
        IProductSheetService productSheetService,
        ITemplateGenerationService templateGenerationService,
        ISpBatchService spBatchService)
    {
        _folderScanService = folderScanService;
        _imageWorkspaceService = imageWorkspaceService;
        _workspaceStateService = workspaceStateService;
        _productSheetService = productSheetService;
        _templateGenerationService = templateGenerationService;
        _spBatchService = spBatchService;

        _chooseFolderCommand = new AsyncRelayCommand(_ => ChooseFolderAsync(), _ => !IsBusy);
        _selectBackupFolderCommand = new AsyncRelayCommand(_ => SelectBackupFolderAsync(), _ => !IsBusy);
        _invertSelectionCommand = new RelayCommand(_ => SelectedTab?.InvertSelection(), _ => !IsBusy && SelectedTab?.ActiveCard is not null);
        _generateProductSheetCommand = new AsyncRelayCommand(_ => GenerateProductSheetAsync(), _ => !IsBusy && SelectedTab?.RootCards.Count > 0);
        _addTabCommand = new AsyncRelayCommand(_ => ChooseFolderAsync(), _ => !IsBusy);
        _showReviewWorkspaceCommand = new RelayCommand(_ => SetSelectedSection(ReviewWorkspaceSection));
        _showImageGenerateCommand = new RelayCommand(_ => SetSelectedSection(ImageGenerateSection));
        _showTemplateGenerateTabCommand = new RelayCommand(_ => SetSelectedImageGenerateTab(TemplateGenerateTab));
        _showSpBatchTabCommand = new RelayCommand(_ => SetSelectedImageGenerateTab(SpBatchTab));
        _chooseTemplateLibraryCommand = new AsyncRelayCommand(_ => ChooseTemplateLibraryAsync(), _ => !IsBusy);
        _chooseGenerationOutputFolderCommand = new AsyncRelayCommand(_ => ChooseGenerationOutputFolderAsync(), _ => !IsBusy);
        _openGenerationOutputFolderCommand = new RelayCommand(_ => OpenGenerationOutputFolder(), _ => Directory.Exists(GenerationOutputDirectory));
        _runTemplateGenerationCommand = new AsyncRelayCommand(_ => RunTemplateGenerationAsync(), _ => !IsBusy);
        _chooseSpBatchInputFolderCommand = new AsyncRelayCommand(_ => ChooseSpBatchInputFolderAsync(), _ => !IsBusy);
        _chooseSpBatchOutputFolderCommand = new AsyncRelayCommand(_ => ChooseSpBatchOutputFolderAsync(), _ => !IsBusy);
        _openSpBatchOutputFolderCommand = new RelayCommand(_ => OpenSpBatchOutputFolder(), _ => Directory.Exists(SpBatchOutputDirectory));
        _runSpBatchCommand = new AsyncRelayCommand(_ => RunSpBatchAsync(), _ => !IsBusy);
    }

    public ObservableCollection<WorkspaceTabViewModel> WorkspaceTabs { get; } = [];
    public ObservableCollection<GenerationPromptCardViewModel> GenerationPromptCards { get; } = [];
    public ObservableCollection<GeneratedImageResultCardViewModel> GeneratedImageResultCards { get; } = [];
    public ObservableCollection<GeneratedImageResultCardViewModel> SpBatchImageResultCards { get; } = [];
    public ObservableCollection<SpBatchResultCardViewModel> SpBatchResultCards { get; } = [];

    public ICommand ChooseFolderCommand => _chooseFolderCommand;
    public ICommand SelectBackupFolderCommand => _selectBackupFolderCommand;
    public ICommand InvertSelectionCommand => _invertSelectionCommand;
    public ICommand GenerateProductSheetCommand => _generateProductSheetCommand;
    public ICommand AddTabCommand => _addTabCommand;
    public ICommand ShowReviewWorkspaceCommand => _showReviewWorkspaceCommand;
    public ICommand ShowImageGenerateCommand => _showImageGenerateCommand;
    public ICommand ShowTemplateGenerateTabCommand => _showTemplateGenerateTabCommand;
    public ICommand ShowSpBatchTabCommand => _showSpBatchTabCommand;
    public ICommand ChooseTemplateLibraryCommand => _chooseTemplateLibraryCommand;
    public ICommand ChooseGenerationOutputFolderCommand => _chooseGenerationOutputFolderCommand;
    public ICommand OpenGenerationOutputFolderCommand => _openGenerationOutputFolderCommand;
    public ICommand RunTemplateGenerationCommand => _runTemplateGenerationCommand;
    public ICommand ChooseSpBatchInputFolderCommand => _chooseSpBatchInputFolderCommand;
    public ICommand ChooseSpBatchOutputFolderCommand => _chooseSpBatchOutputFolderCommand;
    public ICommand OpenSpBatchOutputFolderCommand => _openSpBatchOutputFolderCommand;
    public ICommand RunSpBatchCommand => _runSpBatchCommand;

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
            _generateProductSheetCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasTabs => WorkspaceTabs.Count > 0;
    public bool HasRootCards => SelectedTab?.HasRootCards ?? false;
    public bool IsReviewWorkspaceSelected => _selectedSection == ReviewWorkspaceSection;
    public bool IsImageGenerateSelected => _selectedSection == ImageGenerateSection;
    public bool IsTemplateGenerateTabSelected => _selectedImageGenerateTab == TemplateGenerateTab;
    public bool IsSpBatchTabSelected => _selectedImageGenerateTab == SpBatchTab;
    public bool IsLoadingVisible => IsBusy && !IsTemplateGenerating && !IsSpBatchRunning;
    public bool HasPreviewImage => PreviewImageSource is not null;
    public bool CanOpenGenerationOutputFolder => Directory.Exists(GenerationOutputDirectory);
    public bool CanOpenSpBatchOutputFolder => Directory.Exists(SpBatchOutputDirectory);
    public bool HasGenerationPromptCards => GenerationPromptCards.Count > 0;
    public bool HasGeneratedImageResultCards => GeneratedImageResultCards.Count > 0;
    public bool HasAnyGenerationResultCards => HasGenerationPromptCards || HasGeneratedImageResultCards;
    public bool HasSpBatchResultCards => SpBatchResultCards.Count > 0;
    public bool HasSpBatchImageResultCards => SpBatchImageResultCards.Count > 0;
    public bool HasAnySpBatchResultCards => HasSpBatchResultCards || HasSpBatchImageResultCards;
    public bool ShouldShowSpBatchDetailCards => HasSpBatchResultCards && !HasSpBatchImageResultCards;

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
            _generateProductSheetCommand.RaiseCanExecuteChanged();
            _addTabCommand.RaiseCanExecuteChanged();
            _chooseTemplateLibraryCommand.RaiseCanExecuteChanged();
            _chooseGenerationOutputFolderCommand.RaiseCanExecuteChanged();
            _runTemplateGenerationCommand.RaiseCanExecuteChanged();
            _chooseSpBatchInputFolderCommand.RaiseCanExecuteChanged();
            _chooseSpBatchOutputFolderCommand.RaiseCanExecuteChanged();
            _runSpBatchCommand.RaiseCanExecuteChanged();
            NotifyAutoPublishStateChanged();
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

    public bool IsTemplateGenerating
    {
        get => _isTemplateGenerating;
        private set => SetProperty(ref _isTemplateGenerating, value);
    }

    public string GenerationPageDescription
    {
        get => _generationPageDescription;
        private set => SetProperty(ref _generationPageDescription, value);
    }

    public string TemplateLibraryPath
    {
        get => _templateLibraryPath;
        set => SetProperty(ref _templateLibraryPath, value);
    }

    public string GenerationOutputDirectory
    {
        get => _generationOutputDirectory;
        set
        {
            if (!SetProperty(ref _generationOutputDirectory, value))
            {
                return;
            }

            GenerationResultOutputText = $"输出目录：{value}";
            OnPropertyChanged(nameof(CanOpenGenerationOutputFolder));
            _openGenerationOutputFolderCommand.RaiseCanExecuteChanged();
        }
    }

    public string GenerationCountText
    {
        get => _generationCountText;
        set => SetProperty(ref _generationCountText, value);
    }

    public string GenerationConcurrencyText
    {
        get => _generationConcurrencyText;
        set => SetProperty(ref _generationConcurrencyText, value);
    }

    public bool IsGenerationUniqueScene
    {
        get => _isGenerationUniqueScene;
        set => SetProperty(ref _isGenerationUniqueScene, value);
    }

    public bool IsGenerationPromptsOnly
    {
        get => _isGenerationPromptsOnly;
        set => SetProperty(ref _isGenerationPromptsOnly, value);
    }

    public string GenerationStatusText
    {
        get => _generationStatusText;
        private set => SetProperty(ref _generationStatusText, value);
    }

    public string GenerationResultText
    {
        get => _generationResultText;
        private set => SetProperty(ref _generationResultText, value);
    }

    public string GenerationResultModeText
    {
        get => _generationResultModeText;
        private set => SetProperty(ref _generationResultModeText, value);
    }

    public string GenerationResultOutputText
    {
        get => _generationResultOutputText;
        private set => SetProperty(ref _generationResultOutputText, value);
    }

    public bool IsSpBatchRunning
    {
        get => _isSpBatchRunning;
        private set => SetProperty(ref _isSpBatchRunning, value);
    }

    public string SpBatchInputDirectory
    {
        get => _spBatchInputDirectory;
        set => SetProperty(ref _spBatchInputDirectory, value);
    }

    public string SpBatchOutputDirectory
    {
        get => _spBatchOutputDirectory;
        set
        {
            if (!SetProperty(ref _spBatchOutputDirectory, value))
            {
                return;
            }

            SpBatchResultRootText = $"日期目录：{value}";
            OnPropertyChanged(nameof(CanOpenSpBatchOutputFolder));
            _openSpBatchOutputFolderCommand.RaiseCanExecuteChanged();
        }
    }

    public string SpBatchConcurrencyText
    {
        get => _spBatchConcurrencyText;
        set => SetProperty(ref _spBatchConcurrencyText, value);
    }

    public string SpBatchRetriesText
    {
        get => _spBatchRetriesText;
        set => SetProperty(ref _spBatchRetriesText, value);
    }

    public bool IsSpBatchDryRun
    {
        get => _spBatchMode == SpBatchMode.DryRun;
        set
        {
            if (!value)
            {
                return;
            }

            SetSpBatchMode(SpBatchMode.DryRun);
        }
    }

    public bool IsSpBatchPrepareOnly
    {
        get => _spBatchMode == SpBatchMode.PrepareOnly;
        set
        {
            if (!value)
            {
                return;
            }

            SetSpBatchMode(SpBatchMode.PrepareOnly);
        }
    }

    public bool IsSpBatchGenerateMode
    {
        get => _spBatchMode == SpBatchMode.Generate;
        set
        {
            if (!value)
            {
                return;
            }

            SetSpBatchMode(SpBatchMode.Generate);
        }
    }

    public string SpBatchStatusText
    {
        get => _spBatchStatusText;
        private set => SetProperty(ref _spBatchStatusText, value);
    }

    public string SpBatchSummaryText
    {
        get => _spBatchSummaryText;
        private set => SetProperty(ref _spBatchSummaryText, value);
    }

    public string SpBatchResultRootText
    {
        get => _spBatchResultRootText;
        private set => SetProperty(ref _spBatchResultRootText, value);
    }

    public string SpBatchResultStatsText
    {
        get => _spBatchResultStatsText;
        private set => SetProperty(ref _spBatchResultStatsText, value);
    }

    public async Task InitializeAsync()
    {
        await Task.Yield();
        EnsurePlaceholderTab();
        ResetSummary();
        ResetGenerationSummary();
        ResetSpBatchSummary();
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

        var tab = SelectedTab is not null && SelectedTab.IsPlaceholder
            ? SelectedTab
            : CreateAndAddTab(folderPath);

        tab.SetRootFolder(folderPath);
        SelectedTab = tab;
        await ReloadTabAsync(tab);
    }

    private WorkspaceTabViewModel CreateAndAddTab(string folderPath)
    {
        var tab = CreateTab(folderPath);
        WorkspaceTabs.Add(tab);
        OnPropertyChanged(nameof(HasTabs));
        return tab;
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

    private void EnsurePlaceholderTab()
    {
        if (WorkspaceTabs.Count > 0)
        {
            return;
        }

        var tab = CreateAndAddTab(string.Empty);
        SelectedTab = tab;
    }

    private async Task RunAutoPublishExclusiveAsync(Func<Task> action)
    {
        if (_isAutoPublishRunning)
        {
            StatusMessage = "已有自动上架任务正在执行，请稍后再试。";
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
            var nodes = await _folderScanService.ScanAsync(tab.RootFolder, recursive: true, progress);

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
                StatusMessage = "该目录下没有可显示的图片资源。";
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
                LoadingDetail = "目录或图片较多时会稍慢一些，请稍候。";
            }

            IsBusy = false;
        }
    }

    private async Task ChooseFolderAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "浏览文件夹",
            InitialDirectory = SelectedTab is not null && Directory.Exists(SelectedTab.RootFolder)
                ? SelectedTab.RootFolder
                : WorkspaceDefaults.DefaultOpenFolder,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            return;
        }

        await LoadFolderAsync(dialog.FolderName);
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

    private async Task ChooseTemplateLibraryAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择模板库 Excel",
            Filter = "Excel 文件|*.xlsx;*.xlsm;*.xls|所有文件|*.*",
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(TemplateLibraryPath))
                ? Path.GetDirectoryName(TemplateLibraryPath)
                : @"D:\temu_auto\temp"
        };

        if (dialog.ShowDialog() == true)
        {
            TemplateLibraryPath = dialog.FileName;
        }

        await Task.CompletedTask;
    }

    private async Task ChooseGenerationOutputFolderAsync()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择生图输出目录",
            InitialDirectory = Directory.Exists(GenerationOutputDirectory)
                ? GenerationOutputDirectory
                : WorkspaceDefaults.DefaultOpenFolder,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            GenerationOutputDirectory = dialog.SelectedPath;
        }

        await Task.CompletedTask;
    }

    private void OpenGenerationOutputFolder()
    {
        OpenFolder(GenerationOutputDirectory);
    }

    private async Task ChooseSpBatchInputFolderAsync()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择要批量处理的图片目录",
            InitialDirectory = Directory.Exists(SpBatchInputDirectory)
                ? SpBatchInputDirectory
                : WorkspaceDefaults.DefaultOpenFolder,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            SpBatchInputDirectory = dialog.SelectedPath;
        }

        await Task.CompletedTask;
    }

    private async Task ChooseSpBatchOutputFolderAsync()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择 SP 批处理输出目录",
            InitialDirectory = Directory.Exists(SpBatchOutputDirectory)
                ? SpBatchOutputDirectory
                : WorkspaceDefaults.DefaultSpBatchOutputFolder,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            SpBatchOutputDirectory = dialog.SelectedPath;
        }

        await Task.CompletedTask;
    }

    private void OpenSpBatchOutputFolder()
    {
        OpenFolder(SpBatchOutputDirectory);
    }

    private static void OpenFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folderPath}\"",
            UseShellExecute = true
        });
    }

    private async Task RunTemplateGenerationAsync()
    {
        if (!File.Exists(TemplateLibraryPath))
        {
            throw new InvalidOperationException($"模板库不存在：{TemplateLibraryPath}");
        }

        if (!TryParsePositiveInt(GenerationCountText, "生成数量", out var count))
        {
            return;
        }

        if (!TryParsePositiveInt(GenerationConcurrencyText, "并发数", out var concurrency))
        {
            return;
        }

        Directory.CreateDirectory(GenerationOutputDirectory);

        IsBusy = true;
        IsTemplateGenerating = true;
        GenerationStatusText = IsGenerationPromptsOnly ? "正在生成提示词..." : "正在批量生成图片...";
        GenerationResultText = "正在调用本地模板脚本...";
        GenerationResultModeText = $"模式：{(IsGenerationPromptsOnly ? "只出提示词" : "直接生图")}";
        GenerationResultOutputText = $"输出目录：{GenerationOutputDirectory}";
        ClearGenerationPromptCards();
        ClearGeneratedImageResultCards();
        ClearGeneratedImageResultCards();
        StatusMessage = GenerationStatusText;

        try
        {
            var request = new TemplateGenerateRequest
            {
                TemplatePath = TemplateLibraryPath,
                OutputDirectory = GenerationOutputDirectory,
                Image2ScriptPath = DefaultImage2ScriptPath,
                Count = count,
                Concurrency = concurrency,
                UniqueScene = IsGenerationUniqueScene,
                PromptsOnly = IsGenerationPromptsOnly
            };

            var result = await _templateGenerationService.GenerateAsync(request);
            GenerationStatusText = result.Mode == "prompts_only" ? "提示词生成完成" : "图片生成完成";
            GenerationResultText = BuildGenerationResultText(result);
            ApplyGenerationVisualResult(result);
            StatusMessage = result.Mode == "prompts_only"
                ? $"提示词生成完成，共 {result.Prompts.Count} 条。"
                : $"图片生成完成，共 {result.Items.Count} 张。";
        }
        catch (Exception ex)
        {
            GenerationStatusText = "执行失败";
            GenerationResultText = $"模板随机生成失败：{ex.Message}";
            GenerationResultModeText = "模式：执行失败";
            GenerationResultOutputText = $"输出目录：{GenerationOutputDirectory}";
            ClearGenerationPromptCards();
            ClearGeneratedImageResultCards();
            GenerationPromptCards.Add(new GenerationPromptCardViewModel
            {
                Title = "错误信息",
                PromptText = ex.Message
            });
            OnPropertyChanged(nameof(HasGenerationPromptCards));
            OnPropertyChanged(nameof(HasGeneratedImageResultCards));
            StatusMessage = GenerationResultText;
        }
        finally
        {
            IsTemplateGenerating = false;
            IsBusy = false;
        }
    }

    private async Task RunSpBatchAsync()
    {
        if (string.IsNullOrWhiteSpace(SpBatchInputDirectory) || !Directory.Exists(SpBatchInputDirectory))
        {
            throw new InvalidOperationException("请输入有效的批处理输入目录。");
        }

        if (!TryParsePositiveInt(SpBatchConcurrencyText, "并发数", out var concurrency))
        {
            return;
        }

        if (!TryParsePositiveInt(SpBatchRetriesText, "重试次数", out var retries))
        {
            return;
        }

        Directory.CreateDirectory(SpBatchOutputDirectory);

        IsBusy = true;
        IsSpBatchRunning = true;
        SpBatchStatusText = _spBatchMode switch
        {
            SpBatchMode.DryRun => "正在生成预检查计划...",
            SpBatchMode.PrepareOnly => "正在创建 SP 目录结构...",
            _ => "正在批量生成 SP 资源..."
        };
        SpBatchSummaryText = "任务执行中，请稍候。";
        SpBatchResultRootText = $"日期目录：{SpBatchOutputDirectory}";
        SpBatchResultStatsText = "SP 数量：0  总任务：0  成功：0  跳过：0  失败：0";
        ClearSpBatchResultCards();
        ClearSpBatchImageResultCards();
        ClearSpBatchImageResultCards();
        StatusMessage = SpBatchStatusText;

        try
        {
            var request = new SpBatchRequest
            {
                InputDirectory = SpBatchInputDirectory,
                OutputDirectory = SpBatchOutputDirectory,
                Image2ScriptPath = DefaultImage2ScriptPath,
                Concurrency = concurrency,
                Retries = retries,
                Mode = _spBatchMode
            };

            var result = await _spBatchService.GenerateAsync(request);
            ApplySpBatchVisualResult(result);
            StatusMessage = SpBatchSummaryText;
        }
        catch (Exception ex)
        {
            SpBatchStatusText = "执行失败";
            SpBatchSummaryText = $"SP 批处理失败：{ex.Message}";
            ClearSpBatchResultCards();
            ClearSpBatchImageResultCards();
            SpBatchResultCards.Add(new SpBatchResultCardViewModel
            {
                Title = "错误信息",
                SummaryText = ex.Message,
                StatusText = "失败"
            });
            OnPropertyChanged(nameof(HasSpBatchResultCards));
            OnPropertyChanged(nameof(HasSpBatchImageResultCards));
            StatusMessage = SpBatchSummaryText;
        }
        finally
        {
            IsSpBatchRunning = false;
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
        LoadingTitle = "正在生成商品信息表";
        LoadingDetail = $"正在处理：{firstCard.RootFolderPath}";
        ScanProgressValue = 0;
        IsScanProgressIndeterminate = true;
        StatusMessage = $"开始处理：{firstCard.RootFolderPath}";

        try
        {
            var result = await _productSheetService.GenerateAsync(firstCard.RootFolderPath);
            StatusMessage = $"商品信息表任务状态：{result.Status}";
            LoadingDetail = string.IsNullOrWhiteSpace(result.OutputPath)
                ? StatusMessage
                : $"{StatusMessage}，{result.OutputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成商品信息表失败：{ex.Message}";
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

            PreviewMeta = $"预览失败：{ex.Message}";
            PreviewImageSource = null;
        }
    }

    private void ClearPreview()
    {
        PreviewImageSource = null;
        PreviewMeta = "大小：-\r\n完整路径：-";
    }

    private void ResetSummary()
    {
        LoadedFolderText = "当前文件夹：未选择";
        SelectionSummaryText = "图片 0 张，已选中 0 张，未选中 0 张";
        BackupFolderText = $"备份目录：{BackupFolder}";
        StatusMessage = "请选择文件夹开始筛图。";
    }

    private void ResetGenerationSummary()
    {
        GenerationPageDescription = "用于模板生图与 SP 批量处理";
        GenerationStatusText = "待命";
        GenerationResultText = "这里会显示本次生成的提示词、输出目录和结果明细。";
        GenerationResultModeText = "模式：待命";
        GenerationResultOutputText = $"输出目录：{GenerationOutputDirectory}";
        ClearGenerationPromptCards();
    }

    private void ResetSpBatchSummary()
    {
        SpBatchStatusText = "待命";
        SpBatchSummaryText = "用于批量创建日期目录、SP 结构和 6 色 SKU 图。";
        SpBatchResultRootText = $"日期目录：{SpBatchOutputDirectory}";
        SpBatchResultStatsText = "SP 数量：0  总任务：0  成功：0  跳过：0  失败：0";
        ClearSpBatchResultCards();
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
            EnsurePlaceholderTab();
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

    private void SetSelectedSection(string section)
    {
        if (section == _selectedSection)
        {
            return;
        }

        _selectedSection = section;
        OnPropertyChanged(nameof(IsReviewWorkspaceSelected));
        OnPropertyChanged(nameof(IsImageGenerateSelected));
    }

    private void SetSelectedImageGenerateTab(string tabKey)
    {
        if (_selectedImageGenerateTab == tabKey)
        {
            return;
        }

        _selectedImageGenerateTab = tabKey;
        OnPropertyChanged(nameof(IsTemplateGenerateTabSelected));
        OnPropertyChanged(nameof(IsSpBatchTabSelected));
    }

    private void SetSpBatchMode(SpBatchMode mode)
    {
        if (_spBatchMode == mode)
        {
            return;
        }

        _spBatchMode = mode;
        OnPropertyChanged(nameof(IsSpBatchDryRun));
        OnPropertyChanged(nameof(IsSpBatchPrepareOnly));
        OnPropertyChanged(nameof(IsSpBatchGenerateMode));
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

    private static bool TryParsePositiveInt(string text, string fieldName, out int value)
    {
        if (!int.TryParse(text, out value) || value <= 0)
        {
            throw new InvalidOperationException($"{fieldName}必须是大于 0 的整数。");
        }

        return true;
    }

    private static string BuildGenerationResultText(TemplateGenerateResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"模式：{(result.Mode == "prompts_only" ? "只出提示词" : "直接生图")}");
        builder.AppendLine($"输出目录：{result.OutputDirectory}");
        builder.AppendLine();

        if (result.Prompts.Count > 0)
        {
            builder.AppendLine("提示词：");
            for (var index = 0; index < result.Prompts.Count; index++)
            {
                builder.AppendLine($"{index + 1}. {result.Prompts[index]}");
            }
        }

        if (result.Items.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("生成结果：");
            foreach (var item in result.Items)
            {
                builder.AppendLine($"#{item.Index}  文件名：{item.FileName}");
                builder.AppendLine($"图片路径：{item.ImagePath}");
                builder.AppendLine($"提示词：{item.Prompt}");
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private void ApplyGenerationResult(TemplateGenerateResult result)
    {
        GenerationResultModeText = $"模式：{(result.Mode == "prompts_only" ? "只出提示词" : "直接生图")}";
        GenerationResultOutputText = $"输出目录：{result.OutputDirectory}";
        ClearGenerationPromptCards();

        if (result.Prompts.Count > 0)
        {
            for (var index = 0; index < result.Prompts.Count; index++)
            {
                var metaText = string.Empty;
                var item = result.Items.FirstOrDefault(x => x.Index == index + 1);
                if (item is not null)
                {
                    metaText = string.Join(
                        Environment.NewLine,
                        new[]
                        {
                            string.IsNullOrWhiteSpace(item.FileName) ? null : $"文件名：{item.FileName}",
                            string.IsNullOrWhiteSpace(item.ImagePath) ? null : $"图片路径：{item.ImagePath}"
                        }.Where(text => !string.IsNullOrWhiteSpace(text)));
                }

                GenerationPromptCards.Add(new GenerationPromptCardViewModel
                {
                    Title = $"提示词 {index + 1}",
                    PromptText = result.Prompts[index],
                    MetaText = metaText
                });
            }
        }
        else if (result.Items.Count > 0)
        {
            foreach (var item in result.Items)
            {
                GenerationPromptCards.Add(new GenerationPromptCardViewModel
                {
                    Title = $"结果 {item.Index}",
                    PromptText = item.Prompt,
                    MetaText = string.Join(
                        Environment.NewLine,
                        new[]
                        {
                            string.IsNullOrWhiteSpace(item.FileName) ? null : $"文件名：{item.FileName}",
                            string.IsNullOrWhiteSpace(item.ImagePath) ? null : $"图片路径：{item.ImagePath}"
                        }.Where(text => !string.IsNullOrWhiteSpace(text)))
                });
            }
        }

        OnPropertyChanged(nameof(HasGenerationPromptCards));
    }

    private void ClearGenerationPromptCards()
    {
        if (GenerationPromptCards.Count == 0)
        {
            return;
        }

        GenerationPromptCards.Clear();
        OnPropertyChanged(nameof(HasGenerationPromptCards));
    }

    private void ApplySpBatchResult(SpBatchResult result)
    {
        SpBatchStatusText = result.Mode switch
        {
            "dry_run" => "预检查完成",
            "prepared" => "目录结构创建完成",
            _ => result.FailedCount > 0 ? "部分执行失败" : "批处理完成"
        };

        SpBatchSummaryText = result.Mode switch
        {
            "dry_run" => $"预检查完成，共规划 {result.Results.Count} 个颜色任务。",
            "prepared" => $"目录结构创建完成，共准备 {result.PreparedBundles.Count} 个 SP 目录。",
            _ => $"批处理完成，共 {result.Results.Count} 个任务，成功 {result.SuccessCount}，跳过 {result.SkippedCount}，失败 {result.FailedCount}。"
        };

        SpBatchResultRootText = $"日期目录：{result.DatedRoot}";
        SpBatchResultStatsText = $"SP 数量：{CountSpDirectories(result)}  总任务：{result.Results.Count}  成功：{result.SuccessCount}  跳过：{result.SkippedCount}  失败：{result.FailedCount}";
        ClearSpBatchResultCards();

        foreach (var group in result.Results.GroupBy(item => item.SpDirectory).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var title = Path.GetFileName(group.Key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var successCount = group.Count(item => string.Equals(item.Status, "generated", StringComparison.OrdinalIgnoreCase));
            var skippedCount = group.Count(item => string.Equals(item.Status, "skipped", StringComparison.OrdinalIgnoreCase));
            var failedCount = group.Count(item => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase));
            var plannedCount = group.Count(item => string.Equals(item.Status, "planned", StringComparison.OrdinalIgnoreCase));

            var bundle = result.PreparedBundles.FirstOrDefault(item => string.Equals(item.SpDirectory, group.Key, StringComparison.OrdinalIgnoreCase));
            var detailLines = new List<string>();
            if (bundle is not null)
            {
                detailLines.Add($"main：{bundle.MainDirectory}");
                detailLines.Add($"sku：{bundle.SkuDirectory}");
                detailLines.Add($"detail：{bundle.DetailDirectory}");
                detailLines.Add($"封面：{bundle.SourceCopyPath}");
            }

            var colorLine = string.Join(" / ", group.Select(item => item.Color).Distinct(StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(colorLine))
            {
                detailLines.Add($"颜色：{colorLine}");
            }

            SpBatchResultCards.Add(new SpBatchResultCardViewModel
            {
                Title = string.IsNullOrWhiteSpace(title) ? group.Key : title,
                SummaryText = $"成功 {successCount}  跳过 {skippedCount}  失败 {failedCount}  计划 {plannedCount}",
                DetailText = string.Join(Environment.NewLine, detailLines),
                StatusText = failedCount > 0
                    ? "失败"
                    : plannedCount > 0
                        ? "预检查"
                        : result.Mode == "prepared"
                            ? "已建结构"
                            : "完成"
            });
        }

        if (SpBatchResultCards.Count == 0 && result.PreparedBundles.Count > 0)
        {
            foreach (var bundle in result.PreparedBundles)
            {
                var title = Path.GetFileName(bundle.SpDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                SpBatchResultCards.Add(new SpBatchResultCardViewModel
                {
                    Title = title,
                    SummaryText = "目录结构已创建",
                    DetailText = $"main：{bundle.MainDirectory}{Environment.NewLine}sku：{bundle.SkuDirectory}{Environment.NewLine}detail：{bundle.DetailDirectory}{Environment.NewLine}封面：{bundle.SourceCopyPath}",
                    StatusText = "已建结构"
                });
            }
        }

        OnPropertyChanged(nameof(HasSpBatchResultCards));
    }

    private void ClearSpBatchResultCards()
    {
        if (SpBatchResultCards.Count == 0)
        {
            return;
        }

        SpBatchResultCards.Clear();
        OnPropertyChanged(nameof(HasSpBatchResultCards));
        OnPropertyChanged(nameof(HasAnySpBatchResultCards));
        OnPropertyChanged(nameof(ShouldShowSpBatchDetailCards));
    }

    private void ClearGeneratedImageResultCards()
    {
        if (GeneratedImageResultCards.Count == 0)
        {
            return;
        }

        GeneratedImageResultCards.Clear();
        OnPropertyChanged(nameof(HasGeneratedImageResultCards));
        OnPropertyChanged(nameof(HasAnyGenerationResultCards));
    }

    private void ClearSpBatchImageResultCards()
    {
        if (SpBatchImageResultCards.Count == 0)
        {
            return;
        }

        SpBatchImageResultCards.Clear();
        OnPropertyChanged(nameof(HasSpBatchImageResultCards));
        OnPropertyChanged(nameof(HasAnySpBatchResultCards));
        OnPropertyChanged(nameof(ShouldShowSpBatchDetailCards));
    }

    private void ApplyGenerationVisualResult(TemplateGenerateResult result)
    {
        GenerationResultModeText = $"模式：{(result.Mode == "prompts_only" ? "只出提示词" : "直接生图")}";
        GenerationResultOutputText = $"输出目录：{result.OutputDirectory}";

        if (string.Equals(result.Mode, "prompts_only", StringComparison.OrdinalIgnoreCase))
        {
            if (GenerationPromptCards.Count > 0)
            {
                OnPropertyChanged(nameof(HasGenerationPromptCards));
                OnPropertyChanged(nameof(HasAnyGenerationResultCards));
            }

            OnPropertyChanged(nameof(HasGeneratedImageResultCards));
            OnPropertyChanged(nameof(HasAnyGenerationResultCards));
            return;
        }

        foreach (var item in result.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ImagePath) || !File.Exists(item.ImagePath))
            {
                continue;
            }

            GeneratedImageResultCards.Add(new GeneratedImageResultCardViewModel(item.ImagePath, item.FileName));
        }

        OnPropertyChanged(nameof(HasGenerationPromptCards));
        OnPropertyChanged(nameof(HasGeneratedImageResultCards));
        OnPropertyChanged(nameof(HasAnyGenerationResultCards));
    }

    private void ApplySpBatchVisualResult(SpBatchResult result)
    {
        ApplySpBatchResult(result);

        foreach (var item in result.Results
                     .Where(item => string.Equals(item.Status, "generated", StringComparison.OrdinalIgnoreCase))
                     .Where(item => !string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
                     .OrderBy(item => item.SpDirectory, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Index))
        {
            SpBatchImageResultCards.Add(new GeneratedImageResultCardViewModel(item.ImagePath, Path.GetFileName(item.ImagePath)));
        }

        OnPropertyChanged(nameof(HasSpBatchImageResultCards));
        OnPropertyChanged(nameof(HasAnySpBatchResultCards));
        OnPropertyChanged(nameof(ShouldShowSpBatchDetailCards));
    }

    private static int CountSpDirectories(SpBatchResult result)
    {
        return result.PreparedBundles
            .Select(item => item.SpDirectory)
            .Concat(result.Results.Select(item => item.SpDirectory))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }
}
