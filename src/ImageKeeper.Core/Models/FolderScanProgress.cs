using System;

namespace ImageKeeper.Core.Models;

public sealed class FolderScanProgress
{
	public string Stage { get; init; } = string.Empty;

	public string CurrentFolder { get; init; } = string.Empty;

	public int ProcessedFolders { get; init; }

	public int TotalFolders { get; init; }

	public int ImageCount { get; init; }

	public int SkippedFolders { get; init; }

	public double Percent
	{
		get
		{
			if (TotalFolders > 0)
			{
				return Math.Min(100.0, (double)ProcessedFolders * 100.0 / (double)TotalFolders);
			}
			return 0.0;
		}
	}
}
