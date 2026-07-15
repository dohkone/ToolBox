using System;
using System.IO;

namespace ImageKeeper.Core;

public static class WorkspaceDefaults
{
	private static readonly string LegacyRoot = "D:\\temu_auto";

	private static string UserWorkspaceRoot
	{
		get
		{
			string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			if (!string.IsNullOrWhiteSpace(localAppData))
			{
				return Path.Combine(localAppData, "ToolBox", "workspace");
			}
			return Path.Combine(Path.GetTempPath(), "ToolBox", "workspace");
		}
	}

	private static string PackagedWorkspaceRoot => Path.Combine(AppContext.BaseDirectory, "data", "workspace");

	public static string DefaultOpenFolder => ResolveWorkspaceFolder("review");

	public static string DefaultBackupFolder => ResolveWorkspaceFolder("backup");

	public static string DefaultExcelFolder => ResolveWorkspaceFolder("excel");

	public static string DefaultSpBatchOutputFolder => ResolveWorkspaceFolder("assert");

	public static string DefaultTempFolder => ResolveWorkspaceFolder("temp");

	public static bool IsPackagedWorkspacePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}

		string packagedRoot = Path.GetFullPath(PackagedWorkspaceRoot);
		string fullPath = Path.GetFullPath(path);
		return fullPath.StartsWith(packagedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	}

	public static string ToUserWorkspacePath(string packagedWorkspacePath)
	{
		string packagedRoot = Path.GetFullPath(PackagedWorkspaceRoot);
		string fullPath = Path.GetFullPath(packagedWorkspacePath);
		string relativePath = Path.GetRelativePath(packagedRoot, fullPath);
		return Path.Combine(UserWorkspaceRoot, relativePath);
	}

	private static string ResolveWorkspaceFolder(string folderName)
	{
		if (Directory.Exists(LegacyRoot))
		{
			return Path.Combine(LegacyRoot, folderName);
		}

		return Path.Combine(UserWorkspaceRoot, folderName);
	}
}
