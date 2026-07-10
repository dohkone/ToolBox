using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
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
    private const string TemplateManagerSection = "template-manager";
    private const string TemplateGenerateTab = "template-generate";
    private const string SpBatchTab = "sp-batch";
    private static string DefaultTemplateLibraryPath => ResolveTemplateLibraryPath();
    private static string DefaultImage2ScriptPath => ResolveImage2ScriptPath();
    private static string DefaultFolderPickerDirectory => ResolveDefaultFolderPickerDirectory();

    private readonly IFolderScanService _folderScanService;
    private readonly IImageWorkspaceService _imageWorkspaceService;
    private readonly IWorkspaceStateService _workspaceStateService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IProductSheetService _productSheetService;
    private readonly ITemplateGenerationService _templateGenerationService;
    private readonly ISpBatchService _spBatchService;
    private readonly IMiaoshouPublishService _miaoshouPublishService;
    private readonly IAutoPublishStateService _autoPublishStateService;
    private readonly ICardSizeInfoService _cardSizeInfoService;
    private readonly ITemplateLibraryService _templateLibraryService;
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
    private readonly RelayCommand _showTemplateManagerCommand;
    private readonly RelayCommand _showTemplateGenerateTabCommand;
    private readonly RelayCommand _showSpBatchTabCommand;
    private readonly AsyncRelayCommand _showLayoutTemplateTabCommand;
    private readonly AsyncRelayCommand _showSceneTemplateTabCommand;
    private readonly RelayCommand _newTemplateCommand;
    private readonly AsyncRelayCommand _saveTemplateCommand;
    private readonly AsyncRelayCommand _deleteTemplateCommand;
    private readonly AsyncRelayCommand _deleteManagedTemplateCommand;
    private readonly RelayCommand _selectManagedTemplateCommand;
    private readonly AsyncRelayCommand _chooseTemplatePreviewCommand;
    private readonly AsyncRelayCommand _importLayoutTemplatesCommand;
    private readonly AsyncRelayCommand _exportLayoutTemplatesCommand;
    private readonly RelayCommand _closeTemplateEditorCommand;
    private readonly RelayCommand _addSceneContentLineCommand;
    private readonly RelayCommand _removeSceneContentLineCommand;
    private readonly RelayCommand _startAddTemplateSubjectTagCommand;
    private readonly RelayCommand _commitTemplateSubjectTagCommand;
    private readonly RelayCommand _removeTemplateSubjectTagCommand;
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
    private readonly RelayCommand _openSpBatchOutputFolderCommand;
    private readonly AsyncRelayCommand _runSpBatchCommand;
    private readonly RelayCommand _stopSpBatchCommand;
    private readonly RelayCommand _refreshSkuResultsCommand;
    private readonly AsyncRelayCommand _runBatchAutoPublishCommand;
    private readonly AsyncRelayCommand _saveCardSizeInfoCommand;
    private readonly RelayCommand _cancelCardSizeInfoCommand;
    private CancellationTokenSource? _previewCancellationTokenSource;
    private CancellationTokenSource? _templateGenerationCancellationTokenSource;
    private CancellationTokenSource? _spBatchCancellationTokenSource;
    private WorkspaceTabViewModel? _selectedTab;
    private TemplateItemViewModel? _selectedTemplateItem;
    private string _selectedSection = ImageGenerateSection;
    private string _selectedImageGenerateTab = TemplateGenerateTab;
    private TemplateCategory _selectedTemplateCategory = TemplateCategory.Layout;
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
    private bool _isSpBatchStopping;
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
    private int _spBatchStatsSkuCount;
    private int _spBatchStatsTotalCount;
    private int _spBatchStatsSuccessCount;
    private int _spBatchStatsSkippedCount;
    private int _spBatchStatsFailedCount;

    private RootCardViewModel? _cardSizeDialogCard;
    private string _cardSizeDialogImagePath = string.Empty;
    private string _cardSizeDialogImageHash = string.Empty;
    private string _cardSizeDialogImageLastWriteUtc = string.Empty;
    private string _cardSizeInputText = string.Empty;
    private string _cardSizePreviewMeta = string.Empty;
    private Media.ImageSource? _cardSizePreviewImageSource;
    private bool _isCardSizeDialogOpen;

    private string _templateEditorName = string.Empty;
    private string _templateEditorContent = string.Empty;
    private string _templateEditorSubject = string.Empty;
    private string _newTemplateSubjectText = string.Empty;
    private string _templateEditorPreviewImagePath = string.Empty;
    private bool _templateEditorIsEnabled = true;
    private bool _isTemplateEditorDialogOpen;
    private bool _isAddingTemplateSubjectTag;
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
        IAutoPublishStateService autoPublishStateService,
        ICardSizeInfoService cardSizeInfoService,
        ITemplateLibraryService templateLibraryService)
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
        _cardSizeInfoService = cardSizeInfoService;
        _templateLibraryService = templateLibraryService;

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
        _showTemplateManagerCommand = new RelayCommand(_ => SetSelectedSection(TemplateManagerSection));
        _showTemplateGenerateTabCommand = new RelayCommand(_ => SetSelectedImageGenerateTab(TemplateGenerateTab));
        _showSpBatchTabCommand = new RelayCommand(_ => SetSelectedImageGenerateTab(SpBatchTab));
        _showLayoutTemplateTabCommand = new AsyncRelayCommand(_ => SelectTemplateCategoryAsync(TemplateCategory.Layout));
        _showSceneTemplateTabCommand = new AsyncRelayCommand(_ => SelectTemplateCategoryAsync(TemplateCategory.Scene));
        _newTemplateCommand = new RelayCommand(_ => NewTemplate());
        _saveTemplateCommand = new AsyncRelayCommand(_ => SaveTemplateAsync(), _ => CanSaveTemplate());
        _deleteTemplateCommand = new AsyncRelayCommand(_ => DeleteTemplateAsync(), _ => SelectedTemplateItem is not null && SelectedTemplateItem.Id > 0);
        _deleteManagedTemplateCommand = new AsyncRelayCommand(item => DeleteManagedTemplateAsync(item as TemplateItemViewModel), item => item is TemplateItemViewModel);
        _selectManagedTemplateCommand = new RelayCommand(item => SelectManagedTemplate(item as TemplateItemViewModel), item => item is TemplateItemViewModel);
        _chooseTemplatePreviewCommand = new AsyncRelayCommand(_ => ChooseTemplatePreviewAsync(), _ => IsLayoutTemplateTabSelected);
        _importLayoutTemplatesCommand = new AsyncRelayCommand(_ => ImportLayoutTemplatesAsync(), _ => IsLayoutTemplateTabSelected);
        _exportLayoutTemplatesCommand = new AsyncRelayCommand(_ => ExportLayoutTemplatesAsync(), _ => IsLayoutTemplateTabSelected);
        _closeTemplateEditorCommand = new RelayCommand(_ => IsTemplateEditorDialogOpen = false);
        _addSceneContentLineCommand = new RelayCommand(line => AddSceneContentLine(line as SceneContentLineViewModel));
        _removeSceneContentLineCommand = new RelayCommand(line => RemoveSceneContentLine(line as SceneContentLineViewModel), line => SceneContentLines.Count > 1 && line is SceneContentLineViewModel);
        _startAddTemplateSubjectTagCommand = new RelayCommand(_ => StartAddTemplateSubjectTag());
        _commitTemplateSubjectTagCommand = new RelayCommand(_ => CommitTemplateSubjectTag());
        _removeTemplateSubjectTagCommand = new RelayCommand(tag => RemoveTemplateSubjectTag(tag as TemplateSubjectTagViewModel), tag => tag is TemplateSubjectTagViewModel);
        _chooseTemplateLibraryCommand = new AsyncRelayCommand(_ => ChooseTemplateLibraryAsync(), _ => CanEditTemplateGenerationSettings());
        _openTemplateLibraryFileCommand = new RelayCommand(_ => OpenTemplateLibraryFile(), _ => File.Exists(TemplateLibraryPath));
        _chooseGenerationOutputFolderCommand = new AsyncRelayCommand(_ => ChooseGenerationOutputFolderAsync(), _ => CanEditTemplateGenerationSettings());
        _openGenerationOutputFolderCommand = new RelayCommand(_ => OpenGenerationOutputFolder(), _ => Directory.Exists(GenerationOutputDirectory));
        _runTemplateGenerationCommand = new AsyncRelayCommand(_ => RunTemplateGenerationAsync(), _ => CanRunTemplateGeneration());
        _stopTemplateGenerationCommand = new RelayCommand(_ => StopTemplateGeneration(), _ => IsTemplateGenerating);
        _chooseGenerationImagesCommand = new AsyncRelayCommand(_ => ChooseGenerationImagesAsync(), _ => true);
        _sendSelectedImagesToSpBatchCommand = new RelayCommand(_ => SendSelectedImagesToSpBatch(), _ => CanSendSelectedImagesToSpBatch());
        _chooseSpBatchInputFolderCommand = new AsyncRelayCommand(_ => ChooseSpBatchInputFolderAsync(), _ => CanEditSpBatchSettings());
        _chooseSpBatchOutputFolderCommand = new AsyncRelayCommand(_ => ChooseSpBatchOutputFolderAsync(), _ => CanEditSpBatchSettings());
        _openSpBatchOutputFolderCommand = new RelayCommand(_ => OpenSpBatchOutputFolder(), _ => Directory.Exists(SpBatchOutputDirectory));
        _runSpBatchCommand = new AsyncRelayCommand(_ => RunSpBatchFromStagingAsync(), _ => CanRunSpBatch());
        _stopSpBatchCommand = new RelayCommand(_ => StopSpBatch(), _ => IsSpBatchRunning);
        _refreshSkuResultsCommand = new RelayCommand(_ => RefreshSkuResults(), _ => !IsSpBatchRunning && !IsSpBatchStopping);
        _runBatchAutoPublishCommand = new AsyncRelayCommand(_ => RunBatchAutoPublishAsync(), _ => CanExecuteBatchAutoPublish());
        _saveCardSizeInfoCommand = new AsyncRelayCommand(_ => SaveCardSizeInfoAsync(), _ => CanSaveCardSizeInfo());
        _cancelCardSizeInfoCommand = new RelayCommand(_ => CloseCardSizeDialog());
    }

    public ObservableCollection<WorkspaceTabViewModel> WorkspaceTabs { get; } = [];
    public ObservableCollection<GenerationPromptCardViewModel> GenerationPromptCards { get; } = [];
    public ObservableCollection<GeneratedImageResultCardViewModel> GeneratedImageResultCards { get; } = [];
    public ObservableCollection<GeneratedImageResultCardViewModel> SpBatchSourceImageCards { get; } = [];
    public ObservableCollection<GeneratedImageResultCardViewModel> SpBatchImageResultCards { get; } = [];
    public ObservableCollection<SpBatchResultCardViewModel> SpBatchResultCards { get; } = [];
    public ObservableCollection<TemplateItemViewModel> ManagedTemplateItems { get; } = [];
    public ObservableCollection<SceneContentLineViewModel> SceneContentLines { get; } = [];
    public ObservableCollection<TemplateSubjectTagViewModel> TemplateSubjectTags { get; } = [];
    public ObservableCollection<TemplateSubjectTagViewModel> TemplateSubjectTagItems { get; } = [];

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
    public ICommand ShowTemplateManagerCommand => _showTemplateManagerCommand;
    public ICommand ShowTemplateGenerateTabCommand => _showTemplateGenerateTabCommand;
    public ICommand ShowSpBatchTabCommand => _showSpBatchTabCommand;
    public ICommand ShowLayoutTemplateTabCommand => _showLayoutTemplateTabCommand;
    public ICommand ShowSceneTemplateTabCommand => _showSceneTemplateTabCommand;
    public ICommand NewTemplateCommand => _newTemplateCommand;
    public ICommand SaveTemplateCommand => _saveTemplateCommand;
    public ICommand DeleteTemplateCommand => _deleteTemplateCommand;
    public ICommand DeleteManagedTemplateCommand => _deleteManagedTemplateCommand;
    public ICommand SelectManagedTemplateCommand => _selectManagedTemplateCommand;
    public ICommand ChooseTemplatePreviewCommand => _chooseTemplatePreviewCommand;
    public ICommand ImportLayoutTemplatesCommand => _importLayoutTemplatesCommand;
    public ICommand ExportLayoutTemplatesCommand => _exportLayoutTemplatesCommand;
    public ICommand CloseTemplateEditorCommand => _closeTemplateEditorCommand;
    public ICommand AddSceneContentLineCommand => _addSceneContentLineCommand;
    public ICommand RemoveSceneContentLineCommand => _removeSceneContentLineCommand;
    public ICommand StartAddTemplateSubjectTagCommand => _startAddTemplateSubjectTagCommand;
    public ICommand CommitTemplateSubjectTagCommand => _commitTemplateSubjectTagCommand;
    public ICommand RemoveTemplateSubjectTagCommand => _removeTemplateSubjectTagCommand;
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
    public ICommand OpenSpBatchOutputFolderCommand => _openSpBatchOutputFolderCommand;
    public ICommand RunSpBatchCommand => _runSpBatchCommand;
    public ICommand StopSpBatchCommand => _stopSpBatchCommand;
    public ICommand RefreshSkuResultsCommand => _refreshSkuResultsCommand;
    public ICommand RunBatchAutoPublishCommand => _runBatchAutoPublishCommand;
    public ICommand SaveCardSizeInfoCommand => _saveCardSizeInfoCommand;
    public ICommand CancelCardSizeInfoCommand => _cancelCardSizeInfoCommand;

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

    public TemplateItemViewModel? SelectedTemplateItem
    {
        get => _selectedTemplateItem;
        private set
        {
            if (_selectedTemplateItem == value)
            {
                return;
            }

            if (_selectedTemplateItem is not null)
            {
                _selectedTemplateItem.IsSelected = false;
            }

            _selectedTemplateItem = value;

            if (_selectedTemplateItem is not null)
            {
                _selectedTemplateItem.IsSelected = true;
                LoadTemplateEditor(_selectedTemplateItem);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(TemplateEditorDialogTitle));
            _deleteTemplateCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasTabs => WorkspaceTabs.Count > 0;
    public bool HasRootCards => SelectedTab?.HasRootCards ?? false;
    public bool IsReviewWorkspaceSelected => _selectedSection == ReviewWorkspaceSection;
    public bool IsImageGenerateSelected => _selectedSection == ImageGenerateSection;
    public bool IsTemplateManagerSelected => _selectedSection == TemplateManagerSection;
    public bool IsTemplateGenerateTabSelected => _selectedImageGenerateTab == TemplateGenerateTab;
    public bool IsSpBatchTabSelected => _selectedImageGenerateTab == SpBatchTab;
    public bool IsLayoutTemplateTabSelected => _selectedTemplateCategory == TemplateCategory.Layout;
    public bool IsSceneTemplateTabSelected => _selectedTemplateCategory == TemplateCategory.Scene;
    public bool IsSubjectTemplateTabSelected => _selectedTemplateCategory == TemplateCategory.Subject;
    public bool HasManagedTemplateItems => ManagedTemplateItems.Count > 0;
    public bool IsTemplateEditorPreviewVisible => IsLayoutTemplateTabSelected && !string.IsNullOrWhiteSpace(TemplateEditorPreviewImagePath) && File.Exists(TemplateEditorPreviewImagePath);
    public bool IsTemplateEditorSubjectVisible => IsSceneTemplateTabSelected;
    public string TemplateManagerTitle => _selectedTemplateCategory switch
    {
        TemplateCategory.Scene => "场景模板",
        TemplateCategory.Subject => "主体模板",
        _ => "布局模板"
    };
    public string TemplateManagerDescription => _selectedTemplateCategory switch
    {
        TemplateCategory.Scene => "维护可随机使用的场景描述文本。",
        TemplateCategory.Subject => "维护可复用的主体描述文本。",
        _ => "维护主图提示词布局，并为每个布局配置一张预览图。"
    };
    public string TemplateEditorContentLabel => _selectedTemplateCategory switch
    {
        TemplateCategory.Scene => "场景内容",
        TemplateCategory.Subject => "主体内容",
        _ => "布局模板内容"
    };
    public string TemplateEmptyText => $"暂无{TemplateManagerTitle}，点击“新增模板”开始维护。";
    public string TemplateEditorDialogTitle => SelectedTemplateItem is null
        ? $"新增{TemplateManagerTitle}"
        : $"编辑{TemplateManagerTitle}";
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
    public bool CanGenerateSkuFromTemplate => !IsGenerationPromptsOnly && HasSelectedGeneratedImages;
    public string TemplateGenerationButtonText => IsTemplateGenerationStopping
        ? "正在停止..."
        : IsTemplateGenerating
            ? "停止执行"
            : "开始执行";
    public string SpBatchButtonText => IsSpBatchStopping
        ? "正在停止..."
        : IsSpBatchRunning
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

    public bool IsCardSizeDialogOpen
    {
        get => _isCardSizeDialogOpen;
        private set => SetProperty(ref _isCardSizeDialogOpen, value);
    }

    public string CardSizeDialogTitle => _cardSizeDialogCard is null
        ? "录入尺寸"
        : $"录入尺寸：{_cardSizeDialogCard.DisplayName}";

    public string CardSizeInputText
    {
        get => _cardSizeInputText;
        set
        {
            if (!SetProperty(ref _cardSizeInputText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CardSizePreviewText));
            _saveCardSizeInfoCommand.RaiseCanExecuteChanged();
        }
    }

    public string CardSizePreviewText
    {
        get
        {
            var sizes = BuildDisplaySizes(CardSizeInputText);
            return sizes.Count == 0
                ? "输入示例：50*225 或 50cm*225cm。保存后会自动补全 inch。"
                : string.Join(Environment.NewLine, sizes);
        }
    }

    public string CardSizePreviewMeta
    {
        get => _cardSizePreviewMeta;
        private set => SetProperty(ref _cardSizePreviewMeta, value);
    }

    public Media.ImageSource? CardSizePreviewImageSource
    {
        get => _cardSizePreviewImageSource;
        private set => SetProperty(ref _cardSizePreviewImageSource, value);
    }

    public bool IsTemplateEditorDialogOpen
    {
        get => _isTemplateEditorDialogOpen;
        private set => SetProperty(ref _isTemplateEditorDialogOpen, value);
    }

    public string TemplateEditorName
    {
        get => _templateEditorName;
        set
        {
            if (!SetProperty(ref _templateEditorName, value))
            {
                return;
            }

            _saveTemplateCommand.RaiseCanExecuteChanged();
        }
    }

    public string TemplateEditorContent
    {
        get => _templateEditorContent;
        set
        {
            if (!SetProperty(ref _templateEditorContent, value))
            {
                return;
            }

            _saveTemplateCommand.RaiseCanExecuteChanged();
        }
    }

    public string TemplateEditorSubject
    {
        get => _templateEditorSubject;
        set
        {
            if (!SetProperty(ref _templateEditorSubject, value))
            {
                return;
            }

            _saveTemplateCommand.RaiseCanExecuteChanged();
        }
    }

    public string NewTemplateSubjectText
    {
        get => _newTemplateSubjectText;
        set => SetProperty(ref _newTemplateSubjectText, value);
    }

    public bool IsAddingTemplateSubjectTag
    {
        get => _isAddingTemplateSubjectTag;
        private set => SetProperty(ref _isAddingTemplateSubjectTag, value);
    }

    public string TemplateEditorPreviewImagePath
    {
        get => _templateEditorPreviewImagePath;
        set
        {
            if (!SetProperty(ref _templateEditorPreviewImagePath, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsTemplateEditorPreviewVisible));
        }
    }

    public bool TemplateEditorIsEnabled
    {
        get => _templateEditorIsEnabled;
        set => SetProperty(ref _templateEditorIsEnabled, value);
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
            _openSpBatchOutputFolderCommand.RaiseCanExecuteChanged();
            _runSpBatchCommand.RaiseCanExecuteChanged();
            _stopSpBatchCommand.RaiseCanExecuteChanged();
            _runBatchAutoPublishCommand.RaiseCanExecuteChanged();
            _saveTemplateCommand.RaiseCanExecuteChanged();
            _deleteTemplateCommand.RaiseCanExecuteChanged();
            _chooseTemplatePreviewCommand.RaiseCanExecuteChanged();
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
            _chooseTemplateLibraryCommand.RaiseCanExecuteChanged();
            _chooseGenerationOutputFolderCommand.RaiseCanExecuteChanged();
            _runTemplateGenerationCommand.RaiseCanExecuteChanged();
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
            _chooseTemplateLibraryCommand.RaiseCanExecuteChanged();
            _chooseGenerationOutputFolderCommand.RaiseCanExecuteChanged();
            _runTemplateGenerationCommand.RaiseCanExecuteChanged();
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
        private set
        {
            if (!SetProperty(ref _isSpBatchRunning, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SpBatchButtonText));
            _chooseSpBatchInputFolderCommand.RaiseCanExecuteChanged();
            _chooseSpBatchOutputFolderCommand.RaiseCanExecuteChanged();
            _runSpBatchCommand.RaiseCanExecuteChanged();
            _stopSpBatchCommand.RaiseCanExecuteChanged();
            _refreshSkuResultsCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsSpBatchStopping
    {
        get => _isSpBatchStopping;
        private set
        {
            if (!SetProperty(ref _isSpBatchStopping, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SpBatchButtonText));
            _chooseSpBatchInputFolderCommand.RaiseCanExecuteChanged();
            _chooseSpBatchOutputFolderCommand.RaiseCanExecuteChanged();
            _runSpBatchCommand.RaiseCanExecuteChanged();
            _stopSpBatchCommand.RaiseCanExecuteChanged();
            _refreshSkuResultsCommand.RaiseCanExecuteChanged();
        }
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
            _openSpBatchOutputFolderCommand.RaiseCanExecuteChanged();
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

    public string SpBatchStatsSkuCountText => $"SKU 数量：{_spBatchStatsSkuCount}";
    public string SpBatchStatsTotalText => $"总任务：{_spBatchStatsTotalCount}";
    public string SpBatchStatsSuccessText => $"成功：{_spBatchStatsSuccessCount}";
    public string SpBatchStatsSkippedText => $"跳过：{_spBatchStatsSkippedCount}";
    public string SpBatchStatsFailedText => $"失败：{_spBatchStatsFailedCount}";
    public Media.Brush SpBatchStatsSuccessBrush => new Media.SolidColorBrush(Media.Color.FromRgb(103, 194, 58));
    public Media.Brush SpBatchStatsFailedBrush => new Media.SolidColorBrush(Media.Color.FromRgb(245, 108, 108));

    public async Task InitializeAsync()
    {
        await Task.Yield();
        await _autoPublishStateService.InitializeAsync();
        await _cardSizeInfoService.InitializeAsync();
        await _templateLibraryService.InitializeAsync();
        await LoadManagedTemplatesAsync();
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

        if (IsTemplateManagerSelected)
        {
            await LoadManagedTemplatesAsync();
            StatusMessage = "已刷新模板管理页。";
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

    private async Task SelectTemplateCategoryAsync(TemplateCategory category)
    {
        if (_selectedTemplateCategory == category)
        {
            return;
        }

        _selectedTemplateCategory = category;
        NotifyTemplateCategoryChanged();
        await LoadManagedTemplatesAsync();
    }

    private async Task LoadManagedTemplatesAsync()
    {
        var records = await _templateLibraryService.GetByCategoryAsync(_selectedTemplateCategory);
        ManagedTemplateItems.Clear();
        foreach (var record in records)
        {
            ManagedTemplateItems.Add(new TemplateItemViewModel(record));
        }

        OnPropertyChanged(nameof(HasManagedTemplateItems));
        OnPropertyChanged(nameof(TemplateEmptyText));
        ResetTemplateEditor();
    }

    private void NewTemplate()
    {
        ResetTemplateEditor();
        IsTemplateEditorDialogOpen = true;
    }

    private void ResetTemplateEditor()
    {
        SelectedTemplateItem = null;
        TemplateEditorName = string.Empty;
        TemplateEditorContent = string.Empty;
        TemplateEditorSubject = string.Empty;
        NewTemplateSubjectText = string.Empty;
        IsAddingTemplateSubjectTag = false;
        SetTemplateSubjectTags(string.Empty);
        SetSceneContentLines(string.Empty);
        TemplateEditorPreviewImagePath = string.Empty;
        TemplateEditorIsEnabled = true;
        IsTemplateEditorDialogOpen = false;
        OnPropertyChanged(nameof(TemplateEditorDialogTitle));
        _deleteTemplateCommand.RaiseCanExecuteChanged();
        _saveTemplateCommand.RaiseCanExecuteChanged();
    }

    private void SelectManagedTemplate(TemplateItemViewModel? item)
    {
        SelectedTemplateItem = item;
        if (item is null)
        {
            return;
        }

        LoadTemplateEditor(item);
        IsTemplateEditorDialogOpen = true;
    }

    private void LoadTemplateEditor(TemplateItemViewModel item)
    {
        TemplateEditorName = item.Name;
        TemplateEditorContent = item.Content;
        TemplateEditorSubject = item.Subject;
        NewTemplateSubjectText = string.Empty;
        IsAddingTemplateSubjectTag = false;
        SetTemplateSubjectTags(item.Subject);
        SetSceneContentLines(item.Content);
        TemplateEditorPreviewImagePath = item.PreviewImagePath;
        TemplateEditorIsEnabled = item.IsEnabled;
    }

    private bool CanSaveTemplate()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(TemplateEditorName)
            && (IsSceneTemplateTabSelected
                ? SceneContentLines.Any(line => !string.IsNullOrWhiteSpace(line.Text))
                : !string.IsNullOrWhiteSpace(TemplateEditorContent))
            && (!IsSceneTemplateTabSelected || TemplateSubjectTags.Count > 0);
    }

    private async Task SaveTemplateAsync()
    {
        if (!CanSaveTemplate())
        {
            StatusMessage = "请填写模板名称和模板内容。";
            return;
        }

        var record = new TemplateItemRecord
        {
            Id = SelectedTemplateItem?.Id ?? 0,
            Category = _selectedTemplateCategory,
            Name = TemplateEditorName,
            Content = IsSceneTemplateTabSelected ? JoinSceneContentLines() : TemplateEditorContent,
            Subject = IsSceneTemplateTabSelected ? JoinTemplateSubjectTags() : string.Empty,
            PreviewImagePath = IsLayoutTemplateTabSelected ? TemplateEditorPreviewImagePath : string.Empty,
            SortOrder = 0,
            IsEnabled = TemplateEditorIsEnabled
        };

        var saved = await _templateLibraryService.SaveAsync(record);
        await LoadManagedTemplatesAsync();
        SelectedTemplateItem = ManagedTemplateItems.FirstOrDefault(item => item.Id == saved.Id);
        IsTemplateEditorDialogOpen = false;
        StatusMessage = $"已保存{TemplateManagerTitle}：{saved.Name}";
    }

    private void SetTemplateSubjectTags(string subject)
    {
        TemplateSubjectTags.Clear();

        foreach (var part in subject.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            TemplateSubjectTags.Add(TemplateSubjectTagViewModel.CreateTag(part));
        }

        TemplateEditorSubject = JoinTemplateSubjectTags();
        RefreshTemplateSubjectTagItems();
        _saveTemplateCommand.RaiseCanExecuteChanged();
    }

    private void StartAddTemplateSubjectTag()
    {
        NewTemplateSubjectText = string.Empty;
        IsAddingTemplateSubjectTag = true;
        RefreshTemplateSubjectTagItems();
    }

    private void CommitTemplateSubjectTag()
    {
        var text = NewTemplateSubjectText.Trim();
        if (!string.IsNullOrWhiteSpace(text)
            && !TemplateSubjectTags.Any(tag => string.Equals(tag.Text, text, StringComparison.OrdinalIgnoreCase)))
        {
            TemplateSubjectTags.Add(TemplateSubjectTagViewModel.CreateTag(text));
        }

        NewTemplateSubjectText = string.Empty;
        IsAddingTemplateSubjectTag = false;
        TemplateEditorSubject = JoinTemplateSubjectTags();
        RefreshTemplateSubjectTagItems();
        _saveTemplateCommand.RaiseCanExecuteChanged();
    }

    private void RemoveTemplateSubjectTag(TemplateSubjectTagViewModel? tag)
    {
        if (tag is null)
        {
            return;
        }

        TemplateSubjectTags.Remove(tag);
        TemplateEditorSubject = JoinTemplateSubjectTags();
        RefreshTemplateSubjectTagItems();
        _saveTemplateCommand.RaiseCanExecuteChanged();
    }

    private void RefreshTemplateSubjectTagItems()
    {
        TemplateSubjectTagItems.Clear();

        foreach (var tag in TemplateSubjectTags)
        {
            TemplateSubjectTagItems.Add(tag);
        }

        TemplateSubjectTagItems.Add(IsAddingTemplateSubjectTag
            ? TemplateSubjectTagViewModel.CreateInput()
            : TemplateSubjectTagViewModel.CreateAddButton());
    }

    private string JoinTemplateSubjectTags()
    {
        return string.Join(
            "/",
            TemplateSubjectTags
                .Select(tag => tag.Text.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private void SetSceneContentLines(string content)
    {
        foreach (var line in SceneContentLines)
        {
            line.PropertyChanged -= OnSceneContentLineChanged;
        }

        SceneContentLines.Clear();
        var parts = content
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .DefaultIfEmpty(string.Empty);

        foreach (var part in parts)
        {
            AddSceneContentLineInternal(part);
        }

        RefreshSceneContentLineStates();
        _saveTemplateCommand.RaiseCanExecuteChanged();
    }

    private void AddSceneContentLine(SceneContentLineViewModel? currentLine)
    {
        var insertIndex = currentLine is null
            ? SceneContentLines.Count
            : SceneContentLines.IndexOf(currentLine) + 1;

        if (insertIndex < 0)
        {
            insertIndex = SceneContentLines.Count;
        }

        SceneContentLines.Insert(insertIndex, CreateSceneContentLine(string.Empty));
        RefreshSceneContentLineStates();
        _saveTemplateCommand.RaiseCanExecuteChanged();
    }

    private void RemoveSceneContentLine(SceneContentLineViewModel? line)
    {
        if (line is null || SceneContentLines.Count <= 1)
        {
            return;
        }

        line.PropertyChanged -= OnSceneContentLineChanged;
        SceneContentLines.Remove(line);
        RefreshSceneContentLineStates();
        _saveTemplateCommand.RaiseCanExecuteChanged();
    }

    private void AddSceneContentLineInternal(string text)
    {
        SceneContentLines.Add(CreateSceneContentLine(text));
    }

    private SceneContentLineViewModel CreateSceneContentLine(string text)
    {
        var line = new SceneContentLineViewModel(text);
        line.PropertyChanged += OnSceneContentLineChanged;
        return line;
    }

    private void RefreshSceneContentLineStates()
    {
        for (var index = 0; index < SceneContentLines.Count; index++)
        {
            SceneContentLines[index].CanAdd = index == SceneContentLines.Count - 1;
            SceneContentLines[index].CanRemove = SceneContentLines.Count > 1;
        }

        _removeSceneContentLineCommand.RaiseCanExecuteChanged();
    }

    private void OnSceneContentLineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SceneContentLineViewModel.Text))
        {
            _saveTemplateCommand.RaiseCanExecuteChanged();
        }
    }

    private string JoinSceneContentLines()
    {
        return string.Join(
            "/",
            SceneContentLines
                .Select(line => line.Text.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private async Task DeleteTemplateAsync()
    {
        if (SelectedTemplateItem is null || SelectedTemplateItem.Id <= 0)
        {
            return;
        }

        var name = SelectedTemplateItem.Name;
        await _templateLibraryService.DeleteAsync(SelectedTemplateItem.Id);
        await LoadManagedTemplatesAsync();
        IsTemplateEditorDialogOpen = false;
        StatusMessage = $"已删除{TemplateManagerTitle}：{name}";
    }

    private async Task DeleteManagedTemplateAsync(TemplateItemViewModel? item)
    {
        if (item is null || item.Id <= 0)
        {
            return;
        }

        var name = item.Name;
        var wasEditing = SelectedTemplateItem?.Id == item.Id;
        await _templateLibraryService.DeleteAsync(item.Id);
        await LoadManagedTemplatesAsync();
        if (wasEditing)
        {
            IsTemplateEditorDialogOpen = false;
        }

        StatusMessage = $"已删除{TemplateManagerTitle}：{name}";
    }

    private async Task ImportLayoutTemplatesAsync()
    {
        using var dialog = new Forms.OpenFileDialog
        {
            Title = "导入布局模板",
            Filter = "EcomTool 布局模板包|*.ecomtpl|所有文件|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        var count = await _templateLibraryService.ImportLayoutTemplatesAsync(dialog.FileName);
        await LoadManagedTemplatesAsync();
        StatusMessage = $"已导入布局模板：{count} 个";
    }

    private async Task ExportLayoutTemplatesAsync()
    {
        using var dialog = new Forms.SaveFileDialog
        {
            Title = "导出布局模板",
            Filter = "EcomTool 布局模板包|*.ecomtpl",
            DefaultExt = "ecomtpl",
            AddExtension = true,
            FileName = $"layout_templates_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.ecomtpl"
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        var count = await _templateLibraryService.ExportLayoutTemplatesAsync(dialog.FileName, ImageTemplateType.MainImage);
        StatusMessage = $"已导出布局模板：{count} 个";
    }

    private async Task ChooseTemplatePreviewAsync()
    {
        using var dialog = new Forms.OpenFileDialog
        {
            Title = "选择布局模板预览图",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp|所有文件|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        TemplateEditorPreviewImagePath = await _templateLibraryService.ImportPreviewImageAsync(dialog.FileName);
        StatusMessage = "已导入布局模板预览图。";
    }

    private void NotifyTemplateCategoryChanged()
    {
        OnPropertyChanged(nameof(IsLayoutTemplateTabSelected));
        OnPropertyChanged(nameof(IsSceneTemplateTabSelected));
        OnPropertyChanged(nameof(TemplateManagerTitle));
        OnPropertyChanged(nameof(TemplateManagerDescription));
        OnPropertyChanged(nameof(TemplateEditorContentLabel));
        OnPropertyChanged(nameof(TemplateEmptyText));
        OnPropertyChanged(nameof(IsTemplateEditorPreviewVisible));
        OnPropertyChanged(nameof(IsTemplateEditorSubjectVisible));
        _chooseTemplatePreviewCommand.RaiseCanExecuteChanged();
        _importLayoutTemplatesCommand.RaiseCanExecuteChanged();
        _exportLayoutTemplatesCommand.RaiseCanExecuteChanged();
        _saveTemplateCommand.RaiseCanExecuteChanged();
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
            OpenCardSizeDialogForCardAsync,
            OnBatchSelectionChanged);
        tab.RequestedActivate += OnTabRequestedActivate;
        tab.RequestedClose += OnTabRequestedClose;
        tab.CardImageFilesAdded += OnTabCardImageFilesAdded;
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

    private bool CanEditTemplateGenerationSettings()
    {
        return !IsTemplateGenerating && !IsTemplateGenerationStopping;
    }

    private bool CanRunTemplateGeneration()
    {
        return CanEditTemplateGenerationSettings();
    }

    private bool CanEditSpBatchSettings()
    {
        return !IsSpBatchRunning && !IsSpBatchStopping;
    }

    private bool CanRunSpBatch()
    {
        return CanEditSpBatchSettings();
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

    private void OnTabCardImageFilesAdded(RootCardViewModel card, IReadOnlyList<string> copiedFiles)
    {
        _ = HandleCardImageFilesAddedSafelyAsync(card, copiedFiles);
    }

    private async Task HandleCardImageFilesAddedSafelyAsync(RootCardViewModel card, IReadOnlyList<string> copiedFiles)
    {
        try
        {
            await HandleCardImageFilesAddedAsync(card, copiedFiles);
        }
        catch (Exception ex)
        {
            StatusMessage = $"尺寸录入弹窗打开失败：{ex.Message}";
        }
    }

    private async Task HandleCardImageFilesAddedAsync(RootCardViewModel card, IReadOnlyList<string> copiedFiles)
    {
        if (!TryFindSizeImageFromFiles(card, copiedFiles, out var sizeImagePath))
        {
            return;
        }

        var existing = await _cardSizeInfoService.GetByCardPathAsync(card.AutoPublishKeyPath);
        var fingerprint = await GetSizeImageFingerprintAsync(sizeImagePath);
        if (existing is not null)
        {
            card.ApplyCardSizeInfo(existing);
        }

        await OpenCardSizeDialogAsync(card, sizeImagePath, existing, fingerprint);
    }

    private async Task OpenCardSizeDialogForCardAsync(RootCardViewModel card)
    {
        var sizeImagePath = FindCurrentSizeImagePath(card);
        if (string.IsNullOrWhiteSpace(sizeImagePath) || !File.Exists(sizeImagePath))
        {
            throw new InvalidOperationException($"当前 main 文件夹下没有尺寸图：{Path.Combine(card.AutoPublishKeyPath, "main")}");
        }

        var existing = await _cardSizeInfoService.GetByCardPathAsync(card.AutoPublishKeyPath);
        var fingerprint = await GetSizeImageFingerprintAsync(sizeImagePath);
        if (existing is not null)
        {
            card.ApplyCardSizeInfo(existing);
        }

        await OpenCardSizeDialogAsync(card, sizeImagePath, existing, fingerprint);
    }

    private async Task<bool> IsCardSizeInfoStaleAsync(RootCardViewModel card, CardSizeInfoRecord? record)
    {
        if (record is null || string.IsNullOrWhiteSpace(record.SizeText))
        {
            return false;
        }

        var sizeImagePath = FindCurrentSizeImagePath(card);
        if (string.IsNullOrWhiteSpace(sizeImagePath) || !File.Exists(sizeImagePath))
        {
            return false;
        }

        var fingerprint = await GetSizeImageFingerprintAsync(sizeImagePath);
        return !string.Equals(record.SizeImageHash, fingerprint.Hash, StringComparison.OrdinalIgnoreCase);
    }

    private async Task OpenCardSizeDialogAsync(
        RootCardViewModel card,
        string sizeImagePath,
        CardSizeInfoRecord? existing,
        SizeImageFingerprint? fingerprint = null)
    {
        fingerprint ??= await GetSizeImageFingerprintAsync(sizeImagePath);
        var reuseExistingInput = existing is not null
            && string.Equals(existing.SizeImageHash, fingerprint.Value.Hash, StringComparison.OrdinalIgnoreCase);
        _cardSizeDialogCard = card;
        _cardSizeDialogImagePath = sizeImagePath;
        _cardSizeDialogImageHash = fingerprint.Value.Hash;
        _cardSizeDialogImageLastWriteUtc = fingerprint.Value.LastWriteUtc;
        CardSizeInputText = reuseExistingInput
            ? existing?.SizeRawInput ?? string.Empty
            : string.Empty;
        CardSizePreviewMeta = $"尺寸图：{Path.GetFileName(sizeImagePath)}{Environment.NewLine}录入后会保存到本地数据库，并在上架时使用。";
        CardSizePreviewImageSource = null;
        OnPropertyChanged(nameof(CardSizeDialogTitle));
        IsCardSizeDialogOpen = true;
        _saveCardSizeInfoCommand.RaiseCanExecuteChanged();

        try
        {
            CardSizePreviewImageSource = await Task.Run(() => ImageBitmapLoader.LoadFromFile(sizeImagePath, decodePixelWidth: 1200));
        }
        catch (Exception ex)
        {
            CardSizePreviewMeta = $"尺寸图预览失败：{ex.Message}";
        }
    }

    private bool CanSaveCardSizeInfo()
    {
        return IsCardSizeDialogOpen
            && _cardSizeDialogCard is not null
            && !string.IsNullOrWhiteSpace(CardSizeInputText)
            && BuildDisplaySizes(CardSizeInputText).Count > 0;
    }

    private async Task SaveCardSizeInfoAsync()
    {
        if (_cardSizeDialogCard is null)
        {
            return;
        }

        var sizes = BuildDisplaySizes(CardSizeInputText);
        if (sizes.Count == 0)
        {
            throw new InvalidOperationException("请输入至少一组尺寸，例如：50*225。");
        }

        var record = new CardSizeInfoRecord
        {
            CardPath = _cardSizeDialogCard.AutoPublishKeyPath,
            SizeText = string.Join(Environment.NewLine, sizes),
            SizeRawInput = CardSizeInputText.Trim(),
            SizeImageHash = _cardSizeDialogImageHash,
            SizeImageLastWriteUtc = _cardSizeDialogImageLastWriteUtc,
            SizeUpdatedAt = DateTimeOffset.Now
        };

        await _cardSizeInfoService.UpsertAsync(record);
        _cardSizeDialogCard.ApplyCardSizeInfo(record);
        StatusMessage = $"已保存尺寸：{_cardSizeDialogCard.DisplayName}";
        CloseCardSizeDialog();
    }

    private void CloseCardSizeDialog()
    {
        IsCardSizeDialogOpen = false;
        _cardSizeDialogCard = null;
        _cardSizeDialogImagePath = string.Empty;
        _cardSizeDialogImageHash = string.Empty;
        _cardSizeDialogImageLastWriteUtc = string.Empty;
        CardSizeInputText = string.Empty;
        CardSizePreviewMeta = string.Empty;
        CardSizePreviewImageSource = null;
        OnPropertyChanged(nameof(CardSizeDialogTitle));
        _saveCardSizeInfoCommand.RaiseCanExecuteChanged();
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
            await tab.RefreshCardSizeInfoAsync(_cardSizeInfoService, IsCardSizeInfoStaleAsync);
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
                : DefaultFolderPickerDirectory,
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
            InitialDirectory = Directory.Exists(BackupFolder) ? BackupFolder : DefaultFolderPickerDirectory,
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
        TryOpenFolder(GenerationOutputDirectory, "输出目录");
    }

    private void OpenSpBatchOutputFolder()
    {
        TryOpenFolder(SpBatchOutputDirectory, "输出目录");
    }

    private void OpenTemplateLibraryFile()
    {
        if (!File.Exists(TemplateLibraryPath))
        {
            return;
        }

        try
        {
            ShellOpenHelper.OpenFile(TemplateLibraryPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开文件失败：{ex.Message}";
        }
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

    private void TryOpenFolder(string folderPath, string label)
    {
        if (!Directory.Exists(folderPath))
        {
            StatusMessage = $"{label}不存在：{folderPath}";
            return;
        }

        try
        {
            ShellOpenHelper.OpenFolder(folderPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开目录失败：{ex.Message}";
        }
    }

    private static void OpenFolder(string folderPath)
    {
        ShellOpenHelper.OpenFolder(folderPath);
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

    private void StopSpBatch()
    {
        if (!IsSpBatchRunning || IsSpBatchStopping)
        {
            return;
        }

        IsSpBatchStopping = true;
        SpBatchStatusText = "正在停止...";
        SpBatchSummaryText = "正在停止当前 SKU 批处理任务...";
        StatusMessage = SpBatchSummaryText;
        _spBatchService.CancelCurrentRun();
        _spBatchCancellationTokenSource?.Cancel();
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

        IsSpBatchRunning = true;
        IsSpBatchStopping = false;
        SpBatchStatusText = _spBatchMode switch
        {
            SpBatchMode.PrepareOnly => "正在创建 SP 目录结构...",
            _ => "正在批量生成 SP 资源..."
        };
        SpBatchSummaryText = "任务执行中，请稍候。";
        SpBatchResultRootText = $"日期目录：{SpBatchOutputDirectory}";
        SpBatchResultStatsText = "SP 数量：0  总任务：0  成功：0  跳过：0  失败：0";
        UpdateSpBatchStats(0, 0, 0, 0, 0);
        ClearSpBatchResultCards();
        ClearSpBatchImageResultCards();
        ClearSpBatchImageResultCards();
        StatusMessage = SpBatchStatusText;
        _spBatchCancellationTokenSource?.Cancel();
        _spBatchCancellationTokenSource?.Dispose();
        _spBatchCancellationTokenSource = new CancellationTokenSource();

        try
        {
            var request = new SpBatchRequest
            {
                InputDirectory = SpBatchInputDirectory,
                OutputDirectory = SpBatchOutputDirectory,
                Image2ScriptPath = DefaultImage2ScriptPath,
                Concurrency = concurrency,
                Retries = retries,
                Mode = _spBatchMode,
                Overwrite = true
            };

            var result = await _spBatchService.GenerateAsync(request, _spBatchCancellationTokenSource.Token);
            ApplySpBatchVisualResult(result);
            StatusMessage = SpBatchSummaryText;
        }
        catch (OperationCanceledException)
        {
            IsSpBatchStopping = false;
            SpBatchStatusText = "已停止";
            SpBatchSummaryText = "已停止当前 SKU 批处理任务。";
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
            IsSpBatchStopping = false;
            _spBatchCancellationTokenSource?.Dispose();
            _spBatchCancellationTokenSource = null;
            IsSpBatchRunning = false;
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
        var manualSizes = await EnsureCardSizeForPublishAsync(card);
        var task = await card.PrepareAutoPublishDataAsync(manualSizes);
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

    private async Task<IReadOnlyList<string>> EnsureCardSizeForPublishAsync(RootCardViewModel card)
    {
        var record = await _cardSizeInfoService.GetByCardPathAsync(card.AutoPublishKeyPath);
        var isStale = await IsCardSizeInfoStaleAsync(card, record);
        card.ApplyCardSizeInfo(record, isStale);

        if (isStale)
        {
            throw new InvalidOperationException($"请先重新录入尺寸：{card.DisplayName} 的尺寸图已变更。");
        }

        if (record is null || string.IsNullOrWhiteSpace(record.SizeText))
        {
            throw new InvalidOperationException($"请先录入尺寸：{card.DisplayName}。");
        }

        var sizes = BuildPublishSizeEntries(record.SizeText);
        if (sizes.Count == 0)
        {
            throw new InvalidOperationException($"尺寸缺少 cm 规格，无法匹配规格表：{card.DisplayName}。");
        }

        return sizes;
    }

    private static bool TryFindSizeImageFromFiles(
        RootCardViewModel card,
        IReadOnlyList<string> files,
        out string sizeImagePath)
    {
        var mainFolder = Path.Combine(card.AutoPublishKeyPath, "main");
        sizeImagePath = files
            .Where(IsSupportedImageFile)
            .Where(path => IsInMainFolder(path, mainFolder))
            .Where(IsSizeImageName)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? string.Empty;

        return !string.IsNullOrWhiteSpace(sizeImagePath);
    }

    private static string FindCurrentSizeImagePath(RootCardViewModel card)
    {
        var mainFolder = Path.Combine(card.AutoPublishKeyPath, "main");
        if (!Directory.Exists(mainFolder))
        {
            return string.Empty;
        }

        return Directory.EnumerateFiles(mainFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImageFile)
            .Where(IsSizeImageName)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? string.Empty;
    }

    private static bool IsSizeImageName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return fileName.StartsWith("2-", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("尺寸", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("size", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInMainFolder(string filePath, string expectedMainFolder)
    {
        var folderPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        if (string.Equals(folderPath, expectedMainFolder, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(Path.GetFileName(folderPath), "main", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<SizeImageFingerprint> GetSizeImageFingerprintAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            using var stream = File.OpenRead(imagePath);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            var lastWriteUtc = File.GetLastWriteTimeUtc(imagePath).ToString("O", CultureInfo.InvariantCulture);
            return new SizeImageFingerprint(hash, lastWriteUtc);
        });
    }

    private static IReadOnlyList<string> BuildPublishSizeEntries(string sizeText)
    {
        return BuildDisplaySizes(sizeText)
            .Where(size => Regex.IsMatch(size, @"^\s*\d+(?:\.\d+)?\s*(?:cm)?\s*[*xX×]\s*\d+(?:\.\d+)?\s*cm\b", RegexOptions.IgnoreCase))
            .ToArray();
    }

    private static IReadOnlyList<string> BuildDisplaySizes(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return [];
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cmMatches = Regex.Matches(
            rawInput,
            @"(?<![\d.])(?<width>\d+(?:\.\d+)?)\s*(?:cm)?\s*[*xX×]\s*(?<length>\d+(?:\.\d+)?)(?:\s*cm)?(?!\s*(?:inch|in\b))",
            RegexOptions.IgnoreCase);

        foreach (Match match in cmMatches)
        {
            if (!double.TryParse(match.Groups["width"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var widthCm)
                || !double.TryParse(match.Groups["length"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lengthCm))
            {
                continue;
            }

            var display = $"{FormatCm(widthCm)}*{FormatCm(lengthCm)}cm/{FormatInch(widthCm)}*{FormatInch(lengthCm)}inch";
            if (seen.Add(display))
            {
                result.Add(display);
            }
        }

        if (result.Count > 0)
        {
            return result;
        }

        var inchMatches = Regex.Matches(
            rawInput,
            @"(?<![\d.])(?<width>\d+(?:\.\d+)?)\s*[*xX×]\s*(?<length>\d+(?:\.\d+)?)\s*(?:inch|in\b)",
            RegexOptions.IgnoreCase);
        foreach (Match match in inchMatches)
        {
            var display = $"{match.Groups["width"].Value}*{match.Groups["length"].Value}inch";
            if (seen.Add(display))
            {
                result.Add(display);
            }
        }

        return result;
    }

    private static string FormatCm(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.001
            ? Math.Round(value).ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatInch(double cmValue)
    {
        return (cmValue / 2.54).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private readonly record struct SizeImageFingerprint(string Hash, string LastWriteUtc);

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
        SpBatchSummaryText = "\u7528\u4e8e\u6279\u91cf\u521b\u5efa\u65e5\u671f\u76ee\u5f55\u3001SKU \u7ed3\u6784\u548c 6 \u8272 SKU \u56fe\u3002";
        SpBatchResultRootText = $"\u65e5\u671f\u76ee\u5f55\uff1a{SpBatchOutputDirectory}";
        SpBatchResultStatsText = "SKU \u6570\u91cf\uff1a0  \u603b\u4efb\u52a1\uff1a0  \u6210\u529f\uff1a0  \u8df3\u8fc7\uff1a0  \u5931\u8d25\uff1a0";
        ClearSpBatchResultCards();
    }

    private void RefreshSkuResults()
    {
        ClearSpBatchResultCards();
        ClearSpBatchImageResultCards();
        ResetSpBatchSummary();
        StatusMessage = "\u5df2\u6e05\u7a7a SKU \u7ed3\u679c\u5c55\u793a\uff0c\u672c\u5730\u56fe\u7247\u6587\u4ef6\u672a\u5220\u9664\u3002";
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
        tab.CardImageFilesAdded -= OnTabCardImageFilesAdded;
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
        OnPropertyChanged(nameof(IsTemplateManagerSelected));
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

    private static string ResolveDefaultFolderPickerDirectory()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktop) && Directory.Exists(desktop))
        {
            return desktop;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile) ? string.Empty : userProfile;
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

    private void UpdateSpBatchStats(int skuCount, int totalCount, int successCount, int skippedCount, int failedCount)
    {
        _spBatchStatsSkuCount = skuCount;
        _spBatchStatsTotalCount = totalCount;
        _spBatchStatsSuccessCount = successCount;
        _spBatchStatsSkippedCount = skippedCount;
        _spBatchStatsFailedCount = failedCount;
        OnPropertyChanged(nameof(SpBatchStatsSkuCountText));
        OnPropertyChanged(nameof(SpBatchStatsTotalText));
        OnPropertyChanged(nameof(SpBatchStatsSuccessText));
        OnPropertyChanged(nameof(SpBatchStatsSkippedText));
        OnPropertyChanged(nameof(SpBatchStatsFailedText));
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
            "prepared" => $"\u76ee\u5f55\u7ed3\u6784\u521b\u5efa\u5b8c\u6210\uff0c\u5171\u51c6\u5907 {result.PreparedBundles.Count} \u4e2a SKU \u76ee\u5f55\u3002",
            _ => $"\u6279\u5904\u7406\u5b8c\u6210\uff0c\u5171 {result.Results.Count} \u4e2a\u4efb\u52a1\uff0c\u6210\u529f {result.SuccessCount}\uff0c\u8df3\u8fc7 {result.SkippedCount}\uff0c\u5931\u8d25 {result.FailedCount}\u3002"
        };

        SpBatchResultRootText = $"\u65e5\u671f\u76ee\u5f55\uff1a{result.DatedRoot}";
        SpBatchResultStatsText = $"SKU \u6570\u91cf\uff1a{CountSpDirectories(result)}  \u603b\u4efb\u52a1\uff1a{result.Results.Count}  \u6210\u529f\uff1a{result.SuccessCount}  \u8df3\u8fc7\uff1a{result.SkippedCount}  \u5931\u8d25\uff1a{result.FailedCount}";
        UpdateSpBatchStats(CountSpDirectories(result), result.Results.Count, result.SuccessCount, result.SkippedCount, result.FailedCount);
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
        return !IsGenerationPromptsOnly && GeneratedImageResultCards.Any(card => card.IsSelected);
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

        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in SpBatchSourceImageCards)
        {
            var targetName = GetUniqueStagingFileName(card, usedFileNames);
            var targetPath = Path.Combine(stagingRoot, targetName);
            File.Copy(card.ImagePath, targetPath, overwrite: true);
        }

        return stagingRoot;
    }

    private static string GetUniqueStagingFileName(GeneratedImageResultCardViewModel card, ISet<string> usedFileNames)
    {
        var originalFileName = Path.GetFileName(card.FileName);
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            originalFileName = Path.GetFileName(card.ImagePath);
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = Path.GetExtension(card.ImagePath);
        }

        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = Path.GetFileNameWithoutExtension(card.ImagePath);
        }

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(invalidChar, '_');
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "image";
        }

        var candidate = $"{baseName}{extension}";
        var index = 2;
        while (!usedFileNames.Add(candidate))
        {
            candidate = $"{baseName}_{index}{extension}";
            index++;
        }

        return candidate;
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

        IsSpBatchRunning = true;
        IsSpBatchStopping = false;
        SpBatchStatusText = _spBatchMode switch
        {
            SpBatchMode.PrepareOnly => "正在创建 SP 目录结构...",
            _ => "正在批量生成 SP 资源..."
        };
        SpBatchSummaryText = "任务执行中，请稍候。";
        SpBatchResultRootText = $"日期目录：{SpBatchOutputDirectory}";
        SpBatchResultStatsText = "SP 数量：0  总任务：0  成功：0  跳过：0  失败：0";
        UpdateSpBatchStats(0, 0, 0, 0, 0);
        ClearSpBatchResultCards();
        ClearSpBatchImageResultCards();
        StatusMessage = SpBatchStatusText;
        _spBatchCancellationTokenSource?.Cancel();
        _spBatchCancellationTokenSource?.Dispose();
        _spBatchCancellationTokenSource = new CancellationTokenSource();

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
                Mode = _spBatchMode,
                Overwrite = true
            };

            var result = await _spBatchService.GenerateAsync(request, _spBatchCancellationTokenSource.Token);
            ApplySpBatchVisualResult(result);
            StatusMessage = SpBatchSummaryText;
        }
        catch (OperationCanceledException)
        {
            IsSpBatchStopping = false;
            SpBatchStatusText = "已停止";
            SpBatchSummaryText = "已停止当前 SKU 批处理任务。";
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
            IsSpBatchStopping = false;
            _spBatchCancellationTokenSource?.Dispose();
            _spBatchCancellationTokenSource = null;
            IsSpBatchRunning = false;
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

        try
        {
            ShellOpenHelper.OpenFile(_previewImagePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSupportedImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp" or ".jfif";
    }
}
