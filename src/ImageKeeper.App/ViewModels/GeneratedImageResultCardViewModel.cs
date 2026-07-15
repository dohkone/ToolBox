using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ImageKeeper.App.Utilities;

namespace ImageKeeper.App.ViewModels;

public sealed class GeneratedImageResultCardViewModel : ViewModelBase
{
	private static readonly SemaphoreSlim ThumbnailGate = new SemaphoreSlim(4);

	private readonly string _imagePath;

	private readonly string _fileName;

	private readonly string _linkedCardPath;

	private readonly string _linkedSkuDirectory;

	private readonly Action<GeneratedImageResultCardViewModel>? _selectionChanged;

	private readonly Action<GeneratedImageResultCardViewModel>? _removeRequested;

	private ImageSource? _thumbnailSource;

	private bool _isSelected;

	public string ImagePath => _imagePath;

	public string FileName => _fileName;

	public string LinkedCardPath => _linkedCardPath;

	public string LinkedSkuDirectory => _linkedSkuDirectory;

	public bool IsLinkedToExistingCard => !string.IsNullOrWhiteSpace(_linkedCardPath) && !string.IsNullOrWhiteSpace(_linkedSkuDirectory);

	public bool CanToggleSelection { get; }

	public bool ShowRemoveAction { get; }

	public ICommand OpenFileCommand { get; }

	public ICommand ToggleSelectionCommand { get; }

	public ICommand RemoveCommand { get; }

	public ImageSource? ThumbnailSource
	{
		get
		{
			return _thumbnailSource;
		}
		private set
		{
			SetProperty(ref _thumbnailSource, value, "ThumbnailSource");
		}
	}

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		private set
		{
			SetProperty(ref _isSelected, value, "IsSelected");
		}
	}

	public GeneratedImageResultCardViewModel(string imagePath, string? fileName = null, bool canToggleSelection = false, bool showRemoveAction = false, Action<GeneratedImageResultCardViewModel>? selectionChanged = null, Action<GeneratedImageResultCardViewModel>? removeRequested = null, string? linkedCardPath = null, string? linkedSkuDirectory = null)
	{
		_imagePath = imagePath;
		_fileName = (string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(imagePath) : fileName);
		_linkedCardPath = linkedCardPath ?? string.Empty;
		_linkedSkuDirectory = linkedSkuDirectory ?? string.Empty;
		CanToggleSelection = canToggleSelection;
		ShowRemoveAction = showRemoveAction;
		_selectionChanged = selectionChanged;
		_removeRequested = removeRequested;
		OpenFileCommand = new RelayCommand(delegate
		{
			OpenFile();
		}, (object? _) => File.Exists(ImagePath));
		ToggleSelectionCommand = new RelayCommand(delegate
		{
			ToggleSelection();
		}, (object? _) => CanToggleSelection);
		RemoveCommand = new RelayCommand(delegate
		{
			RequestRemove();
		}, (object? _) => ShowRemoveAction);
		LoadThumbnailAsync();
	}

	public void HandlePrimaryClick(int clickCount)
	{
		if (clickCount >= 2)
		{
			OpenFile();
		}
		else if (CanToggleSelection)
		{
			ToggleSelection();
		}
	}

	public void SetSelected(bool isSelected)
	{
		if (SetProperty(ref _isSelected, isSelected, "IsSelected"))
		{
			_selectionChanged?.Invoke(this);
		}
	}

	private async void LoadThumbnailAsync()
	{
		await ThumbnailGate.WaitAsync();
		try
		{
			ThumbnailSource = await Task.Run(() => ImageBitmapLoader.LoadFromFile(ImagePath, 220));
		}
		catch
		{
			ThumbnailSource = null;
		}
		finally
		{
			ThumbnailGate.Release();
		}
	}

	private void ToggleSelection()
	{
		SetSelected(!IsSelected);
	}

	private void RequestRemove()
	{
		_removeRequested?.Invoke(this);
	}

	private void OpenFile()
	{
		try
		{
			ShellOpenHelper.OpenFile(ImagePath);
		}
		catch (Exception ex)
		{
			MessageBox.Show("打开文件失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}
}
