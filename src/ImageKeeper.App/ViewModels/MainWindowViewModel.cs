using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageKeeper.App.Utilities;
using ImageKeeper.Core;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ImageKeeper.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
	private readonly record struct SizeImageFingerprint(string Hash, string LastWriteUtc);

	private sealed record SpBatchLinkedBundleManifestItem(string FileName, string CardPath, string SkuDirectory, string SourceCopyPath);

	private const string SpBatchLinkedBundlesManifestName = ".sp-batch-linked-bundles.json";

	private sealed record GenerationTabState(string StatusText, string ResultModeText, string ResultOutputText, bool? LastExecutedPromptsOnly, List<GenerationPromptCardViewModel> PromptCards, List<GeneratedImageResultCardViewModel> ImageCards);

	private sealed class TitleTemplatePayload
	{
		public List<string> MainTitles { get; set; } = new List<string>();

		public List<string> SubTitles { get; set; } = new List<string>();

		public List<string> IconWords { get; set; } = new List<string>();
	}

	private const string ReviewWorkspaceSection = "review-workspace";

	private const string ImageGenerateSection = "image-generate";

	private const string TemplateManagerSection = "template-manager";

	private const string MainImageGenerateTab = "main-image-generate";

	private const string SpBatchTab = "sp-batch";

	private const string SkuOptimizeTab = "sku-optimize";

	private const string SceneImageGenerateTab = "scene-image-generate";

	private const string CompareImageGenerateTab = "compare-image-generate";

	private const string Image2GenerationProvider = "image2";

	private const string GitHubLatestReleaseApiUrl = "https://api.github.com/repos/dohkone/ToolBox/releases/latest";

	private const string GitHubReleaseDownloadUrl = "https://github.com/dohkone/ToolBox/releases/latest";

	private const string MainTitleTemplateType = "main-title";

	private const string SubTitleTemplateType = "sub-title";

	private const string IconWordTemplateType = "icon-word";

	private static readonly string[] SkuMasterColorTokens =
	{
		"黑色",
		"米白色",
		"深棕色",
		"深灰色",
		"酒红色",
		"宝蓝色",
	};

	private readonly IFolderScanService _folderScanService;

	private readonly IImageWorkspaceService _imageWorkspaceService;

	private readonly IWorkspaceStateService _workspaceStateService;

	private readonly IAppSettingsService _appSettingsService;

	private readonly IProductSheetService _productSheetService;

	private readonly ITemplateGenerationService _templateGenerationService;

	private readonly ISpBatchService _spBatchService;

	private readonly ISkuOptimizeService _skuOptimizeService;

	private readonly IMiaoshouPublishService _miaoshouPublishService;

	private readonly IAutoPublishStateService _autoPublishStateService;

	private readonly ICardSizeInfoService _cardSizeInfoService;

	private readonly ITemplateLibraryService _templateLibraryService;

	private readonly SemaphoreSlim _autoPublishLock = new SemaphoreSlim(1, 1);

	private readonly AsyncRelayCommand _chooseFolderCommand;

	private readonly AsyncRelayCommand _selectBackupFolderCommand;

	private readonly RelayCommand _openCurrentFolderCommand;

	private readonly RelayCommand _invertSelectionCommand;

	private readonly RelayCommand _selectAllBatchCardsCommand;

	private readonly RelayCommand _clearAllBatchCardsCommand;

	private readonly RelayCommand _setAutoPublishStatusFilterCommand;

	private readonly RelayCommand _openAutoPublishSettingsCommand;

	private readonly RelayCommand _closeAutoPublishSettingsCommand;

	private readonly AsyncRelayCommand _generateProductSheetCommand;

	private readonly AsyncRelayCommand _addTabCommand;

	private readonly RelayCommand _showReviewWorkspaceCommand;

	private readonly RelayCommand _showImageGenerateCommand;

	private readonly RelayCommand _showTemplateManagerCommand;

	private readonly RelayCommand _showTemplateGenerateTabCommand;

	private readonly RelayCommand _showSpBatchTabCommand;

	private readonly RelayCommand _showSkuOptimizeTabCommand;

	private readonly RelayCommand _sendSelectedSpBatchMasterToSkuOptimizeCommand;

	private readonly RelayCommand _openSpBatchColorSelectionCommand;

	private readonly RelayCommand _closeSpBatchColorSelectionCommand;

	private readonly RelayCommand _selectAllSpBatchColorSelectionCommand;

	private readonly RelayCommand _clearAllSpBatchColorSelectionCommand;

	private readonly RelayCommand _saveSpBatchColorSelectionCommand;

	private readonly AsyncRelayCommand _chooseSpBatchSourceImagesCommand;

	private readonly AsyncRelayCommand _chooseSpBatchMasterImagesCommand;

	private readonly RelayCommand _showSceneImageGenerateTabCommand;

	private readonly RelayCommand _showCompareImageGenerateTabCommand;

	private readonly AsyncRelayCommand _showLayoutTemplateTabCommand;

	private readonly AsyncRelayCommand _showSceneTemplateTabCommand;

	private readonly AsyncRelayCommand _showSubjectTemplateTabCommand;

	private readonly AsyncRelayCommand _showTitleTemplateTabCommand;

	private readonly AsyncRelayCommand _showColorTemplateTabCommand;

	private readonly RelayCommand _showMainImageLayoutTypeCommand;

	private readonly RelayCommand _showSceneImageLayoutTypeCommand;

	private readonly RelayCommand _showCompareImageLayoutTypeCommand;

	private readonly RelayCommand _showMainTitleTemplateTypeCommand;

	private readonly RelayCommand _showSubTitleTemplateTypeCommand;

	private readonly RelayCommand _showIconWordTemplateTypeCommand;

	private readonly RelayCommand _newTemplateCommand;

	private readonly AsyncRelayCommand _saveTemplateCommand;

	private readonly AsyncRelayCommand _deleteTemplateCommand;

	private readonly AsyncRelayCommand _deleteManagedTemplateCommand;

	private readonly AsyncRelayCommand _copyManagedLayoutTemplateCommand;

	private readonly AsyncRelayCommand _deleteColorTemplateGroupCommand;

	private readonly RelayCommand _selectManagedTemplateCommand;

	private readonly RelayCommand _selectColorTemplateGroupCommand;

	private readonly AsyncRelayCommand _chooseTemplatePreviewCommand;

	private readonly RelayCommand _openTemplateEditorPreviewCommand;

	private readonly AsyncRelayCommand _importLayoutTemplatesCommand;

	private readonly AsyncRelayCommand _exportLayoutTemplatesCommand;

	private readonly AsyncRelayCommand _importAllTemplatesCommand;

	private readonly AsyncRelayCommand _exportAllTemplatesCommand;

	private readonly RelayCommand _closeTemplateEditorCommand;

	private readonly RelayCommand _addSceneContentLineCommand;

	private readonly RelayCommand _removeSceneContentLineCommand;

	private readonly RelayCommand _addTitleMainLineCommand;

	private readonly RelayCommand _removeTitleMainLineCommand;

	private readonly RelayCommand _addTitleSubLineCommand;

	private readonly RelayCommand _removeTitleSubLineCommand;

	private readonly RelayCommand _openTemplateSubjectPickerCommand;

	private readonly RelayCommand _closeTemplateSubjectPickerCommand;

	private readonly RelayCommand _toggleTemplateSubjectOptionCommand;

	private readonly RelayCommand _removeTemplateSubjectTagCommand;

	private readonly RelayCommand _startAddTemplateSubjectTagCommand;

	private readonly AsyncRelayCommand _commitTemplateSubjectTagCommand;

	private readonly RelayCommand _cancelTemplateSubjectTagCommand;

	private readonly AsyncRelayCommand _chooseTemplateLibraryCommand;

	private readonly RelayCommand _openTemplateLibraryFileCommand;

	private readonly AsyncRelayCommand _openGenerationTemplatePickerCommand;

	private readonly RelayCommand _closeGenerationTemplatePickerCommand;

	private readonly RelayCommand _selectAllGenerationTemplatesCommand;

	private readonly RelayCommand _clearGenerationTemplatesCommand;

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

	private readonly AsyncRelayCommand _generateSpBatchColorSkusCommand;

	private readonly RelayCommand _stopSpBatchCommand;

	private readonly RelayCommand _refreshSkuResultsCommand;

	private readonly AsyncRelayCommand _chooseSkuOptimizeOutputFolderCommand;

	private readonly RelayCommand _openSkuOptimizeOutputFolderCommand;

	private readonly AsyncRelayCommand _runSkuOptimizeCommand;

	private readonly RelayCommand _stopSkuOptimizeCommand;

	private readonly AsyncRelayCommand _runBatchAutoPublishCommand;

	private readonly AsyncRelayCommand _saveCardSizeInfoCommand;

	private readonly RelayCommand _cancelCardSizeInfoCommand;

	private readonly RelayCommand _openImageGenerationKeyDialogCommand;

	private readonly RelayCommand _selectImageGenerationProviderCommand;

	private readonly RelayCommand _saveImageGenerationKeyCommand;

	private readonly RelayCommand _cancelImageGenerationKeyCommand;

	private readonly RelayCommand _addColorTemplateRowCommand;

	private readonly RelayCommand _removeColorTemplateRowCommand;

	private readonly RelayCommand _openColorTemplateColorPreviewCommand;

	private readonly RelayCommand _closeColorTemplateColorPreviewCommand;

	private readonly AsyncRelayCommand _saveColorTemplateGroupCommand;

	private readonly RelayCommand _closeColorTemplateGroupEditorCommand;

	private readonly AsyncRelayCommand _runAppUpdateCommand;

	private CancellationTokenSource? _previewCancellationTokenSource;

	private CancellationTokenSource? _templateGenerationCancellationTokenSource;

	private CancellationTokenSource? _spBatchCancellationTokenSource;

	private CancellationTokenSource? _skuOptimizeCancellationTokenSource;

	private WorkspaceTabViewModel? _selectedTab;

	private TemplateItemViewModel? _selectedTemplateItem;

	private ColorTemplateGroupViewModel? _selectedColorTemplateGroup;

	private ColorTemplateGroupViewModel? _selectedColorTemplateGroupForGeneration;

	private ColorTemplateGroupViewModel? _selectedSpBatchColorTemplateGroupForGeneration;

	private ColorTemplateColorViewModel? _selectedColorTemplateColorPreview;

	private ObservableCollection<TemplateItemViewModel> _managedTemplateItems = new ObservableCollection<TemplateItemViewModel>();

	private ObservableCollection<ColorTemplateGroupViewModel> _colorTemplateGroups = new ObservableCollection<ColorTemplateGroupViewModel>();

	private readonly ObservableCollection<ColorTemplateColorViewModel> _spBatchColorSelectionColors = new ObservableCollection<ColorTemplateColorViewModel>();

	private string _selectedSection = "image-generate";

	private string _selectedImageGenerateTab = "main-image-generate";

	private TemplateCategory _selectedTemplateCategory;

	private ImageTemplateType _selectedLayoutImageType;

	private string _selectedTitleTemplateType = MainTitleTemplateType;

	private long _selectedColorTemplateGroupId;

	private long _selectedGenerationColorTemplateGroupId;

	private long _selectedSpBatchColorTemplateGroupId;

	private readonly Dictionary<long, HashSet<string>> _spBatchSelectedColorNamesByGroupId = new Dictionary<long, HashSet<string>>();

	private bool _isSpBatchColorSelectionOpen;

	private readonly string _appCurrentVersion = GetCurrentAppVersion();

	private string _appVersionText = "v" + GetCurrentAppVersion();

	private bool _isAppUpdateAvailable;

	private bool _isAppUpdateDownloading;

	private string _availableAppUpdateVersion = string.Empty;

	private string _availableAppUpdateReleaseNotes = string.Empty;

	private string _availableAppUpdateInstallerUrl = string.Empty;

	private string _availableAppUpdateInstallerName = string.Empty;

	private int _mainImageLayoutTemplateCount;

	private int _sceneImageLayoutTemplateCount;

	private int _compareImageLayoutTemplateCount;

	private int _mainTitleTemplateCount;

	private int _subTitleTemplateCount;

	private int _iconWordTemplateCount;

	private int _colorTemplateGroupCount;

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

	private ImageSource? _previewImageSource;

	private string _backupFolder = string.Empty;

	private bool _titleChineseOnly;

	private bool _isAutoPublishSettingsDialogOpen;

	private bool _isAutoPublishRunning;

	private bool _isTemplateGenerating;

	private bool _isTemplateGenerationStopping;

	private bool _isSpBatchStopping;

	private string _templateLibraryPath = string.Empty;

	private bool _isGenerationTemplatePickerOpen;

	private string _generationOutputDirectory = string.Empty;

	private string _generationCountText = "1";

	private string _generationConcurrencyText = "1";

	private bool _isGenerationUniqueScene = true;

	private bool _isGenerationPromptsOnly;

	private bool _isImageGenerationKeyDialogOpen;

	private string _imageGenerationKeyText = string.Empty;

	private string _selectedImageGenerationProvider = Image2GenerationProvider;

	private bool? _lastExecutedGenerationPromptsOnly;

	private string? _activeTemplateGenerationTab;

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

	private SpBatchMode? _activeSpBatchMode;

	private string _spBatchStatusText = "待命";

	private string _spBatchColorStatusText = "待命";

	private string _spBatchSummaryText = "用于批量创建日期目录、SP 结构和多色 SKU 图。";

	private string _spBatchResultRootText = "日期目录：未设置";

	private string _spBatchResultStatsText = "SP 数量：0  总任务：0  成功：0  跳过：0  失败：0";

	private int _spBatchStatsSkuCount;

	private int _spBatchStatsTotalCount;

	private int _spBatchStatsSuccessCount;

	private int _spBatchStatsSkippedCount;

	private int _spBatchStatsFailedCount;

	private bool _isSkuOptimizeRunning;

	private bool _isSkuOptimizeStopping;

	private string _skuOptimizeOutputDirectory = string.Empty;

	private string _skuOptimizeConcurrencyText = "1";

	private string _skuOptimizeLengthMultiplierText = "2";

	private bool _isSkuOptimizeLengthGrow = true;

	private bool _isSkuOptimizeLengthShrink;

	private string _skuOptimizeDiameterMultiplierText = "0.67";

	private string _skuOptimizeStatusText = "待命";

	private string _skuOptimizeSummaryText = "用于只调整卷轴的长度与直径，其他内容保持不变。";

	private string _skuOptimizeResultRootText = "输出目录：未设置";

	private string _skuOptimizeResultStatsText = "总任务：0  成功：0  跳过：0  失败：0";

	private int _skuOptimizeStatsTotalCount;

	private int _skuOptimizeStatsSuccessCount;

	private int _skuOptimizeStatsSkippedCount;

	private int _skuOptimizeStatsFailedCount;

	private bool _isSkuOptimizeStagingDropTarget;

	private Dictionary<string, string> _skuOptimizeOriginalPathByStagingPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private RootCardViewModel? _cardSizeDialogCard;

	private string _cardSizeDialogImagePath = string.Empty;

	private string _cardSizeDialogImageHash = string.Empty;

	private string _cardSizeDialogImageLastWriteUtc = string.Empty;

	private string _cardSizeInputText = string.Empty;

	private string _cardSizePreviewMeta = string.Empty;

	private ImageSource? _cardSizePreviewImageSource;

	private bool _isCardSizeDialogOpen;

	private string _templateEditorName = string.Empty;

	private string _templateEditorContent = string.Empty;

	private string _templateEditorPreviewImagePath = string.Empty;

	private bool _templateEditorIsEnabled = true;

	private string _newTemplateSubjectText = string.Empty;

	private bool _isTemplateEditorDialogOpen;

	private bool _isTemplateSubjectPickerOpen;

	private bool _isColorTemplateGroupEditorOpen;

	private bool _isColorTemplateColorPreviewOpen;

	private string _colorTemplateGroupEditorName = string.Empty;

	private bool _isSpBatchStagingDropTarget;

	private bool _isSpBatchMasterDropTarget;

	private readonly Dictionary<long, IReadOnlyList<long>> _sceneSubjectBindingCache = new Dictionary<long, IReadOnlyList<long>>();

	private readonly Dictionary<long, TemplateItemRecord> _subjectTemplateLookup = new Dictionary<long, TemplateItemRecord>();

	private readonly Dictionary<string, GenerationTabState> _generationTabStates = new Dictionary<string, GenerationTabState>();

	private static string DefaultTemplateLibraryPath => ResolveTemplateLibraryPath();

	private string CurrentImageGenerationScriptPath => ResolveImage2ScriptPath();

	private static string DefaultFolderPickerDirectory => ResolveDefaultFolderPickerDirectory();

	public ObservableCollection<WorkspaceTabViewModel> WorkspaceTabs { get; } = new ObservableCollection<WorkspaceTabViewModel>();

	public ObservableCollection<GenerationPromptCardViewModel> GenerationPromptCards { get; } = new ObservableCollection<GenerationPromptCardViewModel>();

	public ObservableCollection<GeneratedImageResultCardViewModel> GeneratedImageResultCards { get; } = new ObservableCollection<GeneratedImageResultCardViewModel>();

	public ObservableCollection<GeneratedImageResultCardViewModel> SpBatchSourceImageCards { get; } = new ObservableCollection<GeneratedImageResultCardViewModel>();

	public ObservableCollection<GeneratedImageResultCardViewModel> SpBatchMasterImageCards { get; } = new ObservableCollection<GeneratedImageResultCardViewModel>();

	public ObservableCollection<GeneratedImageResultCardViewModel> SpBatchImageResultCards { get; } = new ObservableCollection<GeneratedImageResultCardViewModel>();

	public ObservableCollection<SpBatchResultCardViewModel> SpBatchResultCards { get; } = new ObservableCollection<SpBatchResultCardViewModel>();

	public ObservableCollection<GeneratedImageResultCardViewModel> SkuOptimizeSourceImageCards { get; } = new ObservableCollection<GeneratedImageResultCardViewModel>();

	public ObservableCollection<GeneratedImageResultCardViewModel> SkuOptimizeImageResultCards { get; } = new ObservableCollection<GeneratedImageResultCardViewModel>();

	public ObservableCollection<SpBatchResultCardViewModel> SkuOptimizeResultCards { get; } = new ObservableCollection<SpBatchResultCardViewModel>();

	public ObservableCollection<TemplateItemViewModel> ManagedTemplateItems
	{
		get
		{
			return _managedTemplateItems;
		}
		private set
		{
			SetProperty(ref _managedTemplateItems, value, "ManagedTemplateItems");
		}
	}

	public ObservableCollection<ColorTemplateGroupViewModel> ColorTemplateGroups
	{
		get => _colorTemplateGroups;
		private set => SetProperty(ref _colorTemplateGroups, value, "ColorTemplateGroups");
	}

	public ColorTemplateGroupViewModel? SelectedColorTemplateGroupForGeneration
	{
		get => _selectedColorTemplateGroupForGeneration;
		set
		{
			if (SetProperty(ref _selectedColorTemplateGroupForGeneration, value, "SelectedColorTemplateGroupForGeneration"))
			{
				_selectedGenerationColorTemplateGroupId = value?.Id ?? 0;
			}
		}
	}

	public ColorTemplateGroupViewModel? SelectedSpBatchColorTemplateGroupForGeneration
	{
		get => _selectedSpBatchColorTemplateGroupForGeneration;
		set
		{
			if (SetProperty(ref _selectedSpBatchColorTemplateGroupForGeneration, value, "SelectedSpBatchColorTemplateGroupForGeneration"))
			{
				_selectedSpBatchColorTemplateGroupId = value?.Id ?? 0;
				RefreshSpBatchColorSelectionForCurrentGroup();
				OnPropertyChanged("SpBatchColorSelectionTitle");
				OnPropertyChanged("SpBatchColorSelectionSummaryText");
				_openSpBatchColorSelectionCommand.RaiseCanExecuteChanged();
				_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
				PersistUserPathSettings();
			}
		}
	}

	public ObservableCollection<ColorTemplateColorViewModel> SpBatchColorSelectionColors => _spBatchColorSelectionColors;

	public ObservableCollection<SceneContentLineViewModel> SceneContentLines { get; } = new ObservableCollection<SceneContentLineViewModel>();

	public ObservableCollection<SceneContentLineViewModel> TitleMainLines { get; } = new ObservableCollection<SceneContentLineViewModel>();

	public ObservableCollection<SceneContentLineViewModel> TitleSubLines { get; } = new ObservableCollection<SceneContentLineViewModel>();

	public ObservableCollection<TemplateSubjectTagViewModel> TemplateSubjectTags { get; } = new ObservableCollection<TemplateSubjectTagViewModel>();

	public ObservableCollection<TemplateSubjectOptionViewModel> TemplateSubjectOptions { get; } = new ObservableCollection<TemplateSubjectOptionViewModel>();

	public ObservableCollection<GenerationTemplateOptionViewModel> GenerationTemplateOptions { get; } = new ObservableCollection<GenerationTemplateOptionViewModel>();

	public ObservableCollection<ColorTemplateColorViewModel> ColorTemplateEditorColors { get; } = new ObservableCollection<ColorTemplateColorViewModel>();

	public ICommand ChooseFolderCommand => _chooseFolderCommand;

	public ICommand SelectBackupFolderCommand => _selectBackupFolderCommand;

	public ICommand OpenCurrentFolderCommand => _openCurrentFolderCommand;

	public ICommand InvertSelectionCommand => _invertSelectionCommand;

	public ICommand SelectAllBatchCardsCommand => _selectAllBatchCardsCommand;

	public ICommand ClearAllBatchCardsCommand => _clearAllBatchCardsCommand;

	public ICommand SetAutoPublishStatusFilterCommand => _setAutoPublishStatusFilterCommand;

	public ICommand OpenAutoPublishSettingsCommand => _openAutoPublishSettingsCommand;

	public ICommand CloseAutoPublishSettingsCommand => _closeAutoPublishSettingsCommand;

	public ICommand GenerateProductSheetCommand => _generateProductSheetCommand;

	public ICommand AddTabCommand => _addTabCommand;

	public ICommand ShowReviewWorkspaceCommand => _showReviewWorkspaceCommand;

	public ICommand ShowImageGenerateCommand => _showImageGenerateCommand;

	public ICommand ShowTemplateManagerCommand => _showTemplateManagerCommand;

	public ICommand ShowTemplateGenerateTabCommand => _showTemplateGenerateTabCommand;

	public ICommand ShowSpBatchTabCommand => _showSpBatchTabCommand;

	public ICommand ShowSkuOptimizeTabCommand => _showSkuOptimizeTabCommand;

	public ICommand SendSelectedSpBatchMasterToSkuOptimizeCommand => _sendSelectedSpBatchMasterToSkuOptimizeCommand;

	public ICommand OpenSpBatchColorSelectionCommand => _openSpBatchColorSelectionCommand;

	public ICommand CloseSpBatchColorSelectionCommand => _closeSpBatchColorSelectionCommand;

	public ICommand SelectAllSpBatchColorSelectionCommand => _selectAllSpBatchColorSelectionCommand;

	public ICommand ClearAllSpBatchColorSelectionCommand => _clearAllSpBatchColorSelectionCommand;

	public ICommand SaveSpBatchColorSelectionCommand => _saveSpBatchColorSelectionCommand;

	public ICommand ChooseSpBatchSourceImagesCommand => _chooseSpBatchSourceImagesCommand;

	public ICommand ChooseSpBatchMasterImagesCommand => _chooseSpBatchMasterImagesCommand;

	public ICommand ShowSceneImageGenerateTabCommand => _showSceneImageGenerateTabCommand;

	public ICommand ShowCompareImageGenerateTabCommand => _showCompareImageGenerateTabCommand;

	public ICommand ShowSceneGenerateTabCommand => _showSceneImageGenerateTabCommand;

	public ICommand ShowCompareGenerateTabCommand => _showCompareImageGenerateTabCommand;

	public ICommand ShowLayoutTemplateTabCommand => _showLayoutTemplateTabCommand;

	public ICommand ShowSceneTemplateTabCommand => _showSceneTemplateTabCommand;

	public ICommand ShowSubjectTemplateTabCommand => _showSubjectTemplateTabCommand;

	public ICommand ShowTitleTemplateTabCommand => _showTitleTemplateTabCommand;

	public ICommand ShowColorTemplateTabCommand => _showColorTemplateTabCommand;

	public ICommand ShowMainImageLayoutTypeCommand => _showMainImageLayoutTypeCommand;

	public ICommand ShowSceneImageLayoutTypeCommand => _showSceneImageLayoutTypeCommand;

	public ICommand ShowCompareImageLayoutTypeCommand => _showCompareImageLayoutTypeCommand;

	public ICommand ShowMainTitleTemplateTypeCommand => _showMainTitleTemplateTypeCommand;

	public ICommand ShowSubTitleTemplateTypeCommand => _showSubTitleTemplateTypeCommand;

	public ICommand ShowIconWordTemplateTypeCommand => _showIconWordTemplateTypeCommand;

	public ICommand NewTemplateCommand => _newTemplateCommand;

	public ICommand SaveTemplateCommand => _saveTemplateCommand;

	public ICommand DeleteTemplateCommand => _deleteTemplateCommand;

	public ICommand DeleteManagedTemplateCommand => _deleteManagedTemplateCommand;

	public ICommand CopyManagedLayoutTemplateCommand => _copyManagedLayoutTemplateCommand;

	public ICommand DeleteColorTemplateGroupCommand => _deleteColorTemplateGroupCommand;

	public ICommand SelectManagedTemplateCommand => _selectManagedTemplateCommand;

	public ICommand SelectColorTemplateGroupCommand => _selectColorTemplateGroupCommand;

	public ICommand ChooseTemplatePreviewCommand => _chooseTemplatePreviewCommand;

	public ICommand OpenTemplateEditorPreviewCommand => _openTemplateEditorPreviewCommand;

	public ICommand ImportLayoutTemplatesCommand => _importLayoutTemplatesCommand;

	public ICommand ExportLayoutTemplatesCommand => _exportLayoutTemplatesCommand;

	public ICommand ImportAllTemplatesCommand => _importAllTemplatesCommand;

	public ICommand ExportAllTemplatesCommand => _exportAllTemplatesCommand;

	public ICommand CloseTemplateEditorCommand => _closeTemplateEditorCommand;

	public ICommand AddSceneContentLineCommand => _addSceneContentLineCommand;

	public ICommand RemoveSceneContentLineCommand => _removeSceneContentLineCommand;

	public ICommand AddTitleMainLineCommand => _addTitleMainLineCommand;

	public ICommand RemoveTitleMainLineCommand => _removeTitleMainLineCommand;

	public ICommand AddTitleSubLineCommand => _addTitleSubLineCommand;

	public ICommand RemoveTitleSubLineCommand => _removeTitleSubLineCommand;

	public ICommand OpenTemplateSubjectPickerCommand => _openTemplateSubjectPickerCommand;

	public ICommand CloseTemplateSubjectPickerCommand => _closeTemplateSubjectPickerCommand;

	public ICommand ToggleTemplateSubjectOptionCommand => _toggleTemplateSubjectOptionCommand;

	public ICommand RemoveTemplateSubjectTagCommand => _removeTemplateSubjectTagCommand;

	public ICommand StartAddTemplateSubjectTagCommand => _startAddTemplateSubjectTagCommand;

	public ICommand CommitTemplateSubjectTagCommand => _commitTemplateSubjectTagCommand;

	public ICommand CancelTemplateSubjectTagCommand => _cancelTemplateSubjectTagCommand;

	public ICommand ChooseTemplateLibraryCommand => _chooseTemplateLibraryCommand;

	public ICommand OpenTemplateLibraryFileCommand => _openTemplateLibraryFileCommand;

	public ICommand OpenGenerationTemplatePickerCommand => _openGenerationTemplatePickerCommand;

	public ICommand CloseGenerationTemplatePickerCommand => _closeGenerationTemplatePickerCommand;

	public ICommand SelectAllGenerationTemplatesCommand => _selectAllGenerationTemplatesCommand;

	public ICommand ClearGenerationTemplatesCommand => _clearGenerationTemplatesCommand;

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

	public ICommand GenerateSpBatchColorSkusCommand => _generateSpBatchColorSkusCommand;

	public ICommand RunSpBatchMasterCommand => _runSpBatchCommand;

	public ICommand RunSpBatchColorsCommand => _generateSpBatchColorSkusCommand;

	public ICommand StopSpBatchCommand => _stopSpBatchCommand;

	public ICommand RefreshSkuResultsCommand => _refreshSkuResultsCommand;

	public ICommand ChooseSkuOptimizeOutputFolderCommand => _chooseSkuOptimizeOutputFolderCommand;

	public ICommand OpenSkuOptimizeOutputFolderCommand => _openSkuOptimizeOutputFolderCommand;

	public ICommand RunSkuOptimizeCommand => _runSkuOptimizeCommand;

	public ICommand StopSkuOptimizeCommand => _stopSkuOptimizeCommand;

	public ICommand RunBatchAutoPublishCommand => _runBatchAutoPublishCommand;

	public ICommand SaveCardSizeInfoCommand => _saveCardSizeInfoCommand;

	public ICommand CancelCardSizeInfoCommand => _cancelCardSizeInfoCommand;

	public ICommand OpenImageGenerationKeyDialogCommand => _openImageGenerationKeyDialogCommand;

	public ICommand SelectImageGenerationProviderCommand => _selectImageGenerationProviderCommand;

	public ICommand SaveImageGenerationKeyCommand => _saveImageGenerationKeyCommand;

	public ICommand CancelImageGenerationKeyCommand => _cancelImageGenerationKeyCommand;

	public ICommand AddColorTemplateRowCommand => _addColorTemplateRowCommand;

	public ICommand RemoveColorTemplateRowCommand => _removeColorTemplateRowCommand;

	public ICommand OpenColorTemplateColorPreviewCommand => _openColorTemplateColorPreviewCommand;

	public ICommand CloseColorTemplateColorPreviewCommand => _closeColorTemplateColorPreviewCommand;

	public ICommand SaveColorTemplateGroupCommand => _saveColorTemplateGroupCommand;

	public ICommand CloseColorTemplateGroupEditorCommand => _closeColorTemplateGroupEditorCommand;

	public ICommand RunAppUpdateCommand => _runAppUpdateCommand;

	public WorkspaceTabViewModel? SelectedTab
	{
		get
		{
			return _selectedTab;
		}
		private set
		{
			if (_selectedTab != value)
			{
				if (_selectedTab != null)
				{
					_selectedTab.IsSelected = false;
					_selectedTab.SelectionContextChanged -= OnTabSelectionContextChanged;
					_selectedTab.StatusChanged -= OnTabStatusChanged;
					_selectedTab.PreviewRequested -= OnPreviewRequested;
				}
				_selectedTab = value;
				if (_selectedTab != null)
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
				OnPropertyChanged("SelectedTab");
				OnPropertyChanged("HasTabs");
				OnPropertyChanged("HasRootCards");
				OnPropertyChanged("HasBatchSelectedCards");
				ApplyAutoPublishStatusFilter();
				NotifyAutoPublishFilterPropertiesChanged();
				_invertSelectionCommand.RaiseCanExecuteChanged();
				_openCurrentFolderCommand.RaiseCanExecuteChanged();
				_selectAllBatchCardsCommand.RaiseCanExecuteChanged();
				_clearAllBatchCardsCommand.RaiseCanExecuteChanged();
				_generateProductSheetCommand.RaiseCanExecuteChanged();
				_runBatchAutoPublishCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public TemplateItemViewModel? SelectedTemplateItem
	{
		get
		{
			return _selectedTemplateItem;
		}
		private set
		{
			if (_selectedTemplateItem != value)
			{
				if (_selectedTemplateItem != null)
				{
					_selectedTemplateItem.IsSelected = false;
				}
				_selectedTemplateItem = value;
				if (_selectedTemplateItem != null)
				{
					_selectedTemplateItem.IsSelected = true;
					LoadTemplateEditor(_selectedTemplateItem);
				}
				OnPropertyChanged("SelectedTemplateItem");
				OnPropertyChanged("TemplateEditorDialogTitle");
				_deleteTemplateCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool HasTabs => WorkspaceTabs.Count > 0;

	public bool HasRootCards => SelectedTab?.HasRootCards ?? false;

	public bool IsReviewWorkspaceSelected => _selectedSection == "review-workspace";

	public bool IsImageGenerateSelected => _selectedSection == "image-generate";

	public bool IsTemplateManagerSelected => _selectedSection == "template-manager";

	public bool IsTemplateGenerateTabSelected
	{
		get
		{
			if (!(_selectedImageGenerateTab == "main-image-generate") && !(_selectedImageGenerateTab == "scene-image-generate"))
			{
				return _selectedImageGenerateTab == "compare-image-generate";
			}
			return true;
		}
	}

	public bool IsMainImageGenerateTabSelected => _selectedImageGenerateTab == "main-image-generate";

	public bool IsSpBatchTabSelected => _selectedImageGenerateTab == "sp-batch";

	public bool IsSkuOptimizeTabSelected => _selectedImageGenerateTab == "sku-optimize";

	public bool IsSceneImageGenerateTabSelected => _selectedImageGenerateTab == "scene-image-generate";

	public bool IsCompareImageGenerateTabSelected => _selectedImageGenerateTab == "compare-image-generate";

	public bool IsSceneGenerateTabSelected => IsSceneImageGenerateTabSelected;

	public bool IsCompareGenerateTabSelected => IsCompareImageGenerateTabSelected;

	public bool IsTemplateImageGenerateTabSelected => IsTemplateGenerateTabSelected;

	public string CurrentTemplateGenerateTitle => CurrentGenerationImageType switch
	{
		ImageTemplateType.SceneImage => "场景图生成",
		ImageTemplateType.CompareImage => "对比图生成",
		_ => "主图生成"
	};

	public ImageTemplateType CurrentImageTemplateType => CurrentGenerationImageType;

	public bool IsLayoutTemplateTabSelected => _selectedTemplateCategory == TemplateCategory.Layout;

	public bool IsSceneTemplateTabSelected => _selectedTemplateCategory == TemplateCategory.Scene;

	public bool IsSubjectTemplateTabSelected => _selectedTemplateCategory == TemplateCategory.Subject;

	public bool IsTitleTemplateTabSelected => _selectedTemplateCategory == TemplateCategory.Title;

	public bool IsColorTemplateTabSelected => _selectedTemplateCategory == TemplateCategory.Color;

	public double TemplateEditorDialogHeight => IsSubjectTemplateTabSelected ? 230 : (IsTitleTemplateTabSelected ? 310 : 760);

	public bool IsMainImageLayoutTypeSelected => _selectedLayoutImageType == ImageTemplateType.MainImage;

	public bool IsSceneImageLayoutTypeSelected => _selectedLayoutImageType == ImageTemplateType.SceneImage;

	public bool IsCompareImageLayoutTypeSelected => _selectedLayoutImageType == ImageTemplateType.CompareImage;

	public bool IsMainTitleTemplateTypeSelected => _selectedTitleTemplateType == MainTitleTemplateType;

	public bool IsSubTitleTemplateTypeSelected => _selectedTitleTemplateType == SubTitleTemplateType;

	public bool IsIconWordTemplateTypeSelected => _selectedTitleTemplateType == IconWordTemplateType;

	private string CurrentVisualElementTypeName => _selectedTitleTemplateType switch
	{
		SubTitleTemplateType => "小标题",
		IconWordTemplateType => "图标小词",
		_ => "大标题"
	};

	public int MainImageLayoutTemplateCount
	{
		get
		{
			return _mainImageLayoutTemplateCount;
		}
		private set
		{
			SetProperty(ref _mainImageLayoutTemplateCount, value, "MainImageLayoutTemplateCount");
		}
	}

	public int SceneImageLayoutTemplateCount
	{
		get
		{
			return _sceneImageLayoutTemplateCount;
		}
		private set
		{
			SetProperty(ref _sceneImageLayoutTemplateCount, value, "SceneImageLayoutTemplateCount");
		}
	}

	public int CompareImageLayoutTemplateCount
	{
		get
		{
			return _compareImageLayoutTemplateCount;
		}
		private set
		{
			SetProperty(ref _compareImageLayoutTemplateCount, value, "CompareImageLayoutTemplateCount");
		}
	}

	public int MainTitleTemplateCount
	{
		get
		{
			return _mainTitleTemplateCount;
		}
		private set
		{
			SetProperty(ref _mainTitleTemplateCount, value, "MainTitleTemplateCount");
		}
	}

	public int SubTitleTemplateCount
	{
		get
		{
			return _subTitleTemplateCount;
		}
		private set
		{
			SetProperty(ref _subTitleTemplateCount, value, "SubTitleTemplateCount");
		}
	}

	public int IconWordTemplateCount
	{
		get
		{
			return _iconWordTemplateCount;
		}
		private set
		{
			SetProperty(ref _iconWordTemplateCount, value, "IconWordTemplateCount");
		}
	}

	public int ColorTemplateGroupCount
	{
		get
		{
			return _colorTemplateGroupCount;
		}
		private set
		{
			SetProperty(ref _colorTemplateGroupCount, value, "ColorTemplateGroupCount");
		}
	}

	public string SelectedColorTemplateGroupName
	{
		get
		{
			return ColorTemplateGroups.FirstOrDefault(item => item.Id == _selectedColorTemplateGroupId)?.Name ?? "默认颜色组";
		}
	}

	public ImageTemplateType CurrentGenerationImageType
	{
		get
		{
			string selectedImageGenerateTab = _selectedImageGenerateTab;
			if (!(selectedImageGenerateTab == "scene-image-generate"))
			{
				if (selectedImageGenerateTab == "compare-image-generate")
				{
					return ImageTemplateType.CompareImage;
				}
				return ImageTemplateType.MainImage;
			}
			return ImageTemplateType.SceneImage;
		}
	}

	public string CurrentGenerationTabTitle => CurrentGenerationImageType switch
	{
		ImageTemplateType.SceneImage => "场景图生成", 
		ImageTemplateType.CompareImage => "对比图生成", 
		_ => "主图生成", 
	};

	public bool ShouldShowGenerationImageActionButtons => CurrentGenerationImageType == ImageTemplateType.MainImage;

	public string SelectedLayoutImageTypeText => _selectedLayoutImageType switch
	{
		ImageTemplateType.SceneImage => "场景图", 
		ImageTemplateType.CompareImage => "对比图", 
		_ => "主图", 
	};

	public bool HasManagedTemplateItems => IsColorTemplateTabSelected ? ColorTemplateGroups.Count > 0 : ManagedTemplateItems.Count > 0;

	public bool IsTemplateContentPanelVisible => IsColorTemplateTabSelected || HasManagedTemplateItems;

	public int SelectedLayoutExportCount => IsLayoutTemplateTabSelected ? ManagedTemplateItems.Count((TemplateItemViewModel item) => item.IsSelectedForExport) : 0;

	public string LayoutExportSelectionText => SelectedLayoutExportCount > 0 ? $"已选 {SelectedLayoutExportCount} 条，导出选中" : "未选择时导出当前类型全部";

	public string CurrentTemplateImportButtonText => _selectedTemplateCategory switch
	{
		TemplateCategory.Scene => "导入场景",
		TemplateCategory.Subject => "导入主体",
		TemplateCategory.Title => "导入元素",
		TemplateCategory.Color => "导入颜色",
		_ => "导入布局"
	};

	public string CurrentTemplateExportButtonText => _selectedTemplateCategory switch
	{
		TemplateCategory.Scene => "导出场景",
		TemplateCategory.Subject => "导出主体",
		TemplateCategory.Title => "导出元素",
		TemplateCategory.Color => "导出颜色",
		_ => "导出布局"
	};

	public string CurrentNewTemplateButtonText => IsColorTemplateTabSelected ? "新增颜色组" : (IsTitleTemplateTabSelected ? "添加元素" : "新增模板");

	public bool IsTemplateImportExportVisible => true;

	public bool IsTemplateEditorPreviewVisible
	{
		get
		{
			if (IsLayoutTemplateTabSelected && !string.IsNullOrWhiteSpace(TemplateEditorPreviewImagePath))
			{
				return File.Exists(TemplateEditorPreviewImagePath);
			}
			return false;
		}
	}

	public bool IsTemplateEditorSubjectVisible => IsSceneTemplateTabSelected;

	public bool IsTemplateEditorContentVisible => !IsSubjectTemplateTabSelected;

	public bool IsTemplateEditorContentLabelVisible => !IsTitleTemplateTabSelected;

	public bool IsTemplateEditorNameVisible => !IsTitleTemplateTabSelected;

	public bool IsTemplateEditorEnabledVisible => !IsTitleTemplateTabSelected;

	public bool HasTemplateSubjectTags => TemplateSubjectTags.Any((TemplateSubjectTagViewModel tag) => tag.IsTag);

	public bool HasTemplateSubjectOptions => TemplateSubjectOptions.Count > 0;

	public string TemplateManagerTitle => _selectedTemplateCategory switch
	{
		TemplateCategory.Scene => "场景模板", 
		TemplateCategory.Subject => "主体模板", 
		TemplateCategory.Title => "视觉元素", 
		TemplateCategory.Color => "颜色模板",
		_ => "布局模板", 
	};

	public string TemplateManagerDescription => _selectedTemplateCategory switch
	{
		TemplateCategory.Scene => "维护可随机使用的场景描述文本。", 
		TemplateCategory.Subject => "维护可复用的主体描述文本。", 
		TemplateCategory.Title => "维护可复用的大标题、小标题和图标小词。", 
		TemplateCategory.Color => "维护可用于模板生图和 SKU 裂变的颜色组。",
		_ => "维护主图提示词布局，并为每个布局配置一张预览图。", 
	};

	public string TemplateEditorContentLabel => _selectedTemplateCategory switch
	{
		TemplateCategory.Scene => "场景内容", 
		TemplateCategory.Subject => "主体内容", 
		TemplateCategory.Title => "元素内容", 
		_ => "布局模板内容", 
	};

	public string TemplateEmptyText => "暂无" + TemplateManagerTitle + "，点击“" + CurrentNewTemplateButtonText + "”开始维护。";

	public string TemplateEditorDialogTitle
	{
		get
		{
			if (IsTitleTemplateTabSelected)
			{
				return (SelectedTemplateItem != null ? "编辑" : "新增") + CurrentVisualElementTypeName;
			}
			if (SelectedTemplateItem != null)
			{
				return "编辑" + TemplateManagerTitle;
			}
			return "新增" + TemplateManagerTitle;
		}
	}

	public bool IsLoadingVisible
	{
		get
		{
			if (IsBusy && !IsTemplateGenerating)
			{
				return !IsSpBatchRunning;
			}
			return false;
		}
	}

	public bool HasPreviewImage => PreviewImageSource != null;

	public bool HasBatchSelectedCards
	{
		get
		{
			WorkspaceTabViewModel? selectedTab = SelectedTab;
			if (selectedTab == null)
			{
				return false;
			}
			return selectedTab.GetBatchSelectedCards().Count > 0;
		}
	}

	public bool CanRunBatchAutoPublish
	{
		get
		{
			if (HasBatchSelectedCards && !IsBusy)
			{
				return !_isAutoPublishRunning;
			}
			return false;
		}
	}

	public AutoPublishStatusFilter SelectedAutoPublishStatusFilter
	{
		get
		{
			return _selectedAutoPublishStatusFilter;
		}
		private set
		{
			if (SetProperty(ref _selectedAutoPublishStatusFilter, value, "SelectedAutoPublishStatusFilter"))
			{
				OnPropertyChanged("IsAllStatusFilterSelected");
				OnPropertyChanged("IsNotPublishedStatusFilterSelected");
				OnPropertyChanged("IsPublishingStatusFilterSelected");
				OnPropertyChanged("IsSuccessStatusFilterSelected");
				OnPropertyChanged("IsFailedStatusFilterSelected");
			}
		}
	}

	public string AllStatusFilterText => $"全部 {CountAutoPublishStatusFilter(AutoPublishStatusFilter.All)}";

	public string NotPublishedStatusFilterText => $"未上架 {CountAutoPublishStatusFilter(AutoPublishStatusFilter.NotPublished)}";

	public string PublishingStatusFilterText => $"上架中 {CountAutoPublishStatusFilter(AutoPublishStatusFilter.Publishing)}";

	public string SuccessStatusFilterText => $"上架成功 {CountAutoPublishStatusFilter(AutoPublishStatusFilter.Success)}";

	public string FailedStatusFilterText => $"上架失败 {CountAutoPublishStatusFilter(AutoPublishStatusFilter.Failed)}";

	public bool IsAutoPublishFilterEmpty
	{
		get
		{
			if (SelectedTab != null && SelectedTab.HasRootCards)
			{
				return SelectedTab.FilteredRootCards.Count == 0;
			}
			return false;
		}
	}

	public string AutoPublishFilterEmptyText => SelectedAutoPublishStatusFilter switch
	{
		AutoPublishStatusFilter.NotPublished => "当前没有未上架的卡片", 
		AutoPublishStatusFilter.Publishing => "当前没有上架中的卡片", 
		AutoPublishStatusFilter.Success => "当前没有上架成功的卡片", 
		AutoPublishStatusFilter.Failed => "当前没有上架失败的卡片", 
		_ => "当前没有可显示的卡片", 
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

	public bool HasAnyGenerationResultCards
	{
		get
		{
			if (!HasGenerationPromptCards)
			{
				return HasGeneratedImageResultCards;
			}
			return true;
		}
	}

	public bool HasSelectedGeneratedImages => GeneratedImageResultCards.Any((GeneratedImageResultCardViewModel card) => card.IsSelected);

	public bool CanGenerateSkuFromTemplate
	{
		get
		{
			if (!IsGenerationPromptsOnly)
			{
				return HasSelectedGeneratedImages;
			}
			return false;
		}
	}

	public bool IsCurrentTemplateGenerationRunning
	{
		get
		{
			if (IsTemplateGenerating)
			{
				return string.Equals(_activeTemplateGenerationTab, _selectedImageGenerateTab, StringComparison.Ordinal);
			}
			return false;
		}
	}

	public bool IsCurrentTemplateGenerationStopping
	{
		get
		{
			if (IsTemplateGenerationStopping)
			{
				return IsCurrentTemplateGenerationRunning;
			}
			return false;
		}
	}

	public string TemplateGenerationButtonText
	{
		get
		{
			if (!IsTemplateGenerationStopping || !IsCurrentTemplateGenerationRunning)
			{
				if (!IsCurrentTemplateGenerationRunning)
				{
					return "开始执行";
				}
				return "停止执行";
			}
			return "正在停止...";
		}
	}

	public string SpBatchButtonText
	{
		get
		{
			if (!IsSpBatchStopping || !IsSpBatchMasterRunning)
			{
				if (!IsSpBatchMasterRunning)
				{
					return "生成SKU母图";
				}
				return "停止执行";
			}
			return "正在停止...";
		}
	}

	public string SpBatchColorButtonText
	{
		get
		{
			if (!IsSpBatchStopping || !IsSpBatchColorRunning)
			{
				if (!IsSpBatchColorRunning)
				{
					return "生成其他颜色SKU图";
				}
				return "停止执行";
			}
			return "正在停止...";
		}
	}

	public bool IsSpBatchColorSelectionOpen
	{
		get => _isSpBatchColorSelectionOpen;
		private set
		{
			if (SetProperty(ref _isSpBatchColorSelectionOpen, value, "IsSpBatchColorSelectionOpen"))
			{
				_closeSpBatchColorSelectionCommand.RaiseCanExecuteChanged();
				_saveSpBatchColorSelectionCommand.RaiseCanExecuteChanged();
				_selectAllSpBatchColorSelectionCommand.RaiseCanExecuteChanged();
				_clearAllSpBatchColorSelectionCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string SpBatchColorSelectionTitle => SelectedSpBatchColorTemplateGroupForGeneration == null
		? "选择颜色"
		: $"选择颜色 - {SelectedSpBatchColorTemplateGroupForGeneration.Name}";

	public string SpBatchColorSelectionSummaryText
	{
		get
		{
			ColorTemplateGroupViewModel? group = SelectedSpBatchColorTemplateGroupForGeneration;
			if (group == null)
			{
				return "已选颜色：未选择颜色组";
			}
			IReadOnlyList<string> names = GetVisibleSpBatchColorSelectionNames();
			if (names.Count == 0)
			{
				return "已选颜色：未选择颜色";
			}
			if (names.Count >= group.Colors.Count)
			{
				return "已选颜色：全部颜色";
			}
			string summary = string.Join("、", names.Take(4));
			if (names.Count > 4)
			{
				summary += " ...";
			}
			return "已选颜色：" + summary;
		}
	}

	public bool HasSpBatchResultCards => SpBatchResultCards.Count > 0;

	public bool HasSpBatchMasterImageCards => SpBatchMasterImageCards.Count > 0;

	public bool HasSpBatchImageResultCards => SpBatchImageResultCards.Count > 0;

	public bool HasAnySpBatchResultCards
	{
		get
		{
			if (!HasSpBatchResultCards && !HasSpBatchMasterImageCards)
			{
				return HasSpBatchImageResultCards;
			}
			return true;
		}
	}

	public bool HasSpBatchSourceImageCards => SpBatchSourceImageCards.Count > 0;

	public bool ShouldShowSpBatchDetailCards
	{
		get
		{
			if (HasSpBatchResultCards && !HasSpBatchMasterImageCards)
			{
				return !HasSpBatchImageResultCards;
			}
			return false;
		}
	}

	public bool IsSpBatchStagingDropTarget
	{
		get
		{
			return _isSpBatchStagingDropTarget;
		}
		private set
		{
			SetProperty(ref _isSpBatchStagingDropTarget, value, "IsSpBatchStagingDropTarget");
		}
	}

	public bool IsSpBatchMasterDropTarget
	{
		get
		{
			return _isSpBatchMasterDropTarget;
		}
		private set
		{
			SetProperty(ref _isSpBatchMasterDropTarget, value, "IsSpBatchMasterDropTarget");
		}
	}

	public bool IsSkuOptimizeRunning
	{
		get
		{
			return _isSkuOptimizeRunning;
		}
		private set
		{
			if (SetProperty(ref _isSkuOptimizeRunning, value, "IsSkuOptimizeRunning"))
			{
				OnPropertyChanged("SkuOptimizeButtonText");
				_chooseSkuOptimizeOutputFolderCommand.RaiseCanExecuteChanged();
				_openSkuOptimizeOutputFolderCommand.RaiseCanExecuteChanged();
				_runSkuOptimizeCommand.RaiseCanExecuteChanged();
				_runSkuOptimizeCommand.RaiseCanExecuteChanged();
				_stopSkuOptimizeCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsSkuOptimizeStopping
	{
		get
		{
			return _isSkuOptimizeStopping;
		}
		private set
		{
			if (SetProperty(ref _isSkuOptimizeStopping, value, "IsSkuOptimizeStopping"))
			{
				OnPropertyChanged("SkuOptimizeButtonText");
				_chooseSkuOptimizeOutputFolderCommand.RaiseCanExecuteChanged();
				_openSkuOptimizeOutputFolderCommand.RaiseCanExecuteChanged();
				_runSkuOptimizeCommand.RaiseCanExecuteChanged();
				_stopSkuOptimizeCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string SkuOptimizeButtonText
	{
		get
		{
			if (!IsSkuOptimizeStopping)
			{
				if (!IsSkuOptimizeRunning)
				{
					return "开始执行";
				}
				return "停止执行";
			}
			return "正在停止...";
		}
	}

	public string SkuOptimizeOutputDirectory
	{
		get
		{
			return _skuOptimizeOutputDirectory;
		}
		set
		{
			if (SetProperty(ref _skuOptimizeOutputDirectory, value, "SkuOptimizeOutputDirectory"))
			{
				PersistUserPathSettings();
				_openSkuOptimizeOutputFolderCommand.RaiseCanExecuteChanged();
				_runSkuOptimizeCommand.RaiseCanExecuteChanged();
				SkuOptimizeResultRootText = (string.IsNullOrWhiteSpace(value) ? "结果目录：未设置" : ("结果目录：" + value));
			}
		}
	}

	public string SkuOptimizeConcurrencyText
	{
		get
		{
			return _skuOptimizeConcurrencyText;
		}
		set
		{
			if (SetProperty(ref _skuOptimizeConcurrencyText, value, "SkuOptimizeConcurrencyText"))
			{
				_runSkuOptimizeCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string SkuOptimizeLengthMultiplierText
	{
		get
		{
			return _skuOptimizeLengthMultiplierText;
		}
		set
		{
			if (SetProperty(ref _skuOptimizeLengthMultiplierText, value, "SkuOptimizeLengthMultiplierText"))
			{
				_runSkuOptimizeCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsSkuOptimizeLengthGrow
	{
		get
		{
			return _isSkuOptimizeLengthGrow;
		}
		set
		{
			if (!value && !_isSkuOptimizeLengthShrink)
			{
				value = true;
			}
			if (SetProperty(ref _isSkuOptimizeLengthGrow, value, "IsSkuOptimizeLengthGrow"))
			{
				if (value && _isSkuOptimizeLengthShrink)
				{
					_isSkuOptimizeLengthShrink = false;
					OnPropertyChanged("IsSkuOptimizeLengthShrink");
				}
				_runSkuOptimizeCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsSkuOptimizeLengthShrink
	{
		get
		{
			return _isSkuOptimizeLengthShrink;
		}
		set
		{
			if (!value && !_isSkuOptimizeLengthGrow)
			{
				value = true;
			}
			if (SetProperty(ref _isSkuOptimizeLengthShrink, value, "IsSkuOptimizeLengthShrink"))
			{
				if (value && _isSkuOptimizeLengthGrow)
				{
					_isSkuOptimizeLengthGrow = false;
					OnPropertyChanged("IsSkuOptimizeLengthGrow");
				}
				_runSkuOptimizeCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string SkuOptimizeDiameterMultiplierText
	{
		get
		{
			return _skuOptimizeDiameterMultiplierText;
		}
		set
		{
			if (SetProperty(ref _skuOptimizeDiameterMultiplierText, value, "SkuOptimizeDiameterMultiplierText"))
			{
				_runSkuOptimizeCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string SkuOptimizeStatusText
	{
		get
		{
			return _skuOptimizeStatusText;
		}
		private set
		{
			SetProperty(ref _skuOptimizeStatusText, value, "SkuOptimizeStatusText");
		}
	}

	public string SkuOptimizeSummaryText
	{
		get
		{
			return _skuOptimizeSummaryText;
		}
		private set
		{
			SetProperty(ref _skuOptimizeSummaryText, value, "SkuOptimizeSummaryText");
		}
	}

	public string SkuOptimizeResultRootText
	{
		get
		{
			return _skuOptimizeResultRootText;
		}
		private set
		{
			SetProperty(ref _skuOptimizeResultRootText, value, "SkuOptimizeResultRootText");
		}
	}

	public string SkuOptimizeResultStatsText
	{
		get
		{
			return _skuOptimizeResultStatsText;
		}
		private set
		{
			SetProperty(ref _skuOptimizeResultStatsText, value, "SkuOptimizeResultStatsText");
		}
	}

	public string SkuOptimizeStatsTotalText => $"总任务：{_skuOptimizeStatsTotalCount}";

	public string SkuOptimizeStatsSuccessText => $"成功：{_skuOptimizeStatsSuccessCount}";

	public string SkuOptimizeStatsSkippedText => $"跳过：{_skuOptimizeStatsSkippedCount}";

	public string SkuOptimizeStatsFailedText => $"失败：{_skuOptimizeStatsFailedCount}";

	public Brush SkuOptimizeStatsSuccessBrush => new SolidColorBrush(Color.FromRgb(103, 194, 58));

	public Brush SkuOptimizeStatsFailedBrush => new SolidColorBrush(Color.FromRgb(245, 108, 108));

	public bool HasSkuOptimizeResultCards => SkuOptimizeResultCards.Count > 0;

	public bool HasSkuOptimizeImageResultCards => SkuOptimizeImageResultCards.Count > 0;

	public bool HasAnySkuOptimizeResultCards
	{
		get
		{
			if (!HasSkuOptimizeResultCards)
			{
				return HasSkuOptimizeImageResultCards;
			}
			return true;
		}
	}

	public bool HasSkuOptimizeSourceImageCards => SkuOptimizeSourceImageCards.Count > 0;

	public bool ShouldShowSkuOptimizeDetailCards
	{
		get
		{
			if (HasSkuOptimizeResultCards)
			{
				return !HasSkuOptimizeImageResultCards;
			}
			return false;
		}
	}

	public bool IsSkuOptimizeStagingDropTarget
	{
		get
		{
			return _isSkuOptimizeStagingDropTarget;
		}
		private set
		{
			SetProperty(ref _isSkuOptimizeStagingDropTarget, value, "IsSkuOptimizeStagingDropTarget");
		}
	}

	public bool IsCardSizeDialogOpen
	{
		get
		{
			return _isCardSizeDialogOpen;
		}
		private set
		{
			SetProperty(ref _isCardSizeDialogOpen, value, "IsCardSizeDialogOpen");
		}
	}

	public string CardSizeDialogTitle
	{
		get
		{
			if (_cardSizeDialogCard != null)
			{
				return "录入尺寸：" + _cardSizeDialogCard.DisplayName;
			}
			return "录入尺寸";
		}
	}

	public string CardSizeInputText
	{
		get
		{
			return _cardSizeInputText;
		}
		set
		{
			if (SetProperty(ref _cardSizeInputText, value, "CardSizeInputText"))
			{
				OnPropertyChanged("CardSizePreviewText");
				_saveCardSizeInfoCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string CardSizePreviewText
	{
		get
		{
			IReadOnlyList<string> readOnlyList = BuildDisplaySizes(CardSizeInputText);
			if (readOnlyList.Count != 0)
			{
				return string.Join(Environment.NewLine, readOnlyList);
			}
			return "输入示例：50*225 或 50cm*225cm。保存后会自动补全 inch。";
		}
	}

	public string CardSizePreviewMeta
	{
		get
		{
			return _cardSizePreviewMeta;
		}
		private set
		{
			SetProperty(ref _cardSizePreviewMeta, value, "CardSizePreviewMeta");
		}
	}

	public ImageSource? CardSizePreviewImageSource
	{
		get
		{
			return _cardSizePreviewImageSource;
		}
		private set
		{
			SetProperty(ref _cardSizePreviewImageSource, value, "CardSizePreviewImageSource");
		}
	}

	public bool IsTemplateEditorDialogOpen
	{
		get
		{
			return _isTemplateEditorDialogOpen;
		}
		private set
		{
			SetProperty(ref _isTemplateEditorDialogOpen, value, "IsTemplateEditorDialogOpen");
		}
	}

	public bool IsColorTemplateGroupEditorOpen
	{
		get
		{
			return _isColorTemplateGroupEditorOpen;
		}
		private set
		{
			if (SetProperty(ref _isColorTemplateGroupEditorOpen, value, "IsColorTemplateGroupEditorOpen"))
			{
				_saveColorTemplateGroupCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string ColorTemplateGroupEditorTitle => _selectedColorTemplateGroup == null ? "新增颜色组" : "编辑颜色组";

	public string ColorTemplateGroupEditorName
	{
		get
		{
			return _colorTemplateGroupEditorName;
		}
		set
		{
			if (SetProperty(ref _colorTemplateGroupEditorName, value, "ColorTemplateGroupEditorName"))
			{
				_saveColorTemplateGroupCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string TemplateEditorName
	{
		get
		{
			return _templateEditorName;
		}
		set
		{
			if (SetProperty(ref _templateEditorName, value, "TemplateEditorName"))
			{
				_saveTemplateCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string TemplateEditorContent
	{
		get
		{
			return _templateEditorContent;
		}
		set
		{
			if (SetProperty(ref _templateEditorContent, value, "TemplateEditorContent"))
			{
				_saveTemplateCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsColorTemplateColorPreviewOpen
	{
		get
		{
			return _isColorTemplateColorPreviewOpen;
		}
		private set
		{
			SetProperty(ref _isColorTemplateColorPreviewOpen, value, "IsColorTemplateColorPreviewOpen");
		}
	}

	public ColorTemplateColorViewModel? SelectedColorTemplateColorPreview
	{
		get
		{
			return _selectedColorTemplateColorPreview;
		}
		private set
		{
			if (SetProperty(ref _selectedColorTemplateColorPreview, value, "SelectedColorTemplateColorPreview"))
			{
				OnPropertyChanged("ColorTemplateColorPreviewTitle");
			}
		}
	}

	public string ColorTemplateColorPreviewTitle => "颜色预览";

	public string TemplateEditorPreviewImagePath
	{
		get
		{
			return _templateEditorPreviewImagePath;
		}
		set
		{
			if (SetProperty(ref _templateEditorPreviewImagePath, value, "TemplateEditorPreviewImagePath"))
			{
				OnPropertyChanged("IsTemplateEditorPreviewVisible");
				_saveTemplateCommand.RaiseCanExecuteChanged();
				_openTemplateEditorPreviewCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool TemplateEditorIsEnabled
	{
		get
		{
			return _templateEditorIsEnabled;
		}
		set
		{
			SetProperty(ref _templateEditorIsEnabled, value, "TemplateEditorIsEnabled");
		}
	}

	public bool IsTemplateSubjectPickerOpen
	{
		get
		{
			return _isTemplateSubjectPickerOpen;
		}
		private set
		{
			SetProperty(ref _isTemplateSubjectPickerOpen, value, "IsTemplateSubjectPickerOpen");
		}
	}

	public string NewTemplateSubjectText
	{
		get
		{
			return _newTemplateSubjectText;
		}
		set
		{
			if (SetProperty(ref _newTemplateSubjectText, value, "NewTemplateSubjectText"))
			{
				_commitTemplateSubjectTagCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsBusy
	{
		get
		{
			return _isBusy;
		}
		private set
		{
			if (SetProperty(ref _isBusy, value, "IsBusy"))
			{
				OnPropertyChanged("IsLoadingVisible");
				OnPropertyChanged("CanRunBatchAutoPublish");
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
				_chooseSkuOptimizeOutputFolderCommand.RaiseCanExecuteChanged();
				_openSkuOptimizeOutputFolderCommand.RaiseCanExecuteChanged();
				_runSkuOptimizeCommand.RaiseCanExecuteChanged();
				_stopSkuOptimizeCommand.RaiseCanExecuteChanged();
				_runBatchAutoPublishCommand.RaiseCanExecuteChanged();
				_saveTemplateCommand.RaiseCanExecuteChanged();
				_deleteTemplateCommand.RaiseCanExecuteChanged();
				_chooseTemplatePreviewCommand.RaiseCanExecuteChanged();
				NotifyAutoPublishStateChanged();
			}
		}
	}

	public string AppVersionText
	{
		get
		{
			return _appVersionText;
		}
		private set
		{
			SetProperty(ref _appVersionText, value, "AppVersionText");
		}
	}

	public bool IsAppUpdateAvailable
	{
		get
		{
			return _isAppUpdateAvailable;
		}
		private set
		{
			if (SetProperty(ref _isAppUpdateAvailable, value, "IsAppUpdateAvailable"))
			{
				_runAppUpdateCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsAppUpdateDownloading
	{
		get
		{
			return _isAppUpdateDownloading;
		}
		private set
		{
			if (SetProperty(ref _isAppUpdateDownloading, value, "IsAppUpdateDownloading"))
			{
				_runAppUpdateCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsScanProgressIndeterminate
	{
		get
		{
			return _isScanProgressIndeterminate;
		}
		private set
		{
			SetProperty(ref _isScanProgressIndeterminate, value, "IsScanProgressIndeterminate");
		}
	}

	public double ScanProgressValue
	{
		get
		{
			return _scanProgressValue;
		}
		private set
		{
			SetProperty(ref _scanProgressValue, value, "ScanProgressValue");
		}
	}

	public string ScanProgressText
	{
		get
		{
			return _scanProgressText;
		}
		private set
		{
			SetProperty(ref _scanProgressText, value, "ScanProgressText");
		}
	}

	public string StatusMessage
	{
		get
		{
			return _statusMessage;
		}
		private set
		{
			SetProperty(ref _statusMessage, value, "StatusMessage");
		}
	}

	public string LoadedFolderText
	{
		get
		{
			return _loadedFolderText;
		}
		private set
		{
			SetProperty(ref _loadedFolderText, value, "LoadedFolderText");
		}
	}

	public string SelectionSummaryText
	{
		get
		{
			return _selectionSummaryText;
		}
		private set
		{
			SetProperty(ref _selectionSummaryText, value, "SelectionSummaryText");
		}
	}

	public string BackupFolderText
	{
		get
		{
			return _backupFolderText;
		}
		private set
		{
			SetProperty(ref _backupFolderText, value, "BackupFolderText");
		}
	}

	public string LoadingTitle
	{
		get
		{
			return _loadingTitle;
		}
		private set
		{
			SetProperty(ref _loadingTitle, value, "LoadingTitle");
		}
	}

	public string LoadingDetail
	{
		get
		{
			return _loadingDetail;
		}
		private set
		{
			SetProperty(ref _loadingDetail, value, "LoadingDetail");
		}
	}

	public string PreviewMeta
	{
		get
		{
			return _previewMeta;
		}
		private set
		{
			SetProperty(ref _previewMeta, value, "PreviewMeta");
		}
	}

	public ImageSource? PreviewImageSource
	{
		get
		{
			return _previewImageSource;
		}
		private set
		{
			if (SetProperty(ref _previewImageSource, value, "PreviewImageSource"))
			{
				OnPropertyChanged("HasPreviewImage");
			}
		}
	}

	public string BackupFolder
	{
		get
		{
			return _backupFolder;
		}
		set
		{
			if (!SetProperty(ref _backupFolder, value, "BackupFolder"))
			{
				return;
			}
			PersistUserPathSettings();
			BackupFolderText = (string.IsNullOrWhiteSpace(value) ? "备份目录：未设置" : ("备份目录：" + value));
			foreach (WorkspaceTabViewModel workspaceTab in WorkspaceTabs)
			{
				workspaceTab.SetBackupFolder(value);
			}
		}
	}

	public bool TitleChineseOnly
	{
		get
		{
			return _titleChineseOnly;
		}
		set
		{
			if (SetProperty(ref _titleChineseOnly, value, "TitleChineseOnly"))
			{
				PersistUserPathSettings();
			}
		}
	}

	public bool IsAutoPublishSettingsDialogOpen
	{
		get
		{
			return _isAutoPublishSettingsDialogOpen;
		}
		set
		{
			SetProperty(ref _isAutoPublishSettingsDialogOpen, value, "IsAutoPublishSettingsDialogOpen");
		}
	}

	public bool IsTemplateGenerating
	{
		get
		{
			return _isTemplateGenerating;
		}
		private set
		{
			if (SetProperty(ref _isTemplateGenerating, value, "IsTemplateGenerating"))
			{
				OnPropertyChanged("TemplateGenerationButtonText");
				OnPropertyChanged("IsCurrentTemplateGenerationRunning");
				OnPropertyChanged("IsCurrentTemplateGenerationStopping");
				_chooseTemplateLibraryCommand.RaiseCanExecuteChanged();
				_openGenerationTemplatePickerCommand.RaiseCanExecuteChanged();
				_chooseGenerationOutputFolderCommand.RaiseCanExecuteChanged();
				_runTemplateGenerationCommand.RaiseCanExecuteChanged();
				_stopTemplateGenerationCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsTemplateGenerationStopping
	{
		get
		{
			return _isTemplateGenerationStopping;
		}
		private set
		{
			if (SetProperty(ref _isTemplateGenerationStopping, value, "IsTemplateGenerationStopping"))
			{
				OnPropertyChanged("TemplateGenerationButtonText");
				OnPropertyChanged("IsCurrentTemplateGenerationRunning");
				OnPropertyChanged("IsCurrentTemplateGenerationStopping");
				_chooseTemplateLibraryCommand.RaiseCanExecuteChanged();
				_openGenerationTemplatePickerCommand.RaiseCanExecuteChanged();
				_chooseGenerationOutputFolderCommand.RaiseCanExecuteChanged();
				_runTemplateGenerationCommand.RaiseCanExecuteChanged();
				_stopTemplateGenerationCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string GenerationPageDescription
	{
		get
		{
			return _generationPageDescription;
		}
		private set
		{
			SetProperty(ref _generationPageDescription, value, "GenerationPageDescription");
		}
	}

	public string TemplateLibraryPath
	{
		get
		{
			return _templateLibraryPath;
		}
		set
		{
			if (SetProperty(ref _templateLibraryPath, value, "TemplateLibraryPath"))
			{
				PersistUserPathSettings();
				OnPropertyChanged("CanOpenTemplateLibraryFile");
				_openTemplateLibraryFileCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsGenerationTemplatePickerOpen
	{
		get
		{
			return _isGenerationTemplatePickerOpen;
		}
		private set
		{
			SetProperty(ref _isGenerationTemplatePickerOpen, value, "IsGenerationTemplatePickerOpen");
		}
	}

	public string GenerationTemplateSelectionText
	{
		get
		{
			int count = GenerationTemplateOptions.Count;
			if (count == 0)
			{
				return "全部模板";
			}
			int num = GenerationTemplateOptions.Count((GenerationTemplateOptionViewModel item) => item.IsSelected);
			if (num != 0 && num != count)
			{
				return $"已选 {num} 个模板";
			}
			return "全部模板";
		}
	}

	public string GenerationOutputDirectory
	{
		get
		{
			return _generationOutputDirectory;
		}
		set
		{
			if (!SetProperty(ref _generationOutputDirectory, value, "GenerationOutputDirectory"))
			{
				return;
			}
			PersistUserPathSettings();
			GenerationResultOutputText = (string.IsNullOrWhiteSpace(value) ? "输出目录：未设置" : ("输出目录：" + value));
			string[] array = new string[3] { "main-image-generate", "scene-image-generate", "compare-image-generate" };
			foreach (string key in array)
			{
				if (_generationTabStates.TryGetValue(key, out GenerationTabState value2) && value2.PromptCards.Count == 0 && value2.ImageCards.Count == 0 && value2.StatusText == "待命")
				{
					_generationTabStates[key] = value2 with
					{
						ResultOutputText = (string.IsNullOrWhiteSpace(value) ? "输出目录：未设置" : ("输出目录：" + value))
					};
				}
			}
			OnPropertyChanged("CanOpenGenerationOutputFolder");
			_openGenerationOutputFolderCommand.RaiseCanExecuteChanged();
		}
	}

	public string GenerationCountText
	{
		get
		{
			return _generationCountText;
		}
		set
		{
			SetProperty(ref _generationCountText, value, "GenerationCountText");
		}
	}

	public string GenerationConcurrencyText
	{
		get
		{
			return _generationConcurrencyText;
		}
		set
		{
			SetProperty(ref _generationConcurrencyText, value, "GenerationConcurrencyText");
		}
	}

	public bool IsGenerationUniqueScene
	{
		get
		{
			return _isGenerationUniqueScene;
		}
		set
		{
			if (SetProperty(ref _isGenerationUniqueScene, value, "IsGenerationUniqueScene") && value && _isGenerationPromptsOnly)
			{
				_isGenerationPromptsOnly = false;
				OnPropertyChanged("IsGenerationPromptsOnly");
			}
		}
	}

	public bool IsGenerationPromptsOnly
	{
		get
		{
			return _isGenerationPromptsOnly;
		}
		set
		{
			if (SetProperty(ref _isGenerationPromptsOnly, value, "IsGenerationPromptsOnly"))
			{
				if (value && _isGenerationUniqueScene)
				{
					_isGenerationUniqueScene = false;
					OnPropertyChanged("IsGenerationUniqueScene");
				}
				OnPropertyChanged("CanGenerateSkuFromTemplate");
				_sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsImageGenerationKeyDialogOpen
	{
		get
		{
			return _isImageGenerationKeyDialogOpen;
		}
		private set
		{
			SetProperty(ref _isImageGenerationKeyDialogOpen, value, "IsImageGenerationKeyDialogOpen");
		}
	}

	public string ImageGenerationKeyText
	{
		get
		{
			return _imageGenerationKeyText;
		}
		set
		{
			if (SetProperty(ref _imageGenerationKeyText, value, "ImageGenerationKeyText"))
			{
				_saveImageGenerationKeyCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string SelectedImageGenerationProvider
	{
		get
		{
			return _selectedImageGenerationProvider;
		}
		private set
		{
			string normalized = NormalizeImageGenerationProvider(value);
			if (SetProperty(ref _selectedImageGenerationProvider, normalized, "SelectedImageGenerationProvider"))
			{
				OnPropertyChanged("IsImage2GenerationProviderSelected");
				OnPropertyChanged("ImageGenerationProviderText");
			}
		}
	}

	public bool IsImage2GenerationProviderSelected => string.Equals(SelectedImageGenerationProvider, Image2GenerationProvider, StringComparison.OrdinalIgnoreCase);

	public string ImageGenerationProviderText => "image2";

	public string ImageGenerationKeyPathText => "image2 保存位置：" + GetUserImageGenerationKeyPath();

	public string GenerationStatusText
	{
		get
		{
			return _generationStatusText;
		}
		private set
		{
			SetProperty(ref _generationStatusText, value, "GenerationStatusText");
		}
	}

	public string GenerationResultModeText
	{
		get
		{
			return _generationResultModeText;
		}
		private set
		{
			SetProperty(ref _generationResultModeText, value, "GenerationResultModeText");
		}
	}

	public string GenerationResultOutputText
	{
		get
		{
			return _generationResultOutputText;
		}
		private set
		{
			SetProperty(ref _generationResultOutputText, value, "GenerationResultOutputText");
		}
	}

	public bool IsSpBatchRunning
	{
		get
		{
			return _isSpBatchRunning;
		}
		private set
		{
			if (SetProperty(ref _isSpBatchRunning, value, "IsSpBatchRunning"))
			{
				OnPropertyChanged("SpBatchButtonText");
				OnPropertyChanged("SpBatchColorButtonText");
				OnPropertyChanged("IsSpBatchMasterRunning");
				OnPropertyChanged("IsSpBatchColorRunning");
				_chooseSpBatchInputFolderCommand.RaiseCanExecuteChanged();
				_chooseSpBatchOutputFolderCommand.RaiseCanExecuteChanged();
				_runSpBatchCommand.RaiseCanExecuteChanged();
				_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
				_stopSpBatchCommand.RaiseCanExecuteChanged();
				_refreshSkuResultsCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsSpBatchStopping
	{
		get
		{
			return _isSpBatchStopping;
		}
		private set
		{
			if (SetProperty(ref _isSpBatchStopping, value, "IsSpBatchStopping"))
			{
				OnPropertyChanged("SpBatchButtonText");
				OnPropertyChanged("SpBatchColorButtonText");
				OnPropertyChanged("IsSpBatchMasterRunning");
				OnPropertyChanged("IsSpBatchColorRunning");
				_chooseSpBatchInputFolderCommand.RaiseCanExecuteChanged();
				_chooseSpBatchOutputFolderCommand.RaiseCanExecuteChanged();
				_runSpBatchCommand.RaiseCanExecuteChanged();
				_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
				_stopSpBatchCommand.RaiseCanExecuteChanged();
				_refreshSkuResultsCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsSpBatchMasterRunning
	{
		get
		{
			if (IsSpBatchRunning)
			{
				return _activeSpBatchMode == SpBatchMode.GenerateMaster;
			}
			return false;
		}
	}

	public bool IsSpBatchColorRunning
	{
		get
		{
			if (IsSpBatchRunning)
			{
				return _activeSpBatchMode == SpBatchMode.GenerateColors;
			}
			return false;
		}
	}

	public string SpBatchInputDirectory
	{
		get
		{
			return _spBatchInputDirectory;
		}
		set
		{
			if (SetProperty(ref _spBatchInputDirectory, value, "SpBatchInputDirectory"))
			{
				PersistUserPathSettings();
			}
		}
	}

	public string SpBatchOutputDirectory
	{
		get
		{
			return _spBatchOutputDirectory;
		}
		set
		{
			if (SetProperty(ref _spBatchOutputDirectory, value, "SpBatchOutputDirectory"))
			{
				PersistUserPathSettings();
				_openSpBatchOutputFolderCommand.RaiseCanExecuteChanged();
				_runSpBatchCommand.RaiseCanExecuteChanged();
				_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
				SpBatchResultRootText = (string.IsNullOrWhiteSpace(value) ? "日期目录：未设置" : ("日期目录：" + value));
			}
		}
	}

	public string SpBatchConcurrencyText
	{
		get
		{
			return _spBatchConcurrencyText;
		}
		set
		{
			SetProperty(ref _spBatchConcurrencyText, value, "SpBatchConcurrencyText");
		}
	}

	public string SpBatchRetriesText
	{
		get
		{
			return _spBatchRetriesText;
		}
		set
		{
			SetProperty(ref _spBatchRetriesText, value, "SpBatchRetriesText");
		}
	}

	public bool IsSpBatchPrepareOnly
	{
		get
		{
			return _spBatchMode == SpBatchMode.PrepareOnly;
		}
		set
		{
			if (value)
			{
				SetSpBatchMode(SpBatchMode.PrepareOnly);
			}
		}
	}

	public bool IsSpBatchGenerateMode
	{
		get
		{
			return _spBatchMode == SpBatchMode.Generate;
		}
		set
		{
			if (value)
			{
				SetSpBatchMode(SpBatchMode.Generate);
			}
		}
	}

	public string SpBatchStatusText
	{
		get
		{
			return _spBatchStatusText;
		}
		private set
		{
			SetProperty(ref _spBatchStatusText, value, "SpBatchStatusText");
		}
	}

	public string SpBatchColorStatusText
	{
		get
		{
			return _spBatchColorStatusText;
		}
		private set
		{
			SetProperty(ref _spBatchColorStatusText, value, "SpBatchColorStatusText");
		}
	}

	public string SpBatchSummaryText
	{
		get
		{
			return _spBatchSummaryText;
		}
		private set
		{
			SetProperty(ref _spBatchSummaryText, value, "SpBatchSummaryText");
		}
	}

	public string SpBatchResultRootText
	{
		get
		{
			return _spBatchResultRootText;
		}
		private set
		{
			SetProperty(ref _spBatchResultRootText, value, "SpBatchResultRootText");
		}
	}

	public string SpBatchResultStatsText
	{
		get
		{
			return _spBatchResultStatsText;
		}
		private set
		{
			SetProperty(ref _spBatchResultStatsText, value, "SpBatchResultStatsText");
		}
	}

	public string SpBatchStatsSkuCountText => $"SKU 数量：{_spBatchStatsSkuCount}";

	public string SpBatchStatsTotalText => $"总任务：{_spBatchStatsTotalCount}";

	public string SpBatchStatsSuccessText => $"成功：{_spBatchStatsSuccessCount}";

	public string SpBatchStatsSkippedText => $"跳过：{_spBatchStatsSkippedCount}";

	public string SpBatchStatsFailedText => $"失败：{_spBatchStatsFailedCount}";

	public Brush SpBatchStatsSuccessBrush => new SolidColorBrush(Color.FromRgb(103, 194, 58));

	public Brush SpBatchStatsFailedBrush => new SolidColorBrush(Color.FromRgb(245, 108, 108));

	public MainWindowViewModel(IFolderScanService folderScanService, IImageWorkspaceService imageWorkspaceService, IWorkspaceStateService workspaceStateService, IAppSettingsService appSettingsService, IProductSheetService productSheetService, ITemplateGenerationService templateGenerationService, ISpBatchService spBatchService, ISkuOptimizeService skuOptimizeService, IMiaoshouPublishService miaoshouPublishService, IAutoPublishStateService autoPublishStateService, ICardSizeInfoService cardSizeInfoService, ITemplateLibraryService templateLibraryService)
	{
		_folderScanService = folderScanService;
		_imageWorkspaceService = imageWorkspaceService;
		_workspaceStateService = workspaceStateService;
		_appSettingsService = appSettingsService;
		_productSheetService = productSheetService;
		_templateGenerationService = templateGenerationService;
		_spBatchService = spBatchService;
		_skuOptimizeService = skuOptimizeService;
		_miaoshouPublishService = miaoshouPublishService;
		_autoPublishStateService = autoPublishStateService;
		_cardSizeInfoService = cardSizeInfoService;
		_templateLibraryService = templateLibraryService;
		_generationTabStates["main-image-generate"] = CreateEmptyGenerationTabState();
		_generationTabStates["scene-image-generate"] = CreateEmptyGenerationTabState();
		_generationTabStates["compare-image-generate"] = CreateEmptyGenerationTabState();
		_chooseFolderCommand = new AsyncRelayCommand((object? _) => ChooseFolderAsync());
		_selectBackupFolderCommand = new AsyncRelayCommand((object? _) => SelectBackupFolderAsync());
		_openCurrentFolderCommand = new RelayCommand(delegate
		{
			OpenCurrentFolder();
		}, (object? _) => CanOpenCurrentFolder());
		_invertSelectionCommand = new RelayCommand(delegate
		{
			SelectedTab?.InvertSelection();
		}, (object? _) => !IsBusy && SelectedTab?.ActiveCard != null);
		_selectAllBatchCardsCommand = new RelayCommand(delegate
		{
			SelectAllBatchCards();
		}, (object? _) => CanModifyBatchSelection());
		_clearAllBatchCardsCommand = new RelayCommand(delegate
		{
			ClearAllBatchCards();
		}, (object? _) => CanModifyBatchSelection());
		_setAutoPublishStatusFilterCommand = new RelayCommand(delegate(object? parameter)
		{
			SetAutoPublishStatusFilter(parameter);
		});
		_openAutoPublishSettingsCommand = new RelayCommand(delegate
		{
			IsAutoPublishSettingsDialogOpen = true;
		});
		_closeAutoPublishSettingsCommand = new RelayCommand(delegate
		{
			IsAutoPublishSettingsDialogOpen = false;
		});
		_generateProductSheetCommand = new AsyncRelayCommand((object? _) => GenerateProductSheetAsync(), delegate
		{
			if (!IsBusy)
			{
				WorkspaceTabViewModel? selectedTab = SelectedTab;
				if (selectedTab == null)
				{
					return false;
				}
				return selectedTab.RootCards.Count > 0;
			}
			return false;
		});
		_addTabCommand = new AsyncRelayCommand((object? _) => ChooseFolderAsync(), (object? _) => !IsBusy);
		_showReviewWorkspaceCommand = new RelayCommand(delegate
		{
			SetSelectedSection("review-workspace");
		});
		_showImageGenerateCommand = new RelayCommand(delegate
		{
			SetSelectedSection("image-generate");
		});
		_showTemplateManagerCommand = new RelayCommand(delegate
		{
			SetSelectedSection("template-manager");
		});
		_showTemplateGenerateTabCommand = new RelayCommand(delegate
		{
			SetSelectedImageGenerateTab("main-image-generate");
		});
		_showSpBatchTabCommand = new RelayCommand(delegate
		{
			SetSelectedImageGenerateTab("sp-batch");
		});
		_showSkuOptimizeTabCommand = new RelayCommand(delegate
		{
			SetSelectedImageGenerateTab("sku-optimize");
		});
		_sendSelectedSpBatchMasterToSkuOptimizeCommand = new RelayCommand(delegate
		{
			if (TransferSpBatchMasterToSkuOptimize(showPromptWhenMissing: true))
			{
				SetSelectedImageGenerateTab("sku-optimize");
			}
		});
		_openSpBatchColorSelectionCommand = new RelayCommand(delegate
		{
			OpenSpBatchColorSelection();
		}, (object? _) => true);
		_closeSpBatchColorSelectionCommand = new RelayCommand(delegate
		{
			CloseSpBatchColorSelection();
		});
		_selectAllSpBatchColorSelectionCommand = new RelayCommand(delegate
		{
			SelectAllSpBatchColors();
		}, (object? _) => IsSpBatchColorSelectionOpen);
		_clearAllSpBatchColorSelectionCommand = new RelayCommand(delegate
		{
			ClearAllSpBatchColors();
		}, (object? _) => IsSpBatchColorSelectionOpen);
		_saveSpBatchColorSelectionCommand = new RelayCommand(delegate
		{
			SaveSpBatchColorSelection();
		}, (object? _) => IsSpBatchColorSelectionOpen);
		_chooseSpBatchSourceImagesCommand = new AsyncRelayCommand((object? _) => ChooseSpBatchSourceImagesAsync(), (object? _) => true);
		_chooseSpBatchMasterImagesCommand = new AsyncRelayCommand((object? _) => ChooseSpBatchMasterImagesAsync(), (object? _) => true);
		_showSceneImageGenerateTabCommand = new RelayCommand(delegate
		{
			SetSelectedImageGenerateTab("scene-image-generate");
		});
		_showCompareImageGenerateTabCommand = new RelayCommand(delegate
		{
			SetSelectedImageGenerateTab("compare-image-generate");
		});
		_showLayoutTemplateTabCommand = new AsyncRelayCommand((object? _) => SelectTemplateCategoryAsync(TemplateCategory.Layout));
		_showSceneTemplateTabCommand = new AsyncRelayCommand((object? _) => SelectTemplateCategoryAsync(TemplateCategory.Scene));
		_showSubjectTemplateTabCommand = new AsyncRelayCommand((object? _) => SelectTemplateCategoryAsync(TemplateCategory.Subject));
		_showTitleTemplateTabCommand = new AsyncRelayCommand((object? _) => SelectTemplateCategoryAsync(TemplateCategory.Title));
		_showColorTemplateTabCommand = new AsyncRelayCommand((object? _) => SelectTemplateCategoryAsync(TemplateCategory.Color));
		_showMainImageLayoutTypeCommand = new RelayCommand(delegate
		{
			SetSelectedLayoutImageType(ImageTemplateType.MainImage);
		});
		_showSceneImageLayoutTypeCommand = new RelayCommand(delegate
		{
			SetSelectedLayoutImageType(ImageTemplateType.SceneImage);
		});
		_showCompareImageLayoutTypeCommand = new RelayCommand(delegate
		{
			SetSelectedLayoutImageType(ImageTemplateType.CompareImage);
		});
		_showMainTitleTemplateTypeCommand = new RelayCommand(delegate
		{
			SetSelectedTitleTemplateType(MainTitleTemplateType);
		});
		_showSubTitleTemplateTypeCommand = new RelayCommand(delegate
		{
			SetSelectedTitleTemplateType(SubTitleTemplateType);
		});
		_showIconWordTemplateTypeCommand = new RelayCommand(delegate
		{
			SetSelectedTitleTemplateType(IconWordTemplateType);
		});
		_newTemplateCommand = new RelayCommand(delegate
		{
			NewTemplate();
		});
		_saveTemplateCommand = new AsyncRelayCommand((object? _) => SaveTemplateAsync());
		_deleteTemplateCommand = new AsyncRelayCommand((object? _) => DeleteTemplateAsync(), (object? _) => SelectedTemplateItem != null && SelectedTemplateItem.Id > 0);
		_deleteManagedTemplateCommand = new AsyncRelayCommand((object? item) => DeleteManagedTemplateAsync(item as TemplateItemViewModel), (object? item) => item is TemplateItemViewModel);
		_copyManagedLayoutTemplateCommand = new AsyncRelayCommand((object? item) => CopyManagedLayoutTemplateAsync(item as TemplateItemViewModel), (object? item) => item is TemplateItemViewModel template && template.Category == TemplateCategory.Layout);
		_deleteColorTemplateGroupCommand = new AsyncRelayCommand((object? item) => DeleteColorTemplateGroupAsync(item as ColorTemplateGroupViewModel), (object? item) => item is ColorTemplateGroupViewModel);
		_selectManagedTemplateCommand = new RelayCommand(delegate(object? item)
		{
			SelectManagedTemplate(item as TemplateItemViewModel);
		}, (object? item) => item is TemplateItemViewModel);
		_selectColorTemplateGroupCommand = new RelayCommand(delegate(object? item)
		{
			SelectColorTemplateGroup(item as ColorTemplateGroupViewModel);
		}, (object? item) => item is ColorTemplateGroupViewModel);
		_chooseTemplatePreviewCommand = new AsyncRelayCommand((object? _) => ChooseTemplatePreviewAsync(), (object? _) => IsLayoutTemplateTabSelected);
		_openTemplateEditorPreviewCommand = new RelayCommand(delegate
		{
			OpenTemplateEditorPreview();
		}, (object? _) => File.Exists(TemplateEditorPreviewImagePath));
		_importLayoutTemplatesCommand = new AsyncRelayCommand((object? _) => ImportLayoutTemplatesAsync());
		_exportLayoutTemplatesCommand = new AsyncRelayCommand((object? _) => ExportLayoutTemplatesAsync());
		_importAllTemplatesCommand = new AsyncRelayCommand((object? _) => ImportAllTemplatesAsync());
		_exportAllTemplatesCommand = new AsyncRelayCommand((object? _) => ExportAllTemplatesAsync());
		_closeTemplateEditorCommand = new RelayCommand(delegate
		{
			IsTemplateEditorDialogOpen = false;
		});
		_addSceneContentLineCommand = new RelayCommand(delegate(object? line)
		{
			AddSceneContentLine(line as SceneContentLineViewModel);
		});
		_removeSceneContentLineCommand = new RelayCommand(delegate(object? line)
		{
			RemoveSceneContentLine(line as SceneContentLineViewModel);
		}, (object? line) => SceneContentLines.Count > 1 && line is SceneContentLineViewModel);
		_addTitleMainLineCommand = new RelayCommand(delegate
		{
			AddTitleMainLine();
		});
		_removeTitleMainLineCommand = new RelayCommand(delegate(object? line)
		{
			RemoveTitleMainLine(line as SceneContentLineViewModel);
		}, (object? line) => TitleMainLines.Count > 1 && line is SceneContentLineViewModel);
		_addTitleSubLineCommand = new RelayCommand(delegate
		{
			AddTitleSubLine();
		});
		_removeTitleSubLineCommand = new RelayCommand(delegate(object? line)
		{
			RemoveTitleSubLine(line as SceneContentLineViewModel);
		}, (object? line) => TitleSubLines.Count > 1 && line is SceneContentLineViewModel);
		_openTemplateSubjectPickerCommand = new RelayCommand(delegate
		{
			OpenTemplateSubjectPicker();
		}, (object? _) => IsSceneTemplateTabSelected);
		_closeTemplateSubjectPickerCommand = new RelayCommand(delegate
		{
			CloseTemplateSubjectPicker();
		});
		_toggleTemplateSubjectOptionCommand = new RelayCommand(delegate(object? option)
		{
			ToggleTemplateSubjectOption(option as TemplateSubjectOptionViewModel);
		}, (object? option) => option is TemplateSubjectOptionViewModel);
		_removeTemplateSubjectTagCommand = new RelayCommand(delegate(object? tag)
		{
			RemoveTemplateSubjectTag(tag as TemplateSubjectTagViewModel);
		}, (object? tag) => tag is TemplateSubjectTagViewModel);
		_startAddTemplateSubjectTagCommand = new RelayCommand(delegate
		{
			StartAddTemplateSubjectTag();
		}, (object? _) => IsSceneTemplateTabSelected);
		_commitTemplateSubjectTagCommand = new AsyncRelayCommand((object? _) => CommitTemplateSubjectTagAsync(), (object? _) => IsSceneTemplateTabSelected && !string.IsNullOrWhiteSpace(NewTemplateSubjectText));
		_cancelTemplateSubjectTagCommand = new RelayCommand(delegate
		{
			CancelTemplateSubjectTagInput();
		}, (object? _) => IsSceneTemplateTabSelected);
		_chooseTemplateLibraryCommand = new AsyncRelayCommand((object? _) => ChooseTemplateLibraryAsync(), (object? _) => CanEditTemplateGenerationSettings());
		_openTemplateLibraryFileCommand = new RelayCommand(delegate
		{
			OpenTemplateLibraryFile();
		}, (object? _) => File.Exists(TemplateLibraryPath));
		_openGenerationTemplatePickerCommand = new AsyncRelayCommand((object? _) => OpenGenerationTemplatePickerAsync(), (object? _) => CanEditTemplateGenerationSettings());
		_closeGenerationTemplatePickerCommand = new RelayCommand(delegate
		{
			CloseGenerationTemplatePicker();
		});
		_selectAllGenerationTemplatesCommand = new RelayCommand(delegate
		{
			SelectAllGenerationTemplates();
		});
		_clearGenerationTemplatesCommand = new RelayCommand(delegate
		{
			ClearGenerationTemplates();
		});
		_chooseGenerationOutputFolderCommand = new AsyncRelayCommand((object? _) => ChooseGenerationOutputFolderAsync(), (object? _) => CanEditTemplateGenerationSettings());
		_openGenerationOutputFolderCommand = new RelayCommand(delegate
		{
			OpenGenerationOutputFolder();
		}, (object? _) => Directory.Exists(GenerationOutputDirectory));
		_runTemplateGenerationCommand = new AsyncRelayCommand((object? _) => RunTemplateGenerationAsync(), (object? _) => CanRunTemplateGeneration());
		_stopTemplateGenerationCommand = new RelayCommand(delegate
		{
			StopTemplateGeneration();
		}, (object? _) => IsTemplateGenerating);
		_chooseGenerationImagesCommand = new AsyncRelayCommand((object? _) => ChooseGenerationImagesAsync(), (object? _) => true);
		_sendSelectedImagesToSpBatchCommand = new RelayCommand(delegate
		{
			SendSelectedImagesToSpBatch();
		}, (object? _) => CanSendSelectedImagesToSpBatch());
		_chooseSpBatchInputFolderCommand = new AsyncRelayCommand((object? _) => ChooseSpBatchInputFolderAsync(), (object? _) => CanEditSpBatchSettings());
		_chooseSpBatchOutputFolderCommand = new AsyncRelayCommand((object? _) => ChooseSpBatchOutputFolderAsync(), (object? _) => CanEditSpBatchSettings());
		_openSpBatchOutputFolderCommand = new RelayCommand(delegate
		{
			OpenSpBatchOutputFolder();
		}, (object? _) => Directory.Exists(SpBatchOutputDirectory));
		_runSpBatchCommand = new AsyncRelayCommand((object? _) => RunSpBatchFromStagingAsync(), (object? _) => CanRunSpBatch());
		_generateSpBatchColorSkusCommand = new AsyncRelayCommand((object? _) => GenerateSpBatchColorSkusAsync(), (object? _) => CanGenerateSpBatchColorSkus());
		_stopSpBatchCommand = new RelayCommand(delegate
		{
			StopSpBatch();
		}, (object? _) => IsSpBatchRunning);
		_refreshSkuResultsCommand = new RelayCommand(delegate
		{
			RefreshSkuResults();
		}, (object? _) => !IsSpBatchRunning && !IsSpBatchStopping);
		_chooseSkuOptimizeOutputFolderCommand = new AsyncRelayCommand((object? _) => ChooseSkuOptimizeOutputFolderAsync(), (object? _) => CanEditSkuOptimizeSettings());
		_openSkuOptimizeOutputFolderCommand = new RelayCommand(delegate
		{
			OpenSkuOptimizeOutputFolder();
		}, (object? _) => Directory.Exists(SkuOptimizeOutputDirectory));
		_runSkuOptimizeCommand = new AsyncRelayCommand((object? _) => RunSkuOptimizeFromStagingAsync(), (object? _) => CanRunSkuOptimize());
		_stopSkuOptimizeCommand = new RelayCommand(delegate
		{
			StopSkuOptimize();
		}, (object? _) => IsSkuOptimizeRunning);
		_runBatchAutoPublishCommand = new AsyncRelayCommand((object? _) => RunBatchAutoPublishAsync(), (object? _) => CanExecuteBatchAutoPublish());
		_saveCardSizeInfoCommand = new AsyncRelayCommand((object? _) => SaveCardSizeInfoAsync(), (object? _) => CanSaveCardSizeInfo());
		_cancelCardSizeInfoCommand = new RelayCommand(delegate
		{
			CloseCardSizeDialog();
		});
		_openImageGenerationKeyDialogCommand = new RelayCommand(delegate
		{
			OpenImageGenerationKeyDialog();
		});
		_selectImageGenerationProviderCommand = new RelayCommand(delegate(object? parameter)
		{
			SelectImageGenerationProvider(parameter?.ToString());
		});
		_saveImageGenerationKeyCommand = new RelayCommand(delegate
		{
			SaveImageGenerationKey();
		}, (object? _) => !string.IsNullOrWhiteSpace(ImageGenerationKeyText));
		_cancelImageGenerationKeyCommand = new RelayCommand(delegate
		{
			CloseImageGenerationKeyDialog();
		});
		_addColorTemplateRowCommand = new RelayCommand(delegate
		{
			AddColorTemplateEditorRow();
		});
		_removeColorTemplateRowCommand = new RelayCommand(delegate(object? item)
		{
			RemoveColorTemplateEditorRow(item as ColorTemplateColorViewModel);
		}, (object? item) => ColorTemplateEditorColors.Count > 1 && item is ColorTemplateColorViewModel);
		_openColorTemplateColorPreviewCommand = new RelayCommand(delegate(object? item)
		{
			OpenColorTemplateColorPreview(item as ColorTemplateColorViewModel);
		}, (object? item) => item is ColorTemplateColorViewModel);
		_closeColorTemplateColorPreviewCommand = new RelayCommand(delegate
		{
			CloseColorTemplateColorPreview();
		});
		_saveColorTemplateGroupCommand = new AsyncRelayCommand((object? _) => SaveColorTemplateGroupAsync(), (object? _) => CanSaveColorTemplateGroup());
		_closeColorTemplateGroupEditorCommand = new RelayCommand(delegate
		{
			CloseColorTemplateGroupEditor();
		});
		_runAppUpdateCommand = new AsyncRelayCommand((object? _) => RunAppUpdateAsync(), (object? _) => IsAppUpdateAvailable && !IsAppUpdateDownloading);
	}

	public async Task InitializeAsync()
	{
		await Task.Yield();
		await _autoPublishStateService.InitializeAsync();
		await _autoPublishStateService.MarkIncompletePublishingAsFailedAsync();
		await _cardSizeInfoService.InitializeAsync();
		await _templateLibraryService.InitializeAsync();
		await LoadTemplateTabCountsAsync();
		await LoadManagedTemplatesAsync();
		await LoadColorTemplateGroupsAsync();
		AppUserPathsState appUserPathsState = _appSettingsService.LoadUserPaths();
		ApplyUserPathSettings(appUserPathsState);
		EnsurePlaceholderTab();
		ResetSummary();
		ResetGenerationSummary();
		ResetSpBatchSummary();
		ResetSkuOptimizeSummary();
		string reviewRootFolder = NormalizeWritableWorkspacePath(appUserPathsState.ReviewRootFolder ?? string.Empty);
		if (!string.IsNullOrWhiteSpace(reviewRootFolder) && Directory.Exists(reviewRootFolder))
		{
			await LoadFolderAsync(reviewRootFolder);
		}
		_ = CheckForAppUpdateAsync();
	}

	private async Task CheckForAppUpdateAsync()
	{
		try
		{
			using HttpClient client = CreateGitHubHttpClient();
			using HttpResponseMessage response = await client.GetAsync(GitHubLatestReleaseApiUrl);
			if (!response.IsSuccessStatusCode)
			{
				return;
			}
			using Stream stream = await response.Content.ReadAsStreamAsync();
			using JsonDocument document = await JsonDocument.ParseAsync(stream);
			JsonElement root = document.RootElement;
			string latestVersionText = root.TryGetProperty("tag_name", out JsonElement tagElement) ? tagElement.GetString() ?? string.Empty : string.Empty;
			if (!TryParseAppVersion(latestVersionText, out Version? latestVersion) || !TryParseAppVersion(_appCurrentVersion, out Version? currentVersion))
			{
				return;
			}
			if (latestVersion <= currentVersion)
			{
				return;
			}
			string installerUrl = string.Empty;
			string installerName = string.Empty;
			if (root.TryGetProperty("assets", out JsonElement assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
			{
				List<(string Name, string Url)> executableAssets = new List<(string Name, string Url)>();
				foreach (JsonElement asset in assetsElement.EnumerateArray())
				{
					string assetName = asset.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
					string assetUrl = asset.TryGetProperty("browser_download_url", out JsonElement urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty;
					if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(assetUrl))
					{
						executableAssets.Add((assetName, assetUrl));
					}
				}
				(string Name, string Url) preferredAsset = executableAssets.FirstOrDefault(asset => asset.Name.StartsWith("EcomTool_Update_", StringComparison.OrdinalIgnoreCase));
				if (string.IsNullOrWhiteSpace(preferredAsset.Url))
				{
					preferredAsset = executableAssets.FirstOrDefault(asset => asset.Name.StartsWith("EcomTool_Setup_", StringComparison.OrdinalIgnoreCase));
				}
				if (string.IsNullOrWhiteSpace(preferredAsset.Url))
				{
					preferredAsset = executableAssets.FirstOrDefault();
				}
				installerName = preferredAsset.Name ?? string.Empty;
				installerUrl = preferredAsset.Url ?? string.Empty;
			}
			if (string.IsNullOrWhiteSpace(installerUrl))
			{
				return;
			}
			_availableAppUpdateVersion = NormalizeAppVersionText(latestVersionText);
			_availableAppUpdateReleaseNotes = root.TryGetProperty("body", out JsonElement bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty;
			_availableAppUpdateInstallerUrl = installerUrl;
			_availableAppUpdateInstallerName = installerName;
			IsAppUpdateAvailable = true;
		}
		catch
		{
			IsAppUpdateAvailable = false;
		}
	}

	private async Task RunAppUpdateAsync()
	{
		if (string.IsNullOrWhiteSpace(_availableAppUpdateInstallerUrl))
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = GitHubReleaseDownloadUrl,
				UseShellExecute = true
			});
			return;
		}
		string releaseNotes = string.IsNullOrWhiteSpace(_availableAppUpdateReleaseNotes) ? "本次更新未填写更新公告。" : _availableAppUpdateReleaseNotes.Trim();
		string message = "当前版本：" + AppVersionText + Environment.NewLine +
			"最新版本：v" + _availableAppUpdateVersion + Environment.NewLine +
			Environment.NewLine +
			"更新公告：" + Environment.NewLine +
			releaseNotes + Environment.NewLine +
			Environment.NewLine +
			"点击“确定”后开始下载并安装更新。";
		MessageBoxResult result = MessageBox.Show(message, "发现新版本", MessageBoxButton.OKCancel, MessageBoxImage.Information);
		if (result != MessageBoxResult.OK)
		{
			return;
		}
		IsAppUpdateDownloading = true;
		try
		{
			StatusMessage = "正在下载更新：" + _availableAppUpdateVersion;
			string updatesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToolBox", "Updates");
			Directory.CreateDirectory(updatesFolder);
			string installerName = string.IsNullOrWhiteSpace(_availableAppUpdateInstallerName) ? "EcomToolStudio_Update_" + _availableAppUpdateVersion + ".exe" : Path.GetFileName(_availableAppUpdateInstallerName);
			string installerPath = CreateUniqueUpdateFilePath(updatesFolder, installerName);
			await DownloadAppUpdateInstallerAsync(_availableAppUpdateInstallerUrl, installerPath);
			StatusMessage = "更新包下载完成，正在启动安装程序。";
			Process.Start(new ProcessStartInfo
			{
				FileName = installerPath,
				UseShellExecute = true
			});
			Application.Current.Shutdown();
		}
		catch (Exception ex)
		{
			StatusMessage = "更新失败：" + ex.Message;
			MessageBox.Show("更新包下载失败：" + Environment.NewLine + ex.Message + Environment.NewLine + Environment.NewLine + "可以稍后重试，或手动下载安装包。", "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			IsAppUpdateDownloading = false;
		}
	}

	private static async Task DownloadAppUpdateInstallerAsync(string url, string installerPath)
	{
		string directory = Path.GetDirectoryName(installerPath) ?? Path.GetTempPath();
		string tempPath = CreateUniqueUpdateFilePath(directory, Path.GetFileNameWithoutExtension(installerPath) + ".download");
		using HttpClient client = CreateGitHubHttpClient(TimeSpan.FromMinutes(30));
		try
		{
			client.DefaultRequestHeaders.Accept.Clear();
			using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();
			await using Stream remoteStream = await response.Content.ReadAsStreamAsync();
			await using FileStream fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true);
			await remoteStream.CopyToAsync(fileStream);
			File.Move(tempPath, installerPath);
		}
		catch
		{
			TryDeleteFile(tempPath);
			throw;
		}
	}

	private static string CreateUniqueUpdateFilePath(string folder, string fileName)
	{
		string safeName = Path.GetFileName(fileName);
		string extension = Path.GetExtension(safeName);
		string stem = Path.GetFileNameWithoutExtension(safeName);
		for (int index = 0; index < 100; index++)
		{
			string suffix = DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N")[..8];
			string candidate = Path.Combine(folder, stem + "_" + suffix + extension);
			if (!File.Exists(candidate))
			{
				return candidate;
			}
		}
		return Path.Combine(folder, stem + "_" + Guid.NewGuid().ToString("N") + extension);
	}

	private static void TryDeleteFile(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static HttpClient CreateGitHubHttpClient()
	{
		return CreateGitHubHttpClient(TimeSpan.FromSeconds(20));
	}

	private static HttpClient CreateGitHubHttpClient(TimeSpan timeout)
	{
		HttpClient client = new HttpClient
		{
			Timeout = timeout
		};
		client.DefaultRequestHeaders.UserAgent.ParseAdd("EcomTool-Studio-Updater");
		client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
		return client;
	}

	private static string GetCurrentAppVersion()
	{
		Assembly assembly = typeof(MainWindowViewModel).Assembly;
		string? informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (!string.IsNullOrWhiteSpace(informationalVersion))
		{
			return NormalizeAppVersionText(informationalVersion);
		}
		return NormalizeAppVersionText(assembly.GetName().Version?.ToString() ?? "1.0.0");
	}

	private static string NormalizeAppVersionText(string versionText)
	{
		string normalized = versionText.Trim();
		if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
		{
			normalized = normalized.Substring(1);
		}
		int suffixIndex = normalized.IndexOfAny(new[] { '-', '+' });
		if (suffixIndex >= 0)
		{
			normalized = normalized.Substring(0, suffixIndex);
		}
		return normalized;
	}

	private static bool TryParseAppVersion(string versionText, out Version? version)
	{
		string normalized = NormalizeAppVersionText(versionText);
		if (Version.TryParse(normalized, out Version? parsed))
		{
			version = parsed;
			return true;
		}
		version = null;
		return false;
	}

	public async Task LoadFolderAsync(string folderPath)
	{
		if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
		{
			StatusMessage = "目录不存在：" + folderPath;
			return;
		}
		WorkspaceTabViewModel workspaceTabViewModel = WorkspaceTabs.FirstOrDefault((WorkspaceTabViewModel tab) => string.Equals(tab.RootFolder, folderPath, StringComparison.OrdinalIgnoreCase));
		if (workspaceTabViewModel != null)
		{
			SelectedTab = workspaceTabViewModel;
			await ReloadTabAsync(workspaceTabViewModel);
			return;
		}
		WorkspaceTabViewModel workspaceTabViewModel2 = ((SelectedTab != null && SelectedTab.IsPlaceholder) ? SelectedTab : CreateAndAddTab(folderPath));
		workspaceTabViewModel2.SetRootFolder(folderPath);
		SelectedTab = workspaceTabViewModel2;
		PersistUserPathSettings();
		await ReloadTabAsync(workspaceTabViewModel2);
	}

	public async Task RefreshCurrentPageAsync()
	{
		if (IsBusy)
		{
			return;
		}
		if (IsReviewWorkspaceSelected)
		{
			WorkspaceTabViewModel selectedTab = SelectedTab;
			if (selectedTab == null || selectedTab.IsPlaceholder || string.IsNullOrWhiteSpace(selectedTab.RootFolder) || !Directory.Exists(selectedTab.RootFolder))
			{
				StatusMessage = "当前没有可刷新的文件夹。";
				return;
			}
			StatusMessage = "正在刷新：" + selectedTab.RootFolder;
			await ReloadTabAsync(selectedTab);
		}
		else if (IsTemplateManagerSelected)
		{
			await LoadTemplateTabCountsAsync();
			await LoadManagedTemplatesAsync();
			StatusMessage = "已刷新模板管理页。";
		}
		else if (IsImageGenerateSelected)
		{
			if (IsTemplateGenerateTabSelected)
			{
				ResetGenerationSummary();
				StatusMessage = "已刷新图片生成页。";
			}
			else if (IsSpBatchTabSelected)
			{
				ResetSpBatchSummary();
				StatusMessage = "已刷新 SP 批处理页。";
			}
		}
	}

	private async Task SelectTemplateCategoryAsync(TemplateCategory category)
	{
		if (_selectedTemplateCategory != category)
		{
			_selectedTemplateCategory = category;
			NotifyTemplateCategoryChanged();
			await LoadManagedTemplatesAsync();
		}
	}

	private void SetSelectedLayoutImageType(ImageTemplateType imageType)
	{
		if (_selectedLayoutImageType != imageType)
		{
			_selectedLayoutImageType = imageType;
			OnPropertyChanged("IsMainImageLayoutTypeSelected");
			OnPropertyChanged("IsSceneImageLayoutTypeSelected");
			OnPropertyChanged("IsCompareImageLayoutTypeSelected");
			OnPropertyChanged("SelectedLayoutImageTypeText");
			OnPropertyChanged("TemplateManagerTitle");
			OnPropertyChanged("TemplateManagerDescription");
			OnPropertyChanged("TemplateEditorDialogTitle");
			_exportLayoutTemplatesCommand.RaiseCanExecuteChanged();
			_importLayoutTemplatesCommand.RaiseCanExecuteChanged();
			_chooseTemplatePreviewCommand.RaiseCanExecuteChanged();
			_saveTemplateCommand.RaiseCanExecuteChanged();
			_ = LoadManagedTemplatesAsync();
		}
	}

	private void SetSelectedTitleTemplateType(string titleType)
	{
		if (!string.Equals(_selectedTitleTemplateType, titleType, StringComparison.Ordinal))
		{
			_selectedTitleTemplateType = titleType;
			OnPropertyChanged("IsMainTitleTemplateTypeSelected");
			OnPropertyChanged("IsSubTitleTemplateTypeSelected");
			OnPropertyChanged("IsIconWordTemplateTypeSelected");
			OnPropertyChanged("TemplateEditorDialogTitle");
			OnPropertyChanged("CurrentTemplateImportButtonText");
			OnPropertyChanged("CurrentTemplateExportButtonText");
			_ = LoadManagedTemplatesAsync();
		}
	}

	private async Task LoadManagedTemplatesAsync()
	{
		TemplateCategory selectedCategory = _selectedTemplateCategory;
		if (selectedCategory == TemplateCategory.Color)
		{
			await LoadColorTemplateGroupsAsync();
			ManagedTemplateItems = new ObservableCollection<TemplateItemViewModel>();
			OnPropertyChanged("HasManagedTemplateItems");
			OnPropertyChanged("IsTemplateContentPanelVisible");
			OnPropertyChanged("TemplateEmptyText");
			ResetTemplateEditor(refreshSubjectOptions: false);
			CloseColorTemplateGroupEditor();
			return;
		}
		ImageTemplateType selectedLayoutImageType = _selectedLayoutImageType;
		string selectedTitleTemplateType = _selectedTitleTemplateType;
		bool isLayoutTemplateTabSelected = selectedCategory == TemplateCategory.Layout;
		bool isSceneTemplateTabSelected = selectedCategory == TemplateCategory.Scene;
		bool isTitleTemplateTabSelected = selectedCategory == TemplateCategory.Title;
		Task<IReadOnlyList<TemplateItemRecord>> recordsTask = _templateLibraryService.GetByCategoryAsync(selectedCategory, isLayoutTemplateTabSelected ? new ImageTemplateType?(selectedLayoutImageType) : ((ImageTemplateType?)null));
		Task<IReadOnlyList<TemplateItemRecord>>? subjectTemplatesTask = isSceneTemplateTabSelected ? _templateLibraryService.GetByCategoryAsync(TemplateCategory.Subject) : null;
		Task<IReadOnlyDictionary<long, IReadOnlyList<long>>>? sceneBindingsTask = isSceneTemplateTabSelected ? _templateLibraryService.GetSceneSubjectBindingsAsync() : null;
		await recordsTask;
		if (subjectTemplatesTask != null && sceneBindingsTask != null)
		{
			await Task.WhenAll(subjectTemplatesTask, sceneBindingsTask);
		}
		IReadOnlyList<TemplateItemRecord> records = await recordsTask;
		if (isTitleTemplateTabSelected)
		{
			records = records.Where(item => GetTitleTemplateType(item) == selectedTitleTemplateType).ToArray();
		}
		if (isSceneTemplateTabSelected)
		{
			_subjectTemplateLookup.Clear();
			foreach (TemplateItemRecord item in await subjectTemplatesTask!)
			{
				_subjectTemplateLookup[item.Id] = item;
			}
			_sceneSubjectBindingCache.Clear();
			foreach (KeyValuePair<long, IReadOnlyList<long>> item2 in await sceneBindingsTask!)
			{
				_sceneSubjectBindingCache[item2.Key] = item2.Value;
			}
		}
		ManagedTemplateItems = new ObservableCollection<TemplateItemViewModel>(records.Select(CreateTemplateItemViewModel));
		OnPropertyChanged("HasManagedTemplateItems");
		OnPropertyChanged("IsTemplateContentPanelVisible");
		OnPropertyChanged("TemplateEmptyText");
		OnTemplateExportSelectionChanged();
		if (isLayoutTemplateTabSelected)
		{
			await LoadGenerationTemplateOptionsAsync();
		}
		ResetTemplateEditor(refreshSubjectOptions: false);
	}

	private async Task ReloadManagedTemplatesAfterMutationAsync()
	{
		await LoadTemplateTabCountsAsync();
		await LoadManagedTemplatesAsync();
	}

	private async Task LoadTemplateTabCountsAsync()
	{
		Task<IReadOnlyList<TemplateItemRecord>> layoutTemplatesTask = _templateLibraryService.GetByCategoryAsync(TemplateCategory.Layout);
		Task<IReadOnlyList<TemplateItemRecord>> titleTemplatesTask = _templateLibraryService.GetByCategoryAsync(TemplateCategory.Title);
		Task<IReadOnlyList<ColorTemplateGroupRecord>> colorGroupsTask = _templateLibraryService.GetColorGroupsAsync();
		await Task.WhenAll(layoutTemplatesTask, titleTemplatesTask, colorGroupsTask);
		UpdateLayoutTemplateCounts(await layoutTemplatesTask);
		UpdateTitleTemplateCounts(await titleTemplatesTask);
		ColorTemplateGroupCount = (await colorGroupsTask).Count;
	}

	private async Task LoadColorTemplateGroupsAsync()
	{
		IReadOnlyList<ColorTemplateGroupRecord> records = await _templateLibraryService.GetColorGroupsAsync();
		ColorTemplateGroups = new ObservableCollection<ColorTemplateGroupViewModel>(records.Select(item => new ColorTemplateGroupViewModel(item)));
		ColorTemplateGroupCount = records.Count;
		if (_selectedColorTemplateGroupId <= 0 || ColorTemplateGroups.All(item => item.Id != _selectedColorTemplateGroupId))
		{
			_selectedColorTemplateGroupId = ColorTemplateGroups.FirstOrDefault()?.Id ?? 0;
		}
		if (_selectedGenerationColorTemplateGroupId <= 0 || ColorTemplateGroups.All(item => item.Id != _selectedGenerationColorTemplateGroupId))
		{
			_selectedGenerationColorTemplateGroupId = ColorTemplateGroups.FirstOrDefault()?.Id ?? 0;
		}
		if (_selectedSpBatchColorTemplateGroupId <= 0 || ColorTemplateGroups.All(item => item.Id != _selectedSpBatchColorTemplateGroupId))
		{
			_selectedSpBatchColorTemplateGroupId = ColorTemplateGroups.FirstOrDefault()?.Id ?? 0;
		}
		_selectedColorTemplateGroupForGeneration = ColorTemplateGroups.FirstOrDefault(item => item.Id == _selectedGenerationColorTemplateGroupId);
		_selectedSpBatchColorTemplateGroupForGeneration = ColorTemplateGroups.FirstOrDefault(item => item.Id == _selectedSpBatchColorTemplateGroupId);
		RefreshSpBatchColorSelectionForCurrentGroup();
		OnPropertyChanged("HasManagedTemplateItems");
		OnPropertyChanged("IsTemplateContentPanelVisible");
		OnPropertyChanged("SelectedColorTemplateGroupName");
		OnPropertyChanged("SelectedColorTemplateGroupForGeneration");
		OnPropertyChanged("SelectedSpBatchColorTemplateGroupForGeneration");
		OnPropertyChanged("SpBatchColorSelectionTitle");
		OnPropertyChanged("SpBatchColorSelectionSummaryText");
		_openSpBatchColorSelectionCommand.RaiseCanExecuteChanged();
		_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
	}

	private IReadOnlyList<ColorTemplateColorRecord> GetSelectedColorTemplateColors()
	{
		ColorTemplateGroupViewModel? group = ColorTemplateGroups.FirstOrDefault(item => item.Id == _selectedGenerationColorTemplateGroupId);
		return group?.Colors
			.Select((ColorTemplateColorViewModel color, int index) => color.ToRecord(group.Id, index))
			.ToArray()
			?? Array.Empty<ColorTemplateColorRecord>();
	}

	private ColorTemplateGroupViewModel? GetSelectedSpBatchColorTemplateGroup()
	{
		ColorTemplateGroupViewModel? group = ColorTemplateGroups.FirstOrDefault(item => item.Id == _selectedSpBatchColorTemplateGroupId);
		if (group != null)
		{
			return group;
		}
		return SelectedSpBatchColorTemplateGroupForGeneration != null && ColorTemplateGroups.Any(item => item.Id == SelectedSpBatchColorTemplateGroupForGeneration.Id)
			? SelectedSpBatchColorTemplateGroupForGeneration
			: ColorTemplateGroups.FirstOrDefault();
	}

	private IReadOnlyList<string> GetSelectedSpBatchColorNamesForCurrentGroup()
	{
		ColorTemplateGroupViewModel? group = GetSelectedSpBatchColorTemplateGroup();
		if (group == null || group.Colors.Count == 0)
		{
			return Array.Empty<string>();
		}

		if (!_spBatchSelectedColorNamesByGroupId.TryGetValue(group.Id, out HashSet<string>? selectedNames) || selectedNames.Count == 0)
		{
			return Array.Empty<string>();
		}

		string[] names = group.Colors
			.Where(item => selectedNames.Contains(item.Name.Trim()))
			.Select(item => item.Name.Trim())
			.ToArray();

		if (names.Length == 0 || names.Length >= group.Colors.Count)
		{
			return Array.Empty<string>();
		}

		return names;
	}

	private IReadOnlyList<ColorTemplateColorRecord> GetSelectedSpBatchColorTemplateColors()
	{
		ColorTemplateGroupViewModel? group = GetSelectedSpBatchColorTemplateGroup();
		if (group == null)
		{
			return Array.Empty<ColorTemplateColorRecord>();
		}

		IReadOnlyList<string> selectedNames = GetSelectedSpBatchColorNamesForCurrentGroup();
		if (selectedNames.Count == 0)
		{
			return group.Colors.Select(color => color.ToRecord(group.Id, color.SortOrder)).ToArray();
		}

		HashSet<string> selectedSet = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
		return group.Colors
			.Where(item => selectedSet.Contains(item.Name.Trim()))
			.Select(color => color.ToRecord(group.Id, color.SortOrder))
			.ToArray();
	}

	private void RefreshSpBatchColorSelectionForCurrentGroup()
	{
		foreach (ColorTemplateColorViewModel item in SpBatchColorSelectionColors)
		{
			item.PropertyChanged -= SpBatchColorSelectionItem_PropertyChanged;
		}
		SpBatchColorSelectionColors.Clear();
		ColorTemplateGroupViewModel? group = GetSelectedSpBatchColorTemplateGroup();
		if (group == null || group.Colors.Count == 0)
		{
			return;
		}

		bool selectAll = !_spBatchSelectedColorNamesByGroupId.TryGetValue(group.Id, out HashSet<string>? selectedNames) || selectedNames.Count == 0;
		foreach (ColorTemplateColorViewModel color in group.Colors)
		{
			ColorTemplateColorViewModel selectionItem = new ColorTemplateColorViewModel(color.ToRecord(group.Id, color.SortOrder));
			selectionItem.IsSelected = selectAll || (selectedNames != null && selectedNames.Contains(selectionItem.Name.Trim()));
			selectionItem.PropertyChanged += SpBatchColorSelectionItem_PropertyChanged;
			SpBatchColorSelectionColors.Add(selectionItem);
		}
	}

	private void SpBatchColorSelectionItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (string.Equals(e.PropertyName, "IsSelected", StringComparison.Ordinal))
		{
			OnPropertyChanged("SpBatchColorSelectionSummaryText");
			_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
		}
	}

	private void StoreSpBatchColorSelectionForCurrentGroup()
	{
		ColorTemplateGroupViewModel? group = GetSelectedSpBatchColorTemplateGroup();
		if (group == null)
		{
			return;
		}

		string[] selectedNames = SpBatchColorSelectionColors
			.Where(item => item.IsSelected && !string.IsNullOrWhiteSpace(item.Name))
			.Select(item => item.Name.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		if (selectedNames.Length == 0 || selectedNames.Length >= group.Colors.Count)
		{
			_spBatchSelectedColorNamesByGroupId.Remove(group.Id);
		}
		else
		{
			_spBatchSelectedColorNamesByGroupId[group.Id] = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
		}

		OnPropertyChanged("SpBatchColorSelectionSummaryText");
	}

	private TemplateItemViewModel CreateTemplateItemViewModel(TemplateItemRecord record)
	{
		return new TemplateItemViewModel(record, (record.Category == TemplateCategory.Scene) ? BuildSceneSubjectSummary(record.Id) : null, record.Category == TemplateCategory.Layout ? OnTemplateExportSelectionChanged : null);
	}

	private void OnTemplateExportSelectionChanged()
	{
		OnPropertyChanged("SelectedLayoutExportCount");
		OnPropertyChanged("LayoutExportSelectionText");
	}

	private void UpdateLayoutTemplateCounts(IReadOnlyList<TemplateItemRecord> records)
	{
		MainImageLayoutTemplateCount = records.Count((TemplateItemRecord item) => item.ImageType == ImageTemplateType.MainImage);
		SceneImageLayoutTemplateCount = records.Count((TemplateItemRecord item) => item.ImageType == ImageTemplateType.SceneImage);
		CompareImageLayoutTemplateCount = records.Count((TemplateItemRecord item) => item.ImageType == ImageTemplateType.CompareImage);
	}

	private void UpdateTitleTemplateCounts(IReadOnlyList<TemplateItemRecord> records)
	{
		MainTitleTemplateCount = records.Count(item => GetTitleTemplateType(item) == MainTitleTemplateType);
		SubTitleTemplateCount = records.Count(item => GetTitleTemplateType(item) == SubTitleTemplateType);
		IconWordTemplateCount = records.Count(item => GetTitleTemplateType(item) == IconWordTemplateType);
	}

	private static string GetTitleTemplateType(TemplateItemRecord item)
	{
		if (string.Equals(item.Subject, SubTitleTemplateType, StringComparison.Ordinal))
		{
			return SubTitleTemplateType;
		}
		if (string.Equals(item.Subject, IconWordTemplateType, StringComparison.Ordinal))
		{
			return IconWordTemplateType;
		}
		return MainTitleTemplateType;
	}

	private static string CreateTitleTemplateName(string content)
	{
		string text = content.ReplaceLineEndings(" ").Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return "标题";
		}
		return text.Length > 40 ? text.Substring(0, 40) : text;
	}

	private async Task LoadGenerationTemplateOptionsAsync()
	{
		HashSet<long> selectedIds = (from item in GenerationTemplateOptions
			where item.IsSelected
			select item.Id).ToHashSet();
		TemplateItemRecord[] array = (from item in await _templateLibraryService.GetByCategoryAsync(TemplateCategory.Layout, CurrentGenerationImageType)
			where item.IsEnabled
			orderby item.Id
			select item).ToArray();
		GenerationTemplateOptions.Clear();
		bool flag = selectedIds.Count == 0;
		TemplateItemRecord[] array2 = array;
		foreach (TemplateItemRecord templateItemRecord in array2)
		{
			GenerationTemplateOptions.Add(new GenerationTemplateOptionViewModel(templateItemRecord.Id, templateItemRecord.Name.Trim(), flag || selectedIds.Contains(templateItemRecord.Id), OnGenerationTemplateOptionSelectionChanged));
		}
		if (GenerationTemplateOptions.Count > 0 && GenerationTemplateOptions.All((GenerationTemplateOptionViewModel item) => !item.IsSelected))
		{
			foreach (GenerationTemplateOptionViewModel generationTemplateOption in GenerationTemplateOptions)
			{
				generationTemplateOption.IsSelected = true;
			}
		}
		OnPropertyChanged("GenerationTemplateSelectionText");
	}

	private void OnGenerationTemplateOptionSelectionChanged()
	{
		OnPropertyChanged("GenerationTemplateSelectionText");
	}

	private string BuildSceneSubjectSummary(long sceneTemplateId)
	{
		if (!_sceneSubjectBindingCache.TryGetValue(sceneTemplateId, out IReadOnlyList<long> value))
		{
			return string.Empty;
		}
		TemplateItemRecord value2;
		return string.Join("/", (from id in value
			select (!_subjectTemplateLookup.TryGetValue(id, out value2)) ? string.Empty : FormatSubjectDisplayName(value2.Name.Trim(), value2.IsEnabled) into text
			where !string.IsNullOrWhiteSpace(text)
			select text).Distinct<string>(StringComparer.OrdinalIgnoreCase));
	}

	private void NewTemplate()
	{
		if (IsColorTemplateTabSelected)
		{
			NewColorTemplateGroup();
			return;
		}
		ResetTemplateEditor();
		IsTemplateEditorDialogOpen = true;
	}

	private void ResetTemplateEditor(bool refreshSubjectOptions = true)
	{
		SelectedTemplateItem = null;
		TemplateEditorName = string.Empty;
		TemplateEditorContent = string.Empty;
		IsTemplateSubjectPickerOpen = false;
		SetTemplateSubjectTags(Array.Empty<long>());
		if (refreshSubjectOptions)
		{
			RefreshTemplateSubjectOptions();
		}
		SetSceneContentLines(string.Empty);
		TemplateEditorPreviewImagePath = string.Empty;
		TemplateEditorIsEnabled = true;
		IsTemplateEditorDialogOpen = false;
		OnPropertyChanged("TemplateEditorDialogTitle");
		_deleteTemplateCommand.RaiseCanExecuteChanged();
		_saveTemplateCommand.RaiseCanExecuteChanged();
	}

	private void SelectManagedTemplate(TemplateItemViewModel? item)
	{
		SelectedTemplateItem = item;
		if (item != null)
		{
			LoadTemplateEditor(item);
			IsTemplateEditorDialogOpen = true;
		}
	}

	private void LoadTemplateEditor(TemplateItemViewModel item)
	{
		if (item.Category == TemplateCategory.Title)
		{
			_selectedTitleTemplateType = GetTitleTemplateType(item.Model);
			OnPropertyChanged("IsMainTitleTemplateTypeSelected");
			OnPropertyChanged("IsSubTitleTemplateTypeSelected");
			OnPropertyChanged("IsIconWordTemplateTypeSelected");
			OnPropertyChanged("CurrentTemplateImportButtonText");
			OnPropertyChanged("CurrentTemplateExportButtonText");
		}
		TemplateEditorName = item.Category == TemplateCategory.Title ? string.Empty : item.Name;
		TemplateEditorContent = item.Content;
		IsTemplateSubjectPickerOpen = false;
		IReadOnlyList<long> sceneSubjectIdsForEditor = GetSceneSubjectIdsForEditor(item);
		bool removedDisabledSubjects;
		IReadOnlyList<long> readOnlyList = FilterEnabledSubjectIds(sceneSubjectIdsForEditor, out removedDisabledSubjects);
		IReadOnlyList<long> value;
		bool flag = !_sceneSubjectBindingCache.TryGetValue(item.Id, out value) || !AreSubjectBindingsEqual(value, readOnlyList);
		SetTemplateSubjectTags(readOnlyList);
		RefreshTemplateSubjectOptions();
		SetSceneContentLines(IsSceneTemplateTabSelected ? item.Content : string.Empty);
		TemplateEditorPreviewImagePath = item.PreviewImagePath;
		TemplateEditorIsEnabled = item.IsEnabled;
		if (IsSceneTemplateTabSelected && flag && item.Id > 0)
		{
			_ = SyncSceneSubjectBindingsOnOpenAsync(item.Id, readOnlyList);
		}
	}

	private bool CanSaveTemplate()
	{
		string message;
		return TryGetTemplateSaveValidationMessage(out message);
	}

	private bool TryGetTemplateSaveValidationMessage(out string message)
	{
		if (IsBusy)
		{
			message = "当前有任务正在执行，请稍后再保存模板。";
			return false;
		}
		if (IsTitleTemplateTabSelected)
		{
			if (string.IsNullOrWhiteSpace(TemplateEditorContent))
			{
				message = "请先填写元素内容。";
				return false;
			}
			message = string.Empty;
			return true;
		}
		if (string.IsNullOrWhiteSpace(TemplateEditorName))
		{
			message = "请先填写模板名称。";
			return false;
		}
		if (IsSceneTemplateTabSelected)
		{
			if (!SceneContentLines.Any((SceneContentLineViewModel line) => !string.IsNullOrWhiteSpace(line.Text)))
			{
				message = "请至少填写一条场景内容。";
				return false;
			}
			if (!TemplateSubjectTags.Any((TemplateSubjectTagViewModel tag) => tag.IsTag))
			{
				message = "请至少绑定一个主体。";
				return false;
			}
		}
		else if (IsTitleTemplateTabSelected)
		{
			if (!TitleMainLines.Any((SceneContentLineViewModel line) => !string.IsNullOrWhiteSpace(line.Text)))
			{
				message = "请至少填写一条大标题。";
				return false;
			}
			if (!TitleSubLines.Any((SceneContentLineViewModel line) => !string.IsNullOrWhiteSpace(line.Text)))
			{
				message = "请至少填写一条小标题。";
				return false;
			}
		}
		else if (!IsSubjectTemplateTabSelected && string.IsNullOrWhiteSpace(TemplateEditorContent))
		{
			message = "请先填写模板内容。";
			return false;
		}
		message = string.Empty;
		return true;
	}

	private async Task SaveTemplateAsync()
	{
		if (!TryGetTemplateSaveValidationMessage(out string message))
		{
			StatusMessage = message;
			System.Windows.MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		if (!CanSaveTemplate())
		{
			StatusMessage = "请填写模板名称和模板内容。";
			return;
		}
		if (IsTitleTemplateTabSelected && SelectedTemplateItem == null)
		{
			await SaveNewTitleTemplatesAsync();
			return;
		}
		string trimmedName = IsTitleTemplateTabSelected ? CreateTitleTemplateName(TemplateEditorContent) : TemplateEditorName.Trim();
		if (IsSubjectTemplateTabSelected && ManagedTemplateItems.FirstOrDefault((TemplateItemViewModel templateItemViewModel) => templateItemViewModel.Category == TemplateCategory.Subject && templateItemViewModel.Id != (SelectedTemplateItem?.Id ?? 0) && string.Equals(templateItemViewModel.Name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase)) != null)
		{
			StatusMessage = "主体模板“" + trimmedName + "”已存在，未重复添加。";
			System.Windows.MessageBox.Show("主体模板“" + trimmedName + "”已存在，请勿重复添加。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		TemplateItemRecord item = new TemplateItemRecord
		{
			Id = (SelectedTemplateItem?.Id ?? 0),
			Category = _selectedTemplateCategory,
			Name = trimmedName,
			Content = (IsSceneTemplateTabSelected ? JoinSceneContentLines() : (IsSubjectTemplateTabSelected ? trimmedName : TemplateEditorContent)),
			Subject = (IsSceneTemplateTabSelected ? JoinTemplateSubjectTagTexts() : (IsTitleTemplateTabSelected ? _selectedTitleTemplateType : string.Empty)),
			PreviewImagePath = (IsLayoutTemplateTabSelected ? TemplateEditorPreviewImagePath : string.Empty),
			ImageType = (IsLayoutTemplateTabSelected ? _selectedLayoutImageType : ImageTemplateType.MainImage),
			SortOrder = 0,
			IsEnabled = TemplateEditorIsEnabled
		};
		TemplateItemRecord saved = await _templateLibraryService.SaveAsync(item);
		if (saved.Category == TemplateCategory.Scene)
		{
			await _templateLibraryService.SetSceneSubjectBindingsAsync(saved.Id, (from tag in TemplateSubjectTags
				where tag.IsTag && tag.Id > 0
				select tag.Id).ToArray());
		}
		await ReloadManagedTemplatesAfterMutationAsync();
		SelectedTemplateItem = ManagedTemplateItems.FirstOrDefault((TemplateItemViewModel templateItemViewModel) => templateItemViewModel.Id == saved.Id);
		IsTemplateEditorDialogOpen = false;
		StatusMessage = "已保存" + TemplateManagerTitle + "：" + saved.Name;
	}

	private async Task SaveNewTitleTemplatesAsync()
	{
		string titleType = _selectedTitleTemplateType;
		string titleTypeName = CurrentVisualElementTypeName;
		IReadOnlyList<string> inputTitles = SplitTitleTemplateInputLines(TemplateEditorContent);
		if (inputTitles.Count == 0)
		{
			string message = "请至少填写一条" + titleTypeName + "。";
			StatusMessage = message;
			System.Windows.MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}

		IReadOnlyList<TemplateItemRecord> existingTitleTemplates = await _templateLibraryService.GetByCategoryAsync(TemplateCategory.Title);
		HashSet<string> existingContents = existingTitleTemplates
			.Where(item => GetTitleTemplateType(item) == titleType)
			.Select(item => NormalizeTitleTemplateContent(item.Content))
			.Where(text => !string.IsNullOrWhiteSpace(text))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		HashSet<string> inputContents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<long> savedIds = new List<long>();
		int skippedCount = 0;

		foreach (string inputTitle in inputTitles)
		{
			string normalizedTitle = NormalizeTitleTemplateContent(inputTitle);
			if (!inputContents.Add(normalizedTitle) || existingContents.Contains(normalizedTitle))
			{
				skippedCount++;
				continue;
			}

			TemplateItemRecord saved = await _templateLibraryService.SaveAsync(new TemplateItemRecord
			{
				Category = TemplateCategory.Title,
				Name = CreateTitleTemplateName(normalizedTitle),
				Content = normalizedTitle,
				Subject = titleType,
				PreviewImagePath = string.Empty,
				ImageType = ImageTemplateType.MainImage,
				SortOrder = 0,
				IsEnabled = TemplateEditorIsEnabled
			});
			existingContents.Add(normalizedTitle);
			savedIds.Add(saved.Id);
		}

		if (savedIds.Count == 0)
		{
			string message = inputTitles.Count == 1
				? $"{titleTypeName}“{CreateTitleTemplateName(inputTitles[0])}”已存在，未重复添加。"
				: $"没有新增{titleTypeName}，已跳过重复 {skippedCount} 条。";
			StatusMessage = message;
			System.Windows.MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}

		await ReloadManagedTemplatesAfterMutationAsync();
		SelectedTemplateItem = ManagedTemplateItems.FirstOrDefault(item => savedIds.Contains(item.Id));
		IsTemplateEditorDialogOpen = false;
		StatusMessage = skippedCount > 0
			? $"已新增 {savedIds.Count} 条{titleTypeName}，跳过重复 {skippedCount} 条。"
			: $"已新增 {savedIds.Count} 条{titleTypeName}。";
		if (skippedCount > 0)
		{
			System.Windows.MessageBox.Show(StatusMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private void NewColorTemplateGroup()
	{
		_selectedColorTemplateGroup = null;
		ColorTemplateGroupEditorName = string.Empty;
		ColorTemplateEditorColors.Clear();
		AddColorTemplateEditorRow();
		OnPropertyChanged("ColorTemplateGroupEditorTitle");
		IsColorTemplateGroupEditorOpen = true;
	}

	private void SelectColorTemplateGroup(ColorTemplateGroupViewModel? item)
	{
		if (item == null)
		{
			return;
		}
		_selectedColorTemplateGroup = item;
		_selectedColorTemplateGroupId = item.Id;
		ColorTemplateGroupEditorName = item.Name;
		ColorTemplateEditorColors.Clear();
		foreach (ColorTemplateColorViewModel color in item.Colors)
		{
			ColorTemplateEditorColors.Add(new ColorTemplateColorViewModel(color.ToRecord(item.Id, color.SortOrder)));
		}
		if (ColorTemplateEditorColors.Count == 0)
		{
			AddColorTemplateEditorRow();
		}
		OnPropertyChanged("ColorTemplateGroupEditorTitle");
		OnPropertyChanged("SelectedColorTemplateGroupName");
		IsColorTemplateGroupEditorOpen = true;
	}

	private void AddColorTemplateEditorRow()
	{
		ColorTemplateEditorColors.Add(new ColorTemplateColorViewModel());
		_saveColorTemplateGroupCommand.RaiseCanExecuteChanged();
		_removeColorTemplateRowCommand.RaiseCanExecuteChanged();
	}

	private void RemoveColorTemplateEditorRow(ColorTemplateColorViewModel? item)
	{
		if (item != null && ColorTemplateEditorColors.Count > 1 && ColorTemplateEditorColors.Contains(item))
		{
			ColorTemplateEditorColors.Remove(item);
			_saveColorTemplateGroupCommand.RaiseCanExecuteChanged();
			_removeColorTemplateRowCommand.RaiseCanExecuteChanged();
		}
	}

	private void OpenColorTemplateColorPreview(ColorTemplateColorViewModel? item)
	{
		if (item == null)
		{
			return;
		}
		SelectedColorTemplateColorPreview = item;
		IsColorTemplateColorPreviewOpen = true;
	}

	private void CloseColorTemplateColorPreview()
	{
		IsColorTemplateColorPreviewOpen = false;
		SelectedColorTemplateColorPreview = null;
	}

	private bool CanSaveColorTemplateGroup()
	{
		return !IsBusy && IsColorTemplateGroupEditorOpen;
	}

	private async Task SaveColorTemplateGroupAsync()
	{
		string groupName = ColorTemplateGroupEditorName.Trim();
		if (string.IsNullOrWhiteSpace(groupName))
		{
			StatusMessage = "请填写颜色组名称。";
			MessageBox.Show(StatusMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		if (ColorTemplateEditorColors.Count == 0)
		{
			StatusMessage = "请至少添加一个颜色。";
			MessageBox.Show(StatusMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		List<ColorTemplateColorRecord> colors = new List<ColorTemplateColorRecord>();
		HashSet<string> colorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (int index = 0; index < ColorTemplateEditorColors.Count; index++)
		{
			ColorTemplateColorViewModel color = ColorTemplateEditorColors[index];
			string colorName = color.Name.Trim();
			string hexCode = color.HexCode.Trim();
			if (string.IsNullOrWhiteSpace(colorName))
			{
				StatusMessage = $"第 {index + 1} 行颜色中文名不能为空。";
				MessageBox.Show(StatusMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			if (string.IsNullOrWhiteSpace(hexCode))
			{
				StatusMessage = $"第 {index + 1} 行颜色 HEX 不能为空。";
				MessageBox.Show(StatusMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			if (!ColorTemplateColorViewModel.IsValidHex(hexCode))
			{
				StatusMessage = $"第 {index + 1} 行颜色 HEX 格式不正确，请使用 #RRGGBB。";
				MessageBox.Show(StatusMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			if (!colorNames.Add(colorName))
			{
				StatusMessage = $"颜色“{colorName}”重复，请修改后再保存。";
				MessageBox.Show(StatusMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			colors.Add(new ColorTemplateColorRecord
			{
				Name = colorName,
				HexCode = ColorTemplateColorViewModel.NormalizeHex(hexCode),
				SortOrder = index
			});
		}
		ColorTemplateGroupRecord saved = await _templateLibraryService.SaveColorGroupAsync(new ColorTemplateGroupRecord
		{
			Id = _selectedColorTemplateGroup?.Id ?? 0,
			Name = groupName,
			SortOrder = _selectedColorTemplateGroup?.Model.SortOrder ?? ColorTemplateGroups.Count,
			IsEnabled = true,
			Colors = colors
		});
		_selectedColorTemplateGroupId = saved.Id;
		CloseColorTemplateGroupEditor();
		await LoadColorTemplateGroupsAsync();
		StatusMessage = "已保存颜色组：" + saved.Name;
	}

	private async Task DeleteColorTemplateGroupAsync(ColorTemplateGroupViewModel? item)
	{
		if (item == null)
		{
			return;
		}
		if (MessageBox.Show($"确定删除颜色组“{item.Name}”吗？", "确认删除", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
		{
			return;
		}
		await _templateLibraryService.DeleteColorGroupAsync(item.Id);
		if (_selectedColorTemplateGroupId == item.Id)
		{
			_selectedColorTemplateGroupId = 0;
		}
		await LoadColorTemplateGroupsAsync();
		StatusMessage = "已删除颜色组：" + item.Name;
	}

	private void CloseColorTemplateGroupEditor()
	{
		IsColorTemplateGroupEditorOpen = false;
		CloseColorTemplateColorPreview();
		_selectedColorTemplateGroup = null;
		ColorTemplateGroupEditorName = string.Empty;
		ColorTemplateEditorColors.Clear();
		OnPropertyChanged("ColorTemplateGroupEditorTitle");
	}

	private static IReadOnlyList<string> SplitTitleTemplateInputLines(string content)
	{
		return content
			.ReplaceLineEndings("\n")
			.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(NormalizeTitleTemplateContent)
			.Where(text => !string.IsNullOrWhiteSpace(text))
			.ToArray();
	}

	private static string NormalizeTitleTemplateContent(string content)
	{
		return content.Trim();
	}

	private IReadOnlyList<long> GetSceneSubjectIdsForEditor(TemplateItemViewModel item)
	{
		return (from text in item.Subject.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			select _subjectTemplateLookup.Values.FirstOrDefault((TemplateItemRecord subject) => string.Equals(subject.Name, text, StringComparison.OrdinalIgnoreCase) || string.Equals(subject.Content, text, StringComparison.OrdinalIgnoreCase))?.Id ?? 0 into id
			where id > 0
			select id).Distinct().ToArray();
	}

	private void SetTemplateSubjectTags(IEnumerable<long> subjectIds)
	{
		TemplateSubjectTags.Clear();
		foreach (long item in subjectIds.Where((long id) => id > 0).Distinct())
		{
			if (_subjectTemplateLookup.TryGetValue(item, out TemplateItemRecord value))
			{
				TemplateSubjectTags.Add(TemplateSubjectTagViewModel.Create(item, value.Name, value.IsEnabled));
			}
		}
		TemplateSubjectTags.Add(TemplateSubjectTagViewModel.CreateAddButton());
		OnPropertyChanged("HasTemplateSubjectTags");
		_saveTemplateCommand.RaiseCanExecuteChanged();
	}

	private IReadOnlyList<long> FilterEnabledSubjectIds(IEnumerable<long> subjectIds, out bool removedDisabledSubjects)
	{
		removedDisabledSubjects = false;
		List<long> list = new List<long>();
		foreach (long item in subjectIds.Where((long id) => id > 0).Distinct())
		{
			if (!_subjectTemplateLookup.TryGetValue(item, out TemplateItemRecord value))
			{
				removedDisabledSubjects = true;
			}
			else if (!value.IsEnabled)
			{
				removedDisabledSubjects = true;
			}
			else
			{
				list.Add(item);
			}
		}
		return list;
	}

	private static bool AreSubjectBindingsEqual(IReadOnlyList<long> left, IReadOnlyList<long> right)
	{
		if (left == right)
		{
			return true;
		}
		if (left.Count != right.Count)
		{
			return false;
		}
		for (int i = 0; i < left.Count; i++)
		{
			if (left[i] != right[i])
			{
				return false;
			}
		}
		return true;
	}

	private async Task SyncSceneSubjectBindingsOnOpenAsync(long sceneTemplateId, IReadOnlyList<long> enabledSubjectIds)
	{
		try
		{
			await _templateLibraryService.SetSceneSubjectBindingsAsync(sceneTemplateId, enabledSubjectIds);
			_sceneSubjectBindingCache[sceneTemplateId] = enabledSubjectIds.ToArray();
			TemplateItemViewModel templateItemViewModel = ManagedTemplateItems.FirstOrDefault((TemplateItemViewModel item) => item.Id == sceneTemplateId);
			templateItemViewModel?.Update(templateItemViewModel.Model, BuildSceneSubjectSummary(sceneTemplateId));
			StatusMessage = "已按主体启用状态自动刷新场景绑定。";
		}
		catch
		{
		}
	}

	private void OpenTemplateSubjectPicker()
	{
		RefreshTemplateSubjectOptions();
		IsTemplateSubjectPickerOpen = true;
	}

	private void StartAddTemplateSubjectTag()
	{
		OpenTemplateSubjectPicker();
	}

	private async Task CommitTemplateSubjectTagAsync()
	{
		string subjectName = NewTemplateSubjectText.Trim();
		if (string.IsNullOrWhiteSpace(subjectName))
		{
			CancelTemplateSubjectTagInput();
			return;
		}
		TemplateItemRecord existingSubject = _subjectTemplateLookup.Values.FirstOrDefault((TemplateItemRecord subject) => string.Equals(subject.Name.Trim(), subjectName, StringComparison.OrdinalIgnoreCase) || string.Equals(subject.Content.Trim(), subjectName, StringComparison.OrdinalIgnoreCase));
		if (existingSubject != null && TemplateSubjectTags.Any((TemplateSubjectTagViewModel tag) => tag.IsTag && tag.Id == existingSubject.Id))
		{
			CancelTemplateSubjectTagInput();
			StatusMessage = "主体“" + subjectName + "”已存在，未重复添加。";
			System.Windows.MessageBox.Show("主体“" + subjectName + "”已存在，未重复添加。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		if (existingSubject != null && !existingSubject.IsEnabled)
		{
			CancelTemplateSubjectTagInput();
			StatusMessage = "主体“" + subjectName + "”已停用，请先启用后再添加。";
			System.Windows.MessageBox.Show("主体“" + subjectName + "”已停用，请先启用后再添加。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		TemplateItemRecord subjectToAdd = existingSubject ?? await _templateLibraryService.SaveAsync(new TemplateItemRecord
		{
			Category = TemplateCategory.Subject,
			Name = subjectName,
			Content = subjectName,
			Subject = string.Empty,
			PreviewImagePath = string.Empty,
			ImageType = ImageTemplateType.MainImage,
			SortOrder = 0,
			IsEnabled = true
		});
		_subjectTemplateLookup[subjectToAdd.Id] = subjectToAdd;
		TemplateSubjectTagViewModel inputTag = TemplateSubjectTags.FirstOrDefault((TemplateSubjectTagViewModel tag) => tag.IsInput);
		if (inputTag != null)
		{
			TemplateSubjectTags.Remove(inputTag);
		}
		TemplateSubjectTagViewModel addButton = TemplateSubjectTags.FirstOrDefault((TemplateSubjectTagViewModel tag) => tag.IsAddButton);
		TemplateSubjectTagViewModel newTag = TemplateSubjectTagViewModel.Create(subjectToAdd.Id, subjectToAdd.Name, subjectToAdd.IsEnabled);
		if (addButton == null)
		{
			TemplateSubjectTags.Add(newTag);
			TemplateSubjectTags.Add(TemplateSubjectTagViewModel.CreateAddButton());
		}
		else
		{
			TemplateSubjectTags.Insert(TemplateSubjectTags.IndexOf(addButton), newTag);
		}
		NewTemplateSubjectText = string.Empty;
		RefreshTemplateSubjectOptions();
		OnPropertyChanged("HasTemplateSubjectTags");
		_saveTemplateCommand.RaiseCanExecuteChanged();
		StatusMessage = "已添加主体：" + subjectToAdd.Name;
	}

	private void CancelTemplateSubjectTagInput()
	{
		TemplateSubjectTagViewModel inputTag = TemplateSubjectTags.FirstOrDefault((TemplateSubjectTagViewModel tag) => tag.IsInput);
		if (inputTag != null)
		{
			TemplateSubjectTags.Remove(inputTag);
		}
		NewTemplateSubjectText = string.Empty;
		OnPropertyChanged("HasTemplateSubjectTags");
	}

	private void CloseTemplateSubjectPicker()
	{
		IsTemplateSubjectPickerOpen = false;
	}

	private void RefreshTemplateSubjectOptions()
	{
		HashSet<long> hashSet = (from tag in TemplateSubjectTags
			where tag.IsTag
			select tag.Id).ToHashSet();
		TemplateSubjectOptions.Clear();
		foreach (TemplateItemRecord item in _subjectTemplateLookup.Values.OrderBy((TemplateItemRecord item) => item.Id))
		{
			TemplateSubjectOptions.Add(new TemplateSubjectOptionViewModel(item.Id, item.Name, item.IsEnabled)
			{
				IsSelected = hashSet.Contains(item.Id)
			});
		}
		OnPropertyChanged("HasTemplateSubjectOptions");
	}

	private void ToggleTemplateSubjectOption(TemplateSubjectOptionViewModel? option)
	{
		if (option == null)
		{
			return;
		}
		if (option.IsSelected)
		{
			option.IsSelected = false;
			TemplateSubjectTagViewModel templateSubjectTagViewModel = TemplateSubjectTags.FirstOrDefault((TemplateSubjectTagViewModel tag) => tag.IsTag && tag.Id == option.Id);
			if (templateSubjectTagViewModel != null)
			{
				TemplateSubjectTags.Remove(templateSubjectTagViewModel);
			}
		}
		else
		{
			option.IsSelected = true;
			TemplateSubjectTagViewModel templateSubjectTagViewModel2 = TemplateSubjectTags.FirstOrDefault((TemplateSubjectTagViewModel tag) => tag.IsAddButton);
			if (!TemplateSubjectTags.Any((TemplateSubjectTagViewModel tag) => tag.IsTag && tag.Id == option.Id))
			{
				TemplateSubjectTagViewModel item = TemplateSubjectTagViewModel.Create(option.Id, option.Name, option.IsEnabled);
				if (templateSubjectTagViewModel2 == null)
				{
					TemplateSubjectTags.Add(item);
				}
				else
				{
					int index = TemplateSubjectTags.IndexOf(templateSubjectTagViewModel2);
					TemplateSubjectTags.Insert(index, item);
				}
			}
		}
		OnPropertyChanged("HasTemplateSubjectTags");
		_saveTemplateCommand.RaiseCanExecuteChanged();
	}

	private void RemoveTemplateSubjectTag(TemplateSubjectTagViewModel? tag)
	{
		if (tag == null)
		{
			return;
		}
		if (tag.IsAddButton)
		{
			OpenTemplateSubjectPicker();
			return;
		}
		TemplateSubjectTags.Remove(tag);
		TemplateSubjectOptionViewModel templateSubjectOptionViewModel = TemplateSubjectOptions.FirstOrDefault((TemplateSubjectOptionViewModel item) => item.Id == tag.Id);
		if (templateSubjectOptionViewModel != null)
		{
			templateSubjectOptionViewModel.IsSelected = false;
		}
		OnPropertyChanged("HasTemplateSubjectTags");
		_saveTemplateCommand.RaiseCanExecuteChanged();
	}

	private string JoinTemplateSubjectTagTexts()
	{
		return string.Join("/", from tag in TemplateSubjectTags
			where tag.IsTag
			select tag.Text.Trim() into text
			where !string.IsNullOrWhiteSpace(text)
			select text);
	}

	private static string FormatSubjectDisplayName(string name, bool isEnabled)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return string.Empty;
		}
		if (!isEnabled)
		{
			return name + "（停用）";
		}
		return name;
	}

	private void SetSceneContentLines(string content)
	{
		foreach (SceneContentLineViewModel sceneContentLine in SceneContentLines)
		{
			sceneContentLine.PropertyChanged -= OnSceneContentLineChanged;
		}
		SceneContentLines.Clear();
		foreach (string item in content.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).DefaultIfEmpty(string.Empty))
		{
			AddSceneContentLineInternal(item);
		}
		RefreshSceneContentLineStates();
		_saveTemplateCommand.RaiseCanExecuteChanged();
	}

	private void AddSceneContentLine(SceneContentLineViewModel? currentLine)
	{
		int num = ((currentLine == null) ? SceneContentLines.Count : (SceneContentLines.IndexOf(currentLine) + 1));
		if (num < 0)
		{
			num = SceneContentLines.Count;
		}
		SceneContentLines.Insert(num, CreateSceneContentLine(string.Empty));
		RefreshSceneContentLineStates();
		_saveTemplateCommand.RaiseCanExecuteChanged();
	}

	private void RemoveSceneContentLine(SceneContentLineViewModel? line)
	{
		if (line != null && SceneContentLines.Count > 1)
		{
			line.PropertyChanged -= OnSceneContentLineChanged;
			SceneContentLines.Remove(line);
			RefreshSceneContentLineStates();
			_saveTemplateCommand.RaiseCanExecuteChanged();
		}
	}

	private void AddSceneContentLineInternal(string text)
	{
		SceneContentLines.Add(CreateSceneContentLine(text));
	}

	private SceneContentLineViewModel CreateSceneContentLine(string text)
	{
		SceneContentLineViewModel sceneContentLineViewModel = new SceneContentLineViewModel(text);
		sceneContentLineViewModel.PropertyChanged += OnSceneContentLineChanged;
		return sceneContentLineViewModel;
	}

	private void RefreshSceneContentLineStates()
	{
		for (int i = 0; i < SceneContentLines.Count; i++)
		{
			SceneContentLines[i].CanAdd = i == SceneContentLines.Count - 1;
			SceneContentLines[i].CanRemove = SceneContentLines.Count > 1;
		}
		_removeSceneContentLineCommand.RaiseCanExecuteChanged();
	}

	private void OnSceneContentLineChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "Text")
		{
			_saveTemplateCommand.RaiseCanExecuteChanged();
		}
	}

	private string JoinSceneContentLines()
	{
		return string.Join("/", from line in SceneContentLines
			select line.Text.Trim() into text
			where !string.IsNullOrWhiteSpace(text)
			select text);
	}

	private void SetTitleTemplateLines(string content)
	{
		foreach (SceneContentLineViewModel line in TitleMainLines)
		{
			line.PropertyChanged -= OnTitleTemplateLineChanged;
		}
		foreach (SceneContentLineViewModel line in TitleSubLines)
		{
			line.PropertyChanged -= OnTitleTemplateLineChanged;
		}
		TitleMainLines.Clear();
		TitleSubLines.Clear();
		TitleTemplatePayload payload = ParseTitleTemplatePayload(content);
		foreach (string text in payload.MainTitles.DefaultIfEmpty(string.Empty))
		{
			TitleMainLines.Add(CreateTitleTemplateLine(text));
		}
		foreach (string text in payload.SubTitles.DefaultIfEmpty(string.Empty))
		{
			TitleSubLines.Add(CreateTitleTemplateLine(text));
		}
		RefreshTitleTemplateLineStates();
		_saveTemplateCommand.RaiseCanExecuteChanged();
	}

	private void AddTitleMainLine()
	{
		TitleMainLines.Add(CreateTitleTemplateLine(string.Empty));
		RefreshTitleTemplateLineStates();
		_saveTemplateCommand.RaiseCanExecuteChanged();
	}

	private void RemoveTitleMainLine(SceneContentLineViewModel? line)
	{
		if (line != null && TitleMainLines.Count > 1)
		{
			line.PropertyChanged -= OnTitleTemplateLineChanged;
			TitleMainLines.Remove(line);
			RefreshTitleTemplateLineStates();
			_saveTemplateCommand.RaiseCanExecuteChanged();
		}
	}

	private void AddTitleSubLine()
	{
		TitleSubLines.Add(CreateTitleTemplateLine(string.Empty));
		RefreshTitleTemplateLineStates();
		_saveTemplateCommand.RaiseCanExecuteChanged();
	}

	private void RemoveTitleSubLine(SceneContentLineViewModel? line)
	{
		if (line != null && TitleSubLines.Count > 1)
		{
			line.PropertyChanged -= OnTitleTemplateLineChanged;
			TitleSubLines.Remove(line);
			RefreshTitleTemplateLineStates();
			_saveTemplateCommand.RaiseCanExecuteChanged();
		}
	}

	private SceneContentLineViewModel CreateTitleTemplateLine(string text)
	{
		SceneContentLineViewModel line = new SceneContentLineViewModel(text);
		line.PropertyChanged += OnTitleTemplateLineChanged;
		return line;
	}

	private void RefreshTitleTemplateLineStates()
	{
		foreach (SceneContentLineViewModel line in TitleMainLines)
		{
			line.CanRemove = TitleMainLines.Count > 1;
			line.CanAdd = false;
		}
		foreach (SceneContentLineViewModel line in TitleSubLines)
		{
			line.CanRemove = TitleSubLines.Count > 1;
			line.CanAdd = false;
		}
		_removeTitleMainLineCommand.RaiseCanExecuteChanged();
		_removeTitleSubLineCommand.RaiseCanExecuteChanged();
	}

	private void OnTitleTemplateLineChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "Text")
		{
			_saveTemplateCommand.RaiseCanExecuteChanged();
		}
	}

	private string SerializeTitleTemplateContent()
	{
		TitleTemplatePayload payload = new TitleTemplatePayload
		{
			MainTitles = TitleMainLines
				.Select(line => line.Text.Trim())
				.Where(text => !string.IsNullOrWhiteSpace(text))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList(),
			SubTitles = TitleSubLines
				.Select(line => line.Text.Trim())
				.Where(text => !string.IsNullOrWhiteSpace(text))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList()
		};
		return JsonSerializer.Serialize(payload, new JsonSerializerOptions
		{
			WriteIndented = true
		});
	}

	private static TitleTemplatePayload ParseTitleTemplatePayload(string content)
	{
		if (!string.IsNullOrWhiteSpace(content))
		{
			try
			{
				TitleTemplatePayload? payload = JsonSerializer.Deserialize<TitleTemplatePayload>(content);
				if (payload != null)
				{
					payload.MainTitles = payload.MainTitles
						.Where(text => !string.IsNullOrWhiteSpace(text))
						.Select(text => text.Trim())
						.ToList();
					payload.SubTitles = payload.SubTitles
						.Where(text => !string.IsNullOrWhiteSpace(text))
						.Select(text => text.Trim())
						.ToList();
					return payload;
				}
			}
			catch (JsonException)
			{
			}
		}
		return new TitleTemplatePayload();
	}

	private async Task DeleteTemplateAsync()
	{
		if (SelectedTemplateItem != null && SelectedTemplateItem.Id > 0)
		{
			string name = SelectedTemplateItem.Name;
			await _templateLibraryService.DeleteAsync(SelectedTemplateItem.Id);
			await ReloadManagedTemplatesAfterMutationAsync();
			IsTemplateEditorDialogOpen = false;
			StatusMessage = "已删除" + TemplateManagerTitle + "：" + name;
		}
	}

	private async Task DeleteManagedTemplateAsync(TemplateItemViewModel? item)
	{
		if (item != null && item.Id > 0)
		{
			string name = item.Name;
			bool wasEditing = SelectedTemplateItem?.Id == item.Id;
			await _templateLibraryService.DeleteAsync(item.Id);
			await ReloadManagedTemplatesAfterMutationAsync();
			if (wasEditing)
			{
				IsTemplateEditorDialogOpen = false;
			}
			StatusMessage = "已删除" + TemplateManagerTitle + "：" + name;
		}
	}

	private async Task CopyManagedLayoutTemplateAsync(TemplateItemViewModel? item)
	{
		if (item == null || item.Category != TemplateCategory.Layout)
		{
			return;
		}
		string previewImagePath = string.Empty;
		if (!string.IsNullOrWhiteSpace(item.PreviewImagePath) && File.Exists(item.PreviewImagePath))
		{
			previewImagePath = await _templateLibraryService.ImportPreviewImageAsync(item.PreviewImagePath);
		}
		string copyName = CreateLayoutTemplateCopyName(item.Name);
		TemplateItemRecord saved = await _templateLibraryService.SaveAsync(new TemplateItemRecord
		{
			Category = TemplateCategory.Layout,
			Name = copyName,
			Content = item.Content,
			Subject = string.Empty,
			PreviewImagePath = previewImagePath,
			ImageType = item.ImageType,
			SortOrder = item.Model.SortOrder,
			IsEnabled = item.IsEnabled
		});
		await ReloadManagedTemplatesAfterMutationAsync();
		SelectedTemplateItem = ManagedTemplateItems.FirstOrDefault((TemplateItemViewModel templateItemViewModel) => templateItemViewModel.Id == saved.Id);
		StatusMessage = $"已复制布局模板：{saved.Name}";
	}

	private string CreateLayoutTemplateCopyName(string sourceName)
	{
		string baseName = string.IsNullOrWhiteSpace(sourceName) ? "布局模板" : sourceName.Trim();
		string copyBaseName = baseName.EndsWith(" - 副本", StringComparison.OrdinalIgnoreCase) ? baseName : baseName + " - 副本";
		HashSet<string> existingNames = ManagedTemplateItems
			.Where((TemplateItemViewModel item) => item.Category == TemplateCategory.Layout && item.ImageType == _selectedLayoutImageType)
			.Select((TemplateItemViewModel item) => item.Name.Trim())
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (!existingNames.Contains(copyBaseName))
		{
			return copyBaseName;
		}
		int index = 2;
		string candidate;
		do
		{
			candidate = copyBaseName + index.ToString(CultureInfo.InvariantCulture);
			index++;
		}
		while (existingNames.Contains(candidate));
		return candidate;
	}

	private async Task ImportLayoutTemplatesAsync()
	{
		TemplateCategory category = _selectedTemplateCategory;
		System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog
		{
			Title = $"导入{GetTemplateCategoryDisplayName(category)}",
			Filter = "EcomTool 模板包|*.ecomtpl|所有文件|*.*",
			Multiselect = false
		};
		try
		{
			if (dialog.ShowDialog() == DialogResult.OK)
			{
				int count = category switch
				{
					TemplateCategory.Layout => await _templateLibraryService.ImportLayoutTemplatesAsync(dialog.FileName),
					TemplateCategory.Color => await _templateLibraryService.ImportColorTemplatesAsync(dialog.FileName),
					_ => await _templateLibraryService.ImportTemplateCategoryAsync(dialog.FileName, category, category == TemplateCategory.Title ? _selectedTitleTemplateType : null)
				};
				await ReloadManagedTemplatesAfterMutationAsync();
				StatusMessage = $"已导入{GetTemplateCategoryDisplayName(category)}：{count} 个";
			}
		}
		finally
		{
			((IDisposable)(object)dialog)?.Dispose();
		}
	}

	private async Task ExportLayoutTemplatesAsync()
	{
		TemplateCategory category = _selectedTemplateCategory;
		System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog
		{
			Title = $"导出{GetTemplateCategoryDisplayName(category)}",
			Filter = "EcomTool 模板包|*.ecomtpl",
			DefaultExt = "ecomtpl",
			AddExtension = true,
			FileName = GetTemplateCategoryPackageFileName(category)
		};
		try
		{
			if (dialog.ShowDialog() == DialogResult.OK)
			{
				if (category == TemplateCategory.Layout)
				{
					long[] selectedIds = ManagedTemplateItems.Where((TemplateItemViewModel item) => item.IsSelectedForExport).Select((TemplateItemViewModel item) => item.Id).ToArray();
					int value = await _templateLibraryService.ExportLayoutTemplatesAsync(dialog.FileName, _selectedLayoutImageType, selectedIds.Length > 0 ? selectedIds : null);
					StatusMessage = selectedIds.Length > 0 ? $"已导出选中布局模板：{value} 个" : $"已导出布局模板：{value} 个";
				}
				else if (category == TemplateCategory.Color)
				{
					int value = await _templateLibraryService.ExportColorTemplatesAsync(dialog.FileName);
					StatusMessage = $"已导出{GetTemplateCategoryDisplayName(category)}：{value} 个";
				}
				else
				{
					int value = await _templateLibraryService.ExportTemplateCategoryAsync(dialog.FileName, category, category == TemplateCategory.Title ? _selectedTitleTemplateType : null);
					StatusMessage = $"已导出{GetTemplateCategoryDisplayName(category)}：{value} 个";
				}
			}
		}
		finally
		{
			((IDisposable)(object)dialog)?.Dispose();
		}
	}

	private static string GetTemplateCategoryDisplayName(TemplateCategory category)
	{
		return category switch
		{
			TemplateCategory.Scene => "场景模板",
			TemplateCategory.Subject => "主体模板",
			TemplateCategory.Title => "视觉元素",
			TemplateCategory.Color => "颜色模板",
			_ => "布局模板"
		};
	}

	private static string GetTemplateCategoryPackagePrefix(TemplateCategory category)
	{
		return category switch
		{
			TemplateCategory.Scene => "scene_templates",
			TemplateCategory.Subject => "subject_templates",
			TemplateCategory.Title => "visual_elements",
			TemplateCategory.Color => "color_templates",
			_ => "layout_templates"
		};
	}

	private static string GetTemplateCategoryPackageFileName(TemplateCategory category)
	{
		return $"{GetTemplateCategoryPackagePrefix(category)}_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.ecomtpl";
	}

	private async Task ImportAllTemplatesAsync()
	{
		System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog
		{
			Title = "导入全部模板",
			Filter = "EcomTool 模板包|*.ecomtpl|所有文件|*.*",
			Multiselect = false
		};
		try
		{
			if (dialog.ShowDialog() == DialogResult.OK)
			{
				int count = await _templateLibraryService.ImportAllTemplatesAsync(dialog.FileName);
				await ReloadManagedTemplatesAfterMutationAsync();
				StatusMessage = $"已导入全部模板：{count} 个";
			}
		}
		finally
		{
			((IDisposable)(object)dialog)?.Dispose();
		}
	}

	private async Task ExportAllTemplatesAsync()
	{
		System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog
		{
			Title = "导出全部模板",
			Filter = "EcomTool 模板包|*.ecomtpl",
			DefaultExt = "ecomtpl",
			AddExtension = true,
			FileName = $"all_templates_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.ecomtpl"
		};
		try
		{
			if (dialog.ShowDialog() == DialogResult.OK)
			{
				int value = await _templateLibraryService.ExportAllTemplatesAsync(dialog.FileName);
				StatusMessage = $"已导出全部模板：{value} 个";
			}
		}
		finally
		{
			((IDisposable)(object)dialog)?.Dispose();
		}
	}

	private async Task ChooseTemplatePreviewAsync()
	{
		System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog
		{
			Title = "选择布局模板预览图",
			Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp|所有文件|*.*",
			Multiselect = false
		};
		try
		{
			if (dialog.ShowDialog() == DialogResult.OK)
			{
				TemplateEditorPreviewImagePath = await _templateLibraryService.ImportPreviewImageAsync(dialog.FileName);
				_saveTemplateCommand.RaiseCanExecuteChanged();
				StatusMessage = "已导入布局模板预览图。";
			}
		}
		finally
		{
			((IDisposable)(object)dialog)?.Dispose();
		}
	}

	private void OpenTemplateEditorPreview()
	{
		if (!File.Exists(TemplateEditorPreviewImagePath))
		{
			return;
		}
		try
		{
			ShellOpenHelper.OpenFile(TemplateEditorPreviewImagePath);
		}
		catch (Exception ex)
		{
			MessageBox.Show("打开预览图失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void NotifyTemplateCategoryChanged()
	{
		OnPropertyChanged("IsLayoutTemplateTabSelected");
		OnPropertyChanged("IsSceneTemplateTabSelected");
		OnPropertyChanged("IsSubjectTemplateTabSelected");
		OnPropertyChanged("IsTitleTemplateTabSelected");
		OnPropertyChanged("IsColorTemplateTabSelected");
		OnPropertyChanged("HasManagedTemplateItems");
		OnPropertyChanged("IsTemplateContentPanelVisible");
		OnPropertyChanged("TemplateEditorDialogHeight");
		OnPropertyChanged("IsMainImageLayoutTypeSelected");
		OnPropertyChanged("IsSceneImageLayoutTypeSelected");
		OnPropertyChanged("IsCompareImageLayoutTypeSelected");
		OnPropertyChanged("IsMainTitleTemplateTypeSelected");
		OnPropertyChanged("IsSubTitleTemplateTypeSelected");
		OnPropertyChanged("IsIconWordTemplateTypeSelected");
		OnPropertyChanged("SelectedLayoutImageTypeText");
		OnPropertyChanged("TemplateManagerTitle");
		OnPropertyChanged("TemplateManagerDescription");
		OnPropertyChanged("TemplateEditorContentLabel");
		OnPropertyChanged("TemplateEmptyText");
		OnPropertyChanged("CurrentTemplateImportButtonText");
		OnPropertyChanged("CurrentTemplateExportButtonText");
		OnPropertyChanged("CurrentNewTemplateButtonText");
		OnPropertyChanged("IsTemplateEditorPreviewVisible");
		_openTemplateEditorPreviewCommand.RaiseCanExecuteChanged();
		OnPropertyChanged("IsTemplateEditorSubjectVisible");
		OnPropertyChanged("IsTemplateEditorContentVisible");
		OnPropertyChanged("IsTemplateEditorContentLabelVisible");
		OnPropertyChanged("IsTemplateEditorNameVisible");
		OnPropertyChanged("IsTemplateEditorEnabledVisible");
		OnPropertyChanged("HasTemplateSubjectTags");
		OnPropertyChanged("HasTemplateSubjectOptions");
		_chooseTemplatePreviewCommand.RaiseCanExecuteChanged();
		_importLayoutTemplatesCommand.RaiseCanExecuteChanged();
		_exportLayoutTemplatesCommand.RaiseCanExecuteChanged();
		_openTemplateSubjectPickerCommand.RaiseCanExecuteChanged();
		_saveTemplateCommand.RaiseCanExecuteChanged();
	}

	private WorkspaceTabViewModel CreateAndAddTab(string folderPath)
	{
		WorkspaceTabViewModel workspaceTabViewModel = CreateTab(folderPath);
		WorkspaceTabs.Add(workspaceTabViewModel);
		OnPropertyChanged("HasTabs");
		return workspaceTabViewModel;
	}

	private WorkspaceTabViewModel CreateTab(string folderPath)
	{
		WorkspaceTabViewModel workspaceTabViewModel = new WorkspaceTabViewModel(folderPath, BackupFolder, _imageWorkspaceService, _workspaceStateService, _productSheetService, () => _isAutoPublishRunning || IsBusy, RunAutoPublishExclusiveAsync, SetCardAutoPublishStatusAsync, RunSingleAutoPublishAsync, OpenCardSizeDialogForCardAsync, OnBatchSelectionChanged);
		workspaceTabViewModel.RequestedActivate += OnTabRequestedActivate;
		workspaceTabViewModel.RequestedClose += OnTabRequestedClose;
		workspaceTabViewModel.CardImageFilesAdded += OnTabCardImageFilesAdded;
		return workspaceTabViewModel;
	}

	private void EnsurePlaceholderTab()
	{
		if (WorkspaceTabs.Count <= 0)
		{
			WorkspaceTabViewModel selectedTab = CreateAndAddTab(string.Empty);
			SelectedTab = selectedTab;
		}
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
		ApplyAutoPublishStatusFilter();
		NotifyAutoPublishFilterPropertiesChanged();
		OnPropertyChanged("HasBatchSelectedCards");
		OnPropertyChanged("CanRunBatchAutoPublish");
		_selectAllBatchCardsCommand.RaiseCanExecuteChanged();
		_clearAllBatchCardsCommand.RaiseCanExecuteChanged();
		_runBatchAutoPublishCommand.RaiseCanExecuteChanged();
	}

	private void SetAutoPublishStatusFilter(object? parameter)
	{
		AutoPublishStatusFilter result;
		if (parameter is AutoPublishStatusFilter selectedAutoPublishStatusFilter)
		{
			SelectedAutoPublishStatusFilter = selectedAutoPublishStatusFilter;
		}
		else if (parameter is string value && Enum.TryParse<AutoPublishStatusFilter>(value, out result))
		{
			SelectedAutoPublishStatusFilter = result;
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
		OnPropertyChanged("AllStatusFilterText");
		OnPropertyChanged("NotPublishedStatusFilterText");
		OnPropertyChanged("PublishingStatusFilterText");
		OnPropertyChanged("SuccessStatusFilterText");
		OnPropertyChanged("FailedStatusFilterText");
		OnPropertyChanged("IsAutoPublishFilterEmpty");
		OnPropertyChanged("AutoPublishFilterEmptyText");
	}

	private bool CanExecuteBatchAutoPublish()
	{
		return CanRunBatchAutoPublish;
	}

	private bool CanEditTemplateGenerationSettings()
	{
		if (!IsTemplateGenerating)
		{
			return !IsTemplateGenerationStopping;
		}
		return false;
	}

	private bool CanRunTemplateGeneration()
	{
		return CanEditTemplateGenerationSettings();
	}

	private async Task OpenGenerationTemplatePickerAsync()
	{
		await LoadGenerationTemplateOptionsAsync();
		EnsureGenerationTemplateSelection();
		if (GenerationTemplateOptions.Count == 0)
		{
			StatusMessage = "当前没有可用模板。";
		}
		else
		{
			IsGenerationTemplatePickerOpen = true;
		}
	}

	private void CloseGenerationTemplatePicker()
	{
		EnsureGenerationTemplateSelection();
		IsGenerationTemplatePickerOpen = false;
	}

	private void SelectAllGenerationTemplates()
	{
		foreach (GenerationTemplateOptionViewModel generationTemplateOption in GenerationTemplateOptions)
		{
			generationTemplateOption.IsSelected = true;
		}
		OnPropertyChanged("GenerationTemplateSelectionText");
	}

	private void ClearGenerationTemplates()
	{
		foreach (GenerationTemplateOptionViewModel generationTemplateOption in GenerationTemplateOptions)
		{
			generationTemplateOption.IsSelected = false;
		}
		OnPropertyChanged("GenerationTemplateSelectionText");
	}

	private void EnsureGenerationTemplateSelection()
	{
		if (GenerationTemplateOptions.Count == 0 || GenerationTemplateOptions.Any((GenerationTemplateOptionViewModel item) => item.IsSelected))
		{
			return;
		}
		foreach (GenerationTemplateOptionViewModel generationTemplateOption in GenerationTemplateOptions)
		{
			generationTemplateOption.IsSelected = true;
		}
	}

	private IReadOnlyList<long>? GetSelectedGenerationTemplateIds()
	{
		EnsureGenerationTemplateSelection();
		if (GenerationTemplateOptions.Count == 0)
		{
			return null;
		}
		long[] array = (from item in GenerationTemplateOptions
			where item.IsSelected
			select item.Id).ToArray();
		if (array.Length != 0 && array.Length != GenerationTemplateOptions.Count)
		{
			return array;
		}
		return null;
	}

	private bool CanEditSpBatchSettings()
	{
		if (!IsSpBatchRunning)
		{
			return !IsSpBatchStopping;
		}
		return false;
	}

	private bool CanRunSpBatch()
	{
		if (CanEditSpBatchSettings() && HasSpBatchSourceImageCards)
		{
			return !string.IsNullOrWhiteSpace(SpBatchOutputDirectory);
		}
		return false;
	}

	private bool CanGenerateSpBatchColorSkus()
	{
		if (CanEditSpBatchSettings() && HasSpBatchMasterImageCards)
		{
			return !string.IsNullOrWhiteSpace(SpBatchOutputDirectory) && GetSelectedSpBatchColorTemplateColors().Count > 0;
		}
		return false;
	}

	private bool CanOpenSpBatchColorSelection()
	{
		return CanEditSpBatchSettings() && GetSelectedSpBatchColorTemplateGroup() != null;
	}

	private void OpenSpBatchColorSelection()
	{
		if (!CanEditSpBatchSettings())
		{
			MessageBox.Show("\u5f53\u524d\u6b63\u5728\u6267\u884c\u4efb\u52a1\uff0c\u6682\u65f6\u4e0d\u80fd\u6307\u5b9a\u989c\u8272\u3002", "\u63d0\u793a", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		ColorTemplateGroupViewModel? group = GetSelectedSpBatchColorTemplateGroup();
		if (group == null)
		{
			MessageBox.Show("\u8bf7\u5148\u9009\u62e9\u989c\u8272\u7ec4\u3002", "\u63d0\u793a", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		if (group.Colors.Count == 0)
		{
			MessageBox.Show("\u5f53\u524d\u989c\u8272\u7ec4\u6ca1\u6709\u53ef\u9009\u989c\u8272\u3002", "\u63d0\u793a", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		RefreshSpBatchColorSelectionForCurrentGroup();
		IsSpBatchColorSelectionOpen = true;
	}

	private void CloseSpBatchColorSelection()
	{
		RefreshSpBatchColorSelectionForCurrentGroup();
		IsSpBatchColorSelectionOpen = false;
	}

	private void SelectAllSpBatchColors()
	{
		foreach (ColorTemplateColorViewModel color in SpBatchColorSelectionColors)
		{
			color.IsSelected = true;
		}
	}

	private void ClearAllSpBatchColors()
	{
		foreach (ColorTemplateColorViewModel color in SpBatchColorSelectionColors)
		{
			color.IsSelected = false;
		}
	}

	private void SaveSpBatchColorSelection()
	{
		if (!IsSpBatchColorSelectionOpen)
		{
			return;
		}
		ColorTemplateGroupViewModel? group = GetSelectedSpBatchColorTemplateGroup();
		if (group == null)
		{
			throw new InvalidOperationException("请先选择颜色组。");
		}
		if (SpBatchColorSelectionColors.All(item => !item.IsSelected))
		{
			throw new InvalidOperationException("请至少选择一种颜色。");
		}
		StoreSpBatchColorSelectionForCurrentGroup();
		PersistUserPathSettings();
		CloseSpBatchColorSelection();
		_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
	}

	private bool CanEditSkuOptimizeSettings()
	{
		if (!IsSkuOptimizeRunning)
		{
			return !IsSkuOptimizeStopping;
		}
		return false;
	}

	private bool CanRunSkuOptimize()
	{
		if (CanEditSkuOptimizeSettings() && HasSkuOptimizeSourceImageCards)
		{
			return !string.IsNullOrWhiteSpace(SkuOptimizeOutputDirectory);
		}
		return false;
	}

	private bool CanModifyBatchSelection()
	{
		if (!IsBusy && !_isAutoPublishRunning)
		{
			return SelectedTab?.HasBatchSelectableCards() ?? false;
		}
		return false;
	}

	private void SelectAllBatchCards()
	{
		SelectedTab?.SetBatchSelectionForAll(isSelected: true);
		StatusMessage = "已选中当前页全部可批量上架的小卡片。";
	}

	private void ClearAllBatchCards()
	{
		SelectedTab?.SetBatchSelectionForAll(isSelected: false);
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
			StatusMessage = "尺寸录入弹窗打开失败：" + ex.Message;
		}
	}

	private async Task HandleCardImageFilesAddedAsync(RootCardViewModel card, IReadOnlyList<string> copiedFiles)
	{
		if (TryFindSizeImageFromFiles(card, copiedFiles, out string sizeImagePath))
		{
			CardSizeInfoRecord existing = await _cardSizeInfoService.GetByCardPathAsync(card.AutoPublishKeyPath);
			SizeImageFingerprint value = await GetSizeImageFingerprintAsync(sizeImagePath);
			if (existing != null)
			{
				card.ApplyCardSizeInfo(existing);
			}
			await OpenCardSizeDialogAsync(card, sizeImagePath, existing, value);
		}
	}

	private async Task OpenCardSizeDialogForCardAsync(RootCardViewModel card)
	{
		string sizeImagePath = FindCurrentSizeImagePath(card);
		if (string.IsNullOrWhiteSpace(sizeImagePath) || !File.Exists(sizeImagePath))
		{
			throw new InvalidOperationException("当前 main 文件夹下没有尺寸图：" + Path.Combine(card.AutoPublishKeyPath, "main"));
		}
		CardSizeInfoRecord existing = await _cardSizeInfoService.GetByCardPathAsync(card.AutoPublishKeyPath);
		SizeImageFingerprint value = await GetSizeImageFingerprintAsync(sizeImagePath);
		if (existing != null)
		{
			card.ApplyCardSizeInfo(existing);
		}
		await OpenCardSizeDialogAsync(card, sizeImagePath, existing, value);
	}

	private async Task<bool> IsCardSizeInfoStaleAsync(RootCardViewModel card, CardSizeInfoRecord? record)
	{
		if (record == null || string.IsNullOrWhiteSpace(record.SizeText))
		{
			return false;
		}
		string text = FindCurrentSizeImagePath(card);
		if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
		{
			return false;
		}
		SizeImageFingerprint sizeImageFingerprint = await GetSizeImageFingerprintAsync(text);
		return !string.Equals(record.SizeImageHash, sizeImageFingerprint.Hash, StringComparison.OrdinalIgnoreCase);
	}

	private async Task OpenCardSizeDialogAsync(RootCardViewModel card, string sizeImagePath, CardSizeInfoRecord? existing, SizeImageFingerprint? fingerprint = null)
	{
		fingerprint.GetValueOrDefault();
		if (!fingerprint.HasValue)
		{
			fingerprint = await GetSizeImageFingerprintAsync(sizeImagePath);
		}
		bool flag = existing != null && string.Equals(existing.SizeImageHash, fingerprint.Value.Hash, StringComparison.OrdinalIgnoreCase);
		_cardSizeDialogCard = card;
		_cardSizeDialogImagePath = sizeImagePath;
		_cardSizeDialogImageHash = fingerprint.Value.Hash;
		_cardSizeDialogImageLastWriteUtc = fingerprint.Value.LastWriteUtc;
		CardSizeInputText = ((!flag) ? string.Empty : (existing?.SizeRawInput ?? string.Empty));
		CardSizePreviewMeta = "尺寸图：" + Path.GetFileName(sizeImagePath) + Environment.NewLine + "录入后会保存到本地数据库，并在上架时使用。";
		CardSizePreviewImageSource = null;
		OnPropertyChanged("CardSizeDialogTitle");
		IsCardSizeDialogOpen = true;
		_saveCardSizeInfoCommand.RaiseCanExecuteChanged();
		try
		{
			CardSizePreviewImageSource = await Task.Run(() => ImageBitmapLoader.LoadFromFile(sizeImagePath, 1200));
		}
		catch (Exception ex)
		{
			CardSizePreviewMeta = "尺寸图预览失败：" + ex.Message;
		}
	}

	private bool CanSaveCardSizeInfo()
	{
		if (IsCardSizeDialogOpen && _cardSizeDialogCard != null && !string.IsNullOrWhiteSpace(CardSizeInputText))
		{
			return BuildDisplaySizes(CardSizeInputText).Count > 0;
		}
		return false;
	}

	private async Task SaveCardSizeInfoAsync()
	{
		if (_cardSizeDialogCard != null)
		{
			IReadOnlyList<string> readOnlyList = BuildDisplaySizes(CardSizeInputText);
			if (readOnlyList.Count == 0)
			{
				throw new InvalidOperationException("请输入至少一组尺寸，例如：50*225。");
			}
			CardSizeInfoRecord record = new CardSizeInfoRecord
			{
				CardPath = _cardSizeDialogCard.AutoPublishKeyPath,
				SizeText = string.Join(Environment.NewLine, readOnlyList),
				SizeRawInput = CardSizeInputText.Trim(),
				SizeImageHash = _cardSizeDialogImageHash,
				SizeImageLastWriteUtc = _cardSizeDialogImageLastWriteUtc,
				SizeUpdatedAt = DateTimeOffset.Now
			};
			await _cardSizeInfoService.UpsertAsync(record);
			_cardSizeDialogCard.ApplyCardSizeInfo(record);
			StatusMessage = "已保存尺寸：" + _cardSizeDialogCard.DisplayName;
			CloseCardSizeDialog();
		}
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
		OnPropertyChanged("CardSizeDialogTitle");
		_saveCardSizeInfoCommand.RaiseCanExecuteChanged();
	}

	private void NotifyAutoPublishStateChanged()
	{
		OnPropertyChanged("CanRunBatchAutoPublish");
		foreach (WorkspaceTabViewModel workspaceTab in WorkspaceTabs)
		{
			workspaceTab.NotifyAutoPublishStateChanged();
		}
	}

	private async Task ReloadTabAsync(WorkspaceTabViewModel tab)
	{
		IsBusy = true;
		ScanProgressValue = 0.0;
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
			tab.SetRootNodes(Array.Empty<FolderNode>());
			OnPropertyChanged("SelectedTab");
			OnPropertyChanged("HasRootCards");
			ClearPreview();
			UpdateSummaryForTab(tab);
			Progress<FolderScanProgress> progress = new Progress<FolderScanProgress>(OnFolderScanProgress);
			IReadOnlyList<FolderNode> readOnlyList = await _folderScanService.ScanAsync(tab.RootFolder, recursive: true, progress);
			LoadingTitle = "正在生成界面...";
			int value = CountFolders(readOnlyList);
			int value2 = CountImages(readOnlyList);
			LoadingDetail = $"共扫描文件夹 {value} 个，图片 {value2} 张，正在渲染界面。";
			ScanProgressText = "正在生成界面";
			tab.SetRootNodes(readOnlyList);
			await tab.RefreshAutoPublishRecordsAsync(_autoPublishStateService);
			await tab.RefreshCardSizeInfoAsync(_cardSizeInfoService, IsCardSizeInfoStaleAsync);
			ApplyAutoPublishStatusFilter();
			NotifyAutoPublishFilterPropertiesChanged();
			tab.RestoreDefaultSelection();
			UpdateSummaryForTab(tab);
			OnPropertyChanged("SelectedTab");
			OnPropertyChanged("HasRootCards");
			_generateProductSheetCommand.RaiseCanExecuteChanged();
			if (tab.RootCards.Count > 0)
			{
				ScanProgressValue = 100.0;
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
			StatusMessage = "扫描文件夹失败：" + ex.Message;
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
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "浏览文件夹",
			InitialDirectory = ((SelectedTab != null && Directory.Exists(SelectedTab.RootFolder)) ? SelectedTab.RootFolder : DefaultFolderPickerDirectory),
			Multiselect = false
		};
		if (openFolderDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(openFolderDialog.FolderName))
		{
			await LoadFolderAsync(openFolderDialog.FolderName);
		}
	}

	private async Task SelectBackupFolderAsync()
	{
		FolderBrowserDialog dialog = new FolderBrowserDialog
		{
			Description = "选择备份目录",
			InitialDirectory = (Directory.Exists(BackupFolder) ? BackupFolder : DefaultFolderPickerDirectory),
			ShowNewFolderButton = true
		};
		try
		{
			if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
			{
				BackupFolder = dialog.SelectedPath;
				StatusMessage = "备份目录已切换到：" + BackupFolder;
				await Task.CompletedTask;
			}
		}
		finally
		{
			((IDisposable)(object)dialog)?.Dispose();
		}
	}

	private async Task ChooseTemplateLibraryAsync()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "选择模板库 Excel",
			Filter = "Excel 文件|*.xlsx;*.xlsm;*.xls|所有文件|*.*",
			InitialDirectory = (Directory.Exists(Path.GetDirectoryName(TemplateLibraryPath)) ? Path.GetDirectoryName(TemplateLibraryPath) : (Directory.Exists(Path.GetDirectoryName(DefaultTemplateLibraryPath)) ? Path.GetDirectoryName(DefaultTemplateLibraryPath) : WorkspaceDefaults.DefaultTempFolder))
		};
		if (openFileDialog.ShowDialog() == true)
		{
			TemplateLibraryPath = openFileDialog.FileName;
		}
		await Task.CompletedTask;
	}

	private async Task ChooseGenerationOutputFolderAsync()
	{
		FolderBrowserDialog dialog = new FolderBrowserDialog
		{
			Description = "选择生图输出目录",
			InitialDirectory = (Directory.Exists(GenerationOutputDirectory) ? GenerationOutputDirectory : WorkspaceDefaults.DefaultOpenFolder),
			ShowNewFolderButton = true
		};
		try
		{
			if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
			{
				GenerationOutputDirectory = dialog.SelectedPath;
			}
			await Task.CompletedTask;
		}
		finally
		{
			((IDisposable)(object)dialog)?.Dispose();
		}
	}

	private async Task ChooseGenerationImagesAsync()
	{
		string[] fileNames = ChooseImageFiles("选择图片", Directory.Exists(GenerationOutputDirectory) ? GenerationOutputDirectory : WorkspaceDefaults.DefaultOpenFolder);
		if (fileNames.Length != 0)
		{
			AddGenerationImages(fileNames);
			StatusMessage = $"已添加 {fileNames.Length} 张图片到模板生图区。";
		}
		await Task.CompletedTask;
	}

	private async Task ChooseSpBatchSourceImagesAsync()
	{
		string initialDirectory = Directory.Exists(SpBatchInputDirectory) ? SpBatchInputDirectory : (Directory.Exists(GenerationOutputDirectory) ? GenerationOutputDirectory : WorkspaceDefaults.DefaultOpenFolder);
		string[] fileNames = ChooseImageFiles("选择 SKU 图生成图片", initialDirectory);
		if (fileNames.Length != 0)
		{
			AddSpBatchSourceImages(fileNames);
			StatusMessage = $"已添加 {fileNames.Length} 张图片到 SKU 图生成区域。";
		}
		await Task.CompletedTask;
	}

	private async Task ChooseSpBatchMasterImagesAsync()
	{
		string initialDirectory = Directory.Exists(SpBatchOutputDirectory) ? SpBatchOutputDirectory : (Directory.Exists(GenerationOutputDirectory) ? GenerationOutputDirectory : WorkspaceDefaults.DefaultOpenFolder);
		string[] fileNames = ChooseImageFiles("选择 SKU 母图", initialDirectory);
		if (fileNames.Length != 0)
		{
			AddSpBatchMasterImages(fileNames);
			StatusMessage = $"已添加 {fileNames.Length} 张图片到 SKU 母图区域。";
		}
		await Task.CompletedTask;
	}

	private static string[] ChooseImageFiles(string title, string initialDirectory)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = title,
			Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.jfif|所有文件|*.*",
			Multiselect = true,
			InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : WorkspaceDefaults.DefaultOpenFolder
		};
		if (openFileDialog.ShowDialog() == true)
		{
			return openFileDialog.FileNames;
		}
		return Array.Empty<string>();
	}

	private IReadOnlyList<string> GetVisibleSpBatchColorSelectionNames()
	{
		if (SpBatchColorSelectionColors.Count > 0)
		{
			string[] names = SpBatchColorSelectionColors
				.Where(item => item.IsSelected && !string.IsNullOrWhiteSpace(item.Name))
				.Select(item => item.Name.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
			if (names.Length > 0)
			{
				return names;
			}
		}

		return GetSelectedSpBatchColorNamesForCurrentGroup();
	}

	private void OpenGenerationOutputFolder()
	{
		TryOpenFolder(GenerationOutputDirectory, "输出目录");
	}

	private void OpenSpBatchOutputFolder()
	{
		TryOpenFolder(SpBatchOutputDirectory, "输出目录");
	}

	private void OpenSkuOptimizeOutputFolder()
	{
		TryOpenFolder(SkuOptimizeOutputDirectory, "输出目录");
	}

	private bool CanOpenCurrentFolder()
	{
		string? folderPath = SelectedTab?.ActiveCard?.CurrentFolderPath;
		return !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);
	}

	private void OpenCurrentFolder()
	{
		string? folderPath = SelectedTab?.ActiveCard?.CurrentFolderPath;
		if (!string.IsNullOrWhiteSpace(folderPath))
		{
			TryOpenFolder(folderPath, "当前文件夹");
		}
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
			StatusMessage = "打开文件失败：" + ex.Message;
		}
	}

	private async Task ChooseSpBatchInputFolderAsync()
	{
		FolderBrowserDialog dialog = new FolderBrowserDialog
		{
			Description = "选择要批量处理的图片目录",
			InitialDirectory = (Directory.Exists(SpBatchInputDirectory) ? SpBatchInputDirectory : WorkspaceDefaults.DefaultOpenFolder),
			ShowNewFolderButton = false
		};
		try
		{
			if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
			{
				SpBatchInputDirectory = dialog.SelectedPath;
			}
			await Task.CompletedTask;
		}
		finally
		{
			((IDisposable)(object)dialog)?.Dispose();
		}
	}

	private async Task ChooseSpBatchOutputFolderAsync()
	{
		FolderBrowserDialog dialog = new FolderBrowserDialog
		{
			Description = "选择 SP 批处理输出目录",
			InitialDirectory = (Directory.Exists(SpBatchOutputDirectory) ? SpBatchOutputDirectory : WorkspaceDefaults.DefaultSpBatchOutputFolder),
			ShowNewFolderButton = true
		};
		try
		{
			if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
			{
				SpBatchOutputDirectory = dialog.SelectedPath;
			}
			await Task.CompletedTask;
		}
		finally
		{
			((IDisposable)(object)dialog)?.Dispose();
		}
	}

	private async Task ChooseSkuOptimizeOutputFolderAsync()
	{
		FolderBrowserDialog dialog = new FolderBrowserDialog
		{
			Description = "选择 SKU 图优化输出目录",
			InitialDirectory = (Directory.Exists(SkuOptimizeOutputDirectory) ? SkuOptimizeOutputDirectory : WorkspaceDefaults.DefaultSpBatchOutputFolder),
			ShowNewFolderButton = true
		};
		try
		{
			if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
			{
				SkuOptimizeOutputDirectory = dialog.SelectedPath;
			}
			await Task.CompletedTask;
		}
		finally
		{
			((IDisposable)(object)dialog)?.Dispose();
		}
	}

	private void TryOpenFolder(string folderPath, string label)
	{
		if (!Directory.Exists(folderPath))
		{
			StatusMessage = label + "不存在：" + folderPath;
			return;
		}
		try
		{
			ShellOpenHelper.OpenFolder(folderPath);
		}
		catch (Exception ex)
		{
			StatusMessage = "打开目录失败：" + ex.Message;
		}
	}

	private static void OpenFolder(string folderPath)
	{
		ShellOpenHelper.OpenFolder(folderPath);
	}

	private async Task RunTemplateGenerationAsync()
	{
		IsGenerationTemplatePickerOpen = false;
		IReadOnlyList<long> selectedGenerationTemplateIds = GetSelectedGenerationTemplateIds();
		string generationTabKey = _selectedImageGenerateTab;
		ImageTemplateType currentImageType = CurrentGenerationImageType;
		string generationLibraryPath = Path.Combine(Path.GetTempPath(), "EcomToolStudio", "template-generation", currentImageType switch
		{
			ImageTemplateType.SceneImage => "scene-image-library.json", 
			ImageTemplateType.CompareImage => "compare-image-library.json", 
			_ => "main-image-library.json", 
		});
		await _templateLibraryService.ExportGenerationLibraryAsync(currentImageType, generationLibraryPath, selectedGenerationTemplateIds);
		TemplateLibraryPath = generationLibraryPath;
		if (!File.Exists(TemplateLibraryPath))
		{
			throw new InvalidOperationException("模板库不存在：" + TemplateLibraryPath);
		}
		if (!TryParsePositiveInt(GenerationCountText, "生成数量", out var value) || !TryParsePositiveInt(GenerationConcurrencyText, "并发数量", out var value2))
		{
			return;
		}
		Directory.CreateDirectory(GenerationOutputDirectory);
		SetActiveTemplateGenerationTab(generationTabKey);
		IsTemplateGenerating = true;
		GenerationStatusText = (IsGenerationPromptsOnly ? "正在生成提示词..." : "正在批量生成图片...");
		GenerationResultModeText = "模式：" + (IsGenerationPromptsOnly ? "只出提示词" : "直接生图");
		GenerationResultOutputText = "输出目录：" + GenerationOutputDirectory;
		if (IsGenerationPromptsOnly)
		{
			ClearGenerationPromptCards();
			if (_lastExecutedGenerationPromptsOnly == false)
			{
				ClearGeneratedImageResultCards();
			}
		}
		else
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
			TemplateGenerateRequest request = new TemplateGenerateRequest
			{
				TemplatePath = TemplateLibraryPath,
				OutputDirectory = GenerationOutputDirectory,
				Image2ScriptPath = CurrentImageGenerationScriptPath,
				ImageType = currentImageType,
				Count = value,
				Concurrency = value2,
				UniqueScene = IsGenerationUniqueScene,
				PromptsOnly = IsGenerationPromptsOnly,
				ColorTemplateColors = GetSelectedColorTemplateColors()
			};
			TemplateGenerateResult templateGenerateResult = await _templateGenerationService.GenerateAsync(request, _templateGenerationCancellationTokenSource.Token);
			_lastExecutedGenerationPromptsOnly = IsGenerationPromptsOnly;
			GenerationStatusText = ((templateGenerateResult.Mode == "prompts_only") ? "提示词生成完成" : "图片生成完成");
			ApplyGenerationResult(templateGenerateResult);
			ApplyGenerationVisualResult(templateGenerateResult);
			StatusMessage = ((templateGenerateResult.Mode == "prompts_only") ? $"提示词生成完成，共 {templateGenerateResult.Prompts.Count} 条。" : $"图片生成完成，共 {templateGenerateResult.Items.Count} 张。");
		}
		catch (OperationCanceledException)
		{
			IsTemplateGenerationStopping = false;
			GenerationStatusText = "已停止";
			GenerationResultModeText = "模式：已停止";
			GenerationResultOutputText = "输出目录：" + GenerationOutputDirectory;
			StatusMessage = "已停止当前生图任务。";
		}
		catch (Exception ex2)
		{
			GenerationStatusText = "执行失败";
			GenerationResultModeText = "模式：执行失败";
			GenerationResultOutputText = "输出目录：" + GenerationOutputDirectory;
			if (HasVisibleText(ex2.Message))
			{
				GenerationPromptCards.Add(new GenerationPromptCardViewModel
				{
					Title = "错误信息",
					PromptText = ex2.Message
				});
				OnPropertyChanged("HasGenerationPromptCards");
				OnPropertyChanged("HasGeneratedImageResultCards");
				OnPropertyChanged("HasAnyGenerationResultCards");
			}
			StatusMessage = HasVisibleText(ex2.Message) ? "模板随机生成失败：" + ex2.Message : "模板随机生成失败。";
		}
		finally
		{
			IsTemplateGenerationStopping = false;
			_templateGenerationCancellationTokenSource?.Dispose();
			_templateGenerationCancellationTokenSource = null;
			IsTemplateGenerating = false;
			SetActiveTemplateGenerationTab(null);
		}
	}

	private void StopTemplateGeneration()
	{
		if (IsTemplateGenerating && !IsTemplateGenerationStopping)
		{
			IsTemplateGenerationStopping = true;
			GenerationStatusText = "正在停止...";
			StatusMessage = "正在停止当前生图任务...";
			_templateGenerationService.CancelCurrentRun();
			_templateGenerationCancellationTokenSource?.Cancel();
		}
	}

	private void StopSpBatch()
	{
		if (IsSpBatchRunning && !IsSpBatchStopping)
		{
			IsSpBatchStopping = true;
			SpBatchStatusText = "正在停止...";
			SpBatchSummaryText = "正在停止当前 SKU 批处理任务...";
			StatusMessage = SpBatchSummaryText;
			_spBatchService.CancelCurrentRun();
			_spBatchCancellationTokenSource?.Cancel();
		}
	}

	private async Task RunSpBatchAsync()
	{
		if (string.IsNullOrWhiteSpace(SpBatchInputDirectory) || !Directory.Exists(SpBatchInputDirectory))
		{
			throw new InvalidOperationException("请输入有效的批处理输入目录。");
		}
		if (!TryParsePositiveInt(SpBatchConcurrencyText, "并发数量", out var value))
		{
			return;
		}
		int retries = 4;
		Directory.CreateDirectory(SpBatchOutputDirectory);
		IsSpBatchRunning = true;
		IsSpBatchStopping = false;
		string spBatchStatusText = ((_spBatchMode != SpBatchMode.PrepareOnly) ? "正在批量生成 SP 资源..." : "正在创建 SP 目录结构...");
		SpBatchStatusText = spBatchStatusText;
		SpBatchSummaryText = "任务执行中，请稍候。";
		SpBatchResultRootText = "日期目录：" + SpBatchOutputDirectory;
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
			SpBatchRequest request = new SpBatchRequest
			{
				InputDirectory = SpBatchInputDirectory,
				OutputDirectory = SpBatchOutputDirectory,
				Image2ScriptPath = CurrentImageGenerationScriptPath,
				Concurrency = value,
				Retries = retries,
				Mode = _spBatchMode,
				Overwrite = true,
				ColorTemplateColors = GetSelectedColorTemplateColors()
			};
			ApplySpBatchVisualResult(await _spBatchService.GenerateAsync(request, _spBatchCancellationTokenSource.Token));
			StatusMessage = SpBatchSummaryText;
		}
		catch (OperationCanceledException)
		{
			IsSpBatchStopping = false;
			SpBatchStatusText = "已停止";
			SpBatchSummaryText = "已停止当前 SKU 批处理任务。";
			StatusMessage = SpBatchSummaryText;
		}
		catch (Exception ex2)
		{
			SpBatchStatusText = "执行失败";
			SpBatchSummaryText = "SP 批处理失败：" + ex2.Message;
			ClearSpBatchResultCards();
			ClearSpBatchImageResultCards();
			SpBatchResultCards.Add(new SpBatchResultCardViewModel
			{
				Title = "错误信息",
				SummaryText = ex2.Message,
				StatusText = "失败"
			});
			OnPropertyChanged("HasSpBatchResultCards");
			OnPropertyChanged("HasSpBatchImageResultCards");
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
		RootCardViewModel rootCardViewModel = SelectedTab?.RootCards.FirstOrDefault();
		if (rootCardViewModel == null)
		{
			return;
		}
		IsBusy = true;
		LoadingTitle = "正在生成上架 JSON";
		LoadingDetail = "正在处理：" + rootCardViewModel.RootFolderPath;
		ScanProgressValue = 0.0;
		IsScanProgressIndeterminate = true;
		StatusMessage = "开始处理：" + rootCardViewModel.RootFolderPath;
		try
		{
			ProductSheetTask productSheetTask = await _productSheetService.GenerateAsync(rootCardViewModel.RootFolderPath, titleChineseOnly: TitleChineseOnly);
			StatusMessage = "上架 JSON 任务状态：" + productSheetTask.Status;
			LoadingDetail = (string.IsNullOrWhiteSpace(productSheetTask.ProductsJsonPath) ? StatusMessage : (StatusMessage + "，" + productSheetTask.ProductsJsonPath));
		}
		catch (Exception ex)
		{
			StatusMessage = "生成上架 JSON 失败：" + ex.Message;
			LoadingDetail = StatusMessage;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task RunBatchAutoPublishAsync()
	{
		IReadOnlyList<RootCardViewModel> selectedCards = SelectedTab?.GetBatchSelectedCards();
		if (selectedCards == null || selectedCards.Count == 0)
		{
			StatusMessage = "请先勾选需要批量上架的已修改卡片。";
			return;
		}
		await RunAutoPublishExclusiveAsync(async delegate
		{
			LoadingTitle = "正在批量上架";
			IsScanProgressIndeterminate = true;
			ScanProgressValue = 0.0;
			List<RootCardViewModel> publishingCards = new List<RootCardViewModel>();
			try
			{
				List<JsonElement> productItems = new List<JsonElement>();
				for (int index = 0; index < selectedCards.Count; index++)
				{
					RootCardViewModel card = selectedCards[index];
					string collapsedSummaryText = card.CollapsedSummaryText;
					LoadingDetail = $"正在准备第 {index + 1}/{selectedCards.Count} 个：{collapsedSummaryText}";
					StatusMessage = $"正在准备妙手上架数据：{index + 1}/{selectedCards.Count}";
					await SetCardAutoPublishStatusAsync(card, AutoPublishStatus.Publishing);
					publishingCards.Add(card);
					List<JsonElement> list = productItems;
					list.AddRange(await PrepareAutoPublishProductItemsAsync(card));
				}
				MiaoshouPublishResult result = await RunMiaoshouPublishAsync(productItems, selectedCards);
				await ApplyBatchPublishResultAsync(selectedCards, result);
				foreach (RootCardViewModel item in selectedCards)
				{
					item.IsBatchSelected = false;
				}
				LoadingDetail = $"妙手批量上架完成：成功 {result.SuccessCount}，失败 {result.FailedCount}。";
				StatusMessage = LoadingDetail;
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				await MarkPublishingCardsFailedAsync(publishingCards, ex2.Message);
				LoadingDetail = ex2.Message;
				StatusMessage = "批量上架失败：" + ex2.Message;
				ExceptionDispatchInfo.Capture(ex2).Throw();
				throw;
			}
			finally
			{
				OnBatchSelectionChanged();
			}
		});
	}

	private async Task RunSingleAutoPublishAsync(RootCardViewModel card)
	{
		await RunAutoPublishExclusiveAsync(async delegate
		{
			LoadingTitle = "正在自动上架";
			IsScanProgressIndeterminate = true;
			ScanProgressValue = 0.0;
			try
			{
				LoadingDetail = "正在准备：" + card.CollapsedSummaryText;
				StatusMessage = "正在准备妙手上架数据：" + card.DisplayName;
				await SetCardAutoPublishStatusAsync(card, AutoPublishStatus.Publishing);
				List<JsonElement> productItems = await PrepareAutoPublishProductItemsAsync(card);
				MiaoshouPublishResult result = await RunMiaoshouPublishAsync(productItems, new[] { card });
				await ApplyBatchPublishResultAsync(new[] { card }, result);
				LoadingDetail = $"自动上架完成：成功 {result.SuccessCount}，失败 {result.FailedCount}。";
				StatusMessage = LoadingDetail;
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				await SetCardAutoPublishStatusAsync(card, AutoPublishStatus.Failed, ex2.Message);
				LoadingDetail = ex2.Message;
				StatusMessage = "自动上架失败：" + ex2.Message;
				ExceptionDispatchInfo.Capture(ex2).Throw();
				throw;
			}
			finally
			{
				OnBatchSelectionChanged();
			}
		});
	}

	private async Task<List<JsonElement>> PrepareAutoPublishProductItemsAsync(RootCardViewModel card)
	{
		ProductSheetTask productSheetTask = await card.PrepareAutoPublishDataAsync(await EnsureCardSizeForPublishAsync(card), TitleChineseOnly);
		if (!File.Exists(productSheetTask.ProductsJsonPath))
		{
			throw new FileNotFoundException("商品 JSON 未生成。", productSheetTask.ProductsJsonPath);
		}
		List<JsonElement> productItems = new List<JsonElement>();
		using JsonDocument jsonDocument = JsonDocument.Parse(await File.ReadAllTextAsync(productSheetTask.ProductsJsonPath));
		foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
		{
			productItems.Add(item.Clone());
		}
		return productItems;
	}

	private async Task<MiaoshouPublishResult> RunMiaoshouPublishAsync(List<JsonElement> productItems, IReadOnlyList<RootCardViewModel> cards)
	{
		Dictionary<string, RootCardViewModel> cardsByPath = cards.ToDictionary<RootCardViewModel, string>((RootCardViewModel card) => NormalizeCardPath(card.AutoPublishKeyPath), StringComparer.OrdinalIgnoreCase);
		MiaoshouPublishRequest request = CreateMiaoshouPublishRequest((MiaoshouPublishProgressEvent progressEvent) => ApplyMiaoshouProgressEventAsync(cardsByPath, progressEvent));
		Directory.CreateDirectory(Path.GetDirectoryName(request.ManifestPath));
		await File.WriteAllTextAsync(request.ManifestPath, JsonSerializer.Serialize(productItems, new JsonSerializerOptions
		{
			WriteIndented = true
		}), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		LoadingDetail = $"已生成 {productItems.Count} 条商品数据，正在启动妙手自动上架。";
		StatusMessage = "正在运行妙手 Playwright 自动上架。";
		return await _miaoshouPublishService.PublishAsync(request);
	}

	private async Task ApplyMiaoshouProgressEventAsync(IReadOnlyDictionary<string, RootCardViewModel> cardsByPath, MiaoshouPublishProgressEvent progressEvent)
	{
		string key = NormalizeCardPath(progressEvent.CardPath);
		if (cardsByPath.TryGetValue(key, out RootCardViewModel card))
		{
			AutoPublishStatus status = (string.Equals(progressEvent.Type, "product_success", StringComparison.OrdinalIgnoreCase) ? AutoPublishStatus.Success : AutoPublishStatus.Failed);
			string lastError = ((status == AutoPublishStatus.Failed) ? progressEvent.Error : string.Empty);
			await SetCardAutoPublishStatusAsync(card, status, lastError);
			await System.Windows.Application.Current.Dispatcher.InvokeAsync(delegate
			{
				StatusMessage = ((status == AutoPublishStatus.Success) ? ("已上架成功：" + card.DisplayName) : ("上架失败：" + card.DisplayName));
			});
		}
	}

	private async Task SetCardAutoPublishStatusAsync(RootCardViewModel card, AutoPublishStatus status, string lastError = "")
	{
		if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
		{
			await System.Windows.Application.Current.Dispatcher.InvokeAsync(delegate
			{
				card.SetAutoPublishStatus(status, lastError);
				ApplyAutoPublishStatusFilter();
				NotifyAutoPublishFilterPropertiesChanged();
			});
		}
		else
		{
			card.SetAutoPublishStatus(status, lastError);
			ApplyAutoPublishStatusFilter();
			NotifyAutoPublishFilterPropertiesChanged();
		}
		await _autoPublishStateService.UpsertStatusAsync(card.AutoPublishKeyPath, card.DisplayName, status, lastError);
	}

	private async Task ApplyBatchPublishResultAsync(IReadOnlyList<RootCardViewModel> selectedCards, MiaoshouPublishResult result)
	{
		if (IsWholeBatchSuccessful(selectedCards, result))
		{
			foreach (RootCardViewModel selectedCard in selectedCards)
			{
				await SetCardAutoPublishStatusAsync(selectedCard, AutoPublishStatus.Success);
			}
			return;
		}
		Dictionary<string, RootCardViewModel> cardsByPath = selectedCards.ToDictionary<RootCardViewModel, string>((RootCardViewModel card) => NormalizeCardPath(card.AutoPublishKeyPath), StringComparer.OrdinalIgnoreCase);
		HashSet<string> handledPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (MiaoshouPublishItemResult result2 in result.Results)
		{
			string text = NormalizeCardPath(result2.CardPath);
			if (cardsByPath.TryGetValue(text, out var value))
			{
				handledPaths.Add(text);
				AutoPublishStatus status = (string.Equals(result2.Status, "success", StringComparison.OrdinalIgnoreCase) ? AutoPublishStatus.Success : AutoPublishStatus.Failed);
				await SetCardAutoPublishStatusAsync(value, status, result2.Error);
			}
		}
		if (result.Results.Count == 0)
		{
			AutoPublishStatus fallbackStatus = ((result.FailedCount > 0 || string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase)) ? AutoPublishStatus.Failed : AutoPublishStatus.Success);
			foreach (RootCardViewModel selectedCard2 in selectedCards)
			{
				await SetCardAutoPublishStatusAsync(selectedCard2, fallbackStatus, result.Error);
			}
			return;
		}
		foreach (RootCardViewModel selectedCard3 in selectedCards)
		{
			if (!handledPaths.Contains(NormalizeCardPath(selectedCard3.AutoPublishKeyPath)))
			{
				await SetCardAutoPublishStatusAsync(selectedCard3, AutoPublishStatus.Failed, "未收到该卡片的上架结果。");
			}
		}
	}

	private static bool IsWholeBatchSuccessful(IReadOnlyList<RootCardViewModel> selectedCards, MiaoshouPublishResult result)
	{
		if (selectedCards.Count > 0 && result.FailedCount == 0 && result.SuccessCount == selectedCards.Count)
		{
			return string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private async Task MarkPublishingCardsFailedAsync(IEnumerable<RootCardViewModel> cards, string error)
	{
		foreach (RootCardViewModel item in cards.Where((RootCardViewModel card) => card.AutoPublishStatus == AutoPublishStatus.Publishing))
		{
			await SetCardAutoPublishStatusAsync(item, AutoPublishStatus.Failed, error);
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
		CardSizeInfoRecord record = await _cardSizeInfoService.GetByCardPathAsync(card.AutoPublishKeyPath);
		bool flag = await IsCardSizeInfoStaleAsync(card, record);
		card.ApplyCardSizeInfo(record, flag);
		if (flag)
		{
			throw new InvalidOperationException("请先重新录入尺寸：" + card.DisplayName + " 的尺寸图已变更。");
		}
		if (record == null || string.IsNullOrWhiteSpace(record.SizeText))
		{
			throw new InvalidOperationException("请先录入尺寸：" + card.DisplayName + "。");
		}
		IReadOnlyList<string> readOnlyList = BuildPublishSizeEntries(record.SizeText);
		if (readOnlyList.Count == 0)
		{
			throw new InvalidOperationException("尺寸缺少 cm 规格，无法匹配规格表：" + card.DisplayName + "。");
		}
		return readOnlyList;
	}

	private static bool TryFindSizeImageFromFiles(RootCardViewModel card, IReadOnlyList<string> files, out string sizeImagePath)
	{
		string mainFolder = Path.Combine(card.AutoPublishKeyPath, "main");
		sizeImagePath = (from path in files.Where(IsSupportedImageFile)
			where IsInMainFolder(path, mainFolder)
			select path).Where(IsSizeImageName).OrderBy<string, string>((string path) => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? string.Empty;
		return !string.IsNullOrWhiteSpace(sizeImagePath);
	}

	private static string FindCurrentSizeImagePath(RootCardViewModel card)
	{
		string path = Path.Combine(card.AutoPublishKeyPath, "main");
		if (!Directory.Exists(path))
		{
			return string.Empty;
		}
		return Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Where(IsSupportedImageFile).Where(IsSizeImageName)
			.OrderBy<string, string>((string result) => result, StringComparer.OrdinalIgnoreCase)
			.FirstOrDefault() ?? string.Empty;
	}

	private static bool IsSizeImageName(string filePath)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
		if (!fileNameWithoutExtension.StartsWith("2-", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.Contains("尺寸", StringComparison.OrdinalIgnoreCase))
		{
			return fileNameWithoutExtension.Contains("size", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsInMainFolder(string filePath, string expectedMainFolder)
	{
		string directoryName = Path.GetDirectoryName(filePath);
		if (string.IsNullOrWhiteSpace(directoryName))
		{
			return false;
		}
		if (string.Equals(directoryName, expectedMainFolder, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return string.Equals(Path.GetFileName(directoryName), "main", StringComparison.OrdinalIgnoreCase);
	}

	private static async Task<SizeImageFingerprint> GetSizeImageFingerprintAsync(string imagePath)
	{
		return await Task.Run(delegate
		{
			using FileStream source = File.OpenRead(imagePath);
			string hash = Convert.ToHexString(SHA256.HashData(source));
			string lastWriteUtc = File.GetLastWriteTimeUtc(imagePath).ToString("O", CultureInfo.InvariantCulture);
			return new SizeImageFingerprint(hash, lastWriteUtc);
		});
	}

	private static IReadOnlyList<string> BuildPublishSizeEntries(string sizeText)
	{
		return (from size in BuildDisplaySizes(sizeText)
			where Regex.IsMatch(size, "^\\s*\\d+(?:\\.\\d+)?\\s*(?:cm)?\\s*[*xX×]\\s*\\d+(?:\\.\\d+)?\\s*cm\\b", RegexOptions.IgnoreCase)
			select size).ToArray();
	}

	private static IReadOnlyList<string> BuildDisplaySizes(string rawInput)
	{
		if (string.IsNullOrWhiteSpace(rawInput))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (Match item3 in Regex.Matches(rawInput, "(?<![\\d.])(?<width>\\d+(?:\\.\\d+)?)\\s*(?:cm)?\\s*[*xX×]\\s*(?<length>\\d+(?:\\.\\d+)?)(?:\\s*cm)?(?!\\s*(?:inch|in\\b))", RegexOptions.IgnoreCase))
		{
			if (double.TryParse(item3.Groups["width"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.TryParse(item3.Groups["length"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
			{
				string item = $"{FormatCm(result)}*{FormatCm(result2)}cm/{FormatInch(result)}*{FormatInch(result2)}inch";
				if (hashSet.Add(item))
				{
					list.Add(item);
				}
			}
		}
		if (list.Count > 0)
		{
			return list;
		}
		foreach (Match item4 in Regex.Matches(rawInput, "(?<![\\d.])(?<width>\\d+(?:\\.\\d+)?)\\s*[*xX×]\\s*(?<length>\\d+(?:\\.\\d+)?)\\s*(?:inch|in\\b)", RegexOptions.IgnoreCase))
		{
			string item2 = item4.Groups["width"].Value + "*" + item4.Groups["length"].Value + "inch";
			if (hashSet.Add(item2))
			{
				list.Add(item2);
			}
		}
		return list;
	}

	private static string FormatCm(double value)
	{
		if (!(Math.Abs(value - Math.Round(value)) < 0.001))
		{
			return value.ToString("0.##", CultureInfo.InvariantCulture);
		}
		return Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
	}

	private static string FormatInch(double cmValue)
	{
		return (cmValue / 2.54).ToString("0.##", CultureInfo.InvariantCulture);
	}

	private static MiaoshouPublishRequest CreateMiaoshouPublishRequest(Func<MiaoshouPublishProgressEvent, Task>? progressHandler = null)
	{
		string path = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		string path2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToolBox", "output", "miaoshou", path);
		return new MiaoshouPublishRequest
		{
			ManifestPath = Path.Combine(path2, "batch-manifest.json"),
			ResultPath = Path.Combine(path2, "batch-result.json"),
			EventsPath = Path.Combine(path2, "events.jsonl"),
			LogPath = Path.Combine(path2, "publish.log"),
			ConfigPath = Path.Combine(AppContext.BaseDirectory, "config", "miaoshou.json"),
			ProgressHandler = progressHandler
		};
	}

	private void OnFolderScanProgress(FolderScanProgress progress)
	{
		LoadingTitle = progress.Stage;
		IsScanProgressIndeterminate = progress.TotalFolders <= 0;
		ScanProgressValue = progress.Percent;
		string value = ((progress.TotalFolders > 0) ? $"已扫描文件夹 {progress.ProcessedFolders}/{progress.TotalFolders} 个" : $"已发现文件夹 {progress.ProcessedFolders} 个");
		LoadingDetail = $"{value}，已发现图片 {progress.ImageCount} 张";
		ScanProgressText = ((progress.TotalFolders > 0) ? $"{progress.Percent:0}%" : "读取目录结构中");
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
		CancellationToken token = _previewCancellationTokenSource.Token;
		if (request == null)
		{
			ClearPreview();
			return;
		}
		_previewImagePath = request.FilePath;
		PreviewMeta = $"大小：{FormatFileSize(request.FileSize)}{Environment.NewLine}完整路径：{request.FilePath}";
		PreviewImageSource = null;
		try
		{
			BitmapSource previewImageSource = await Task.Run(() => ImageBitmapLoader.LoadFromFile(request.FilePath, 1800), token);
			if (!token.IsCancellationRequested)
			{
				PreviewImageSource = previewImageSource;
			}
		}
		catch (Exception ex)
		{
			if (!token.IsCancellationRequested)
			{
				PreviewMeta = "预览失败：" + ex.Message;
				_previewImagePath = string.Empty;
				PreviewImageSource = null;
			}
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
		BackupFolderText = (string.IsNullOrWhiteSpace(BackupFolder) ? "备份目录：未设置" : ("备份目录：" + BackupFolder));
		StatusMessage = "请选择文件夹开始筛图。";
	}

	private void ResetGenerationSummary()
	{
		GenerationPageDescription = "用于模板生图与 SP 批量处理";
		GenerationStatusText = "待命";
		GenerationResultModeText = "模式：待命";
		GenerationResultOutputText = "输出目录：" + GenerationOutputDirectory;
		ClearGenerationPromptCards();
		ClearGeneratedImageResultCards();
		_lastExecutedGenerationPromptsOnly = null;
	}

	private void SetActiveTemplateGenerationTab(string? tabKey)
	{
		_activeTemplateGenerationTab = tabKey;
		OnPropertyChanged("TemplateGenerationButtonText");
		OnPropertyChanged("IsCurrentTemplateGenerationRunning");
		OnPropertyChanged("IsCurrentTemplateGenerationStopping");
	}

	private GenerationTabState CreateEmptyGenerationTabState()
	{
		return new GenerationTabState("待命", "模式：待命", "输出目录：" + GenerationOutputDirectory, null, new List<GenerationPromptCardViewModel>(), new List<GeneratedImageResultCardViewModel>());
	}

	private void SaveCurrentGenerationTabState()
	{
		if (_generationTabStates.ContainsKey(_selectedImageGenerateTab) && IsTemplateGenerateTabSelected)
		{
			_generationTabStates[_selectedImageGenerateTab] = new GenerationTabState(GenerationStatusText, GenerationResultModeText, GenerationResultOutputText, _lastExecutedGenerationPromptsOnly, GenerationPromptCards.ToList(), GeneratedImageResultCards.ToList());
		}
	}

	private void RestoreGenerationTabState(string tabKey)
	{
		if (!_generationTabStates.TryGetValue(tabKey, out GenerationTabState value))
		{
			value = CreateEmptyGenerationTabState();
			_generationTabStates[tabKey] = value;
		}
		GenerationStatusText = value.StatusText;
		GenerationResultModeText = value.ResultModeText;
		GenerationResultOutputText = value.ResultOutputText;
		_lastExecutedGenerationPromptsOnly = value.LastExecutedPromptsOnly;
		GenerationPromptCards.Clear();
		foreach (GenerationPromptCardViewModel promptCard in value.PromptCards)
		{
			GenerationPromptCards.Add(promptCard);
		}
		GeneratedImageResultCards.Clear();
		foreach (GeneratedImageResultCardViewModel imageCard in value.ImageCards)
		{
			GeneratedImageResultCards.Add(imageCard);
		}
		OnPropertyChanged("HasGenerationPromptCards");
		OnPropertyChanged("HasGeneratedImageResultCards");
		OnPropertyChanged("HasAnyGenerationResultCards");
		OnPropertyChanged("HasSelectedGeneratedImages");
		OnPropertyChanged("CanGenerateSkuFromTemplate");
		_sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
	}

	private void ResetSpBatchSummary()
	{
		SpBatchStatusText = "待命";
		SpBatchColorStatusText = "待命";
		SpBatchSummaryText = "用于批量创建日期目录、SKU 结构和多色 SKU 图。";
		SpBatchResultRootText = "日期目录：" + SpBatchOutputDirectory;
		SpBatchResultStatsText = "SKU 数量：0  总任务：0  成功：0  跳过：0  失败：0";
		ClearSpBatchResultCards();
		ClearSpBatchMasterImageCards();
		ClearSpBatchImageResultCards();
	}

	private void ResetSkuOptimizeSummary()
	{
		SkuOptimizeStatusText = "待命";
		SkuOptimizeSummaryText = "用于在不改动画面主体、场景和构图的前提下，只优化卷轴长度和直径。";
		SkuOptimizeResultRootText = (string.IsNullOrWhiteSpace(SkuOptimizeOutputDirectory) ? "结果目录：未设置" : ("结果目录：" + SkuOptimizeOutputDirectory));
		SkuOptimizeResultStatsText = "总任务：0  成功：0  跳过：0  失败：0";
		UpdateSkuOptimizeStats(0, 0, 0, 0);
		ClearSkuOptimizeResultCards();
		ClearSkuOptimizeImageResultCards();
	}

	private void RefreshSkuResults()
	{
		ClearSpBatchResultCards();
		ClearSpBatchMasterImageCards();
		ClearSpBatchImageResultCards();
		ResetSpBatchSummary();
		StatusMessage = "已清空 SKU 结果展示，本地图片文件未删除。";
	}

	private void UpdateSkuOptimizeStats(int totalCount, int successCount, int skippedCount, int failedCount)
	{
		_skuOptimizeStatsTotalCount = totalCount;
		_skuOptimizeStatsSuccessCount = successCount;
		_skuOptimizeStatsSkippedCount = skippedCount;
		_skuOptimizeStatsFailedCount = failedCount;
		OnPropertyChanged("SkuOptimizeStatsTotalText");
		OnPropertyChanged("SkuOptimizeStatsSuccessText");
		OnPropertyChanged("SkuOptimizeStatsSkippedText");
		OnPropertyChanged("SkuOptimizeStatsFailedText");
	}

	private void UpdateSummaryForTab(WorkspaceTabViewModel tab)
	{
		LoadedFolderText = tab.GetCurrentFolderText();
		SelectionSummaryText = tab.GetSelectionSummaryText();
		BackupFolderText = (string.IsNullOrWhiteSpace(BackupFolder) ? "备份目录：未设置" : ("备份目录：" + BackupFolder));
	}

	private void OnTabRequestedActivate(WorkspaceTabViewModel tab)
	{
		SelectedTab = tab;
	}

	private void OnTabRequestedClose(WorkspaceTabViewModel tab)
	{
		if (WorkspaceTabs.Contains(tab))
		{
			tab.RequestedActivate -= OnTabRequestedActivate;
			tab.RequestedClose -= OnTabRequestedClose;
			tab.CardImageFilesAdded -= OnTabCardImageFilesAdded;
			tab.SelectionContextChanged -= OnTabSelectionContextChanged;
			tab.StatusChanged -= OnTabStatusChanged;
			tab.PreviewRequested -= OnPreviewRequested;
			int val = WorkspaceTabs.IndexOf(tab);
			WorkspaceTabs.Remove(tab);
			OnPropertyChanged("HasTabs");
			if (WorkspaceTabs.Count == 0)
			{
				EnsurePlaceholderTab();
				return;
			}
			int index = Math.Min(val, WorkspaceTabs.Count - 1);
			SelectedTab = WorkspaceTabs[index];
		}
	}

	private void OnTabSelectionContextChanged(WorkspaceTabViewModel tab)
	{
		if (SelectedTab == tab)
		{
			UpdateSummaryForTab(tab);
			OnPropertyChanged("SelectedTab");
			_openCurrentFolderCommand.RaiseCanExecuteChanged();
		}
	}

	private void OnTabStatusChanged(string message)
	{
		StatusMessage = message;
		if (SelectedTab != null)
		{
			UpdateSummaryForTab(SelectedTab);
			_openCurrentFolderCommand.RaiseCanExecuteChanged();
		}
	}

	private void SetSelectedSection(string section)
	{
		if (!(section == _selectedSection))
		{
			_selectedSection = section;
			OnPropertyChanged("IsReviewWorkspaceSelected");
			OnPropertyChanged("IsImageGenerateSelected");
			OnPropertyChanged("IsTemplateManagerSelected");
		}
	}

	private void SetSelectedImageGenerateTab(string tabKey)
	{
		if (!(_selectedImageGenerateTab == tabKey))
		{
			if (IsTemplateGenerateTabSelected)
			{
				SaveCurrentGenerationTabState();
			}
			_selectedImageGenerateTab = tabKey;
			GenerationTemplateOptions.Clear();
			switch (tabKey)
			{
			case "main-image-generate":
			case "scene-image-generate":
			case "compare-image-generate":
				RestoreGenerationTabState(tabKey);
				break;
			}
			OnPropertyChanged("IsTemplateGenerateTabSelected");
			OnPropertyChanged("IsMainImageGenerateTabSelected");
			OnPropertyChanged("IsSpBatchTabSelected");
			OnPropertyChanged("IsSkuOptimizeTabSelected");
			OnPropertyChanged("IsSceneImageGenerateTabSelected");
			OnPropertyChanged("IsCompareImageGenerateTabSelected");
			OnPropertyChanged("IsSceneGenerateTabSelected");
			OnPropertyChanged("IsCompareGenerateTabSelected");
			OnPropertyChanged("IsTemplateImageGenerateTabSelected");
			OnPropertyChanged("CurrentGenerationImageType");
			OnPropertyChanged("CurrentImageTemplateType");
			OnPropertyChanged("CurrentGenerationTabTitle");
			OnPropertyChanged("CurrentTemplateGenerateTitle");
			OnPropertyChanged("ShouldShowGenerationImageActionButtons");
			OnPropertyChanged("GenerationTemplateSelectionText");
			OnPropertyChanged("TemplateGenerationButtonText");
			OnPropertyChanged("IsCurrentTemplateGenerationRunning");
			OnPropertyChanged("IsCurrentTemplateGenerationStopping");
		}
	}

	private void SetSpBatchMode(SpBatchMode mode)
	{
		if (_spBatchMode != mode)
		{
			_spBatchMode = mode;
			OnPropertyChanged("IsSpBatchPrepareOnly");
			OnPropertyChanged("IsSpBatchGenerateMode");
		}
	}

	private static int CountFolders(IEnumerable<FolderNode> nodes)
	{
		return nodes.Sum((Func<FolderNode, int>)CountFolders);
	}

	private static int CountFolders(FolderNode node)
	{
		return 1 + ((IEnumerable<FolderNode>)node.Children).Sum((Func<FolderNode, int>)CountFolders);
	}

	private static int CountImages(IEnumerable<FolderNode> nodes)
	{
		return nodes.Sum((Func<FolderNode, int>)CountImages);
	}

	private static int CountImages(FolderNode node)
	{
		return node.Images.Count + ((IEnumerable<FolderNode>)node.Children).Sum((Func<FolderNode, int>)CountImages);
	}

	private static string FormatFileSize(long bytes)
	{
		string[] array = new string[4] { "B", "KB", "MB", "GB" };
		double num = bytes;
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		return $"{num:0.##} {array[num2]}";
	}

	private static string ResolveTemplateLibraryPath()
	{
		string baseDirectory = AppContext.BaseDirectory;
		string[] array = new string[3]
		{
			Path.Combine(baseDirectory, "data", "workspace", "temp", "文生图模板库_Codex.xlsx"),
			Path.Combine(WorkspaceDefaults.DefaultTempFolder, "文生图模板库_Codex.xlsx"),
			"D:\\temu_auto\\temp\\文生图模板库_Codex.xlsx"
		};
		return array.FirstOrDefault(File.Exists) ?? array[0];
	}

	private static string ResolveImage2ScriptPath()
	{
		string baseDirectory = AppContext.BaseDirectory;
		string[] array = new string[2]
		{
			Path.Combine(baseDirectory, "tools", "python", "image2-generate", "scripts", "generate_image.py"),
			Path.Combine("D:\\new_project\\tools\\python", "image2-generate", "scripts", "generate_image.py")
		};
		return array.FirstOrDefault(File.Exists) ?? array[0];
	}

	private static string GetUserImageGenerationKeyPath()
	{
		string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string root = string.IsNullOrWhiteSpace(localAppData) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".toolbox") : Path.Combine(localAppData, "ToolBox");
		return Path.Combine(root, "image2_api_key");
	}

	private static string GetBundledImageGenerationKeyPath()
	{
		return Path.Combine(AppContext.BaseDirectory, "tools", "python", "image2-generate", ".image2_api_key");
	}

	private void OpenImageGenerationKeyDialog()
	{
		string userKeyPath = GetUserImageGenerationKeyPath();
		string bundledKeyPath = GetBundledImageGenerationKeyPath();
		string keyPath = File.Exists(userKeyPath) ? userKeyPath : bundledKeyPath;
		ImageGenerationKeyText = File.Exists(keyPath) ? File.ReadAllText(keyPath, Encoding.UTF8).Trim().TrimStart('\ufeff') : string.Empty;
		OnPropertyChanged("ImageGenerationKeyPathText");
		IsImageGenerationKeyDialogOpen = true;
	}

	private void SaveImageGenerationKey()
	{
		string key = ImageGenerationKeyText.Trim().TrimStart('\ufeff');
		if (string.IsNullOrWhiteSpace(key))
		{
			StatusMessage = "至少填写一个生图 Key。";
			return;
		}
		if (!string.IsNullOrWhiteSpace(key))
		{
			WriteImageGenerationKey(GetUserImageGenerationKeyPath(), key);
		}
		StatusMessage = "已保存生图 Key。";
		CloseImageGenerationKeyDialog();
	}

	private static void WriteImageGenerationKey(string path, string key)
	{
		string? directoryName = Path.GetDirectoryName(path);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		File.WriteAllText(path, key, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	private void CloseImageGenerationKeyDialog()
	{
		IsImageGenerationKeyDialogOpen = false;
	}

	private void SelectImageGenerationProvider(string? provider)
	{
		string normalized = NormalizeImageGenerationProvider(provider);
		if (string.Equals(SelectedImageGenerationProvider, normalized, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		SelectedImageGenerationProvider = normalized;
		PersistUserPathSettings();
		StatusMessage = "生图接口已切换为：" + ImageGenerationProviderText;
	}

	private static string NormalizeImageGenerationProvider(string? provider)
	{
		return Image2GenerationProvider;
	}

	private static string ResolveDefaultFolderPickerDirectory()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
		{
			return folderPath;
		}
		string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (!string.IsNullOrWhiteSpace(folderPath2))
		{
			return folderPath2;
		}
		return string.Empty;
	}

	private void ApplyUserPathSettings(AppUserPathsState state)
	{
		BackupFolder = NormalizeWritableWorkspacePath(state.BackupFolder ?? string.Empty);
		TemplateLibraryPath = state.TemplateLibraryPath ?? string.Empty;
		GenerationOutputDirectory = NormalizeWritableWorkspacePath(state.GenerationOutputDirectory ?? string.Empty);
		SpBatchInputDirectory = NormalizeWritableWorkspacePath(state.SpBatchInputDirectory ?? string.Empty);
		SpBatchOutputDirectory = NormalizeWritableWorkspacePath(state.SpBatchOutputDirectory ?? string.Empty);
		SkuOptimizeOutputDirectory = NormalizeWritableWorkspacePath(state.SkuOptimizeOutputDirectory ?? string.Empty);
		SelectedImageGenerationProvider = state.ImageGenerationProvider;
		TitleChineseOnly = state.TitleChineseOnly;
		long restoredSpBatchColorGroupId = state.SelectedSpBatchColorTemplateGroupId;
		_spBatchSelectedColorNamesByGroupId.Clear();
		foreach (KeyValuePair<long, string[]> item in state.SpBatchSelectedColorNamesByGroupId ?? new Dictionary<long, string[]>())
		{
			string[] selectedNames = item.Value?
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Select(name => name.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray() ?? Array.Empty<string>();
			if (selectedNames.Length > 0)
			{
				_spBatchSelectedColorNamesByGroupId[item.Key] = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
			}
		}
		ColorTemplateGroupViewModel? spBatchGroup = ColorTemplateGroups.FirstOrDefault(item => item.Id == restoredSpBatchColorGroupId)
			?? _selectedSpBatchColorTemplateGroupForGeneration
			?? ColorTemplateGroups.FirstOrDefault();
		_selectedSpBatchColorTemplateGroupId = spBatchGroup?.Id ?? 0;
		_selectedSpBatchColorTemplateGroupForGeneration = spBatchGroup;
		RefreshSpBatchColorSelectionForCurrentGroup();
		OnPropertyChanged("SelectedSpBatchColorTemplateGroupForGeneration");
		OnPropertyChanged("SpBatchColorSelectionTitle");
		OnPropertyChanged("SpBatchColorSelectionSummaryText");
		_openSpBatchColorSelectionCommand.RaiseCanExecuteChanged();
		_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
	}

	private static string NormalizeWritableWorkspacePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		try
		{
			return WorkspaceDefaults.IsPackagedWorkspacePath(path) ? WorkspaceDefaults.ToUserWorkspacePath(path) : path;
		}
		catch
		{
			return path;
		}
	}

	private void PersistUserPathSettings()
	{
		IAppSettingsService appSettingsService = _appSettingsService;
		AppUserPathsState appUserPathsState = new AppUserPathsState();
		WorkspaceTabViewModel? selectedTab = SelectedTab;
		appUserPathsState.ReviewRootFolder = ((selectedTab != null && !selectedTab.IsPlaceholder) ? SelectedTab.RootFolder : string.Empty);
		appUserPathsState.BackupFolder = BackupFolder;
		appUserPathsState.TemplateLibraryPath = TemplateLibraryPath;
		appUserPathsState.GenerationOutputDirectory = GenerationOutputDirectory;
		appUserPathsState.SpBatchInputDirectory = SpBatchInputDirectory;
		appUserPathsState.SpBatchOutputDirectory = SpBatchOutputDirectory;
		appUserPathsState.SkuOptimizeOutputDirectory = SkuOptimizeOutputDirectory;
		appUserPathsState.ImageGenerationProvider = SelectedImageGenerationProvider;
		appUserPathsState.TitleChineseOnly = TitleChineseOnly;
		appUserPathsState.SelectedSpBatchColorTemplateGroupId = _selectedSpBatchColorTemplateGroupId;
		appUserPathsState.SpBatchSelectedColorNamesByGroupId = _spBatchSelectedColorNamesByGroupId.ToDictionary(
			item => item.Key,
			item => item.Value.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
		appSettingsService.SaveUserPaths(appUserPathsState);
	}

	private static bool TryParsePositiveInt(string text, string fieldName, out int value)
	{
		if (!int.TryParse(text, out value) || value <= 0)
		{
			throw new InvalidOperationException(fieldName + "必须是大于 0 的整数。");
		}
		return true;
	}

	private static bool TryParsePositiveDouble(string text, string fieldName, out double value)
	{
		if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value <= 0.0)
		{
			throw new InvalidOperationException(fieldName + "必须是大于 0 的数字。");
		}
		return true;
	}

	private static bool TryParseSkuOptimizeMultiplier(string text, string fieldName, out double value)
	{
		if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < 0.0)
		{
			throw new InvalidOperationException(fieldName + "必须是大于或等于 0 的数字。输入 0 表示不变。");
		}
		return true;
	}

	private static bool TryParseSkuOptimizeLengthFactor(string text, string fieldName, bool shrink, out double value)
	{
		if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double factor) || factor < 0.0)
		{
			throw new InvalidOperationException(fieldName + "必须是大于或等于 0 的数字。输入 0 表示不变。");
		}
		if (factor == 0.0)
		{
			value = 0.0;
			return true;
		}
		if (!shrink)
		{
			value = factor;
			return true;
		}
		double targetRatio = factor <= 1.0 ? factor : (1.0 / factor);
		value = -targetRatio;
		return true;
	}

	private static bool HasVisibleText(string? text)
	{
		return !string.IsNullOrWhiteSpace(text) && text.Any((char ch) => !char.IsWhiteSpace(ch) && !char.IsControl(ch));
	}

	private static bool HasSkuMasterColorToken(string fileName)
	{
		string normalizedName = NormalizeSkuMasterColorText(Path.GetFileNameWithoutExtension(fileName));
		return SkuMasterColorTokens.Any((string token) => normalizedName.Contains(NormalizeSkuMasterColorText(token), StringComparison.OrdinalIgnoreCase));
	}

	private static string NormalizeSkuMasterColorText(string text)
	{
		return Regex.Replace(text, @"[\s_\-#]+", string.Empty).ToLowerInvariant();
	}

	private static string EnsureImageExtension(string fileName, string fallbackExtension)
	{
		if (!string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
		{
			return Path.GetFileName(fileName);
		}
		return Path.GetFileName(fileName) + (string.IsNullOrWhiteSpace(fallbackExtension) ? ".png" : fallbackExtension);
	}

	private static string CopySkuMasterImageToRenamedTempFile(string sourcePath, string fileName)
	{
		string directory = Path.Combine(Path.GetTempPath(), "ImageKeeper", "sku-master-renamed");
		Directory.CreateDirectory(directory);
		string safeName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
		string targetPath = Path.Combine(directory, safeName);
		File.Copy(sourcePath, targetPath, overwrite: true);
		return targetPath;
	}

	private static string? ShowSkuMasterRenameDialog(string originalFileName)
	{
		Window dialog = new Window
		{
			Title = "修改 SKU 母图文件名",
			Width = 420.0,
			Height = 210.0,
			WindowStartupLocation = WindowStartupLocation.CenterScreen,
			ResizeMode = ResizeMode.NoResize,
			Background = Brushes.White,
			ShowInTaskbar = false
		};
		Grid grid = new Grid
		{
			Margin = new Thickness(18.0)
		};
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		TextBlock title = new TextBlock
		{
			Text = "文件名需要包含颜色",
			FontSize = 16.0,
			FontWeight = FontWeights.SemiBold
		};
		grid.Children.Add(title);
		TextBlock description = new TextBlock
		{
			Margin = new Thickness(0.0, 8.0, 0.0, 10.0),
			Text = "例如：黑色.png、深棕色.png、宝蓝色.png",
			Foreground = new SolidColorBrush(Color.FromRgb(96, 98, 102))
		};
		Grid.SetRow(description, 1);
		grid.Children.Add(description);
		TextBox input = new TextBox
		{
			Text = originalFileName,
			Padding = new Thickness(8.0, 5.0, 8.0, 5.0)
		};
		Grid.SetRow(input, 2);
		grid.Children.Add(input);
		StackPanel buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button cancelButton = new Button
		{
			Content = "取消",
			Width = 76.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			IsCancel = true
		};
		Button okButton = new Button
		{
			Content = "确定",
			Width = 76.0,
			IsDefault = true
		};
		buttons.Children.Add(cancelButton);
		buttons.Children.Add(okButton);
		Grid.SetRow(buttons, 4);
		grid.Children.Add(buttons);
		string? result = null;
		okButton.Click += delegate
		{
			result = input.Text;
			dialog.DialogResult = true;
		};
		cancelButton.Click += delegate
		{
			dialog.DialogResult = false;
		};
		dialog.Content = grid;
		input.SelectAll();
		input.Focus();
		return dialog.ShowDialog() == true ? result : null;
	}

	private void ApplyGenerationResult(TemplateGenerateResult result)
	{
		GenerationResultModeText = "模式：" + ((result.Mode == "prompts_only") ? "只出提示词" : "直接生图");
		GenerationResultOutputText = "输出目录：" + result.OutputDirectory;
		if (!string.Equals(result.Mode, "prompts_only", StringComparison.OrdinalIgnoreCase))
		{
			OnPropertyChanged("HasGenerationPromptCards");
			OnPropertyChanged("HasAnyGenerationResultCards");
			return;
		}
		if (result.Prompts.Count > 0)
		{
			int index;
			for (index = 0; index < result.Prompts.Count; index++)
			{
				string metaText = string.Empty;
				TemplateGenerateItem templateGenerateItem = result.Items.FirstOrDefault((TemplateGenerateItem x) => x.Index == index + 1);
				if (templateGenerateItem != null)
				{
					metaText = string.Join(Environment.NewLine, new string[2]
					{
						string.IsNullOrWhiteSpace(templateGenerateItem.FileName) ? null : ("文件名：" + templateGenerateItem.FileName),
						string.IsNullOrWhiteSpace(templateGenerateItem.ImagePath) ? null : ("图片路径：" + templateGenerateItem.ImagePath)
					}.Where((string text) => !string.IsNullOrWhiteSpace(text)));
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
			foreach (TemplateGenerateItem item in result.Items)
			{
				GenerationPromptCards.Add(new GenerationPromptCardViewModel
				{
					Title = $"结果 {item.Index}",
					PromptText = item.Prompt,
					MetaText = string.Join(Environment.NewLine, new string[2]
					{
						string.IsNullOrWhiteSpace(item.FileName) ? null : ("文件名：" + item.FileName),
						string.IsNullOrWhiteSpace(item.ImagePath) ? null : ("图片路径：" + item.ImagePath)
					}.Where((string text) => !string.IsNullOrWhiteSpace(text)))
				});
			}
		}
		OnPropertyChanged("HasGenerationPromptCards");
		OnPropertyChanged("HasAnyGenerationResultCards");
	}

	private void ClearGenerationPromptCards()
	{
		if (GenerationPromptCards.Count != 0)
		{
			GenerationPromptCards.Clear();
			OnPropertyChanged("HasGenerationPromptCards");
			OnPropertyChanged("HasAnyGenerationResultCards");
		}
	}

	private void UpdateSpBatchStats(int skuCount, int totalCount, int successCount, int skippedCount, int failedCount)
	{
		_spBatchStatsSkuCount = skuCount;
		_spBatchStatsTotalCount = totalCount;
		_spBatchStatsSuccessCount = successCount;
		_spBatchStatsSkippedCount = skippedCount;
		_spBatchStatsFailedCount = failedCount;
		OnPropertyChanged("SpBatchStatsSkuCountText");
		OnPropertyChanged("SpBatchStatsTotalText");
		OnPropertyChanged("SpBatchStatsSuccessText");
		OnPropertyChanged("SpBatchStatsSkippedText");
		OnPropertyChanged("SpBatchStatsFailedText");
	}

	private void SetActiveSpBatchMode(SpBatchMode? mode)
	{
		_activeSpBatchMode = mode;
		OnPropertyChanged("SpBatchButtonText");
		OnPropertyChanged("SpBatchColorButtonText");
		OnPropertyChanged("IsSpBatchMasterRunning");
		OnPropertyChanged("IsSpBatchColorRunning");
	}

	private void SetSpBatchStatusForMode(SpBatchMode mode, string statusText)
	{
		if (mode == SpBatchMode.GenerateColors)
		{
			SpBatchColorStatusText = statusText;
		}
		else
		{
			SpBatchStatusText = statusText;
		}
	}

	private void ApplySpBatchResult(SpBatchResult result)
	{
		SpBatchStatusText = result.Mode switch
		{
			"dry_run" => "预检查完成", 
			"prepared" => "目录结构创建完成", 
			"master_generated" => (result.FailedCount > 0) ? "SKU 母图部分失败" : "SKU 母图生成完成", 
			"recolor_generated" => (result.FailedCount > 0) ? "颜色 SKU 部分失败" : "颜色 SKU 生成完成", 
			_ => (result.FailedCount > 0) ? "部分执行失败" : "批处理完成", 
		};
		SpBatchSummaryText = result.Mode switch
		{
			"dry_run" => $"预检查完成，共规划 {result.Results.Count} 个颜色任务。", 
			"prepared" => $"目录结构创建完成，共准备 {result.PreparedBundles.Count} 个 SKU 目录。", 
			"master_generated" => $"SKU 母图生成完成，共 {result.Results.Count} 个任务，成功 {result.SuccessCount}，跳过 {result.SkippedCount}，失败 {result.FailedCount}。", 
			"recolor_generated" => $"颜色 SKU 生成完成，共 {result.Results.Count} 个任务，成功 {result.SuccessCount}，跳过 {result.SkippedCount}，失败 {result.FailedCount}。", 
			_ => $"批处理完成，共 {result.Results.Count} 个任务，成功 {result.SuccessCount}，跳过 {result.SkippedCount}，失败 {result.FailedCount}。", 
		};
		SpBatchResultRootText = "日期目录：" + result.DatedRoot;
		SpBatchResultStatsText = $"SKU 数量：{CountSpDirectories(result)}  总任务：{result.Results.Count}  成功：{result.SuccessCount}  跳过：{result.SkippedCount}  失败：{result.FailedCount}";
		UpdateSpBatchStats(CountSpDirectories(result), result.Results.Count, result.SuccessCount, result.SkippedCount, result.FailedCount);
		if (string.Equals(result.Mode, "recolor_generated", StringComparison.OrdinalIgnoreCase))
		{
			SpBatchColorStatusText = ((result.FailedCount > 0) ? "执行失败" : "生成完成");
		}
		else
		{
			SpBatchStatusText = ((result.FailedCount > 0) ? "执行失败" : "生成完成");
		}
		ClearSpBatchResultCards();
		foreach (IGrouping<string, SpBatchJobResult> group in (from item in result.Results
			group item by item.SpDirectory).OrderBy<IGrouping<string, SpBatchJobResult>, string>((IGrouping<string, SpBatchJobResult> grouping) => grouping.Key, StringComparer.OrdinalIgnoreCase))
		{
			string fileName = Path.GetFileName(group.Key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			int num = group.Count((SpBatchJobResult item) => string.Equals(item.Status, "generated", StringComparison.OrdinalIgnoreCase));
			int num2 = group.Count((SpBatchJobResult item) => string.Equals(item.Status, "skipped", StringComparison.OrdinalIgnoreCase));
			int num3 = group.Count((SpBatchJobResult item) => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase));
			int num4 = group.Count((SpBatchJobResult item) => string.Equals(item.Status, "planned", StringComparison.OrdinalIgnoreCase));
			SpBatchBundle spBatchBundle = result.PreparedBundles.FirstOrDefault((SpBatchBundle item) => string.Equals(item.SpDirectory, group.Key, StringComparison.OrdinalIgnoreCase));
			List<string> list = new List<string>();
			if (spBatchBundle != null)
			{
				list.Add("主图目录：" + spBatchBundle.MainDirectory);
				list.Add("SKU 目录：" + spBatchBundle.SkuDirectory);
				list.Add("详情目录：" + spBatchBundle.DetailDirectory);
			}
			foreach (SpBatchJobResult item in group.OrderBy((SpBatchJobResult item) => item.Index))
			{
				list.Add($"#{item.Index}  颜色：{item.Color}  状态：{item.Status}");
				if (!string.IsNullOrWhiteSpace(item.ImagePath))
				{
					list.Add("输出：" + item.ImagePath);
				}
				if (!string.IsNullOrWhiteSpace(item.Error))
				{
					list.Add("错误：" + item.Error);
				}
			}
			string statusText = ((num3 > 0) ? "失败" : ((num4 > 0) ? "已规划" : ((num2 > 0 && num == 0) ? "已跳过" : "完成")));
			string summaryText = $"成功 {num}，跳过 {num2}，失败 {num3}";
			if (num4 > 0)
			{
				summaryText = $"已规划 {num4} 个任务";
			}
			SpBatchResultCards.Add(new SpBatchResultCardViewModel
			{
				Title = fileName,
				SummaryText = summaryText,
				StatusText = statusText,
				DetailText = string.Join(Environment.NewLine, list)
			});
		}
		OnPropertyChanged("HasSpBatchResultCards");
		OnPropertyChanged("HasAnySpBatchResultCards");
		OnPropertyChanged("ShouldShowSpBatchDetailCards");
	}

	private void ClearSpBatchResultCards()
	{
		if (SpBatchResultCards.Count != 0)
		{
			SpBatchResultCards.Clear();
			OnPropertyChanged("HasSpBatchResultCards");
			OnPropertyChanged("HasAnySpBatchResultCards");
			OnPropertyChanged("ShouldShowSpBatchDetailCards");
		}
	}

	private void ClearGeneratedImageResultCards()
	{
		if (GeneratedImageResultCards.Count != 0)
		{
			GeneratedImageResultCards.Clear();
			OnPropertyChanged("HasGeneratedImageResultCards");
			OnPropertyChanged("HasAnyGenerationResultCards");
			OnPropertyChanged("HasSelectedGeneratedImages");
			OnPropertyChanged("CanGenerateSkuFromTemplate");
			_sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
		}
	}

	private void ClearSpBatchImageResultCards()
	{
		if (SpBatchImageResultCards.Count != 0)
		{
			SpBatchImageResultCards.Clear();
			OnPropertyChanged("HasSpBatchImageResultCards");
			OnPropertyChanged("HasAnySpBatchResultCards");
			OnPropertyChanged("ShouldShowSpBatchDetailCards");
		}
	}

	private void ClearSpBatchMasterImageCards()
	{
		if (SpBatchMasterImageCards.Count != 0)
		{
			SpBatchMasterImageCards.Clear();
			OnPropertyChanged("HasSpBatchMasterImageCards");
			OnPropertyChanged("HasAnySpBatchResultCards");
			OnPropertyChanged("ShouldShowSpBatchDetailCards");
			_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
			_sendSelectedSpBatchMasterToSkuOptimizeCommand.RaiseCanExecuteChanged();
		}
	}

	private void ApplyGenerationVisualResult(TemplateGenerateResult result)
	{
		GenerationResultModeText = "模式：" + ((result.Mode == "prompts_only") ? "只出提示词" : "直接生图");
		GenerationResultOutputText = "输出目录：" + result.OutputDirectory;
		if (string.Equals(result.Mode, "prompts_only", StringComparison.OrdinalIgnoreCase))
		{
			if (GenerationPromptCards.Count > 0)
			{
				OnPropertyChanged("HasGenerationPromptCards");
				OnPropertyChanged("HasAnyGenerationResultCards");
			}
			OnPropertyChanged("HasGeneratedImageResultCards");
			OnPropertyChanged("HasAnyGenerationResultCards");
			return;
		}
		foreach (TemplateGenerateItem item in result.Items)
		{
			if (!string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
			{
				GeneratedImageResultCards.Add(new GeneratedImageResultCardViewModel(item.ImagePath, item.FileName, canToggleSelection: true, showRemoveAction: true, OnGeneratedImageSelectionChanged, OnGeneratedImageRemoved));
			}
		}
		OnPropertyChanged("HasGenerationPromptCards");
		OnPropertyChanged("HasGeneratedImageResultCards");
		OnPropertyChanged("HasAnyGenerationResultCards");
		OnPropertyChanged("HasSelectedGeneratedImages");
		OnPropertyChanged("CanGenerateSkuFromTemplate");
		_sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
	}

	private void ApplySpBatchVisualResult(SpBatchResult result)
	{
		ApplySpBatchResult(result);
		foreach (SpBatchJobResult item in (from item in result.Results
			where string.Equals(item.Status, "generated", StringComparison.OrdinalIgnoreCase)
			where !string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath)
			select item).OrderBy<SpBatchJobResult, string>((SpBatchJobResult item) => item.SpDirectory, StringComparer.OrdinalIgnoreCase).ThenBy((SpBatchJobResult item) => item.Index))
		{
			if (string.Equals(item.Stage, "master", StringComparison.OrdinalIgnoreCase))
			{
				string linkedSkuDirectory = (!string.IsNullOrWhiteSpace(item.SpDirectory) ? Path.Combine(item.SpDirectory, "sku") : (Path.GetDirectoryName(item.ImagePath) ?? string.Empty));
				GeneratedImageResultCardViewModel card = new GeneratedImageResultCardViewModel(item.ImagePath, Path.GetFileName(item.ImagePath), canToggleSelection: true, showRemoveAction: true, OnSpBatchMasterSelectionChanged, OnSpBatchMasterImageRemoved, item.SpDirectory, linkedSkuDirectory);
				card.SetSelected(isSelected: true);
				SpBatchMasterImageCards.Add(card);
			}
			else
			{
				SpBatchImageResultCards.Add(new GeneratedImageResultCardViewModel(item.ImagePath, Path.GetFileName(item.ImagePath)));
			}
		}
		OnPropertyChanged("HasSpBatchMasterImageCards");
		OnPropertyChanged("HasSpBatchImageResultCards");
		OnPropertyChanged("HasAnySpBatchResultCards");
		OnPropertyChanged("ShouldShowSpBatchDetailCards");
		_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
	}

	private static int CountSpDirectories(SpBatchResult result)
	{
		return (from path in result.PreparedBundles.Select((SpBatchBundle item) => item.SpDirectory).Concat(result.Results.Select((SpBatchJobResult item) => item.SpDirectory))
			where !string.IsNullOrWhiteSpace(path)
			select path).Distinct<string>(StringComparer.OrdinalIgnoreCase).Count();
	}

	private bool CanSendSelectedImagesToSpBatch()
	{
		if (!IsGenerationPromptsOnly)
		{
			return GeneratedImageResultCards.Any((GeneratedImageResultCardViewModel card) => card.IsSelected);
		}
		return false;
	}

	private void OnGeneratedImageSelectionChanged(GeneratedImageResultCardViewModel _)
	{
		OnPropertyChanged("HasSelectedGeneratedImages");
		OnPropertyChanged("CanGenerateSkuFromTemplate");
		_sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
	}

	private void OnGeneratedImageRemoved(GeneratedImageResultCardViewModel card)
	{
		if (GeneratedImageResultCards.Contains(card))
		{
			GeneratedImageResultCards.Remove(card);
			OnPropertyChanged("HasGeneratedImageResultCards");
			OnPropertyChanged("HasAnyGenerationResultCards");
			OnPropertyChanged("HasSelectedGeneratedImages");
			OnPropertyChanged("CanGenerateSkuFromTemplate");
			_sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
		}
	}

	private void OnSpBatchSourceImageRemoved(GeneratedImageResultCardViewModel card)
	{
		if (SpBatchSourceImageCards.Contains(card))
		{
			SpBatchSourceImageCards.Remove(card);
			OnPropertyChanged("HasSpBatchSourceImageCards");
			_runSpBatchCommand.RaiseCanExecuteChanged();
		}
	}

	private void OnSpBatchMasterImageRemoved(GeneratedImageResultCardViewModel card)
	{
		if (SpBatchMasterImageCards.Contains(card))
		{
			SpBatchMasterImageCards.Remove(card);
			OnPropertyChanged("HasSpBatchMasterImageCards");
			OnPropertyChanged("HasAnySpBatchResultCards");
			OnPropertyChanged("ShouldShowSpBatchDetailCards");
			_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
		}
	}

	private void OnSpBatchMasterSelectionChanged(GeneratedImageResultCardViewModel selectedCard)
	{
		_sendSelectedSpBatchMasterToSkuOptimizeCommand.RaiseCanExecuteChanged();
	}

	private void AddSpBatchMasterImage(string imagePath, string? fileName = null, string? linkedCardPath = null, string? linkedSkuDirectory = null)
	{
		if (File.Exists(imagePath) && IsSupportedImageFile(imagePath))
		{
			GeneratedImageResultCardViewModel card = new GeneratedImageResultCardViewModel(imagePath, fileName ?? Path.GetFileName(imagePath), canToggleSelection: true, showRemoveAction: true, OnSpBatchMasterSelectionChanged, OnSpBatchMasterImageRemoved, linkedCardPath, linkedSkuDirectory);
			card.SetSelected(isSelected: true);
			SpBatchMasterImageCards.Add(card);
			OnPropertyChanged("HasSpBatchMasterImageCards");
			OnPropertyChanged("HasAnySpBatchResultCards");
			OnPropertyChanged("ShouldShowSpBatchDetailCards");
			_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
		}
	}

	private bool TryResolveLinkedSpBatchCard(string imagePath, out string cardPath, out string skuDirectory)
	{
		cardPath = string.Empty;
		skuDirectory = string.Empty;
		string fullImagePath;
		try
		{
			fullImagePath = Path.GetFullPath(imagePath);
		}
		catch
		{
			return false;
		}
		foreach (RootCardViewModel card in WorkspaceTabs.SelectMany((WorkspaceTabViewModel tab) => tab.RootCards))
		{
			string candidateCardPath = card.AutoPublishKeyPath;
			if (string.IsNullOrWhiteSpace(candidateCardPath) || !IsPathInsideDirectory(fullImagePath, candidateCardPath))
			{
				continue;
			}
			cardPath = Path.GetFullPath(candidateCardPath);
			skuDirectory = Path.Combine(cardPath, "sku");
			return true;
		}
		return false;
	}

	private static bool IsPathInsideDirectory(string path, string directory)
	{
		try
		{
			string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			if (string.Equals(fullPath, fullDirectory, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(fullDirectory + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private void AddSpBatchMasterImageWithColorNameCheck(string imagePath)
	{
		if (!File.Exists(imagePath) || !IsSupportedImageFile(imagePath))
		{
			return;
		}
		string displayFileName = Path.GetFileName(imagePath);
		string targetImagePath = imagePath;
		string? linkedCardPath = null;
		string? linkedSkuDirectory = null;
		if (!HasSkuMasterColorToken(displayFileName))
		{
			string? renamedFileName = ShowSkuMasterRenameDialog(displayFileName);
			if (string.IsNullOrWhiteSpace(renamedFileName))
			{
				StatusMessage = "已取消添加 SKU 母图。";
				return;
			}
			displayFileName = EnsureImageExtension(renamedFileName.Trim(), Path.GetExtension(imagePath));
			if (!HasSkuMasterColorToken(displayFileName))
			{
				MessageBox.Show("文件名需要包含颜色，例如：黑色.png、深棕色.png、宝蓝色.png。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
				StatusMessage = "SKU 母图文件名未包含颜色，已取消添加。";
				return;
			}
		}
		if (TryResolveLinkedSpBatchCard(imagePath, out string cardPath, out string skuDirectory))
		{
			linkedCardPath = cardPath;
			linkedSkuDirectory = skuDirectory;
		}
		else if (!string.Equals(displayFileName, Path.GetFileName(imagePath), StringComparison.OrdinalIgnoreCase))
		{
			targetImagePath = CopySkuMasterImageToRenamedTempFile(imagePath, displayFileName);
		}
		AddSpBatchMasterImage(targetImagePath, displayFileName, linkedCardPath, linkedSkuDirectory);
	}

	private void AddSpBatchMasterImages(IEnumerable<string> filePaths)
	{
		IReadOnlyList<string> images = filePaths.Where(File.Exists).Where(IsSupportedImageFile).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		if (images.Count > 0)
		{
			ClearSpBatchImageResultCards();
			foreach (string image in images)
			{
				AddSpBatchMasterImageWithColorNameCheck(image);
			}
			StatusMessage = "已替换 SKU 母图，后续颜色图将基于这张母图生成。";
		}
	}

	private bool TransferSpBatchMasterToSkuOptimize(bool showPromptWhenMissing)
	{
		GeneratedImageResultCardViewModel[] selectedCards = SpBatchMasterImageCards
			.Where((GeneratedImageResultCardViewModel card) => card.IsSelected && File.Exists(card.ImagePath) && IsSupportedImageFile(card.ImagePath))
			.ToArray();
		if (selectedCards.Length == 0)
		{
			if (showPromptWhenMissing)
			{
				MessageBox.Show("请先选中一张 SKU 母图。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			return false;
		}
		SkuOptimizeSourceImageCards.Clear();
		foreach (GeneratedImageResultCardViewModel masterCard in selectedCards)
		{
			SkuOptimizeSourceImageCards.Add(new GeneratedImageResultCardViewModel(masterCard.ImagePath, masterCard.FileName, canToggleSelection: false, showRemoveAction: true, null, OnSkuOptimizeSourceImageRemoved, masterCard.LinkedCardPath, masterCard.LinkedSkuDirectory));
		}
		OnPropertyChanged("HasSkuOptimizeSourceImageCards");
		_runSkuOptimizeCommand.RaiseCanExecuteChanged();
		StatusMessage = $"已将 {selectedCards.Length} 张 SKU 母图带入 SKU 图优化。";
		return true;
	}

	private void SendSelectedImagesToSpBatch()
	{
		GeneratedImageResultCardViewModel[] array = GeneratedImageResultCards.Where((GeneratedImageResultCardViewModel card) => card.IsSelected).ToArray();
		if (array.Length != 0)
		{
			GeneratedImageResultCardViewModel[] array2 = array;
			foreach (GeneratedImageResultCardViewModel generatedImageResultCardViewModel in array2)
			{
				AddSpBatchSourceImage(generatedImageResultCardViewModel.ImagePath, generatedImageResultCardViewModel.FileName);
			}
			SetSelectedImageGenerateTab("sp-batch");
			StatusMessage = $"已将 {array.Length} 张图片加入 SP 批处理区域。";
		}
	}

	private void AddGenerationImages(IEnumerable<string> filePaths)
	{
		bool flag = false;
		foreach (string filePath in filePaths.Where(File.Exists).Where(IsSupportedImageFile).Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			if (!GeneratedImageResultCards.Any((GeneratedImageResultCardViewModel card) => string.Equals(card.ImagePath, filePath, StringComparison.OrdinalIgnoreCase)))
			{
				GeneratedImageResultCards.Add(new GeneratedImageResultCardViewModel(filePath, Path.GetFileName(filePath), canToggleSelection: true, showRemoveAction: true, OnGeneratedImageSelectionChanged, OnGeneratedImageRemoved));
				ObservableCollection<GeneratedImageResultCardViewModel> generatedImageResultCards = GeneratedImageResultCards;
				generatedImageResultCards[generatedImageResultCards.Count - 1].SetSelected(isSelected: true);
				flag = true;
			}
		}
		if (flag)
		{
			OnPropertyChanged("HasGeneratedImageResultCards");
			OnPropertyChanged("HasAnyGenerationResultCards");
			OnPropertyChanged("HasSelectedGeneratedImages");
			OnPropertyChanged("CanGenerateSkuFromTemplate");
			_sendSelectedImagesToSpBatchCommand.RaiseCanExecuteChanged();
		}
	}

	private void AddSpBatchSourceImage(string imagePath, string? fileName = null)
	{
		if (File.Exists(imagePath) && IsSupportedImageFile(imagePath) && !SpBatchSourceImageCards.Any((GeneratedImageResultCardViewModel card) => string.Equals(card.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase)))
		{
			SpBatchSourceImageCards.Add(new GeneratedImageResultCardViewModel(imagePath, fileName ?? Path.GetFileName(imagePath), canToggleSelection: false, showRemoveAction: true, null, OnSpBatchSourceImageRemoved));
			OnPropertyChanged("HasSpBatchSourceImageCards");
			_runSpBatchCommand.RaiseCanExecuteChanged();
		}
	}

	private void AddSpBatchSourceImages(IEnumerable<string> filePaths)
	{
		foreach (string filePath in filePaths)
		{
			AddSpBatchSourceImage(filePath);
		}
		StatusMessage = $"SP 批处理区域当前共有 {SpBatchSourceImageCards.Count} 张图片。";
	}

	private string CreateSpBatchStagingInputDirectory(IEnumerable<GeneratedImageResultCardViewModel>? sourceCards = null)
	{
		string text = Path.Combine(Path.GetTempPath(), "ImageKeeper", "sp-batch-staging", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
		Directory.CreateDirectory(text);
		HashSet<string> usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<SpBatchLinkedBundleManifestItem> linkedItems = new List<SpBatchLinkedBundleManifestItem>();
		foreach (GeneratedImageResultCardViewModel item in sourceCards ?? SpBatchSourceImageCards)
		{
			string uniqueStagingFileName = GetUniqueStagingFileName(item, usedFileNames);
			File.Copy(destFileName: Path.Combine(text, uniqueStagingFileName), sourceFileName: item.ImagePath, overwrite: true);
			if (item.IsLinkedToExistingCard)
			{
				linkedItems.Add(new SpBatchLinkedBundleManifestItem(uniqueStagingFileName, item.LinkedCardPath, item.LinkedSkuDirectory, item.ImagePath));
			}
		}
		if (linkedItems.Count > 0)
		{
			string manifestPath = Path.Combine(text, SpBatchLinkedBundlesManifestName);
			string manifestJson = JsonSerializer.Serialize(new { items = linkedItems }, new JsonSerializerOptions { WriteIndented = true });
			File.WriteAllText(manifestPath, manifestJson, Encoding.UTF8);
		}
		return text;
	}

	private static string GetUniqueStagingFileName(GeneratedImageResultCardViewModel card, ISet<string> usedFileNames)
	{
		string fileName = Path.GetFileName(card.FileName);
		if (string.IsNullOrWhiteSpace(fileName))
		{
			fileName = Path.GetFileName(card.ImagePath);
		}
		string extension = Path.GetExtension(fileName);
		if (string.IsNullOrWhiteSpace(extension))
		{
			extension = Path.GetExtension(card.ImagePath);
		}
		string text = Path.GetFileNameWithoutExtension(fileName);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = Path.GetFileNameWithoutExtension(card.ImagePath);
		}
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			text = text.Replace(oldChar, '_');
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "image";
		}
		string text2 = text + extension;
		int num = 2;
		while (!usedFileNames.Add(text2))
		{
			text2 = $"{text}_{num}{extension}";
			num++;
		}
		return text2;
	}

	private static string GetUniqueOutputFileName(GeneratedImageResultCardViewModel card, ISet<string> usedFileNames)
	{
		string fileName = Path.GetFileName(card.FileName);
		if (string.IsNullOrWhiteSpace(fileName))
		{
			fileName = Path.GetFileName(card.ImagePath);
		}
		string text = Path.GetFileNameWithoutExtension(fileName);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = Path.GetFileNameWithoutExtension(card.ImagePath);
		}
		foreach (char oldChar in Path.GetInvalidFileNameChars())
		{
			text = text.Replace(oldChar, '_');
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "image";
		}
		string outputName = text + ".png";
		int index = 2;
		while (!usedFileNames.Add(outputName))
		{
			outputName = $"{text}_{index}.png";
			index++;
		}
		return outputName;
	}

	private async Task RunSpBatchFromStagingAsync()
	{
		if (SpBatchSourceImageCards.Count == 0)
		{
			throw new InvalidOperationException("请先添加需要生成 SKU 母图的原始图。");
		}
		await RunSpBatchFromCardsAsync(SpBatchSourceImageCards, SpBatchMode.GenerateMaster, "正在生成 SKU 母图...", clearMasterCards: true, clearColorCards: true);
	}

	private async Task GenerateSpBatchColorSkusAsync()
	{
		if (SpBatchMasterImageCards.Count == 0)
		{
			throw new InvalidOperationException("请先生成或添加 SKU 母图。");
		}
		await RunSpBatchFromCardsAsync(SpBatchMasterImageCards, SpBatchMode.GenerateColors, "正在基于 SKU 母图生成其他颜色...", clearMasterCards: false, clearColorCards: true);
	}

	private async Task RunSpBatchFromCardsAsync(IEnumerable<GeneratedImageResultCardViewModel> sourceCards, SpBatchMode mode, string runningText, bool clearMasterCards, bool clearColorCards)
	{
		if (string.IsNullOrWhiteSpace(SpBatchOutputDirectory))
		{
			throw new InvalidOperationException("请选择 SKU 图输出目录。");
		}
		if (!TryParsePositiveInt(SpBatchConcurrencyText, "并发数量", out var value))
		{
			return;
		}
		GeneratedImageResultCardViewModel[] array = sourceCards.ToArray();
		if (array.Length == 0)
		{
			throw new InvalidOperationException("请先添加需要处理的图片。");
		}
		int retries = 4;
		Directory.CreateDirectory(SpBatchOutputDirectory);
		SetActiveSpBatchMode(mode);
		IsSpBatchRunning = true;
		IsSpBatchStopping = false;
		SpBatchStatusText = runningText;
		SpBatchSummaryText = "任务执行中，请稍候。";
		SpBatchResultRootText = "日期目录：" + SpBatchOutputDirectory;
		SpBatchResultStatsText = "SP 数量：0  总任务：0  成功：0  跳过：0  失败：0";
		UpdateSpBatchStats(0, 0, 0, 0, 0);
		SetSpBatchStatusForMode(mode, "任务执行中");
		if (mode == SpBatchMode.GenerateColors)
		{
			SpBatchStatusText = "待命";
		}
		if (mode == SpBatchMode.GenerateMaster)
		{
			SpBatchColorStatusText = "待命";
		}
		ClearSpBatchResultCards();
		if (clearMasterCards)
		{
			ClearSpBatchMasterImageCards();
		}
		if (clearColorCards)
		{
			ClearSpBatchImageResultCards();
		}
		StatusMessage = ((mode == SpBatchMode.GenerateColors) ? SpBatchColorStatusText : SpBatchStatusText);
		_spBatchCancellationTokenSource?.Cancel();
		_spBatchCancellationTokenSource?.Dispose();
		_spBatchCancellationTokenSource = new CancellationTokenSource();
		try
		{
			string inputDirectory = (SpBatchInputDirectory = CreateSpBatchStagingInputDirectory(array));
			SpBatchRequest request = new SpBatchRequest
			{
				InputDirectory = inputDirectory,
				OutputDirectory = SpBatchOutputDirectory,
				Image2ScriptPath = CurrentImageGenerationScriptPath,
				Concurrency = value,
				Retries = retries,
				Mode = mode,
				Overwrite = true,
				ColorTemplateColors = GetSelectedSpBatchColorTemplateColors(),
				SelectedColors = GetSelectedSpBatchColorNamesForCurrentGroup()
			};
			ApplySpBatchVisualResult(await _spBatchService.GenerateAsync(request, _spBatchCancellationTokenSource.Token));
			StatusMessage = SpBatchSummaryText;
		}
		catch (OperationCanceledException)
		{
			IsSpBatchStopping = false;
			SpBatchStatusText = "已停止";
			SpBatchSummaryText = "已停止当前 SKU 图生成任务。";
			SetSpBatchStatusForMode(mode, "已停止");
			StatusMessage = SpBatchSummaryText;
		}
		catch (Exception ex2)
		{
			SpBatchStatusText = "执行失败";
			SpBatchSummaryText = "SKU 图生成失败：" + ex2.Message;
			SetSpBatchStatusForMode(mode, "执行失败");
			ClearSpBatchResultCards();
			if (clearMasterCards)
			{
				ClearSpBatchMasterImageCards();
			}
			if (clearColorCards)
			{
				ClearSpBatchImageResultCards();
			}
			SpBatchResultCards.Add(new SpBatchResultCardViewModel
			{
				Title = "错误信息",
				SummaryText = ex2.Message,
				StatusText = "失败"
			});
			OnPropertyChanged("HasSpBatchResultCards");
			OnPropertyChanged("HasSpBatchMasterImageCards");
			OnPropertyChanged("HasSpBatchImageResultCards");
			OnPropertyChanged("HasAnySpBatchResultCards");
			StatusMessage = SpBatchSummaryText;
		}
		finally
		{
			IsSpBatchStopping = false;
			_spBatchCancellationTokenSource?.Dispose();
			_spBatchCancellationTokenSource = null;
			IsSpBatchRunning = false;
			SetActiveSpBatchMode(null);
		}
	}

	public void SetSpBatchStagingDropTarget(bool isActive)
	{
		IsSpBatchStagingDropTarget = isActive;
	}

	public void SetSpBatchMasterDropTarget(bool isActive)
	{
		IsSpBatchMasterDropTarget = isActive;
	}

	private void ApplySkuOptimizeResult(SkuOptimizeResult result)
	{
		SkuOptimizeStatusText = ((result.FailedCount > 0) ? "部分执行失败" : "优化完成");
		SkuOptimizeSummaryText = $"共 {result.Results.Count} 个任务，成功 {result.SuccessCount}，跳过 {result.SkippedCount}，失败 {result.FailedCount}。";
		SkuOptimizeResultRootText = (string.IsNullOrWhiteSpace(result.ResultRoot) ? ("结果目录：" + SkuOptimizeOutputDirectory) : ("结果目录：" + result.ResultRoot));
		SkuOptimizeResultStatsText = $"总任务：{result.Results.Count}  成功：{result.SuccessCount}  跳过：{result.SkippedCount}  失败：{result.FailedCount}";
		UpdateSkuOptimizeStats(result.Results.Count, result.SuccessCount, result.SkippedCount, result.FailedCount);
		ClearSkuOptimizeResultCards();
		foreach (SkuOptimizeJobResult item in result.Results.OrderBy((SkuOptimizeJobResult item) => item.Index))
		{
			string statusText = (string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase) ? "失败" : (string.Equals(item.Status, "skipped", StringComparison.OrdinalIgnoreCase) ? "已跳过" : "成功"));
			List<string> list = new List<string> { "源图：" + item.SourceImage };
			if (!string.IsNullOrWhiteSpace(item.ImagePath))
			{
				list.Add("输出：" + item.ImagePath);
			}
			if (!string.IsNullOrWhiteSpace(item.Error))
			{
				list.Add("错误：" + item.Error);
			}
			SkuOptimizeResultCards.Add(new SpBatchResultCardViewModel
			{
				Title = $"图片 {item.Index}",
				SummaryText = Path.GetFileName(item.SourceImage),
				StatusText = statusText,
				DetailText = string.Join(Environment.NewLine, list)
			});
		}
		OnPropertyChanged("HasSkuOptimizeResultCards");
		OnPropertyChanged("HasAnySkuOptimizeResultCards");
		OnPropertyChanged("ShouldShowSkuOptimizeDetailCards");
	}

	private void ApplySkuOptimizeVisualResult(SkuOptimizeResult result)
	{
		ApplySkuOptimizeResult(result);
		ReplaceOriginalSkuMasterImages(result);
		foreach (SkuOptimizeJobResult item in from item in result.Results
			where string.Equals(item.Status, "generated", StringComparison.OrdinalIgnoreCase)
			where !string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath)
			orderby item.Index
			select item)
		{
			SkuOptimizeImageResultCards.Add(new GeneratedImageResultCardViewModel(item.ImagePath, Path.GetFileName(item.ImagePath)));
		}
		OnPropertyChanged("HasSkuOptimizeImageResultCards");
		OnPropertyChanged("HasAnySkuOptimizeResultCards");
		OnPropertyChanged("ShouldShowSkuOptimizeDetailCards");
	}

	private void ReplaceOriginalSkuMasterImages(SkuOptimizeResult result)
	{
		Dictionary<string, string> replacedByOriginalPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (SkuOptimizeJobResult item in result.Results)
		{
			if (!string.Equals(item.Status, "generated", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(item.SourceImage) || string.IsNullOrWhiteSpace(item.ImagePath) || !File.Exists(item.ImagePath))
			{
				continue;
			}
			string sourceFullPath = Path.GetFullPath(item.SourceImage);
			if (!_skuOptimizeOriginalPathByStagingPath.TryGetValue(sourceFullPath, out string originalPath) || string.IsNullOrWhiteSpace(originalPath))
			{
				continue;
			}
			string originalFullPath = Path.GetFullPath(originalPath);
			string resultFullPath = Path.GetFullPath(item.ImagePath);
			if (string.Equals(originalFullPath, resultFullPath, StringComparison.OrdinalIgnoreCase))
			{
				replacedByOriginalPath[originalFullPath] = Path.GetFileName(item.ImagePath);
				continue;
			}
			File.Copy(resultFullPath, originalFullPath, overwrite: true);
			replacedByOriginalPath[originalFullPath] = Path.GetFileName(item.ImagePath);
		}
		if (replacedByOriginalPath.Count == 0)
		{
			return;
		}
		RefreshSkuOptimizeSourceCards(replacedByOriginalPath);
		RefreshSpBatchMasterCards(replacedByOriginalPath);
		StatusMessage = $"已优化并替换 {replacedByOriginalPath.Count} 张 SKU 母图。";
	}

	private void RefreshSkuOptimizeSourceCards(IReadOnlyDictionary<string, string> replacedByOriginalPath)
	{
		if (SkuOptimizeSourceImageCards.Count == 0)
		{
			return;
		}
		GeneratedImageResultCardViewModel[] cards = SkuOptimizeSourceImageCards.ToArray();
		SkuOptimizeSourceImageCards.Clear();
		foreach (GeneratedImageResultCardViewModel card in cards)
		{
			SkuOptimizeSourceImageCards.Add(new GeneratedImageResultCardViewModel(card.ImagePath, card.FileName, canToggleSelection: false, showRemoveAction: true, null, OnSkuOptimizeSourceImageRemoved, card.LinkedCardPath, card.LinkedSkuDirectory));
		}
		OnPropertyChanged("HasSkuOptimizeSourceImageCards");
		_runSkuOptimizeCommand.RaiseCanExecuteChanged();
	}

	private void RefreshSpBatchMasterCards(IReadOnlyDictionary<string, string> replacedByOriginalPath)
	{
		if (SpBatchMasterImageCards.Count == 0)
		{
			return;
		}
		GeneratedImageResultCardViewModel[] cards = SpBatchMasterImageCards.ToArray();
		SpBatchMasterImageCards.Clear();
		foreach (GeneratedImageResultCardViewModel card in cards)
		{
			GeneratedImageResultCardViewModel refreshedCard = new GeneratedImageResultCardViewModel(card.ImagePath, card.FileName, canToggleSelection: true, showRemoveAction: true, OnSpBatchMasterSelectionChanged, OnSpBatchMasterImageRemoved, card.LinkedCardPath, card.LinkedSkuDirectory);
			refreshedCard.SetSelected(card.IsSelected);
			SpBatchMasterImageCards.Add(refreshedCard);
		}
		OnPropertyChanged("HasSpBatchMasterImageCards");
		OnPropertyChanged("HasAnySpBatchResultCards");
		OnPropertyChanged("ShouldShowSpBatchDetailCards");
		_generateSpBatchColorSkusCommand.RaiseCanExecuteChanged();
		_sendSelectedSpBatchMasterToSkuOptimizeCommand.RaiseCanExecuteChanged();
	}

	private void ClearSkuOptimizeResultCards()
	{
		if (SkuOptimizeResultCards.Count != 0)
		{
			SkuOptimizeResultCards.Clear();
			OnPropertyChanged("HasSkuOptimizeResultCards");
			OnPropertyChanged("HasAnySkuOptimizeResultCards");
			OnPropertyChanged("ShouldShowSkuOptimizeDetailCards");
		}
	}

	private void ClearSkuOptimizeImageResultCards()
	{
		if (SkuOptimizeImageResultCards.Count != 0)
		{
			SkuOptimizeImageResultCards.Clear();
			OnPropertyChanged("HasSkuOptimizeImageResultCards");
			OnPropertyChanged("HasAnySkuOptimizeResultCards");
			OnPropertyChanged("ShouldShowSkuOptimizeDetailCards");
		}
	}

	private void OnSkuOptimizeSourceImageRemoved(GeneratedImageResultCardViewModel card)
	{
		if (SkuOptimizeSourceImageCards.Contains(card))
		{
			SkuOptimizeSourceImageCards.Remove(card);
			OnPropertyChanged("HasSkuOptimizeSourceImageCards");
			_runSkuOptimizeCommand.RaiseCanExecuteChanged();
		}
	}

	private void AddSkuOptimizeSourceImage(string imagePath, string? fileName = null)
	{
		if (File.Exists(imagePath) && IsSupportedImageFile(imagePath) && !SkuOptimizeSourceImageCards.Any((GeneratedImageResultCardViewModel card) => string.Equals(card.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase)))
		{
			SkuOptimizeSourceImageCards.Add(new GeneratedImageResultCardViewModel(imagePath, fileName ?? Path.GetFileName(imagePath), canToggleSelection: false, showRemoveAction: true, null, OnSkuOptimizeSourceImageRemoved));
			OnPropertyChanged("HasSkuOptimizeSourceImageCards");
			_runSkuOptimizeCommand.RaiseCanExecuteChanged();
		}
	}

	private void AddSkuOptimizeSourceImages(IEnumerable<string> filePaths)
	{
		foreach (string filePath in filePaths)
		{
			AddSkuOptimizeSourceImage(filePath);
		}
		StatusMessage = $"SKU 图优化区域当前共有 {SkuOptimizeSourceImageCards.Count} 张图片。";
	}

	private string CreateSkuOptimizeStagingInputDirectory()
	{
		string text = Path.Combine(Path.GetTempPath(), "ImageKeeper", "sku-optimize-staging", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
		Directory.CreateDirectory(text);
		HashSet<string> usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> usedOutputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<object> list = new List<object>();
		Dictionary<string, string> originalPathByStagingPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (GeneratedImageResultCardViewModel skuOptimizeSourceImageCard in SkuOptimizeSourceImageCards)
		{
			string uniqueStagingFileName = GetUniqueStagingFileName(skuOptimizeSourceImageCard, usedFileNames);
			string destFileName = Path.Combine(text, uniqueStagingFileName);
			File.Copy(skuOptimizeSourceImageCard.ImagePath, destFileName, overwrite: true);
			string output_name = GetUniqueOutputFileName(skuOptimizeSourceImageCard, usedOutputNames);
			originalPathByStagingPath[Path.GetFullPath(destFileName)] = skuOptimizeSourceImageCard.ImagePath;
			list.Add(new
			{
				staging_name = uniqueStagingFileName,
				output_name = output_name
			});
		}
		_skuOptimizeOriginalPathByStagingPath = originalPathByStagingPath;
		string path = Path.Combine(text, ".sku-optimize-manifest.json");
		string contents = JsonSerializer.Serialize(new
		{
			images = list
		});
		File.WriteAllText(path, contents, Encoding.UTF8);
		return text;
	}

	private async Task RunSkuOptimizeFromStagingAsync()
	{
		if (SkuOptimizeSourceImageCards.Count == 0)
		{
			throw new InvalidOperationException("请先添加需要优化的 SKU 图片。");
		}
		if (string.IsNullOrWhiteSpace(SkuOptimizeOutputDirectory))
		{
			throw new InvalidOperationException("请选择 SKU 图优化输出目录。");
		}
		if (!TryParsePositiveInt(SkuOptimizeConcurrencyText, "并发数量", out var value) || !TryParseSkuOptimizeLengthFactor(SkuOptimizeLengthMultiplierText, "长度倍数", IsSkuOptimizeLengthShrink, out var value2) || !TryParseSkuOptimizeMultiplier(SkuOptimizeDiameterMultiplierText, "缩小直径", out var value3))
		{
			return;
		}
		Directory.CreateDirectory(SkuOptimizeOutputDirectory);
		IsSkuOptimizeRunning = true;
		IsSkuOptimizeStopping = false;
		SkuOptimizeStatusText = "正在优化 SKU 图...";
		SkuOptimizeSummaryText = "任务执行中，请稍候。";
		SkuOptimizeResultRootText = "结果目录：" + SkuOptimizeOutputDirectory;
		SkuOptimizeResultStatsText = "总任务：0  成功：0  跳过：0  失败：0";
		UpdateSkuOptimizeStats(0, 0, 0, 0);
		ClearSkuOptimizeResultCards();
		ClearSkuOptimizeImageResultCards();
		StatusMessage = SkuOptimizeStatusText;
		_skuOptimizeCancellationTokenSource?.Cancel();
		_skuOptimizeCancellationTokenSource?.Dispose();
		_skuOptimizeCancellationTokenSource = new CancellationTokenSource();
		try
		{
			string inputDirectory = CreateSkuOptimizeStagingInputDirectory();
			SkuOptimizeRequest request = new SkuOptimizeRequest
			{
				InputDirectory = inputDirectory,
				OutputDirectory = SkuOptimizeOutputDirectory,
				Image2ScriptPath = CurrentImageGenerationScriptPath,
				Concurrency = value,
				LengthMultiplier = value2,
				DiameterMultiplier = value3,
				Overwrite = true,
				ColorTemplateColors = GetSelectedColorTemplateColors()
			};
			ApplySkuOptimizeVisualResult(await _skuOptimizeService.GenerateAsync(request, _skuOptimizeCancellationTokenSource.Token));
			StatusMessage = SkuOptimizeSummaryText;
		}
		catch (OperationCanceledException)
		{
			IsSkuOptimizeStopping = false;
			SkuOptimizeStatusText = "已停止";
			SkuOptimizeSummaryText = "已停止当前 SKU 图优化任务。";
			StatusMessage = SkuOptimizeSummaryText;
		}
		catch (Exception ex2)
		{
			SkuOptimizeStatusText = "执行失败";
			SkuOptimizeSummaryText = "SKU 图优化失败：" + ex2.Message;
			ClearSkuOptimizeResultCards();
			ClearSkuOptimizeImageResultCards();
			SkuOptimizeResultCards.Add(new SpBatchResultCardViewModel
			{
				Title = "错误信息",
				SummaryText = ex2.Message,
				StatusText = "失败"
			});
			OnPropertyChanged("HasSkuOptimizeResultCards");
			OnPropertyChanged("HasAnySkuOptimizeResultCards");
			StatusMessage = SkuOptimizeSummaryText;
		}
		finally
		{
			IsSkuOptimizeStopping = false;
			_skuOptimizeCancellationTokenSource?.Dispose();
			_skuOptimizeCancellationTokenSource = null;
			IsSkuOptimizeRunning = false;
		}
	}

	private void StopSkuOptimize()
	{
		if (IsSkuOptimizeRunning && !IsSkuOptimizeStopping)
		{
			IsSkuOptimizeStopping = true;
			SkuOptimizeStatusText = "正在停止...";
			SkuOptimizeSummaryText = "正在停止当前 SKU 图优化任务...";
			StatusMessage = SkuOptimizeSummaryText;
			_skuOptimizeService.CancelCurrentRun();
			_skuOptimizeCancellationTokenSource?.Cancel();
		}
	}

	public void SetSkuOptimizeStagingDropTarget(bool isActive)
	{
		IsSkuOptimizeStagingDropTarget = isActive;
	}

	public void AddDroppedImagesToSkuOptimize(IEnumerable<string> filePaths)
	{
		AddSkuOptimizeSourceImages(filePaths);
	}

	public void AddDroppedImagesToSpBatch(IEnumerable<string> filePaths)
	{
		AddSpBatchSourceImages(filePaths);
	}

	public void AddDroppedImagesToSpBatchMaster(IEnumerable<string> filePaths)
	{
		AddSpBatchMasterImages(filePaths);
	}

	public void HandleGeneratedImageCardClick(GeneratedImageResultCardViewModel? card, int clickCount)
	{
		card?.HandlePrimaryClick(clickCount);
	}

	public void HandleSpBatchSourceImageCardClick(GeneratedImageResultCardViewModel? card, int clickCount)
	{
		card?.HandlePrimaryClick(clickCount);
	}

	public void HandleSkuOptimizeSourceImageCardClick(GeneratedImageResultCardViewModel? card, int clickCount)
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
		string text = Path.GetExtension(filePath).ToLowerInvariant();
		if (text != null)
		{
			int length = text.Length;
			if (length != 4)
			{
				if (length == 5)
				{
					switch (text[2])
					{
					case 'p':
						break;
					case 'i':
						goto IL_00df;
					case 'e':
						goto IL_00ee;
					case 'f':
						goto IL_00fd;
					default:
						goto IL_010e;
					}
					if (text == ".jpeg")
					{
						goto IL_010a;
					}
				}
			}
			else
			{
				char c = text[1];
				if ((uint)c <= 103u)
				{
					if (c != 'b')
					{
						if (c == 'g' && text == ".gif")
						{
							goto IL_010a;
						}
					}
					else if (text == ".bmp")
					{
						goto IL_010a;
					}
				}
				else if (c != 'j')
				{
					if (c != 'p')
					{
						if (c == 't' && text == ".tif")
						{
							goto IL_010a;
						}
					}
					else if (text == ".png")
					{
						goto IL_010a;
					}
				}
				else if (text == ".jpg")
				{
					goto IL_010a;
				}
			}
		}
		goto IL_010e;
		IL_010a:
		return true;
		IL_010e:
		return false;
		IL_00ee:
		if (text == ".webp")
		{
			goto IL_010a;
		}
		goto IL_010e;
		IL_00df:
		if (text == ".tiff")
		{
			goto IL_010a;
		}
		goto IL_010e;
		IL_00fd:
		if (text == ".jfif")
		{
			goto IL_010a;
		}
		goto IL_010e;
	}
}
