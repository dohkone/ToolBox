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
		using FileStream streamSource = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
		if (decodePixelWidth.HasValue)
		{
			bitmapImage.DecodePixelWidth = decodePixelWidth.Value;
		}
		if (decodePixelHeight.HasValue)
		{
			bitmapImage.DecodePixelHeight = decodePixelHeight.Value;
		}
		bitmapImage.StreamSource = streamSource;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		return bitmapImage;
	}
}
