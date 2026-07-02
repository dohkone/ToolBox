using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Windows;
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
    private static string DefaultTemplateLibraryPath => ResolveTemplateLibraryPath();
    private static string DefaultImage2ScriptPath => ResolveImage2ScriptPath();

    private readonly IFolderScanService _folderScanService;
    private readonly IImageWorkspaceService _imageWorkspaceService;
    private readonly IWorkspaceStateService _workspaceStateService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IProductSheetService _productSheetService;
    private readonly ITemplateGenerationService _templateGenerationService;
    private readonly ISpBatchService _spBatchService;
    private readonly IMiaoshouPublishService _miaoshouPublishService;
    private readonly IAutoPublishStateService _autoPublishStateService;
    private readonly SemaphoreSlim _autoPublishLock = new(1, 1);
    private readonly AsyncRelayCommand _chooseFolderCommand;
    private readonly AsyncRelayCommand _selectBackupFolderCommand;
    private readonly RelayCommand _invertSelectionCommand;
    private readonly RelayCommand _selectAllBatchCardsCommand;
    private readonly RelayCommand _clearAllBatchCardsCommand;
    private readonly RelayCommand _setAutoPublishStatusFilterCommand;
    private readonly AsyncRelayCommand _generateProductSheetCommand;
    private readonly AsyncRelayCommand _addTabCommand;
    private readonly RelayCommand _showReviewWorkspaceCommand;
    private readonly RelayCommand _showImageGenerateCommand;
    private readonly RelayCommand _showTemplateGenerateTabCommand;
    private readonly RelayCommand _showSpBatchTabCommand;
    private readonly AsyncRelayCommand _chooseTemplateLibraryCommand;
    private readonly RelayCommand _openTemplateLibraryFileCommand;
    private readonly AsyncRelayCommand _chooseGenerationOutputFolderCommand;
    private readonly RelayCommand _openGenerationOutputFolderCommand;
    private readonly AsyncRelayCommand _runTemplateGenerationCommand;
    private readonly RelayCommand _stopTemplateGenerationCommand;
    private readonly AsyncRelayCommand _chooseGenerationImagesCommand;
    private readonly RelayCommand _sendSelectedImagesToSpBatchCommand;
    private readonly AsyncRelayCommand _chooseSpBatchInputFolderCommand;
    private readonly AsyncRelayCommand _chooseSpBatchOutputFolderCommand;
    private readonly AsyncRelayCommand _runSpBatchCommand;
    private readonly AsyncRelayCommand _runBatchAutoPublishCommand;
    private CancellationTokenSource? _previewCancellationTokenSource;
    private CancellationTokenSource? _templateGenerationCancellationTokenSource;
    private WorkspaceTabViewModel? _selectedTab;
    private string _selectedSection = ReviewWorkspaceSection;
    private string _selectedImageGenerateTab = TemplateGenerateTab;
    private bool _isBusy;
    private bool _isScanProgressIndeterminate = true;
    private double _scanProgressValue;
    private string _statusMessage = "请选择文件夹开始筛图。";
    private string _loadedFolderText = "当前文件夹：未选择";
    private string _selectionSummaryText = "图片 0 张，已选中 0 张，未选中 0 张";
    private string _backupFolderText = "备份目录：未设置";
    private AutoPublishStatusFilter _selectedAutoPublishStatusFilter = AutoPublishStatusFilter.All;
    private string _loadingTitle = "正在加载图片资源...";
    private string _loadingDetail = "目录或图片较多时会稍慢一些，请稍候。";
    private string _scanProgressText = "等待加载";
    private string _previewMeta = "大小：-\r\n完整路径：-";
    private string _previewImagePath = string.Empty;
    private Media.ImageSource? _previewImageSource;
    private string _backupFolder = string.Empty;
    private bool _isAutoPublishRunning;
    private bool _isTemplateGenerating;
    private bool _isTemplateGenerationStopping;
    private string _templateLibraryPath = string.Empty;
    private string _generationOutputDirectory = string.Empty;
    private string _generationCountText = "1";
    private string _generationConcurrencyText = "1";
    private bool _isGenerationUniqueScene = true;
    private bool _isGenerationPromptsOnly = false;
    private string _generationPageDescription = "用于模板生图与 SP 批量处理";
    private string _generationStatusText = "待命";
    private string _generationResultModeText = "模式：待命";
    private string _generationResultOutputText = "输出目录：未设置";
    private bool _isSpBatchRunning;
    private string _spBatchInputDirectory = string.Empty;
    private string _spBatchOutputDirectory = string.Empty;
    private string _spBatchConcurrencyText = "2";
    private string _spBatchRetriesText = "4";
    private SpBatchMode _spBatchMode = SpBatchMode.Generate;
    private string _spBatchStatusText = "待命";
    private string _spBatchSummaryText = "用于批量创建日期目录、SP 结构和 6 色 SKU 图。";
    private string _spBatchResultRootText = "日期目录：未设置";
    private string _spBatchResultStatsText = "SP 数量：0  总任务：0  成功：0  跳过：0  失败：0";

    private bool _isSpBatchStagingDropTarget;

    public MainWindowViewModel(
        IFolderScanService folderScanService,
        IImageWorkspaceService imageWorkspaceService,
        IWorkspaceStateService workspaceStateService,
        IAppSettingsService appSettingsService,
        IProductSheetService productSheetService,
        ITemplateGenerationService templateGenerationService,
        ISpBatchService spBatchService,
        IMiaoshouPublishService miaoshouPublishService,
        IAutoPublishStateService autoPublishStateService)
    {
        _folderScanService = folderScanService;
        _imageWorkspaceService = imageWorkspaceService;
        _workspaceStateService = workspaceStateService;
        _appSettingsService = appSettingsService;
        _productSheetService = productSheetService;
        _templateGenerationService = templateGenerationService;
        _spBatchService = spBatchService;
        _miaoshouPublishService = miaoshouPublishService;
        _autoPublishStateService = autoPublishStateService;

        _chooseFolderCommand = new AsyncRelayCommand(_ => ChooseFolderAsync());
        _selectBackupFolderCommand = new AsyncRelayCommand(_ => SelectBackupFolderAsync());
        _invertSelectionCommand = new RelayCommand(_ => SelectedTab?.InvertSelection(), _ => !IsBusy && SelectedTab?.ActiveCard is not null);
        _selectAllBatchCardsCommand = new RelayCommand(_ => SelectAllBatchCards(), _ => CanModifyBatchSelection());
        _clearAllBatchCardsCommand = new RelayCommand(_ => ClearAllBatchCards(), _ => CanModifyBatchSelection());
        _setAutoPublishStatusFilterCommand = new RelayCommand(parameter => SetAutoPublishStatusFilter(parameter));
        _generateProductSheetCommand = new AsyncRelayCommand(_ => GenerateProductSheetAsync(), _ => !IsBusy && SelectedTab?.RootCards.Count > 0);
        _addTabCommand = new AsyncRelayCommand(_ => ChooseFolderAsync(), _ => !IsBusy);
        _showReviewWorkspaceCommand = new RelayCommand(_ => SetSelectedSection(ReviewWorkspaceSection));
        _showImageGenerateCommand = new RelayCommand(_ => SetSelectedSection(ImageGenerateSection));
        _showTemplateGenerateTabCommand = new RelayCommand(_ => SetSelectedImageGenerateTab(TemplateGenerateTab));
        _showSpBatchTabCommand = new RelayCommand(_ => SetSelectedImageGenerateTab(SpBatchTab));
        _chooseTemplateLibraryCommand = new AsyncRelayCommand(_ => ChooseTemplateLibraryAsync(), _ => !IsBusy);
        _openTemplateLibraryFileCommand = new RelayCommand(_ => OpenTemplateLibraryFile(), _ => File.Exists(TemplateLibraryPath));
        _chooseGenerationOutputFolderCommand = new AsyncRelayCommand(_ => ChooseGenerationOutputFolderAsync(), _ => !IsBusy);
        _openGenerationOutputFolderCommand = new RelayCommand(_ => OpenGenerationOutputFolder(), _ => Directory.Exists(GenerationOutputDirectory));
        _runTemplateGenerationCommand = new AsyncRelayCommand(_ => RunTemplateGenerationAsync(), _ => !IsBusy);
        _stopTemplateGenerationCommand = new RelayCommand(_ => StopTemplateGeneration(), _ => IsTemplateGenerating);
        _chooseGenerationImagesCommand = new AsyncRelayCommand(_ => ChooseGenerationImagesAsync(), _ => !IsBusy);
        _sendSelectedImagesToSpBatchCommand = new RelayCommand(_ => SendSelectedImagesToSpBatch(), _ => CanSendSelectedImagesToSpBatch());
        _chooseSpBatchInputFolderCommand = new AsyncRelayCommand(_ => ChooseSpBatchInputFolderAsync(), _ => !IsBusy);
        _chooseSpBatchOutputFolderCommand = new AsyncRelayCommand(_ => ChooseSpBatchOutputFolderAsync(), _ => !IsBusy);
        _runSpBatchCommand = new AsyncRelayCommand(_ => RunSpBatchFromStagingAsync(), _ => !IsBusy);
        _runBatchAutoPublishCommand = new AsyncRelayCommand(_ => RunBatchAutoPublishAsync(), _ => CanExecuteBatchAutoPublish());
    }

    public ObservableCollection<WorkspaceTabViewModel> WorkspaceTabs { get; } = [];
    public ObservableCollection<GenerationPromptCardViewModel> GenerationPromptCards { get; } = [];
    public ObservableCollection<GeneratedImageResultCardViewModel> GeneratedImageResultCards { get; } = [];
    public ObservableCollection<GeneratedImageResultCardViewModel> SpBatchSourceImageCards { get; } = [];
    public ObservableCollection<GeneratedImageResultCardViewModel> SpBatchImageResultCards { get; } = [];
    public ObservableCollection<SpBatchResultCardViewModel> SpBatchResultCards { get; } = [];

    public ICommand ChooseFolderCommand => _chooseFolderCommand;
    public ICommand SelectBackupFolderCommand => _selectBackupFolderCommand;
    public ICommand InvertSelectionCommand => _invertSelectionCommand;
    public ICommand SelectAllBatchCardsCommand => _selectAllBatchCardsCommand;
    public ICommand ClearAllBatchCardsCommand => _clearAllBatchCardsCommand;
    public ICommand SetAutoPublishStatusFilterCommand => _setAutoPublishStatusFilterCommand;
    public ICommand GenerateProductSheetCommand => _generateProductSheetCommand;
    public ICommand AddTabCommand => _addTabCommand;
    public ICommand ShowReviewWorkspaceCommand => _showReviewWorkspaceCommand;
    public ICommand ShowImageGenerateCommand => _showImageGenerateCommand;
    public ICommand ShowTemplateGenerateTabCommand => _showTemplateGenerateTabCommand;
    public ICommand ShowSpBatchTabCommand => _showSpBatchTabCommand;
    public ICommand ChooseTemplateLibraryCommand => _chooseTemplateLibraryCommand;
    public ICommand OpenTemplateLibraryFileCommand => _openTemplateLibraryFileCommand;
    public ICommand ChooseGenerationOutputFolderCommand => _chooseGenerationOutputFolderCommand;
    public ICommand OpenGenerationOutputFolderCommand => _openGenerationOutputFolderCommand;
    public ICommand RunTemplateGenerationCommand => _runTemplateGenerationCommand;
    public ICommand StopTemplateGenerationCommand => _stopTemplateGenerationCommand;
    public ICommand ChooseGenerationImagesCommand => _chooseGenerationImagesCommand;
    public ICommand SendSelectedImagesToSpBatchCommand => _sendSelectedImagesToSpBatchCommand;
    public ICommand ChooseSpBatchInputFolderCommand => _chooseSpBatchInputFolderCommand;
    public ICommand ChooseSpBatchOutputFolderCommand => _chooseSpBatchOutputFolderCommand;
    public ICommand RunSpBatchCommand => _runSpBatchCommand;
    public ICommand RunBatchAutoPublishCommand => _runBatchAutoPublishCommand;

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
            OnPropertyChanged(nameof(HasBatchSelectedCards));
            ApplyAutoPublishStatusFilter();
            NotifyAutoPublishFilterPropertiesChanged();
            _invertSelectionCommand.RaiseCanExecuteChanged();
            _selectAllBatchCardsCommand.RaiseCanExecuteChanged();
            _clearAllBatchCardsCommand.RaiseCanExecuteChanged();
            _generateProductSheetCommand.RaiseCanExecuteChanged();
            _runBatchAutoPublishCommand.RaiseCanExecuteChanged();
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
    public bool HasBatchSelectedCards => SelectedTab?.GetBatchSelectedCards().Count > 0;
    public bool CanRunBatchAutoPublish => HasBatchSelectedCards && !IsBusy && !_isAutoPublishRunning;
    public AutoPublishStatusFilter SelectedAutoPublishStatusFilter
    {
        get => _selectedAutoPublishStatusFilter;
        private set
        {
            if (!SetProperty(ref _selectedAutoPublishStatusFilter, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsAllStatusFilterSelected));
            OnPropertyChanged(nameof(IsNotPublishedStatusFilterSelected));
            OnPropertyChanged(nameof(IsPublishingStatusFilterSelected));
            OnPropertyChanged(nameof(IsSuccessStatusFilterSelected));
            OnPropertyChanged(nameof(IsFailedStatusFilterSelected));
        }
    }

    public string AllStatusFilterText => $"全部 {CountAutoPublishStatusFilter(AutoPublishStatusFilter.All)}";
    public string NotPublishedStatusFilterText => $"未上架 {CountAutoPublishStatusFilter(AutoPublishStatusFilter.NotPublished)}";
    public string PublishingStatusFilterText => $"上架中 {CountAutoPublishStatusFilter(AutoPublishStatusFilter.Publishing)}";
    public string SuccessStatusFilterText => $"上架成功 {CountAutoPublishStatusFilter(AutoPublishStatusFilter.Success)}";
    public string FailedStatusFilterText => $"上架失败 {CountAutoPublishStatusFilter(AutoPublishStatusFilter.Failed)}";
    public bool IsAutoPublishFilterEmpty => SelectedTab is not null
        && SelectedTab.HasRootCards
        && SelectedTab.FilteredRootCards.Count == 0;
    public string AutoPublishFilterEmptyText => SelectedAutoPublishStatusFilter switch
    {
        AutoPublishStatusFilter.NotPublished => "当前没有未上架的卡片",
        AutoPublishStatusFilter.Publishing => "当前没有上架中的卡片",
        AutoPublishStatusFilter.Success => "当前没有上架成功的卡片",
        AutoPublishStatusFilter.Failed => "当前没有上架失败的卡片",
        _ => "当前没有可显示的卡片"
    };
    public bool IsAllStatusFilterSelected => SelectedAutoPublishStatusFilter == AutoPublishStatusFilter.All;
    public bool IsNotPublishedStatusFilterSelected => SelectedAutoPublishStatusFilter == AutoPublishStatusFilter.NotPublished;
    public bool IsPublishingStatusFilterSelected => SelectedAutoPublishStatusFilter == AutoPublishStatusFilter.Publishing;
    public bool IsSuccessStatusFilterSelected => SelectedAutoPublishStatusFilter == AutoPublishStatusFilter.Success;
    public bool IsFailedStatusFilterSelected => SelectedAutoPublishStatusFilter == AutoPublishStatusFilter.Failed;
    public bool CanOpenTemplateLibraryFile => File.Exists(TemplateLibraryPath);
    public bool CanOpenGenerationOutputFolder => Directory.Exists(GenerationOutputDirectory);
    public bool HasGenerationPromptCards => GenerationPromptCards.Count > 0;
    public bool HasGeneratedImageResultCards => GeneratedImageResultCards.Count > 0;
    public bool HasAnyGenerationResultCards => HasGenerationPromptCards || HasGeneratedImageResultCards;
    public bool HasSelectedGeneratedImages => GeneratedImageResultCards.Any(card => card.IsSelected);
    public bool CanGenerateSkuFromTemplate => !IsGenerationPromptsOnly && HasSelectedGeneratedImages && !IsBusy;
    public string TemplateGenerationButtonText => IsTemplateGenerationStopping
        ? "正在停止..."
        : IsTemplateGenerating
            ? "停止执行"
            : "开始执行";
    public bool HasSpBatchResultCards => SpBatchResultCards.Count > 0;
    public bool HasSpBatchImageResultCards => SpBatchImageResultCards.Count > 0;
    public bool HasAnySpBatchResultCards => HasSpBatchResultCards || HasSpBatchImageResultCards;
    public bool HasSpBatchSourceImageCards => SpBatchSourceImageCards.Count > 0;
    public bool ShouldShowSpBatchDetailCards => HasSpBatchResultCards && !HasSpBatchImageResultCards;
    public bool IsSpBatchStagingDropTarget
    {
        get => _isSpBatchStagingDropTarget;
        private set => SetProperty(ref _isSpBatchStagingDropTarget, value);
    }

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
            OnPropertyChanged(nameof(CanRunBatchAutoPublish));
            NotifyAutoPublishFilterPropertiesChanged();
            _chooseFolderCommand.RaiseCanExecuteChanged();
            _selectBackupFolderCommand.RaiseCanExecuteChanged();
            _invertSelectionCommand.RaiseCanExecuteChanged();
            _selectAllBatchCardsCommand.RaiseCanExecuteChanged();
            _clearAllBatchCardsCommand.RaiseCanExecuteChanged();
            _generateProductSheetCommand.RaiseCanExecuteChanged();
            _addTabCommand.RaiseCanExecuteChanged();
            _chooseTemplateLibraryCommand.RaiseCanExecuteChanged();
            _chooseGenerationOutputFolderCommand.RaiseCanExecuteChanged();
            _runTemplateGenerationCommand.RaiseCanExecuteChanged();
            _stopTemplateGenerationCommand.RaiseCanExecuteChanged();
            _chooseGenerationImagesCommand.RaiseCanExecuteChanged();
            _sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
            _chooseSpBatchInputFolderCommand.RaiseCanExecuteChanged();
            _chooseSpBatchOutputFolderCommand.RaiseCanExecuteChanged();
            _runSpBatchCommand.RaiseCanExecuteChanged();
            _runBatchAutoPublishCommand.RaiseCanExecuteChanged();
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

            PersistUserPathSettings();
            BackupFolderText = string.IsNullOrWhiteSpace(value)
                ? "备份目录：未设置"
                : $"备份目录：{value}";
            foreach (var tab in WorkspaceTabs)
            {
                tab.SetBackupFolder(value);
            }
        }
    }

    public bool IsTemplateGenerating
    {
        get => _isTemplateGenerating;
        private set
        {
            if (!SetProperty(ref _isTemplateGenerating, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TemplateGenerationButtonText));
            _stopTemplateGenerationCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsTemplateGenerationStopping
    {
        get => _isTemplateGenerationStopping;
        private set
        {
            if (!SetProperty(ref _isTemplateGenerationStopping, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TemplateGenerationButtonText));
            _stopTemplateGenerationCommand.RaiseCanExecuteChanged();
        }
    }

    public string GenerationPageDescription
    {
        get => _generationPageDescription;
        private set => SetProperty(ref _generationPageDescription, value);
    }

    public string TemplateLibraryPath
    {
        get => _templateLibraryPath;
        set
        {
            if (!SetProperty(ref _templateLibraryPath, value))
            {
                return;
            }

            PersistUserPathSettings();
            OnPropertyChanged(nameof(CanOpenTemplateLibraryFile));
            _openTemplateLibraryFileCommand.RaiseCanExecuteChanged();
        }
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

            PersistUserPathSettings();
            GenerationResultOutputText = string.IsNullOrWhiteSpace(value)
                ? "输出目录：未设置"
                : $"输出目录：{value}";
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
        set
        {
            if (!SetProperty(ref _isGenerationUniqueScene, value))
            {
                return;
            }

            if (value && _isGenerationPromptsOnly)
            {
                _isGenerationPromptsOnly = false;
                OnPropertyChanged(nameof(IsGenerationPromptsOnly));
            }
        }
    }

    public bool IsGenerationPromptsOnly
    {
        get => _isGenerationPromptsOnly;
        set
        {
            if (!SetProperty(ref _isGenerationPromptsOnly, value))
            {
                return;
            }

            if (value && _isGenerationUniqueScene)
            {
                _isGenerationUniqueScene = false;
                OnPropertyChanged(nameof(IsGenerationUniqueScene));
            }

            OnPropertyChanged(nameof(CanGenerateSkuFromTemplate));
            _sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
        }
    }

    public string GenerationStatusText
    {
        get => _generationStatusText;
        private set => SetProperty(ref _generationStatusText, value);
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
        set
        {
            if (!SetProperty(ref _spBatchInputDirectory, value))
            {
                return;
            }

            PersistUserPathSettings();
        }
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

            PersistUserPathSettings();
            SpBatchResultRootText = string.IsNullOrWhiteSpace(value)
                ? "日期目录：未设置"
                : $"日期目录：{value}";
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
        await _autoPublishStateService.InitializeAsync();
        var userSettings = _appSettingsService.LoadUserPaths();
        ApplyUserPathSettings(userSettings);
        EnsurePlaceholderTab();
        ResetSummary();
        ResetGenerationSummary();
        ResetSpBatchSummary();

        if (!string.IsNullOrWhiteSpace(userSettings.ReviewRootFolder) && Directory.Exists(userSettings.ReviewRootFolder))
        {
            await LoadFolderAsync(userSettings.ReviewRootFolder);
        }
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
        PersistUserPathSettings();
        await ReloadTabAsync(tab);
    }

    public async Task RefreshCurrentPageAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (IsReviewWorkspaceSelected)
        {
            var tab = SelectedTab;
            if (tab is null || tab.IsPlaceholder || string.IsNullOrWhiteSpace(tab.RootFolder) || !Directory.Exists(tab.RootFolder))
            {
                StatusMessage = "当前没有可刷新的文件夹。";
                return;
            }

            StatusMessage = $"正在刷新：{tab.RootFolder}";
            await ReloadTabAsync(tab);
            return;
        }

        if (IsImageGenerateSelected)
        {
            if (IsTemplateGenerateTabSelected)
            {
                ResetGenerationSummary();
                StatusMessage = "已刷新图片生成页。";
                return;
            }

            if (IsSpBatchTabSelected)
            {
                ResetSpBatchSummary();
                StatusMessage = "已刷新 SP 批处理页。";
            }
        }
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
            RunAutoPublishExclusiveAsync,
            SetCardAutoPublishStatusAsync,
            RunSingleAutoPublishAsync,
            OnBatchSelectionChanged);
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

    private void OnBatchSelectionChanged()
    {
        OnPropertyChanged(nameof(HasBatchSelectedCards));
        OnPropertyChanged(nameof(CanRunBatchAutoPublish));
        _selectAllBatchCardsCommand.RaiseCanExecuteChanged();
        _clearAllBatchCardsCommand.RaiseCanExecuteChanged();
        _runBatchAutoPublishCommand.RaiseCanExecuteChanged();
    }

    private void SetAutoPublishStatusFilter(object? parameter)
    {
        if (parameter is AutoPublishStatusFilter filter)
        {
            SelectedAutoPublishStatusFilter = filter;
        }
        else if (parameter is string text && Enum.TryParse(text, out AutoPublishStatusFilter parsed))
        {
            SelectedAutoPublishStatusFilter = parsed;
        }
        else
        {
            SelectedAutoPublishStatusFilter = AutoPublishStatusFilter.All;
        }

        ApplyAutoPublishStatusFilter();
        NotifyAutoPublishFilterPropertiesChanged();
    }

    private void ApplyAutoPublishStatusFilter()
    {
        SelectedTab?.ApplyAutoPublishStatusFilter(SelectedAutoPublishStatusFilter);
    }

    private int CountAutoPublishStatusFilter(AutoPublishStatusFilter filter)
    {
        return SelectedTab?.CountByAutoPublishStatusFilter(filter) ?? 0;
    }

    private void NotifyAutoPublishFilterPropertiesChanged()
    {
        OnPropertyChanged(nameof(AllStatusFilterText));
        OnPropertyChanged(nameof(NotPublishedStatusFilterText));
        OnPropertyChanged(nameof(PublishingStatusFilterText));
        OnPropertyChanged(nameof(SuccessStatusFilterText));
        OnPropertyChanged(nameof(FailedStatusFilterText));
        OnPropertyChanged(nameof(IsAutoPublishFilterEmpty));
        OnPropertyChanged(nameof(AutoPublishFilterEmptyText));
    }

    private bool CanExecuteBatchAutoPublish()
    {
        return CanRunBatchAutoPublish;
    }

    private bool CanModifyBatchSelection()
    {
        return !IsBusy
            && !_isAutoPublishRunning
            && (SelectedTab?.HasBatchSelectableCards() ?? false);
    }

    private void SelectAllBatchCards()
    {
        SelectedTab?.SetBatchSelectionForAll(true);
        StatusMessage = "已选中当前页全部可批量上架的小卡片。";
    }

    private void ClearAllBatchCards()
    {
        SelectedTab?.SetBatchSelectionForAll(false);
        StatusMessage = "已取消当前页全部小卡片的批量选中。";
    }

    private void NotifyAutoPublishStateChanged()
    {
        OnPropertyChanged(nameof(CanRunBatchAutoPublish));
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
            await tab.RefreshAutoPublishRecordsAsync(_autoPublishStateService);
            ApplyAutoPublishStatusFilter();
            NotifyAutoPublishFilterPropertiesChanged();
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
            InitialDirectory = Directory.Exists(BackupFolder) ? BackupFolder : string.Empty,
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
                : Directory.Exists(Path.GetDirectoryName(DefaultTemplateLibraryPath))
                    ? Path.GetDirectoryName(DefaultTemplateLibraryPath)
                : WorkspaceDefaults.DefaultTempFolder
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

    private async Task ChooseGenerationImagesAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择图片",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.jfif|所有文件|*.*",
            Multiselect = true,
            InitialDirectory = Directory.Exists(GenerationOutputDirectory)
                ? GenerationOutputDirectory
                : WorkspaceDefaults.DefaultOpenFolder
        };

        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
        {
            return;
        }

        ClearGenerationPromptCards();
        AddGenerationImages(dialog.FileNames);
        StatusMessage = $"已添加 {dialog.FileNames.Length} 张图片到模板生图区。";
        await Task.CompletedTask;
    }

    private void OpenGenerationOutputFolder()
    {
        OpenFolder(GenerationOutputDirectory);
    }

    private void OpenTemplateLibraryFile()
    {
        if (!File.Exists(TemplateLibraryPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = TemplateLibraryPath,
            UseShellExecute = true
        });
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

        if (!TryParsePositiveInt(GenerationConcurrencyText, "并发数量", out var concurrency))
        {
            return;
        }

        Directory.CreateDirectory(GenerationOutputDirectory);

        IsBusy = true;
        IsTemplateGenerating = true;
        GenerationStatusText = IsGenerationPromptsOnly ? "正在生成提示词..." : "正在批量生成图片...";
        GenerationResultModeText = $"模式：{(IsGenerationPromptsOnly ? "只出提示词" : "直接生图")}";
        GenerationResultOutputText = $"输出目录：{GenerationOutputDirectory}";
        if (IsGenerationPromptsOnly)
        {
            ClearGenerationPromptCards();
        }

        StatusMessage = GenerationStatusText;
        IsTemplateGenerationStopping = false;
        _templateGenerationCancellationTokenSource?.Cancel();
        _templateGenerationCancellationTokenSource?.Dispose();
        _templateGenerationCancellationTokenSource = new CancellationTokenSource();

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

            var result = await _templateGenerationService.GenerateAsync(request, _templateGenerationCancellationTokenSource.Token);
            GenerationStatusText = result.Mode == "prompts_only" ? "提示词生成完成" : "图片生成完成";
            ApplyGenerationResult(result);
            ApplyGenerationVisualResult(result);
            StatusMessage = result.Mode == "prompts_only"
                ? $"提示词生成完成，共 {result.Prompts.Count} 条。"
                : $"图片生成完成，共 {result.Items.Count} 张。";
        }
        catch (OperationCanceledException)
        {
            IsTemplateGenerationStopping = false;
            GenerationStatusText = "已停止";
            GenerationResultModeText = "模式：已停止";
            GenerationResultOutputText = $"输出目录：{GenerationOutputDirectory}";
            StatusMessage = "已停止当前生图任务。";
        }
        catch (Exception ex)
        {
            GenerationStatusText = "执行失败";
            GenerationResultModeText = "模式：执行失败";
            GenerationResultOutputText = $"输出目录：{GenerationOutputDirectory}";
            GenerationPromptCards.Add(new GenerationPromptCardViewModel
            {
                Title = "错误信息",
                PromptText = ex.Message
            });
            OnPropertyChanged(nameof(HasGenerationPromptCards));
            OnPropertyChanged(nameof(HasGeneratedImageResultCards));
            StatusMessage = $"模板随机生成失败：{ex.Message}";
        }
        finally
        {
            IsTemplateGenerationStopping = false;
            _templateGenerationCancellationTokenSource?.Dispose();
            _templateGenerationCancellationTokenSource = null;
            IsTemplateGenerating = false;
            IsBusy = false;
        }
    }

    private void StopTemplateGeneration()
    {
        if (!IsTemplateGenerating || IsTemplateGenerationStopping)
        {
            return;
        }

        IsTemplateGenerationStopping = true;
        GenerationStatusText = "正在停止...";
        StatusMessage = "正在停止当前生图任务...";
        _templateGenerationService.CancelCurrentRun();
        _templateGenerationCancellationTokenSource?.Cancel();
    }

    private async Task RunSpBatchAsync()
    {
        if (string.IsNullOrWhiteSpace(SpBatchInputDirectory) || !Directory.Exists(SpBatchInputDirectory))
        {
            throw new InvalidOperationException("请输入有效的批处理输入目录。");
        }

        if (!TryParsePositiveInt(SpBatchConcurrencyText, "并发数量", out var concurrency))
        {
            return;
        }

        var retries = 4;

        Directory.CreateDirectory(SpBatchOutputDirectory);

        IsBusy = true;
        IsSpBatchRunning = true;
        SpBatchStatusText = _spBatchMode switch
        {
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
        LoadingTitle = "正在生成上架 JSON";
        LoadingDetail = $"正在处理：{firstCard.RootFolderPath}";
        ScanProgressValue = 0;
        IsScanProgressIndeterminate = true;
        StatusMessage = $"开始处理：{firstCard.RootFolderPath}";

        try
        {
            var result = await _productSheetService.GenerateAsync(firstCard.RootFolderPath);
            StatusMessage = $"上架 JSON 任务状态：{result.Status}";
            LoadingDetail = string.IsNullOrWhiteSpace(result.ProductsJsonPath)
                ? StatusMessage
                : $"{StatusMessage}，{result.ProductsJsonPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成上架 JSON 失败：{ex.Message}";
            LoadingDetail = StatusMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunBatchAutoPublishAsync()
    {
        var selectedCards = SelectedTab?.GetBatchSelectedCards();
        if (selectedCards is null || selectedCards.Count == 0)
        {
            StatusMessage = "请先勾选需要批量上架的已修改卡片。";
            return;
        }

        await RunAutoPublishExclusiveAsync(async () =>
        {
            IsBusy = true;
            LoadingTitle = "正在批量上架";
            IsScanProgressIndeterminate = true;
            ScanProgressValue = 0;
            var publishingCards = new List<RootCardViewModel>();

            try
            {
                var productItems = new List<JsonElement>();
                for (var index = 0; index < selectedCards.Count; index++)
                {
                    var card = selectedCards[index];
                    var summary = card.CollapsedSummaryText;
                    LoadingDetail = $"正在准备第 {index + 1}/{selectedCards.Count} 个：{summary}";
                    StatusMessage = $"正在准备妙手上架数据：{index + 1}/{selectedCards.Count}";
                    await SetCardAutoPublishStatusAsync(card, AutoPublishStatus.Publishing);
                    publishingCards.Add(card);
                    productItems.AddRange(await PrepareAutoPublishProductItemsAsync(card));
                }

                var result = await RunMiaoshouPublishAsync(productItems, selectedCards);
                await ApplyBatchPublishResultAsync(selectedCards, result);

                foreach (var card in selectedCards)
                {
                    card.IsBatchSelected = false;
                }

                LoadingDetail = $"妙手批量上架完成：成功 {result.SuccessCount}，失败 {result.FailedCount}。";
                StatusMessage = LoadingDetail;
            }
            catch (Exception ex)
            {
                await MarkPublishingCardsFailedAsync(publishingCards, ex.Message);
                LoadingDetail = ex.Message;
                StatusMessage = $"批量上架失败：{ex.Message}";
                throw;
            }
            finally
            {
                IsBusy = false;
                OnBatchSelectionChanged();
            }
        });
    }

    private async Task RunSingleAutoPublishAsync(RootCardViewModel card)
    {
        await RunAutoPublishExclusiveAsync(async () =>
        {
            IsBusy = true;
            LoadingTitle = "正在自动上架";
            IsScanProgressIndeterminate = true;
            ScanProgressValue = 0;

            try
            {
                LoadingDetail = $"正在准备：{card.CollapsedSummaryText}";
                StatusMessage = $"正在准备妙手上架数据：{card.DisplayName}";
                await SetCardAutoPublishStatusAsync(card, AutoPublishStatus.Publishing);

                var productItems = await PrepareAutoPublishProductItemsAsync(card);
                var result = await RunMiaoshouPublishAsync(productItems, [card]);
                await ApplyBatchPublishResultAsync([card], result);

                LoadingDetail = $"自动上架完成：成功 {result.SuccessCount}，失败 {result.FailedCount}。";
                StatusMessage = LoadingDetail;
            }
            catch (Exception ex)
            {
                await SetCardAutoPublishStatusAsync(card, AutoPublishStatus.Failed, ex.Message);
                LoadingDetail = ex.Message;
                StatusMessage = $"自动上架失败：{ex.Message}";
                throw;
            }
            finally
            {
                IsBusy = false;
                OnBatchSelectionChanged();
            }
        });
    }

    private async Task<List<JsonElement>> PrepareAutoPublishProductItemsAsync(RootCardViewModel card)
    {
        var task = await card.PrepareAutoPublishDataAsync();
        if (!File.Exists(task.ProductsJsonPath))
        {
            throw new FileNotFoundException("商品 JSON 未生成。", task.ProductsJsonPath);
        }

        var productItems = new List<JsonElement>();
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(task.ProductsJsonPath));
        foreach (var item in document.RootElement.EnumerateArray())
        {
            productItems.Add(item.Clone());
        }

        return productItems;
    }

    private async Task<MiaoshouPublishResult> RunMiaoshouPublishAsync(
        List<JsonElement> productItems,
        IReadOnlyList<RootCardViewModel> cards)
    {
        var cardsByPath = cards.ToDictionary(
            card => NormalizeCardPath(card.AutoPublishKeyPath),
            StringComparer.OrdinalIgnoreCase);
        var request = CreateMiaoshouPublishRequest(progressEvent => ApplyMiaoshouProgressEventAsync(cardsByPath, progressEvent));

        Directory.CreateDirectory(Path.GetDirectoryName(request.ManifestPath)!);
        await File.WriteAllTextAsync(
            request.ManifestPath,
            JsonSerializer.Serialize(productItems, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        LoadingDetail = $"已生成 {productItems.Count} 条商品数据，正在启动妙手自动上架。";
        StatusMessage = "正在运行妙手 Playwright 自动上架。";
        return await _miaoshouPublishService.PublishAsync(request);
    }

    private async Task ApplyMiaoshouProgressEventAsync(
        IReadOnlyDictionary<string, RootCardViewModel> cardsByPath,
        MiaoshouPublishProgressEvent progressEvent)
    {
        var normalizedPath = NormalizeCardPath(progressEvent.CardPath);
        if (!cardsByPath.TryGetValue(normalizedPath, out var card))
        {
            return;
        }

        var status = string.Equals(progressEvent.Type, "product_success", StringComparison.OrdinalIgnoreCase)
            ? AutoPublishStatus.Success
            : AutoPublishStatus.Failed;
        var error = status == AutoPublishStatus.Failed ? progressEvent.Error : string.Empty;

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await SetCardAutoPublishStatusAsync(card, status, error);
            StatusMessage = status == AutoPublishStatus.Success
                ? $"已上架成功：{card.DisplayName}"
                : $"上架失败：{card.DisplayName}";
        });
    }

    private async Task SetCardAutoPublishStatusAsync(
        RootCardViewModel card,
        AutoPublishStatus status,
        string lastError = "")
    {
        card.SetAutoPublishStatus(status, lastError);
        await _autoPublishStateService.UpsertStatusAsync(
            card.AutoPublishKeyPath,
            card.DisplayName,
            status,
            lastError);
        ApplyAutoPublishStatusFilter();
        NotifyAutoPublishFilterPropertiesChanged();
    }

    private async Task ApplyBatchPublishResultAsync(
        IReadOnlyList<RootCardViewModel> selectedCards,
        MiaoshouPublishResult result)
    {
        if (IsWholeBatchSuccessful(selectedCards, result))
        {
            foreach (var card in selectedCards)
            {
                await SetCardAutoPublishStatusAsync(card, AutoPublishStatus.Success);
            }

            return;
        }

        var cardsByPath = selectedCards.ToDictionary(
            card => NormalizeCardPath(card.AutoPublishKeyPath),
            StringComparer.OrdinalIgnoreCase);
        var handledPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in result.Results)
        {
            var normalizedPath = NormalizeCardPath(item.CardPath);
            if (!cardsByPath.TryGetValue(normalizedPath, out var card))
            {
                continue;
            }

            handledPaths.Add(normalizedPath);
            var status = string.Equals(item.Status, "success", StringComparison.OrdinalIgnoreCase)
                ? AutoPublishStatus.Success
                : AutoPublishStatus.Failed;
            await SetCardAutoPublishStatusAsync(card, status, item.Error);
        }

        if (result.Results.Count == 0)
        {
            var fallbackStatus = result.FailedCount > 0 || string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase)
                ? AutoPublishStatus.Failed
                : AutoPublishStatus.Success;

            foreach (var card in selectedCards)
            {
                await SetCardAutoPublishStatusAsync(card, fallbackStatus, result.Error);
            }

            return;
        }

        foreach (var card in selectedCards)
        {
            if (!handledPaths.Contains(NormalizeCardPath(card.AutoPublishKeyPath)))
            {
                await SetCardAutoPublishStatusAsync(card, AutoPublishStatus.Failed, "未收到该卡片的上架结果。");
            }
        }
    }

    private static bool IsWholeBatchSuccessful(
        IReadOnlyList<RootCardViewModel> selectedCards,
        MiaoshouPublishResult result)
    {
        return selectedCards.Count > 0
            && result.FailedCount == 0
            && result.SuccessCount == selectedCards.Count
            && string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase);
    }

    private async Task MarkPublishingCardsFailedAsync(
        IEnumerable<RootCardViewModel> cards,
        string error)
    {
        foreach (var card in cards.Where(card => card.AutoPublishStatus == AutoPublishStatus.Publishing))
        {
            await SetCardAutoPublishStatusAsync(card, AutoPublishStatus.Failed, error);
        }
    }

    private static string NormalizeCardPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static MiaoshouPublishRequest CreateMiaoshouPublishRequest(
        Func<MiaoshouPublishProgressEvent, Task>? progressHandler = null)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputRoot = Path.Combine(AppContext.BaseDirectory, "output", "miaoshou", timestamp);
        return new MiaoshouPublishRequest
        {
            ManifestPath = Path.Combine(outputRoot, "batch-manifest.json"),
            ResultPath = Path.Combine(outputRoot, "batch-result.json"),
            EventsPath = Path.Combine(outputRoot, "events.jsonl"),
            LogPath = Path.Combine(outputRoot, "publish.log"),
            ConfigPath = Path.Combine(AppContext.BaseDirectory, "config", "miaoshou.json"),
            ProgressHandler = progressHandler
        };
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

        _previewImagePath = request.FilePath;
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
            _previewImagePath = string.Empty;
            PreviewImageSource = null;
        }
    }

    private void ClearPreview()
    {
        _previewImagePath = string.Empty;
        PreviewImageSource = null;
        PreviewMeta = "大小：-\r\n完整路径：-";
    }

    private void ResetSummary()
    {
        LoadedFolderText = "当前文件夹：未选择";
        SelectionSummaryText = "图片 0 张，已选中 0 张，未选中 0 张";
        BackupFolderText = string.IsNullOrWhiteSpace(BackupFolder)
            ? "备份目录：未设置"
            : $"备份目录：{BackupFolder}";
        StatusMessage = "请选择文件夹开始筛图。";
    }

    private void ResetGenerationSummary()
    {
        GenerationPageDescription = "\u7528\u4e8e\u6a21\u677f\u751f\u56fe\u4e0e SP \u6279\u91cf\u5904\u7406";
        GenerationStatusText = "\u5f85\u547d";
        GenerationResultModeText = "\u6a21\u5f0f\uff1a\u5f85\u547d";
        GenerationResultOutputText = $"\u8f93\u51fa\u76ee\u5f55\uff1a{GenerationOutputDirectory}";
        ClearGenerationPromptCards();
    }

    private void ResetSpBatchSummary()
    {
        SpBatchStatusText = "\u5f85\u547d";
        SpBatchSummaryText = "\u7528\u4e8e\u6279\u91cf\u521b\u5efa\u65e5\u671f\u76ee\u5f55\u3001SP \u7ed3\u6784\u548c 6 \u8272 SKU \u56fe\u3002";
        SpBatchResultRootText = $"\u65e5\u671f\u76ee\u5f55\uff1a{SpBatchOutputDirectory}";
        SpBatchResultStatsText = "SP \u6570\u91cf\uff1a0  \u603b\u4efb\u52a1\uff1a0  \u6210\u529f\uff1a0  \u8df3\u8fc7\uff1a0  \u5931\u8d25\uff1a0";
        ClearSpBatchResultCards();
    }

    private void UpdateSummaryForTab(WorkspaceTabViewModel tab)
    {
        LoadedFolderText = tab.GetCurrentFolderText();
        SelectionSummaryText = tab.GetSelectionSummaryText();
        BackupFolderText = string.IsNullOrWhiteSpace(BackupFolder)
            ? "备份目录：未设置"
            : $"备份目录：{BackupFolder}";
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


    private static string ResolveTemplateLibraryPath()
    {
        var appBase = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appBase, "data", "workspace", "temp", "\u6587\u751f\u56fe\u6a21\u677f\u5e93_Codex.xlsx"),
            Path.Combine(WorkspaceDefaults.DefaultTempFolder, "\u6587\u751f\u56fe\u6a21\u677f\u5e93_Codex.xlsx"),
            @"D:\temu_auto\temp\文生图模板库_Codex.xlsx"
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static string ResolveImage2ScriptPath()
    {
        var appBase = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appBase, "tools", "python", "image2-generate", "scripts", "generate_image.py"),
            Path.Combine(@"D:\new_project\tools\python", "image2-generate", "scripts", "generate_image.py")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private void ApplyUserPathSettings(AppUserPathsState state)
    {
        BackupFolder = state.BackupFolder ?? string.Empty;
        TemplateLibraryPath = state.TemplateLibraryPath ?? string.Empty;
        GenerationOutputDirectory = state.GenerationOutputDirectory ?? string.Empty;
        SpBatchInputDirectory = state.SpBatchInputDirectory ?? string.Empty;
        SpBatchOutputDirectory = state.SpBatchOutputDirectory ?? string.Empty;
    }

    private void PersistUserPathSettings()
    {
        _appSettingsService.SaveUserPaths(new AppUserPathsState
        {
            ReviewRootFolder = SelectedTab?.IsPlaceholder == false ? SelectedTab.RootFolder : string.Empty,
            BackupFolder = BackupFolder,
            TemplateLibraryPath = TemplateLibraryPath,
            GenerationOutputDirectory = GenerationOutputDirectory,
            SpBatchInputDirectory = SpBatchInputDirectory,
            SpBatchOutputDirectory = SpBatchOutputDirectory
        });
    }


    private static bool TryParsePositiveInt(string text, string fieldName, out int value)
    {
        if (!int.TryParse(text, out value) || value <= 0)
        {
            throw new InvalidOperationException($"{fieldName}\u5fc5\u987b\u662f\u5927\u4e8e 0 \u7684\u6574\u6570\u3002");
        }

        return true;
    }

    private void ApplyGenerationResult(TemplateGenerateResult result)
    {
        GenerationResultModeText = $"\u6a21\u5f0f\uff1a{(result.Mode == "prompts_only" ? "\u53ea\u51fa\u63d0\u793a\u8bcd" : "\u76f4\u63a5\u751f\u56fe")}";
        GenerationResultOutputText = $"\u8f93\u51fa\u76ee\u5f55\uff1a{result.OutputDirectory}";

        if (!string.Equals(result.Mode, "prompts_only", StringComparison.OrdinalIgnoreCase))
        {
            OnPropertyChanged(nameof(HasGenerationPromptCards));
            OnPropertyChanged(nameof(HasAnyGenerationResultCards));
            return;
        }

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
                            string.IsNullOrWhiteSpace(item.FileName) ? null : $"\u6587\u4ef6\u540d\uff1a{item.FileName}",
                            string.IsNullOrWhiteSpace(item.ImagePath) ? null : $"\u56fe\u7247\u8def\u5f84\uff1a{item.ImagePath}"
                        }.Where(text => !string.IsNullOrWhiteSpace(text)));
                }

                GenerationPromptCards.Add(new GenerationPromptCardViewModel
                {
                    Title = $"\u63d0\u793a\u8bcd {index + 1}",
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
                    Title = $"\u7ed3\u679c {item.Index}",
                    PromptText = item.Prompt,
                    MetaText = string.Join(
                        Environment.NewLine,
                        new[]
                        {
                            string.IsNullOrWhiteSpace(item.FileName) ? null : $"\u6587\u4ef6\u540d\uff1a{item.FileName}",
                            string.IsNullOrWhiteSpace(item.ImagePath) ? null : $"\u56fe\u7247\u8def\u5f84\uff1a{item.ImagePath}"
                        }.Where(text => !string.IsNullOrWhiteSpace(text)))
                });
            }
        }

        OnPropertyChanged(nameof(HasGenerationPromptCards));
        OnPropertyChanged(nameof(HasAnyGenerationResultCards));
    }

    private void ClearGenerationPromptCards()
    {
        if (GenerationPromptCards.Count == 0)
        {
            return;
        }

        GenerationPromptCards.Clear();
        OnPropertyChanged(nameof(HasGenerationPromptCards));
        OnPropertyChanged(nameof(HasAnyGenerationResultCards));
    }

    private void ApplySpBatchResult(SpBatchResult result)
    {
        SpBatchStatusText = result.Mode switch
        {
            "dry_run" => "\u9884\u68c0\u67e5\u5b8c\u6210",
            "prepared" => "\u76ee\u5f55\u7ed3\u6784\u521b\u5efa\u5b8c\u6210",
            _ => result.FailedCount > 0 ? "\u90e8\u5206\u6267\u884c\u5931\u8d25" : "\u6279\u5904\u7406\u5b8c\u6210"
        };

        SpBatchSummaryText = result.Mode switch
        {
            "dry_run" => $"\u9884\u68c0\u67e5\u5b8c\u6210\uff0c\u5171\u89c4\u5212 {result.Results.Count} \u4e2a\u989c\u8272\u4efb\u52a1\u3002",
            "prepared" => $"\u76ee\u5f55\u7ed3\u6784\u521b\u5efa\u5b8c\u6210\uff0c\u5171\u51c6\u5907 {result.PreparedBundles.Count} \u4e2a SP \u76ee\u5f55\u3002",
            _ => $"\u6279\u5904\u7406\u5b8c\u6210\uff0c\u5171 {result.Results.Count} \u4e2a\u4efb\u52a1\uff0c\u6210\u529f {result.SuccessCount}\uff0c\u8df3\u8fc7 {result.SkippedCount}\uff0c\u5931\u8d25 {result.FailedCount}\u3002"
        };

        SpBatchResultRootText = $"\u65e5\u671f\u76ee\u5f55\uff1a{result.DatedRoot}";
        SpBatchResultStatsText = $"SP \u6570\u91cf\uff1a{CountSpDirectories(result)}  \u603b\u4efb\u52a1\uff1a{result.Results.Count}  \u6210\u529f\uff1a{result.SuccessCount}  \u8df3\u8fc7\uff1a{result.SkippedCount}  \u5931\u8d25\uff1a{result.FailedCount}";
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
                detailLines.Add($"\u4e3b\u56fe\u76ee\u5f55\uff1a{bundle.MainDirectory}");
                detailLines.Add($"SKU \u76ee\u5f55\uff1a{bundle.SkuDirectory}");
                detailLines.Add($"\u8be6\u60c5\u76ee\u5f55\uff1a{bundle.DetailDirectory}");
            }

            foreach (var item in group.OrderBy(item => item.Index))
            {
                detailLines.Add($"#{item.Index}  \u989c\u8272\uff1a{item.Color}  \u72b6\u6001\uff1a{item.Status}");
                if (!string.IsNullOrWhiteSpace(item.ImagePath))
                {
                    detailLines.Add($"\u8f93\u51fa\uff1a{item.ImagePath}");
                }
                if (!string.IsNullOrWhiteSpace(item.Error))
                {
                    detailLines.Add($"\u9519\u8bef\uff1a{item.Error}");
                }
            }

            var statusText = failedCount > 0
                ? "\u5931\u8d25"
                : plannedCount > 0
                    ? "\u5df2\u89c4\u5212"
                    : skippedCount > 0 && successCount == 0
                        ? "\u5df2\u8df3\u8fc7"
                        : "\u5b8c\u6210";

            var summaryText = $"\u6210\u529f {successCount}\uff0c\u8df3\u8fc7 {skippedCount}\uff0c\u5931\u8d25 {failedCount}";
            if (plannedCount > 0)
            {
                summaryText = $"\u5df2\u89c4\u5212 {plannedCount} \u4e2a\u4efb\u52a1";
            }

            SpBatchResultCards.Add(new SpBatchResultCardViewModel
            {
                Title = title,
                SummaryText = summaryText,
                StatusText = statusText,
                DetailText = string.Join(Environment.NewLine, detailLines)
            });
        }

        OnPropertyChanged(nameof(HasSpBatchResultCards));
        OnPropertyChanged(nameof(HasAnySpBatchResultCards));
        OnPropertyChanged(nameof(ShouldShowSpBatchDetailCards));
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
        OnPropertyChanged(nameof(HasSelectedGeneratedImages));
        OnPropertyChanged(nameof(CanGenerateSkuFromTemplate));
        _sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
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

            GeneratedImageResultCards.Add(new GeneratedImageResultCardViewModel(
                item.ImagePath,
                item.FileName,
                canToggleSelection: true,
                showRemoveAction: true,
                selectionChanged: OnGeneratedImageSelectionChanged,
                removeRequested: OnGeneratedImageRemoved));
        }

        OnPropertyChanged(nameof(HasGenerationPromptCards));
        OnPropertyChanged(nameof(HasGeneratedImageResultCards));
        OnPropertyChanged(nameof(HasAnyGenerationResultCards));
        OnPropertyChanged(nameof(HasSelectedGeneratedImages));
        OnPropertyChanged(nameof(CanGenerateSkuFromTemplate));
        _sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
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

    private bool CanSendSelectedImagesToSpBatch()
    {
        return !IsBusy && !IsGenerationPromptsOnly && GeneratedImageResultCards.Any(card => card.IsSelected);
    }

    private void OnGeneratedImageSelectionChanged(GeneratedImageResultCardViewModel _)
    {
        OnPropertyChanged(nameof(HasSelectedGeneratedImages));
        OnPropertyChanged(nameof(CanGenerateSkuFromTemplate));
        _sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
    }

    private void OnGeneratedImageRemoved(GeneratedImageResultCardViewModel card)
    {
        if (!GeneratedImageResultCards.Contains(card))
        {
            return;
        }

        GeneratedImageResultCards.Remove(card);
        OnPropertyChanged(nameof(HasGeneratedImageResultCards));
        OnPropertyChanged(nameof(HasAnyGenerationResultCards));
        OnPropertyChanged(nameof(HasSelectedGeneratedImages));
        OnPropertyChanged(nameof(CanGenerateSkuFromTemplate));
        _sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
    }

    private void OnSpBatchSourceImageRemoved(GeneratedImageResultCardViewModel card)
    {
        if (!SpBatchSourceImageCards.Contains(card))
        {
            return;
        }

        SpBatchSourceImageCards.Remove(card);
        OnPropertyChanged(nameof(HasSpBatchSourceImageCards));
    }

    private void SendSelectedImagesToSpBatch()
    {
        var selectedCards = GeneratedImageResultCards
            .Where(card => card.IsSelected)
            .ToArray();

        if (selectedCards.Length == 0)
        {
            return;
        }

        foreach (var card in selectedCards)
        {
            AddSpBatchSourceImage(card.ImagePath, card.FileName);
        }

        SetSelectedImageGenerateTab(SpBatchTab);
        StatusMessage = $"已将 {selectedCards.Length} 张图片加入 SP 批处理区域。";
    }

    private void AddGenerationImages(IEnumerable<string> filePaths)
    {
        var added = false;

        foreach (var filePath in filePaths
                     .Where(File.Exists)
                     .Where(IsSupportedImageFile)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (GeneratedImageResultCards.Any(card => string.Equals(card.ImagePath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            GeneratedImageResultCards.Add(new GeneratedImageResultCardViewModel(
                filePath,
                Path.GetFileName(filePath),
                canToggleSelection: true,
                showRemoveAction: true,
                selectionChanged: OnGeneratedImageSelectionChanged,
                removeRequested: OnGeneratedImageRemoved));
            GeneratedImageResultCards[^1].SetSelected(true);
            added = true;
        }

        if (added)
        {
            OnPropertyChanged(nameof(HasGeneratedImageResultCards));
            OnPropertyChanged(nameof(HasAnyGenerationResultCards));
            OnPropertyChanged(nameof(HasSelectedGeneratedImages));
            OnPropertyChanged(nameof(CanGenerateSkuFromTemplate));
            _sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
        }
    }

    private void AddSpBatchSourceImage(string imagePath, string? fileName = null)
    {
        if (!File.Exists(imagePath) || !IsSupportedImageFile(imagePath))
        {
            return;
        }

        if (SpBatchSourceImageCards.Any(card => string.Equals(card.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SpBatchSourceImageCards.Add(new GeneratedImageResultCardViewModel(
            imagePath,
            fileName ?? Path.GetFileName(imagePath),
            showRemoveAction: true,
            removeRequested: OnSpBatchSourceImageRemoved));
        OnPropertyChanged(nameof(HasSpBatchSourceImageCards));
    }

    private void AddSpBatchSourceImages(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths)
        {
            AddSpBatchSourceImage(filePath);
        }

        StatusMessage = $"SP 批处理区域当前共有 {SpBatchSourceImageCards.Count} 张图片。";
    }

    private string CreateSpBatchStagingInputDirectory()
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), "ImageKeeper", "sp-batch-staging", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        Directory.CreateDirectory(stagingRoot);

        var index = 1;
        foreach (var card in SpBatchSourceImageCards)
        {
            var extension = Path.GetExtension(card.ImagePath);
            var targetName = $"{index:000}{extension}";
            var targetPath = Path.Combine(stagingRoot, targetName);
            File.Copy(card.ImagePath, targetPath, overwrite: true);
            index++;
        }

        return stagingRoot;
    }

    private async Task RunSpBatchFromStagingAsync()
    {
        if (SpBatchSourceImageCards.Count == 0)
        {
            throw new InvalidOperationException("请先添加需要进行 SP 批处理的图片。");
        }

        if (string.IsNullOrWhiteSpace(SpBatchOutputDirectory))
        {
            throw new InvalidOperationException("请选择 SP 批处理输出目录。");
        }

        if (!TryParsePositiveInt(SpBatchConcurrencyText, "并发数量", out var concurrency))
        {
            return;
        }

        var retries = 4;

        Directory.CreateDirectory(SpBatchOutputDirectory);

        IsBusy = true;
        IsSpBatchRunning = true;
        SpBatchStatusText = _spBatchMode switch
        {
            SpBatchMode.PrepareOnly => "正在创建 SP 目录结构...",
            _ => "正在批量生成 SP 资源..."
        };
        SpBatchSummaryText = "任务执行中，请稍候。";
        SpBatchResultRootText = $"日期目录：{SpBatchOutputDirectory}";
        SpBatchResultStatsText = "SP 数量：0  总任务：0  成功：0  跳过：0  失败：0";
        ClearSpBatchResultCards();
        ClearSpBatchImageResultCards();
        StatusMessage = SpBatchStatusText;

        try
        {
            var stagingInputDirectory = CreateSpBatchStagingInputDirectory();
            SpBatchInputDirectory = stagingInputDirectory;

            var request = new SpBatchRequest
            {
                InputDirectory = stagingInputDirectory,
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

    public void SetSpBatchStagingDropTarget(bool isActive)
    {
        IsSpBatchStagingDropTarget = isActive;
    }

    public void AddDroppedImagesToSpBatch(IEnumerable<string> filePaths)
    {
        AddSpBatchSourceImages(filePaths);
    }

    public void HandleGeneratedImageCardClick(GeneratedImageResultCardViewModel? card, int clickCount)
    {
        card?.HandlePrimaryClick(clickCount);
    }

    public void HandleSpBatchSourceImageCardClick(GeneratedImageResultCardViewModel? card, int clickCount)
    {
        card?.HandlePrimaryClick(clickCount);
    }

    public bool OpenCurrentPreviewImage()
    {
        if (string.IsNullOrWhiteSpace(_previewImagePath) || !File.Exists(_previewImagePath))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _previewImagePath,
            UseShellExecute = true
        });
        return true;
    }

    private static bool IsSupportedImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp" or ".jfif";
    }
}
