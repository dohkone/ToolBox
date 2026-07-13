using System;
using System.IO;

namespace ImageKeeper.PythonBridge;

internal static class PythonProcessHelper
{
	private static readonly string WritableWorkingDirectory = CreateWritableWorkingDirectory();

	public static string GetWritableWorkingDirectory()
	{
		return WritableWorkingDirectory;
	}

	private static string CreateWritableWorkingDirectory()
	{
		string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToolBox", "python-work");
		Directory.CreateDirectory(folderPath);
		return folderPath;
	}
}
