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
    private Media.ImageSource? _thumbnailSource;

    public GeneratedImageResultCardViewModel(string imagePath, string? fileName = null)
    {
        _imagePath = imagePath;
        _fileName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(imagePath) : fileName;
        OpenFileCommand = new RelayCommand(_ => OpenFile(), _ => File.Exists(ImagePath));
        LoadThumbnailAsync();
    }

    public string ImagePath => _imagePath;

    public string FileName => _fileName;

    public ICommand OpenFileCommand { get; }

    public Media.ImageSource? ThumbnailSource
    {
        get => _thumbnailSource;
        private set => SetProperty(ref _thumbnailSource, value);
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

    private void OpenFile()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = ImagePath,
            UseShellExecute = true
        });
    }
}
