using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ImageKeeper.App.Utilities;
using ImageKeeper.Core.Models;

namespace ImageKeeper.App.ViewModels;

public sealed class ImageItemViewModel : ViewModelBase
{
	private static readonly SemaphoreSlim ThumbnailGate = new SemaphoreSlim(4);

	private readonly Action<ImageItemViewModel> _selectionChanged;

	private readonly Action<ImageItemViewModel> _previewRequested;

	private bool _isSelected;

	private ImageSource? _thumbnailSource;

	public ImageItem Model { get; }

	public string FilePath => Model.FilePath;

	public string FileName => Model.FileName;

	public ICommand PreviewCommand { get; }

	public ICommand OpenFileCommand { get; }

	public ICommand OpenContainingFolderCommand { get; }

	public ICommand CopyPathCommand { get; }

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
		set
		{
			if (SetProperty(ref _isSelected, value, "IsSelected"))
			{
				Model.IsSelected = value;
				OnPropertyChanged("CardBackground");
				OnPropertyChanged("CardBorderBrush");
				_selectionChanged(this);
			}
		}
	}

	public Brush CardBackground
	{
		get
		{
			if (!IsSelected)
			{
				return Brushes.White;
			}
			return new SolidColorBrush(Color.FromRgb(238, 238, 238));
		}
	}

	public Brush CardBorderBrush
	{
		get
		{
			if (!IsSelected)
			{
				return new SolidColorBrush(Color.FromRgb(217, 225, 236));
			}
			return Brushes.Transparent;
		}
	}

	public ImageItemViewModel(ImageItem model, Action<ImageItemViewModel> selectionChanged, Action<ImageItemViewModel> previewRequested)
	{
		Model = model;
		_selectionChanged = selectionChanged;
		_previewRequested = previewRequested;
		_isSelected = model.IsSelected;
		PreviewCommand = new RelayCommand(delegate
		{
			_previewRequested(this);
		});
		OpenFileCommand = new RelayCommand(delegate
		{
			OpenFile();
		}, (object? _) => File.Exists(FilePath));
		OpenContainingFolderCommand = new RelayCommand(delegate
		{
			OpenContainingFolder();
		}, (object? _) => File.Exists(FilePath));
		CopyPathCommand = new RelayCommand(delegate
		{
			Clipboard.SetText(FilePath);
		}, (object? _) => !string.IsNullOrWhiteSpace(FilePath));
		LoadThumbnailAsync();
	}

	public void SyncSelectionStateFromModel()
	{
		if (_isSelected != Model.IsSelected)
		{
			_isSelected = Model.IsSelected;
			OnPropertyChanged("IsSelected");
			OnPropertyChanged("CardBackground");
			OnPropertyChanged("CardBorderBrush");
		}
	}

	private async void LoadThumbnailAsync()
	{
		await ThumbnailGate.WaitAsync();
		try
		{
			ThumbnailSource = await Task.Run(() => ImageBitmapLoader.LoadFromFile(FilePath, 220));
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

	private void OpenFile()
	{
		try
		{
			ShellOpenHelper.OpenFile(FilePath);
		}
		catch (Exception ex)
		{
			MessageBox.Show("打开文件失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void OpenContainingFolder()
	{
		try
		{
			ShellOpenHelper.RevealInFolder(FilePath);
		}
		catch (Exception ex)
		{
			MessageBox.Show("打开所在目录失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}
}
