using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ImageKeeper.App.Utilities;
using Media = System.Windows.Media;

namespace ImageKeeper.App.ViewModels;

public sealed class GeneratedImageResultCardViewModel : ViewModelBase
{
    private static readonly SemaphoreSlim ThumbnailGate = new(4);
    private readonly string _imagePath;
    private readonly string _fileName;
    private readonly Action<GeneratedImageResultCardViewModel>? _selectionChanged;
    private readonly Action<GeneratedImageResultCardViewModel>? _removeRequested;
    private Media.ImageSource? _thumbnailSource;
    private bool _isSelected;

    public GeneratedImageResultCardViewModel(
        string imagePath,
        string? fileName = null,
        bool canToggleSelection = false,
        bool showRemoveAction = false,
        Action<GeneratedImageResultCardViewModel>? selectionChanged = null,
        Action<GeneratedImageResultCardViewModel>? removeRequested = null)
    {
        _imagePath = imagePath;
        _fileName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(imagePath) : fileName;
        CanToggleSelection = canToggleSelection;
        ShowRemoveAction = showRemoveAction;
        _selectionChanged = selectionChanged;
        _removeRequested = removeRequested;
        OpenFileCommand = new RelayCommand(_ => OpenFile(), _ => File.Exists(ImagePath));
        ToggleSelectionCommand = new RelayCommand(_ => ToggleSelection(), _ => CanToggleSelection);
        RemoveCommand = new RelayCommand(_ => RequestRemove(), _ => ShowRemoveAction);
        LoadThumbnailAsync();
    }

    public string ImagePath => _imagePath;

    public string FileName => _fileName;

    public bool CanToggleSelection { get; }

    public bool ShowRemoveAction { get; }

    public ICommand OpenFileCommand { get; }

    public ICommand ToggleSelectionCommand { get; }

    public ICommand RemoveCommand { get; }

    public Media.ImageSource? ThumbnailSource
    {
        get => _thumbnailSource;
        private set => SetProperty(ref _thumbnailSource, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        private set => SetProperty(ref _isSelected, value);
    }

    public void HandlePrimaryClick(int clickCount)
    {
        if (clickCount >= 2)
        {
            OpenFile();
            return;
        }

        if (CanToggleSelection)
        {
            ToggleSelection();
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (!SetProperty(ref _isSelected, isSelected, nameof(IsSelected)))
        {
            return;
        }

        _selectionChanged?.Invoke(this);
    }

    private async void LoadThumbnailAsync()
    {
        await ThumbnailGate.WaitAsync();

        try
        {
            ThumbnailSource = await Task.Run(() => ImageBitmapLoader.LoadFromFile(ImagePath, decodePixelWidth: 220));
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
        Process.Start(new ProcessStartInfo
        {
            FileName = ImagePath,
            UseShellExecute = true
        });
    }
}
