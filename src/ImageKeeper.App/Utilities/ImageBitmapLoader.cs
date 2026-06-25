using System.IO;
using System.Windows.Media.Imaging;

namespace ImageKeeper.App.Utilities;

public static class ImageBitmapLoader
{
    public static BitmapSource? LoadFromFile(string filePath, int? decodePixelWidth = null, int? decodePixelHeight = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;

        if (decodePixelWidth.HasValue)
        {
            bitmap.DecodePixelWidth = decodePixelWidth.Value;
        }

        if (decodePixelHeight.HasValue)
        {
            bitmap.DecodePixelHeight = decodePixelHeight.Value;
        }

        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
