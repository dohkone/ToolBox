using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

	private readonly Func<RootCardViewModel, AutoPublishStatus, string, Task>? _setAutoPublishStatusAsync;

	private readonly Func<RootCardViewModel, Task>? _runAutoPublishCardAsync;

	private readonly Func<RootCardViewModel, Task>? _openCardSizeDialogAsync;

	private readonly Action? _batchSelectionChanged;

	private readonly RelayCommand _activateCommand;

	private readonly RelayCommand _closeCommand;

	private string _rootFolder;

	private string _backupFolder;

	private string _title;

	private bool _isSelected;

	public ObservableCollection<RootCardViewModel> RootCards { get; } = new ObservableCollection<RootCardViewModel>();

	public ObservableCollection<RootCardViewModel> FilteredRootCards { get; } = new ObservableCollection<RootCardViewModel>();

	public RelayCommand ActivateCommand => _activateCommand;

	public RelayCommand CloseCommand => _closeCommand;

	public string RootFolder
	{
		get
		{
			return _rootFolder;
		}
		private set
		{
			if (SetProperty(ref _rootFolder, value, "RootFolder"))
			{
				OnPropertyChanged("LoadedFolderText");
				OnPropertyChanged("IsPlaceholder");
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
			if (!SetProperty(ref _backupFolder, value, "BackupFolder"))
			{
				return;
			}
			foreach (RootCardViewModel rootCard in RootCards)
			{
				rootCard.SetBackupFolder(value);
			}
		}
	}

	public string Title
	{
		get
		{
			return _title;
		}
		private set
		{
			SetProperty(ref _title, value, "Title");
		}
	}

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			SetProperty(ref _isSelected, value, "IsSelected");
		}
	}

	public bool HasRootCards => RootCards.Count > 0;

	public bool IsPlaceholder => string.IsNullOrWhiteSpace(RootFolder);

	public RootCardViewModel? ActiveCard { get; private set; }

	public string LoadedFolderText
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(RootFolder))
			{
				return "当前父目录：" + RootFolder;
			}
			return "当前文件夹：未选择";
		}
	}

	public event Action<WorkspaceTabViewModel>? RequestedActivate;

	public event Action<WorkspaceTabViewModel>? RequestedClose;

	public event Action<PreviewRequest?>? PreviewRequested;

	public event Action<string>? StatusChanged;

	public event Action<RootCardViewModel, IReadOnlyList<string>>? CardImageFilesAdded;

	public event Action<WorkspaceTabViewModel>? SelectionContextChanged;

	public WorkspaceTabViewModel(string rootFolder, string backupFolder, IImageWorkspaceService imageWorkspaceService, IWorkspaceStateService workspaceStateService, IProductSheetService? productSheetService = null, Func<bool>? isAutoPublishBusyProvider = null, Func<Func<Task>, Task>? runExclusiveAsync = null, Func<RootCardViewModel, AutoPublishStatus, string, Task>? setAutoPublishStatusAsync = null, Func<RootCardViewModel, Task>? runAutoPublishCardAsync = null, Func<RootCardViewModel, Task>? openCardSizeDialogAsync = null, Action? batchSelectionChanged = null)
	{
		_rootFolder = rootFolder;
		_backupFolder = backupFolder;
		_imageWorkspaceService = imageWorkspaceService;
		_workspaceStateService = workspaceStateService;
		_productSheetService = productSheetService;
		_isAutoPublishBusyProvider = isAutoPublishBusyProvider;
		_runExclusiveAsync = runExclusiveAsync;
		_setAutoPublishStatusAsync = setAutoPublishStatusAsync;
		_runAutoPublishCardAsync = runAutoPublishCardAsync;
		_openCardSizeDialogAsync = openCardSizeDialogAsync;
		_batchSelectionChanged = batchSelectionChanged;
		_title = BuildTitle(rootFolder);
		_activateCommand = new RelayCommand(delegate
		{
			this.RequestedActivate?.Invoke(this);
		});
		_closeCommand = new RelayCommand(delegate
		{
			this.RequestedClose?.Invoke(this);
		});
	}

	public NodeCounts GetTabCounts()
	{
		return _imageWorkspaceService.CalculateCounts(RootCards.Select((RootCardViewModel card) => card.RootNode.Model));
	}

	public void SetRootFolder(string rootFolder)
	{
		RootFolder = rootFolder;
		Title = BuildTitle(rootFolder);
	}

	public void SetRootNodes(IEnumerable<FolderNode> nodes)
	{
		foreach (RootCardViewModel rootCard in RootCards)
		{
			rootCard.PreviewRequested -= OnCardPreviewRequested;
			rootCard.StatusChanged -= OnCardStatusChanged;
			rootCard.Activated -= OnCardActivated;
			rootCard.SelectionContextChanged -= OnCardSelectionContextChanged;
			rootCard.ImageFilesAdded -= OnCardImageFilesAdded;
		}
		RootCards.Clear();
		FilteredRootCards.Clear();
		ActiveCard = null;
		foreach (FolderNode node in nodes)
		{
			RootCardViewModel rootCardViewModel = new RootCardViewModel(node, _imageWorkspaceService, _workspaceStateService, BackupFolder, _productSheetService, _isAutoPublishBusyProvider, _runExclusiveAsync, _setAutoPublishStatusAsync, _runAutoPublishCardAsync, _openCardSizeDialogAsync, _batchSelectionChanged);
			rootCardViewModel.PreviewRequested += OnCardPreviewRequested;
			rootCardViewModel.StatusChanged += OnCardStatusChanged;
			rootCardViewModel.Activated += OnCardActivated;
			rootCardViewModel.SelectionContextChanged += OnCardSelectionContextChanged;
			rootCardViewModel.ImageFilesAdded += OnCardImageFilesAdded;
			RootCards.Add(rootCardViewModel);
		}
		ApplyAutoPublishStatusFilter(AutoPublishStatusFilter.All);
		if (RootCards.Count > 0)
		{
			SetActiveCard(RootCards[0]);
		}
		OnPropertyChanged("HasRootCards");
	}

	public void ApplyAutoPublishStatusFilter(AutoPublishStatusFilter filter)
	{
		FilteredRootCards.Clear();
		foreach (RootCardViewModel item in RootCards.Where((RootCardViewModel card) => MatchesAutoPublishStatusFilter(card, filter)))
		{
			FilteredRootCards.Add(item);
		}
	}

	public int CountByAutoPublishStatusFilter(AutoPublishStatusFilter filter)
	{
		return RootCards.Count((RootCardViewModel card) => MatchesAutoPublishStatusFilter(card, filter));
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
		foreach (RootCardViewModel rootCard in RootCards)
		{
			rootCard.RestoreOrSelectDefaultNode(activateCard: false);
		}
		RootCardViewModel activeCard = RootCards.FirstOrDefault((RootCardViewModel card) => card.GetFirstNodeWithImages() != null) ?? RootCards[0];
		SetActiveCard(activeCard);
	}

	public string GetSelectionSummaryText()
	{
		if (ActiveCard?.SelectedNode != null)
		{
			return ActiveCard.SelectionSummaryText;
		}
		NodeCounts tabCounts = GetTabCounts();
		return $"图片 {tabCounts.Total} 张，已选中 {tabCounts.Selected} 张，未选中 {tabCounts.Unselected} 张";
	}

	public string GetCurrentFolderText()
	{
		if (ActiveCard?.SelectedNode != null)
		{
			return "当前文件夹：" + ActiveCard.CurrentFolderPath;
		}
		if (!string.IsNullOrWhiteSpace(RootFolder))
		{
			return "当前父目录：" + RootFolder;
		}
		return "当前文件夹：未选择";
	}

	public void InvertSelection()
	{
		ActiveCard?.InvertSelectionFromToolbar();
	}

	public void NotifyAutoPublishStateChanged()
	{
		foreach (RootCardViewModel rootCard in RootCards)
		{
			rootCard.NotifyAutoPublishStateChanged();
		}
		_batchSelectionChanged?.Invoke();
	}

	public async Task RefreshAutoPublishRecordsAsync(IAutoPublishStateService autoPublishStateService, CancellationToken cancellationToken = default(CancellationToken))
	{
		RootCardViewModel[] cards = RootCards.Where((RootCardViewModel card) => !string.IsNullOrWhiteSpace(card.AutoPublishKeyPath)).ToArray();
		IReadOnlyDictionary<string, AutoPublishCardRecord> readOnlyDictionary = await autoPublishStateService.GetByCardPathsAsync(cards.Select((RootCardViewModel card) => card.AutoPublishKeyPath), cancellationToken);
		RootCardViewModel[] array = cards;
		foreach (RootCardViewModel rootCardViewModel in array)
		{
			readOnlyDictionary.TryGetValue(rootCardViewModel.AutoPublishKeyPath, out var value);
			rootCardViewModel.ApplyAutoPublishRecord(value);
		}
	}

	public async Task RefreshCardSizeInfoAsync(ICardSizeInfoService cardSizeInfoService, Func<RootCardViewModel, CardSizeInfoRecord?, Task<bool>> isStaleAsync, CancellationToken cancellationToken = default(CancellationToken))
	{
		RootCardViewModel[] cards = RootCards.Where((RootCardViewModel rootCardViewModel) => !string.IsNullOrWhiteSpace(rootCardViewModel.AutoPublishKeyPath)).ToArray();
		IReadOnlyDictionary<string, CardSizeInfoRecord> records = await cardSizeInfoService.GetByCardPathsAsync(cards.Select((RootCardViewModel rootCardViewModel) => rootCardViewModel.AutoPublishKeyPath), cancellationToken);
		RootCardViewModel[] array = cards;
		foreach (RootCardViewModel card in array)
		{
			records.TryGetValue(card.AutoPublishKeyPath, out CardSizeInfoRecord record);
			bool hasStaleSizeImage = await isStaleAsync(card, record);
			card.ApplyCardSizeInfo(record, hasStaleSizeImage);
			record = null;
		}
	}

	private static bool MatchesAutoPublishStatusFilter(RootCardViewModel card, AutoPublishStatusFilter filter)
	{
		if (filter != AutoPublishStatusFilter.All)
		{
			return card.AutoPublishStatus == (AutoPublishStatus)filter;
		}
		return true;
	}

	public IReadOnlyList<RootCardViewModel> GetBatchSelectedCards()
	{
		return RootCards.Where((RootCardViewModel card) => card.IsCollapsed && card.IsBatchSelected && card.CanBatchSelect).ToArray();
	}

	public bool HasBatchSelectableCards()
	{
		return RootCards.Any((RootCardViewModel card) => card.CanBatchSelect);
	}

	public void SetBatchSelectionForAll(bool isSelected)
	{
		foreach (RootCardViewModel item in RootCards.Where((RootCardViewModel card) => card.CanBatchSelect))
		{
			item.IsBatchSelected = isSelected;
		}
		_batchSelectionChanged?.Invoke();
	}

	private void OnCardPreviewRequested(PreviewRequest? request)
	{
		this.PreviewRequested?.Invoke(request);
	}

	private void OnCardStatusChanged(string message)
	{
		this.StatusChanged?.Invoke(message);
	}

	private void OnCardActivated(RootCardViewModel card)
	{
		SetActiveCard(card);
	}

	private void OnCardSelectionContextChanged(RootCardViewModel card)
	{
		if (ActiveCard == card)
		{
			this.SelectionContextChanged?.Invoke(this);
		}
	}

	private void OnCardImageFilesAdded(RootCardViewModel card, IReadOnlyList<string> copiedFiles)
	{
		this.CardImageFilesAdded?.Invoke(card, copiedFiles);
	}

	private void SetActiveCard(RootCardViewModel? card)
	{
		if (ActiveCard != card)
		{
			if (ActiveCard != null)
			{
				ActiveCard.IsActive = false;
			}
			ActiveCard = card;
			if (ActiveCard != null)
			{
				ActiveCard.IsActive = true;
			}
			this.SelectionContextChanged?.Invoke(this);
		}
	}

	private static string BuildTitle(string rootFolder)
	{
		if (string.IsNullOrWhiteSpace(rootFolder))
		{
			return "新标签页";
		}
		string fileName = Path.GetFileName(rootFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		if (!string.IsNullOrWhiteSpace(fileName))
		{
			return fileName;
		}
		return rootFolder;
	}
}
