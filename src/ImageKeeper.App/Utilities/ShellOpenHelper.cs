using System;
using System.Diagnostics;
using System.IO;

namespace ImageKeeper.App.Utilities;

internal static class ShellOpenHelper
{
	public static void OpenFile(string filePath)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException("File does not exist.", filePath);
		}
		Start(new ProcessStartInfo
		{
			FileName = filePath,
			UseShellExecute = true,
			Verb = "open"
		});
	}

	public static void OpenFolder(string folderPath)
	{
		if (!Directory.Exists(folderPath))
		{
			throw new DirectoryNotFoundException(folderPath);
		}
		Start(new ProcessStartInfo
		{
			FileName = "explorer.exe",
			Arguments = "\"" + Path.GetFullPath(folderPath) + "\"",
			UseShellExecute = true
		});
	}

	public static void RevealInFolder(string filePath)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException("File does not exist.", filePath);
		}
		try
		{
			Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "/select,\"" + filePath + "\"",
				UseShellExecute = true
			});
		}
		catch
		{
			string? directoryName = Path.GetDirectoryName(filePath);
			if (string.IsNullOrWhiteSpace(directoryName))
			{
				throw;
			}
			OpenFolder(directoryName);
		}
	}

	private static void Start(ProcessStartInfo startInfo)
	{
		if (Process.Start(startInfo) == null)
		{
			throw new InvalidOperationException("Failed to launch shell process.");
		}
	}
}
