using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ImageKeeper.App.Utilities;
using ImageKeeper.Core.Models;
using Media = System.Windows.Media;

namespace ImageKeeper.App.ViewModels;

public sealed class ImageItemViewModel : ViewModelBase
{
    private static readonly SemaphoreSlim ThumbnailGate = new(4);
    private readonly Action<ImageItemViewModel> _selectionChanged;
    private readonly Action<ImageItemViewModel> _previewRequested;
    private bool _isSelected;
    private Media.ImageSource? _thumbnailSource;

    public ImageItemViewModel(
        ImageItem model,
        Action<ImageItemViewModel> selectionChanged,
        Action<ImageItemViewModel> previewRequested)
    {
        Model = model;
        _selectionChanged = selectionChanged;
        _previewRequested = previewRequested;
        _isSelected = model.IsSelected;
        PreviewCommand = new RelayCommand(_ => _previewRequested(this));
        OpenFileCommand = new RelayCommand(_ => OpenFile(), _ => File.Exists(FilePath));
        OpenContainingFolderCommand = new RelayCommand(_ => OpenContainingFolder(), _ => File.Exists(FilePath));
        CopyPathCommand = new RelayCommand(_ => System.Windows.Clipboard.SetText(FilePath), _ => !string.IsNullOrWhiteSpace(FilePath));
        LoadThumbnailAsync();
    }

    public ImageItem Model { get; }

    public string FilePath => Model.FilePath;

    public string FileName => Model.FileName;

    public ICommand PreviewCommand { get; }

    public ICommand OpenFileCommand { get; }

    public ICommand OpenContainingFolderCommand { get; }

    public ICommand CopyPathCommand { get; }

    public Media.ImageSource? ThumbnailSource
    {
        get => _thumbnailSource;
        private set => SetProperty(ref _thumbnailSource, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            Model.IsSelected = value;
            OnPropertyChanged(nameof(CardBackground));
            OnPropertyChanged(nameof(CardBorderBrush));
            _selectionChanged(this);
        }
    }

    public Media.Brush CardBackground => IsSelected
        ? new Media.SolidColorBrush(Media.Color.FromRgb(234, 241, 255))
        : Media.Brushes.White;

    public Media.Brush CardBorderBrush => IsSelected
        ? new Media.SolidColorBrush(Media.Color.FromRgb(159, 190, 255))
        : new Media.SolidColorBrush(Media.Color.FromRgb(217, 225, 236));

    public void SyncSelectionStateFromModel()
    {
        if (_isSelected == Model.IsSelected)
        {
            return;
        }

        _isSelected = Model.IsSelected;
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(CardBackground));
        OnPropertyChanged(nameof(CardBorderBrush));
    }

    private async void LoadThumbnailAsync()
    {
        await ThumbnailGate.WaitAsync();

        try
        {
            ThumbnailSource = await Task.Run(() => ImageBitmapLoader.LoadFromFile(FilePath, decodePixelWidth: 220));
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
        Process.Start(new ProcessStartInfo
        {
            FileName = FilePath,
            UseShellExecute = true
        });
    }

    private void OpenContainingFolder()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{FilePath}\"",
            UseShellExecute = true
        });
    }
}
