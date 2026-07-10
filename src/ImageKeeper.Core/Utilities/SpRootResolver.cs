using System;
using System.IO;

namespace ImageKeeper.Core.Utilities;

public static class SpRootResolver
{
	public static string? Resolve(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		DirectoryInfo directoryInfo = new DirectoryInfo(path);
		if (!directoryInfo.Exists && directoryInfo.Parent != null)
		{
			directoryInfo = directoryInfo.Parent;
		}
		while (directoryInfo != null)
		{
			if (directoryInfo.Name.StartsWith("SP", StringComparison.OrdinalIgnoreCase))
			{
				return directoryInfo.FullName;
			}
			directoryInfo = directoryInfo.Parent;
		}
		return null;
	}
}
