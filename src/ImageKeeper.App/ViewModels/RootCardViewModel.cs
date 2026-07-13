using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;
using ImageKeeper.Core.Utilities;
using Microsoft.Win32;

namespace ImageKeeper.App.ViewModels;

public sealed class RootCardViewModel : ViewModelBase
{
	private const int ImagePageSize = 60;

	private static readonly HashSet<string> SizeImageExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".png",
		".jpg",
		".jpeg",
		".webp",
		".bmp",
		".gif",
		".tif",
		".tiff",
		".jfif"
	};

	private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".png",
		".jpg",
		".jpeg",
		".webp",
		".bmp",
		".gif",
		".tif",
		".tiff",
		".jfif"
	};

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

	private readonly AsyncRelayCommand _openCardSizeDialogCommand;

	private readonly AsyncRelayCommand _copyMainToDetailCommand;

	private readonly AsyncRelayCommand _moveSelectedCommand;

	private readonly AsyncRelayCommand _deleteCardCommand;

	private readonly AsyncRelayCommand _autoPublishCommand;

	private readonly RelayCommand _toggleExpandCommand;

	private readonly RelayCommand _selectNodeCommand;

	private readonly RelayCommand _loadMoreImagesCommand;

	private readonly IProductSheetService? _productSheetService;

	private readonly Func<bool>? _isAutoPublishBusyProvider;

	private readonly Func<Func<Task>, Task>? _runExclusiveAsync;

	private readonly Func<RootCardViewModel, AutoPublishStatus, string, Task>? _setAutoPublishStatusAsync;

	private readonly Func<RootCardViewModel, Task>? _runAutoPublishCardAsync;

	private readonly Func<RootCardViewModel, Task>? _openCardSizeDialogAsync;

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

	private AutoPublishStatus _autoPublishStatus;

	private string _autoPublishLastError = string.Empty;

	private string _sizeText = string.Empty;

	private string _sizeRawInput = string.Empty;

	private bool _hasStaleSizeImage;

	public string DisplayName => _rootNode.DisplayName;

	public string RootFolderPath => _rootNode.FolderPath;

	public string AutoPublishKeyPath => GetSpRootFolder() ?? RootFolderPath;

	public ObservableCollection<FolderNodeViewModel> VisibleNodes { get; } = new ObservableCollection<FolderNodeViewModel>();

	public ObservableCollection<ImageItemViewModel> CurrentImages { get; } = new ObservableCollection<ImageItemViewModel>();

	public RelayCommand InvertSelectionCommand => _invertSelectionCommand;

	public RelayCommand SelectAllCommand => _selectAllCommand;

	public RelayCommand ClearSelectionCommand => _clearSelectionCommand;

	public RelayCommand CollapseCommand => _collapseCommand;

	public RelayCommand ExpandCommand => _expandCommand;

	public AsyncRelayCommand AddImagesCommand => _addImagesCommand;

	public AsyncRelayCommand OpenCardSizeDialogCommand => _openCardSizeDialogCommand;

	public AsyncRelayCommand CopyMainToDetailCommand => _copyMainToDetailCommand;

	public AsyncRelayCommand MoveSelectedCommand => _moveSelectedCommand;

	public AsyncRelayCommand DeleteCardCommand => _deleteCardCommand;

	public AsyncRelayCommand AutoPublishCommand => _autoPublishCommand;

	public RelayCommand ToggleExpandCommand => _toggleExpandCommand;

	public RelayCommand SelectNodeCommand => _selectNodeCommand;

	public RelayCommand LoadMoreImagesCommand => _loadMoreImagesCommand;

	public FolderNodeViewModel RootNode => _rootNode;

	public IReadOnlyList<FolderNodeViewModel> ChildFolders => _rootNode.Children;

	public FolderNodeViewModel? SelectedNode
	{
		get
		{
			return _selectedNode;
		}
		private set
		{
			if (SetProperty(ref _selectedNode, value, "SelectedNode"))
			{
				OnPropertyChanged("CurrentFolderTitle");
				OnPropertyChanged("CurrentFolderPath");
				OnPropertyChanged("CurrentFolderMetaText");
				OnPropertyChanged("HasImages");
				OnPropertyChanged("ContentEmptyText");
				OnPropertyChanged("AutoPublishKeyPath");
				OnPropertyChanged("CollapsedSummaryText");
				OnPropertyChanged("CollapsedPathText");
				RefreshCurrentImages();
			}
		}
	}

	public bool IsCollapsed
	{
		get
		{
			return _isCollapsed;
		}
		private set
		{
			if (SetProperty(ref _isCollapsed, value, "IsCollapsed"))
			{
				OnPropertyChanged("CollapsedSummaryText");
				OnPropertyChanged("CollapsedPathText");
				OnPropertyChanged("CanBatchSelect");
				if (!value)
				{
					IsBatchSelected = false;
				}
				_batchSelectionChanged?.Invoke();
			}
		}
	}

	public bool IsBatchSelected
	{
		get
		{
			return _isBatchSelected;
		}
		set
		{
			if (SetProperty(ref _isBatchSelected, value, "IsBatchSelected"))
			{
				_batchSelectionChanged?.Invoke();
			}
		}
	}

	public bool IsActive
	{
		get
		{
			return _isActive;
		}
		set
		{
			if (SetProperty(ref _isActive, value, "IsActive"))
			{
				OnPropertyChanged("CardBorderBrush");
			}
		}
	}

	public bool IsDropTarget
	{
		get
		{
			return _isDropTarget;
		}
		private set
		{
			if (SetProperty(ref _isDropTarget, value, "IsDropTarget"))
			{
				OnPropertyChanged("ContentBorderBrush");
				OnPropertyChanged("ContentBackgroundBrush");
			}
		}
	}

	public string BackupFolder
	{
		get
		{
			return _backupFolder;
		}
		private set
		{
			SetProperty(ref _backupFolder, value, "BackupFolder");
		}
	}

	public int TotalCount
	{
		get
		{
			return _totalCount;
		}
		private set
		{
			if (SetProperty(ref _totalCount, value, "TotalCount"))
			{
				OnPropertyChanged("CountsText");
				OnPropertyChanged("SelectionSummaryText");
				OnPropertyChanged("UnselectedCount");
			}
		}
	}

	public int SelectedCount
	{
		get
		{
			return _selectedCount;
		}
		private set
		{
			if (SetProperty(ref _selectedCount, value, "SelectedCount"))
			{
				OnPropertyChanged("CountsText");
				OnPropertyChanged("SelectionSummaryText");
				OnPropertyChanged("UnselectedCount");
				_moveSelectedCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public int UnselectedCount => TotalCount - SelectedCount;

	public int LoadedImageCount
	{
		get
		{
			return _loadedImageCount;
		}
		private set
		{
			if (SetProperty(ref _loadedImageCount, value, "LoadedImageCount"))
			{
				OnPropertyChanged("ImageLoadSummaryText");
				OnPropertyChanged("HasMoreImages");
				_loadMoreImagesCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public int TotalImageCount => SelectedNode?.Model.Images.Count ?? 0;

	public bool HasMoreImages => LoadedImageCount < TotalImageCount;

	public string ImageLoadSummaryText
	{
		get
		{
			if (TotalImageCount != 0)
			{
				return $"已显示 {LoadedImageCount} / 共 {TotalImageCount} 张";
			}
			return "当前目录没有图片";
		}
	}

	public string CurrentFolderTitle => SelectedNode?.DisplayName ?? DisplayName;

	public string CurrentFolderPath => SelectedNode?.FolderPath ?? RootFolderPath;

	public string CurrentFolderHeaderText
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(CurrentFolderPath))
			{
				return CurrentFolderTitle + "  " + CurrentFolderPath;
			}
			return CurrentFolderTitle;
		}
	}

	public string CurrentFolderMetaText
	{
		get
		{
			if (SelectedNode != null)
			{
				return CurrentFolderPath;
			}
			return "从左侧目录选择一个文件夹后，这里显示该目录下的图片。";
		}
	}

	public string CountsText => $"共 {TotalCount} 张 / 已选中 {SelectedCount} 张 / 未选中 {UnselectedCount} 张";

	public string SelectionSummaryText
	{
		get
		{
			if (SelectedNode != null)
			{
				return $"当前目录：{CurrentFolderTitle}，共 {TotalCount} 张，已选中 {SelectedCount} 张，未选中 {UnselectedCount} 张";
			}
			return "请选择左侧目录中的文件夹。";
		}
	}

	public bool HasImages => CurrentImages.Count > 0;

	public string ContentEmptyText
	{
		get
		{
			if (SelectedNode != null)
			{
				return "当前文件夹没有图片。可以点击“添加图片”，或直接拖拽图片到这里。";
			}
			return "从左侧目录选择一个文件夹后，这里会显示该目录下的图片。";
		}
	}

	public string CollapsedSummaryText => DisplayName + "    " + AutoPublishKeyPath;

	public string CollapsedPathText => AutoPublishKeyPath;

	public string SizeText
	{
		get
		{
			return _sizeText;
		}
		private set
		{
			if (SetProperty(ref _sizeText, value, "SizeText"))
			{
				OnPropertyChanged("HasSizeText");
				OnPropertyChanged("SizeSummaryText");
				OnPropertyChanged("SizeStatusForeground");
			}
		}
	}

	public string SizeRawInput
	{
		get
		{
			return _sizeRawInput;
		}
		private set
		{
			SetProperty(ref _sizeRawInput, value, "SizeRawInput");
		}
	}

	public bool HasSizeText => !string.IsNullOrWhiteSpace(SizeText);

	public bool HasStaleSizeImage
	{
		get
		{
			return _hasStaleSizeImage;
		}
		private set
		{
			if (SetProperty(ref _hasStaleSizeImage, value, "HasStaleSizeImage"))
			{
				OnPropertyChanged("SizeSummaryText");
				OnPropertyChanged("SizeStatusForeground");
			}
		}
	}

	public string SizeSummaryText
	{
		get
		{
			if (HasStaleSizeImage)
			{
				return "尺寸：需重新录入";
			}
			if (!HasSizeText)
			{
				return "尺寸：未录入";
			}
			return "尺寸：" + SizeText.Replace(Environment.NewLine, " ");
		}
	}

	public Brush SizeStatusForeground
	{
		get
		{
			if (!HasSizeText || HasStaleSizeImage)
			{
				return new SolidColorBrush(Color.FromRgb(245, 108, 108));
			}
			return new SolidColorBrush(Color.FromRgb(96, 98, 102));
		}
	}

	public bool IsAutoPublishBusy => _isAutoPublishBusyProvider?.Invoke() ?? false;

	public bool CanBatchSelect
	{
		get
		{
			if (IsCollapsed)
			{
				return !string.IsNullOrWhiteSpace(GetSpRootFolder());
			}
			return false;
		}
	}

	public AutoPublishStatus AutoPublishStatus
	{
		get
		{
			return _autoPublishStatus;
		}
		private set
		{
			if (SetProperty(ref _autoPublishStatus, value, "AutoPublishStatus"))
			{
				OnPropertyChanged("AutoPublishStatusText");
				OnPropertyChanged("AutoPublishStatusForeground");
				OnPropertyChanged("AutoPublishStatusBackground");
				OnPropertyChanged("AutoPublishStatusBorderBrush");
			}
		}
	}

	public string AutoPublishLastError
	{
		get
		{
			return _autoPublishLastError;
		}
		private set
		{
			SetProperty(ref _autoPublishLastError, value, "AutoPublishLastError");
		}
	}

	public string AutoPublishStatusText => AutoPublishStatus switch
	{
		AutoPublishStatus.Publishing => "上架中", 
		AutoPublishStatus.Success => "上架成功", 
		AutoPublishStatus.Failed => "上架失败", 
		_ => "未上架", 
	};

	public Brush AutoPublishStatusForeground => AutoPublishStatus switch
	{
		AutoPublishStatus.Publishing => new SolidColorBrush(Color.FromRgb(45, 106, 227)), 
		AutoPublishStatus.Success => new SolidColorBrush(Color.FromRgb(103, 194, 58)), 
		AutoPublishStatus.Failed => new SolidColorBrush(Color.FromRgb(245, 108, 108)), 
		_ => new SolidColorBrush(Color.FromRgb(124, 138, 165)), 
	};

	public Brush AutoPublishStatusBackground => AutoPublishStatus switch
	{
		AutoPublishStatus.Publishing => new SolidColorBrush(Color.FromRgb(234, 241, byte.MaxValue)), 
		AutoPublishStatus.Success => new SolidColorBrush(Color.FromRgb(240, 249, 235)), 
		AutoPublishStatus.Failed => new SolidColorBrush(Color.FromRgb(254, 240, 240)), 
		_ => new SolidColorBrush(Color.FromRgb(246, 248, 252)), 
	};

	public Brush AutoPublishStatusBorderBrush => AutoPublishStatus switch
	{
		AutoPublishStatus.Publishing => new SolidColorBrush(Color.FromRgb(159, 190, byte.MaxValue)), 
		AutoPublishStatus.Success => new SolidColorBrush(Color.FromRgb(205, 231, 176)), 
		AutoPublishStatus.Failed => new SolidColorBrush(Color.FromRgb(252, 196, 196)), 
		_ => new SolidColorBrush(Color.FromRgb(217, 225, 236)), 
	};

	public Brush CardBorderBrush
	{
		get
		{
			if (!IsActive)
			{
				return new SolidColorBrush(Color.FromRgb(217, 225, 236));
			}
			return new SolidColorBrush(Color.FromRgb(159, 190, byte.MaxValue));
		}
	}

	public Brush ContentBorderBrush
	{
		get
		{
			if (!IsDropTarget)
			{
				return new SolidColorBrush(Color.FromRgb(217, 225, 236));
			}
			return new SolidColorBrush(Color.FromRgb(45, 106, 227));
		}
	}

	public Brush ContentBackgroundBrush
	{
		get
		{
			if (!IsDropTarget)
			{
				return new SolidColorBrush(Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue));
			}
			return new SolidColorBrush(Color.FromRgb(238, 244, byte.MaxValue));
		}
	}

	public event Action<PreviewRequest?>? PreviewRequested;

	public event Action<string>? StatusChanged;

	public event Action<RootCardViewModel>? Activated;

	public event Action<RootCardViewModel>? SelectionContextChanged;

	public event Action<RootCardViewModel, IReadOnlyList<string>>? ImageFilesAdded;

	public event Action<RootCardViewModel>? Deleted;

	public RootCardViewModel(FolderNode rootNode, IImageWorkspaceService workspaceService, IWorkspaceStateService workspaceStateService, string backupFolder, IProductSheetService? productSheetService = null, Func<bool>? isAutoPublishBusyProvider = null, Func<Func<Task>, Task>? runExclusiveAsync = null, Func<RootCardViewModel, AutoPublishStatus, string, Task>? setAutoPublishStatusAsync = null, Func<RootCardViewModel, Task>? runAutoPublishCardAsync = null, Func<RootCardViewModel, Task>? openCardSizeDialogAsync = null, Action? batchSelectionChanged = null)
	{
		_workspaceService = workspaceService;
		_workspaceStateService = workspaceStateService;
		_backupFolder = backupFolder;
		_productSheetService = productSheetService;
		_isAutoPublishBusyProvider = isAutoPublishBusyProvider;
		_runExclusiveAsync = runExclusiveAsync;
		_setAutoPublishStatusAsync = setAutoPublishStatusAsync;
		_runAutoPublishCardAsync = runAutoPublishCardAsync;
		_openCardSizeDialogAsync = openCardSizeDialogAsync;
		_batchSelectionChanged = batchSelectionChanged;
		_rootNode = new FolderNodeViewModel(rootNode);
		_cardState = _workspaceStateService.GetOrCreateCardState(rootNode.Id);
		_isCollapsed = _cardState.IsCollapsed;
		_invertSelectionCommand = new RelayCommand(delegate
		{
			InvertSelection();
		}, (object? _) => SelectedNode != null && TotalImageCount > 0);
		_selectAllCommand = new RelayCommand(delegate
		{
			SetSelectionState(isSelected: true);
		}, (object? _) => SelectedNode != null && TotalImageCount > 0);
		_clearSelectionCommand = new RelayCommand(delegate
		{
			SetSelectionState(isSelected: false);
		}, (object? _) => SelectedNode != null && TotalImageCount > 0);
		_collapseCommand = new RelayCommand(delegate
		{
			SetCollapsed(collapsed: true);
		});
		_expandCommand = new RelayCommand(delegate
		{
			SetCollapsed(collapsed: false);
			Activate();
		});
		_addImagesCommand = new AsyncRelayCommand((object? _) => AddImagesAsync(), (object? _) => SelectedNode != null);
		_openCardSizeDialogCommand = new AsyncRelayCommand((object? _) => OpenCardSizeDialogAsync(), (object? _) => CanOpenCardSizeDialog());
		_copyMainToDetailCommand = new AsyncRelayCommand((object? _) => CopyMainToDetailAsync(), (object? _) => CanCopyMainToDetail());
		_moveSelectedCommand = new AsyncRelayCommand((object? _) => MoveSelectedAsync(), (object? _) => SelectedNode != null && SelectedCount > 0);
		_deleteCardCommand = new AsyncRelayCommand((object? _) => DeleteCardAsync(), (object? _) => CanDeleteCard());
		_autoPublishCommand = new AsyncRelayCommand((object? _) => AutoPublishAsync(), (object? _) => CanAutoPublish());
		_toggleExpandCommand = new RelayCommand(delegate(object? node)
		{
			ToggleExpand(node as FolderNodeViewModel);
		}, (object? node) => node is FolderNodeViewModel folderNodeViewModel && folderNodeViewModel.HasChildren);
		_selectNodeCommand = new RelayCommand(delegate(object? node)
		{
			SelectNode(node as FolderNodeViewModel);
		}, (object? node) => node is FolderNodeViewModel);
		_loadMoreImagesCommand = new RelayCommand(delegate
		{
			LoadMoreImages();
		}, (object? _) => HasMoreImages);
		RebuildVisibleNodes();
		RestoreOrSelectDefaultNode(activateCard: false);
	}

	public void SetBackupFolder(string backupFolder)
	{
		BackupFolder = backupFolder;
	}

	public void NotifyAutoPublishStateChanged()
	{
		OnPropertyChanged("IsAutoPublishBusy");
		_autoPublishCommand.RaiseCanExecuteChanged();
		_deleteCardCommand.RaiseCanExecuteChanged();
		_batchSelectionChanged?.Invoke();
	}

	public void ApplyAutoPublishRecord(AutoPublishCardRecord? record)
	{
		AutoPublishStatus = record?.Status ?? AutoPublishStatus.NotPublished;
		AutoPublishLastError = record?.LastError ?? string.Empty;
	}

	public void ApplyCardSizeInfo(CardSizeInfoRecord? record, bool hasStaleSizeImage = false)
	{
		SizeText = (hasStaleSizeImage ? string.Empty : (record?.SizeText ?? string.Empty));
		SizeRawInput = ((!hasStaleSizeImage) ? (record?.SizeRawInput ?? string.Empty) : (record?.SizeRawInput ?? string.Empty));
		HasStaleSizeImage = hasStaleSizeImage;
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
		this.Activated?.Invoke(this);
	}

	public void RestoreOrSelectDefaultNode(bool activateCard)
	{
		FolderNodeViewModel node = FindNodeById(_cardState.SelectedNodeId) ?? FindFirstNodeWithImages(ChildFolders) ?? ChildFolders.FirstOrDefault() ?? _rootNode;
		SelectNode(node, activateCard);
	}

	public FolderNodeViewModel? GetFirstNodeWithImages()
	{
		FolderNodeViewModel folderNodeViewModel = FindFirstNodeWithImages(ChildFolders);
		if (folderNodeViewModel == null)
		{
			if (_rootNode.Model.Images.Count <= 0)
			{
				return null;
			}
			folderNodeViewModel = _rootNode;
		}
		return folderNodeViewModel;
	}

	public bool CanAcceptDrop(IReadOnlyList<string> files)
	{
		if (files.Count > 0 && SelectedNode != null)
		{
			return Directory.Exists(CurrentFolderPath);
		}
		return false;
	}

	public void SetDropTarget(bool isActive)
	{
		IsDropTarget = isActive;
	}

	private bool CanAutoPublish()
	{
		if (_productSheetService != null && _runExclusiveAsync != null && _runAutoPublishCardAsync != null && !IsAutoPublishBusy)
		{
			return !string.IsNullOrWhiteSpace(GetSpRootFolder());
		}
		return false;
	}

	private bool CanDeleteCard()
	{
		string spRootFolder = GetSpRootFolder();
		if (IsAutoPublishBusy || string.IsNullOrWhiteSpace(spRootFolder))
		{
			return false;
		}
		return Directory.Exists(spRootFolder);
	}

	private bool CanOpenCardSizeDialog()
	{
		if (_openCardSizeDialogAsync != null)
		{
			return !string.IsNullOrWhiteSpace(GetSpRootFolder());
		}
		return false;
	}

	private bool CanCopyMainToDetail()
	{
		string spRootFolder = GetSpRootFolder();
		if (string.IsNullOrWhiteSpace(spRootFolder))
		{
			return false;
		}
		return Directory.Exists(Path.Combine(spRootFolder, "main"));
	}

	public async Task<string> RunAutoPublishInternalAsync(IReadOnlyList<string>? manualSizes = null)
	{
		ProductSheetTask productSheetTask = await PrepareAutoPublishDataAsync(manualSizes);
		string text = (string.IsNullOrWhiteSpace(productSheetTask.ProductsJsonPath) ? "上架 JSON 已生成。" : productSheetTask.ProductsJsonPath);
		this.StatusChanged?.Invoke("自动上架准备完成：" + Path.GetFileName(productSheetTask.SpRootFolder) + "。" + text);
		return text;
	}

	public async Task<ProductSheetTask> PrepareAutoPublishDataAsync(IReadOnlyList<string>? manualSizes = null)
	{
		string spRootFolder = GetSpRootFolder();
		if (string.IsNullOrWhiteSpace(spRootFolder) || _productSheetService == null)
		{
			throw new InvalidOperationException("当前卡片未解析到 SP 根目录，无法自动上架。");
		}
		string text = ValidateAutoPublishInput(spRootFolder);
		if (text != null)
		{
			throw new InvalidOperationException(text);
		}
		Activate();
		NotifyAutoPublishStateChanged();
		try
		{
			this.StatusChanged?.Invoke("开始准备上架数据：" + Path.GetFileName(spRootFolder));
			ProductSheetTask productSheetTask = await _productSheetService.GenerateAsync(spRootFolder, manualSizes);
			if (productSheetTask.Status == "Completed")
			{
				this.StatusChanged?.Invoke("上架数据准备完成：" + Path.GetFileName(spRootFolder));
				return productSheetTask;
			}
			throw new InvalidOperationException("自动上架失败：" + Path.GetFileName(spRootFolder));
		}
		catch (Exception ex)
		{
			this.StatusChanged?.Invoke("自动上架失败：" + ex.Message);
			throw;
		}
		finally
		{
			NotifyAutoPublishStateChanged();
		}
	}

	private async Task AutoPublishAsync()
	{
		Func<RootCardViewModel, Task> runAutoPublishCardAsync = _runAutoPublishCardAsync;
		if (_productSheetService == null || _runExclusiveAsync == null || runAutoPublishCardAsync == null)
		{
			this.StatusChanged?.Invoke("当前卡片未解析到 SP 根目录，无法自动上架。");
		}
		else
		{
			await runAutoPublishCardAsync(this);
		}
	}

	private async Task OpenCardSizeDialogAsync()
	{
		if (_openCardSizeDialogAsync == null)
		{
			this.StatusChanged?.Invoke("当前卡片未接入尺寸录入功能。");
			return;
		}
		string? spRootFolder = GetSpRootFolder();
		if (string.IsNullOrWhiteSpace(spRootFolder))
		{
			throw new InvalidOperationException("当前卡片未解析到 SP 根目录，无法手动录入尺寸。");
		}
		string text = Path.Combine(spRootFolder, "main");
		if (!Directory.Exists(text))
		{
			throw new InvalidOperationException("未找到 main 文件夹：" + text);
		}
		if (!HasSizeImageFile(text))
		{
			throw new InvalidOperationException("当前 main 文件夹下没有尺寸图：" + text);
		}
		Activate();
		await _openCardSizeDialogAsync(this);
	}

	private string? GetSpRootFolder()
	{
		return SpRootResolver.Resolve(RootFolderPath) ?? SpRootResolver.Resolve(CurrentFolderPath);
	}

	private static string? ValidateAutoPublishInput(string spRootFolder)
	{
		string text = Path.Combine(spRootFolder, "main");
		if (!Directory.Exists(text))
		{
			return "自动上架前置校验失败：" + text + " 不存在。";
		}
		if (!HasSizeImageFile(text))
		{
			return "自动上架前置校验失败：" + text + " 下缺少尺寸图。";
		}
		return null;
	}

	private static bool HasSizeImageFile(string mainFolder)
	{
		return Directory.EnumerateFiles(mainFolder, "*", SearchOption.TopDirectoryOnly).Any(IsSizeImageFile);
	}

	private static bool IsSizeImageFile(string filePath)
	{
		if (!SizeImageExtensions.Contains(Path.GetExtension(filePath)))
		{
			return false;
		}
		string name = Path.GetFileNameWithoutExtension(filePath);
		return name.StartsWith("2-", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("\u5C3A\u5BF8", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("size", StringComparison.OrdinalIgnoreCase);
	}

	public async Task AddImageFilesAsync(IReadOnlyList<string> sourceFiles, bool showStatusMessage = true)
	{
		if (SelectedNode == null || sourceFiles.Count == 0)
		{
			return;
		}
		Activate();
		string targetFolder = SelectedNode.FolderPath;
		List<string> list = await Task.Run(delegate
		{
			List<string> list2 = new List<string>();
			foreach (string sourceFile in sourceFiles)
			{
				string uniquePath = GetUniquePath(targetFolder, Path.GetFileName(sourceFile));
				File.Copy(sourceFile, uniquePath, overwrite: false);
				list2.Add(uniquePath);
			}
			return list2;
		});
		foreach (string item2 in list)
		{
			FileInfo fileInfo = new FileInfo(item2);
			ImageItem item = new ImageItem
			{
				FilePath = fileInfo.FullName,
				FileName = fileInfo.Name,
				FileSize = fileInfo.Length,
				LastWriteTime = fileInfo.LastWriteTime
			};
			SelectedNode.Model.Images.Add(item);
		}
		SelectedNode.Model.Images.Sort((ImageItem left, ImageItem right) => StringComparer.OrdinalIgnoreCase.Compare(left.FileName, right.FileName));
		RefreshCurrentImages(loadAll: true);
		UpdateCounts();
		if (list.Count > 0)
		{
			RaisePreviewForPath(list[0]);
		}
		if (showStatusMessage)
		{
			this.StatusChanged?.Invoke($"已向 {SelectedNode.DisplayName} 添加 {list.Count} 张图片。");
		}
		if (list.Count > 0)
		{
			this.ImageFilesAdded?.Invoke(this, list);
		}
	}

	private void ToggleExpand(FolderNodeViewModel? node)
	{
		if (node != null && node.HasChildren)
		{
			node.IsExpanded = !node.IsExpanded;
			RebuildVisibleNodes();
		}
	}

	private void SelectNode(FolderNodeViewModel? node, bool activateCard = true)
	{
		if (node == null)
		{
			return;
		}
		foreach (FolderNodeViewModel item in EnumerateAll(_rootNode))
		{
			item.IsSelected = false;
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
		if (SelectedNode == null)
		{
			return;
		}
		_workspaceService.InvertSelection(SelectedNode.Model);
		foreach (ImageItemViewModel currentImage in CurrentImages)
		{
			currentImage.IsSelected = currentImage.Model.IsSelected;
		}
		UpdateCounts();
	}

	private void SetSelectionState(bool isSelected)
	{
		if (SelectedNode == null)
		{
			return;
		}
		_workspaceService.SetSelectionState(SelectedNode.Model, isSelected);
		foreach (ImageItemViewModel currentImage in CurrentImages)
		{
			currentImage.SyncSelectionStateFromModel();
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
		if (SelectedNode != null)
		{
			Activate();
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "选择要添加到当前目录的图片",
				InitialDirectory = SelectedNode.FolderPath,
				Multiselect = true,
				Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.jfif"
			};
			if (openFileDialog.ShowDialog() == true)
			{
				await AddImageFilesAsync(openFileDialog.FileNames);
			}
		}
	}

	private async Task CopyMainToDetailAsync()
	{
		string spRootFolder = GetSpRootFolder();
		if (string.IsNullOrWhiteSpace(spRootFolder))
		{
			this.StatusChanged?.Invoke("当前卡片未解析到 SP 根目录，无法复制 main 到 detail。");
			return;
		}
		Activate();
		string mainFolder = Path.Combine(spRootFolder, "main");
		string detailFolder = Path.Combine(spRootFolder, "detail");
		if (!Directory.Exists(mainFolder))
		{
			this.StatusChanged?.Invoke("复制失败：" + mainFolder + " 不存在。");
			return;
		}
		Directory.CreateDirectory(detailFolder);
		List<string> list = await Task.Run(delegate
		{
			List<string> list2 = new List<string>();
			foreach (string item in Directory.EnumerateFiles(mainFolder, "*", SearchOption.TopDirectoryOnly).Where(IsSupportedImageFile))
			{
				string text = Path.Combine(detailFolder, Path.GetFileName(item));
				File.Copy(item, text, overwrite: true);
				list2.Add(text);
			}
			return list2;
		});
		RefreshNodeImages(detailFolder)?.RefreshImageCount();
		UpdateCounts();
		if (SelectedNode != null && string.Equals(Path.GetFullPath(SelectedNode.FolderPath), Path.GetFullPath(detailFolder), StringComparison.OrdinalIgnoreCase))
		{
			RefreshCurrentImages(loadAll: true);
			OnPropertyChanged("HasImages");
			OnPropertyChanged("ContentEmptyText");
			OnPropertyChanged("CurrentFolderMetaText");
			if (CurrentImages.Count > 0)
			{
				RaisePreviewForPath(CurrentImages[0].FilePath);
			}
		}
		this.StatusChanged?.Invoke($"已复制 main 到 detail，共 {list.Count} 张图片，同名文件已覆盖。");
	}

	private async Task MoveSelectedAsync()
	{
		if (SelectedNode == null)
		{
			return;
		}
		Activate();
		List<ImageItemViewModel> selectedItems = CurrentImages.Where((ImageItemViewModel image) => image.IsSelected).ToList();
		if (selectedItems.Count == 0)
		{
			return;
		}
		Directory.CreateDirectory(BackupFolder);
		List<ImageItemViewModel> list = await Task.Run(delegate
		{
			List<ImageItemViewModel> list2 = new List<ImageItemViewModel>();
			foreach (ImageItemViewModel item in selectedItems)
			{
				string uniquePath = GetUniquePath(BackupFolder, item.FileName);
				File.Move(item.FilePath, uniquePath);
				list2.Add(item);
			}
			return list2;
		});
		foreach (ImageItemViewModel item2 in list)
		{
			SelectedNode.Model.Images.Remove(item2.Model);
			CurrentImages.Remove(item2);
		}
		LoadedImageCount = CurrentImages.Count;
		UpdateCounts();
		this.PreviewRequested?.Invoke(null);
		this.StatusChanged?.Invoke($"已移动 {list.Count} 张图片到备份目录。");
	}

	private async Task DeleteCardAsync()
	{
		string spRootFolder = GetSpRootFolder();
		if (string.IsNullOrWhiteSpace(spRootFolder) || !Directory.Exists(spRootFolder))
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(BackupFolder))
		{
			throw new InvalidOperationException("请先设置备份目录。");
		}
		string sourceRoot = Path.GetFullPath(spRootFolder);
		string backupRoot = Path.GetFullPath(BackupFolder);
		if (IsUnsafeDeleteTarget(sourceRoot))
		{
			throw new InvalidOperationException("当前目录不安全，已取消删除：" + sourceRoot);
		}
		if (IsSameOrChildPath(backupRoot, sourceRoot))
		{
			throw new InvalidOperationException("备份目录不能放在当前 SP 目录里面，请先切换备份目录。");
		}
		IReadOnlyList<string> imageFiles = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
			.Where(IsSupportedImageFile)
			.OrderBy<string, string>((string path) => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();
		MessageBoxResult messageBoxResult = MessageBox.Show(
			$"确认删除 {DisplayName} 吗？\n\n将先把当前目录内 {imageFiles.Count} 张图片复制回备份目录，然后删除整个目录：\n{sourceRoot}",
			"删除链接卡片",
			MessageBoxButton.OKCancel,
			MessageBoxImage.Warning);
		if (messageBoxResult != MessageBoxResult.OK)
		{
			return;
		}
		Activate();
		int movedCount = await Task.Run(delegate
		{
			Directory.CreateDirectory(backupRoot);
			int count = 0;
			foreach (string imageFile in imageFiles)
			{
				string uniquePath = GetUniquePath(backupRoot, Path.GetFileName(imageFile));
				File.Copy(imageFile, uniquePath);
				count++;
			}
			Directory.Delete(sourceRoot, recursive: true);
			return count;
		});
		this.PreviewRequested?.Invoke(null);
		this.StatusChanged?.Invoke($"已删除 {DisplayName}，并将 {movedCount} 张图片放回备份目录。");
		this.Deleted?.Invoke(this);
	}

	private void RebuildVisibleNodes()
	{
		VisibleNodes.Clear();
		foreach (FolderNodeViewModel item in EnumerateVisible(_rootNode))
		{
			VisibleNodes.Add(item);
		}
	}

	private IEnumerable<FolderNodeViewModel> EnumerateVisible(FolderNodeViewModel node)
	{
		yield return node;
		if (!node.IsExpanded)
		{
			yield break;
		}
		foreach (FolderNodeViewModel child in node.Children)
		{
			foreach (FolderNodeViewModel item in EnumerateVisible(child))
			{
				yield return item;
			}
		}
	}

	private IEnumerable<FolderNodeViewModel> EnumerateAll(FolderNodeViewModel node)
	{
		yield return node;
		foreach (FolderNodeViewModel child in node.Children)
		{
			foreach (FolderNodeViewModel item in EnumerateAll(child))
			{
				yield return item;
			}
		}
	}

	private static void ExpandNodeAncestors(FolderNodeViewModel? node)
	{
		for (FolderNodeViewModel folderNodeViewModel = node; folderNodeViewModel != null; folderNodeViewModel = FindParent(folderNodeViewModel))
		{
			folderNodeViewModel.IsExpanded = true;
		}
	}

	private static FolderNodeViewModel? FindParent(FolderNodeViewModel node)
	{
		if (node.Model.Parent != null)
		{
			return FindNode(node, node.Model.Parent.Id);
		}
		return null;
	}

	private static FolderNodeViewModel? FindNode(FolderNodeViewModel current, Guid targetId)
	{
		if (current.Id == targetId)
		{
			return current;
		}
		foreach (FolderNodeViewModel child in current.Children)
		{
			FolderNodeViewModel folderNodeViewModel = FindNode(child, targetId);
			if (folderNodeViewModel != null)
			{
				return folderNodeViewModel;
			}
		}
		return null;
	}

	private FolderNodeViewModel? RefreshNodeImages(string folderPath)
	{
		FolderNodeViewModel folderNodeViewModel = FindNodeByFolderPath(_rootNode, folderPath);
		if (folderNodeViewModel == null || !Directory.Exists(folderPath))
		{
			return null;
		}
		folderNodeViewModel.Model.Images.Clear();
		foreach (FileInfo item in (from file in new DirectoryInfo(folderPath).EnumerateFiles()
			where IsSupportedImageFile(file.FullName)
			select file).OrderBy<FileInfo, string>((FileInfo file) => file.Name, StringComparer.OrdinalIgnoreCase))
		{
			folderNodeViewModel.Model.Images.Add(new ImageItem
			{
				FilePath = item.FullName,
				FileName = item.Name,
				FileSize = item.Length,
				LastWriteTime = item.LastWriteTime
			});
		}
		return folderNodeViewModel;
	}

	private static FolderNodeViewModel? FindNodeByFolderPath(FolderNodeViewModel current, string folderPath)
	{
		string fullPath = Path.GetFullPath(folderPath);
		if (string.Equals(Path.GetFullPath(current.FolderPath), fullPath, StringComparison.OrdinalIgnoreCase))
		{
			return current;
		}
		foreach (FolderNodeViewModel child in current.Children)
		{
			FolderNodeViewModel folderNodeViewModel = FindNodeByFolderPath(child, folderPath);
			if (folderNodeViewModel != null)
			{
				return folderNodeViewModel;
			}
		}
		return null;
	}

	private FolderNodeViewModel? FindNodeById(Guid? nodeId)
	{
		if (!nodeId.HasValue)
		{
			return null;
		}
		return FindNode(_rootNode, nodeId.Value);
	}

	private static FolderNodeViewModel? FindFirstNodeWithImages(IEnumerable<FolderNodeViewModel> nodes)
	{
		foreach (FolderNodeViewModel node in nodes)
		{
			if (node.Model.Images.Count > 0)
			{
				return node;
			}
			if (node.Children.Count > 0)
			{
				FolderNodeViewModel folderNodeViewModel = FindFirstNodeWithImages(node.Children);
				if (folderNodeViewModel != null)
				{
					return folderNodeViewModel;
				}
			}
		}
		return nodes.FirstOrDefault();
	}

	private void RefreshCurrentImages(bool loadAll = false)
	{
		CurrentImages.Clear();
		if (SelectedNode == null)
		{
			LoadedImageCount = 0;
			UpdateCounts();
			return;
		}
		List<ImageItem> images = SelectedNode.Model.Images;
		int num = (loadAll ? images.Count : Math.Min(images.Count, 60));
		for (int i = 0; i < num; i++)
		{
			CurrentImages.Add(CreateImageViewModel(images[i]));
		}
		LoadedImageCount = CurrentImages.Count;
		UpdateCounts();
	}

	private void LoadMoreImages()
	{
		if (SelectedNode != null && HasMoreImages)
		{
			List<ImageItem> images = SelectedNode.Model.Images;
			int num = Math.Min(images.Count, LoadedImageCount + 60);
			for (int i = LoadedImageCount; i < num; i++)
			{
				CurrentImages.Add(CreateImageViewModel(images[i]));
			}
			LoadedImageCount = CurrentImages.Count;
			OnPropertyChanged("HasImages");
		}
	}

	private ImageItemViewModel CreateImageViewModel(ImageItem model)
	{
		return new ImageItemViewModel(model, delegate
		{
			UpdateCounts();
		}, delegate(ImageItemViewModel image)
		{
			Activate();
			this.PreviewRequested?.Invoke(new PreviewRequest
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
		FileInfo fileInfo = new FileInfo(filePath);
		this.PreviewRequested?.Invoke(new PreviewRequest
		{
			FilePath = fileInfo.FullName,
			FileName = fileInfo.Name,
			FolderPath = CurrentFolderPath,
			FileSize = (fileInfo.Exists ? fileInfo.Length : 0)
		});
	}

	private void UpdateCounts()
	{
		if (SelectedNode == null)
		{
			TotalCount = 0;
			SelectedCount = 0;
		}
		else
		{
			NodeCounts nodeCounts = _workspaceService.CalculateCounts(SelectedNode.Model);
			TotalCount = nodeCounts.Total;
			SelectedCount = nodeCounts.Selected;
		}
		OnPropertyChanged("HasImages");
		OnPropertyChanged("ContentEmptyText");
		OnPropertyChanged("TotalImageCount");
		OnPropertyChanged("ImageLoadSummaryText");
		OnPropertyChanged("HasMoreImages");
		OnPropertyChanged("CountsText");
		_invertSelectionCommand.RaiseCanExecuteChanged();
		_selectAllCommand.RaiseCanExecuteChanged();
		_clearSelectionCommand.RaiseCanExecuteChanged();
		_addImagesCommand.RaiseCanExecuteChanged();
		_copyMainToDetailCommand.RaiseCanExecuteChanged();
		_loadMoreImagesCommand.RaiseCanExecuteChanged();
		this.SelectionContextChanged?.Invoke(this);
	}

	private static string GetUniquePath(string folderPath, string fileName)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
		string extension = Path.GetExtension(fileName);
		string text = Path.Combine(folderPath, fileName);
		int num = 1;
		while (File.Exists(text))
		{
			text = Path.Combine(folderPath, $"{fileNameWithoutExtension} ({num}){extension}");
			num++;
		}
		return text;
	}

	private static bool IsUnsafeDeleteTarget(string folderPath)
	{
		string fullPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? string.Empty;
		return string.IsNullOrWhiteSpace(fullPath) || string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsSameOrChildPath(string path, string parentPath)
	{
		string normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string normalizedParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		return string.Equals(normalizedPath, normalizedParent, StringComparison.OrdinalIgnoreCase)
			|| normalizedPath.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
			|| normalizedPath.StartsWith(normalizedParent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsSupportedImageFile(string filePath)
	{
		return SupportedImageExtensions.Contains(Path.GetExtension(filePath));
	}
}
