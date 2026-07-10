using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.Infrastructure.Services;

public sealed class FolderScanService : IFolderScanService
{
	public Task<IReadOnlyList<FolderNode>> ScanAsync(string rootFolder, bool recursive, IProgress<FolderScanProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		return Task.Run(() => ScanCore(rootFolder, recursive, progress, cancellationToken), cancellationToken);
	}

	private static IReadOnlyList<FolderNode> ScanCore(string rootFolder, bool recursive, IProgress<FolderScanProgress>? progress, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
		{
			return Array.Empty<FolderNode>();
		}
		progress?.Report(new FolderScanProgress
		{
			Stage = "正在准备扫描",
			CurrentFolder = rootFolder
		});
		DirectoryInfo directoryInfo = new DirectoryInfo(rootFolder);
		List<DirectoryInfo> list = CollectDirectories(directoryInfo, recursive, progress, cancellationToken);
		Dictionary<string, FolderNode> dictionary = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (DirectoryInfo item in list)
		{
			cancellationToken.ThrowIfCancellationRequested();
			num++;
			FolderNode folderNode = ((item.Parent == null) ? null : dictionary.GetValueOrDefault(item.Parent.FullName));
			int num4 = GetDepth(directoryInfo.FullName, item.FullName) - 1;
			FolderNode folderNode2 = new FolderNode
			{
				FolderPath = item.FullName,
				DisplayName = item.Name,
				Parent = folderNode,
				Depth = num4,
				IsExpanded = (num4 <= 0)
			};
			try
			{
				foreach (FileInfo item2 in item.EnumerateFiles().Where(IsImage))
				{
					cancellationToken.ThrowIfCancellationRequested();
					folderNode2.Images.Add(new ImageItem
					{
						FilePath = item2.FullName,
						FileName = item2.Name,
						FileSize = item2.Length,
						LastWriteTime = item2.LastWriteTime
					});
				}
			}
			catch (UnauthorizedAccessException)
			{
				num3++;
			}
			catch (IOException)
			{
				num3++;
			}
			num2 += folderNode2.Images.Count;
			dictionary[item.FullName] = folderNode2;
			folderNode?.Children.Add(folderNode2);
			progress?.Report(new FolderScanProgress
			{
				Stage = "正在扫描文件夹",
				CurrentFolder = item.FullName,
				ProcessedFolders = num,
				TotalFolders = list.Count,
				ImageCount = num2,
				SkippedFolders = num3
			});
		}
		if (!dictionary.TryGetValue(directoryInfo.FullName, out var value))
		{
			return Array.Empty<FolderNode>();
		}
		SortTree(value);
		progress?.Report(new FolderScanProgress
		{
			Stage = "扫描完成",
			CurrentFolder = rootFolder,
			ProcessedFolders = list.Count,
			TotalFolders = list.Count,
			ImageCount = num2,
			SkippedFolders = num3
		});
		if (value.Children.Count <= 0)
		{
			int num5 = 1;
			List<FolderNode> list2 = new List<FolderNode>(num5);
			CollectionsMarshal.SetCount(list2, num5);
			Span<FolderNode> span = CollectionsMarshal.AsSpan(list2);
			int index = 0;
			span[index] = value;
			return list2;
		}
		return value.Children;
	}

	private static List<DirectoryInfo> CollectDirectories(DirectoryInfo rootDirectory, bool recursive, IProgress<FolderScanProgress>? progress, CancellationToken cancellationToken)
	{
		List<DirectoryInfo> list = new List<DirectoryInfo> { rootDirectory };
		if (!recursive)
		{
			return list;
		}
		Queue<DirectoryInfo> queue = new Queue<DirectoryInfo>();
		queue.Enqueue(rootDirectory);
		int num = 0;
		while (queue.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			DirectoryInfo directoryInfo = queue.Dequeue();
			IEnumerable<DirectoryInfo> enumerable;
			try
			{
				enumerable = (from child in directoryInfo.EnumerateDirectories()
					where (child.Attributes & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint
					select child).ToArray();
			}
			catch (UnauthorizedAccessException)
			{
				num++;
				continue;
			}
			catch (IOException)
			{
				num++;
				continue;
			}
			foreach (DirectoryInfo item in enumerable)
			{
				list.Add(item);
				queue.Enqueue(item);
			}
			progress?.Report(new FolderScanProgress
			{
				Stage = "正在读取目录结构",
				CurrentFolder = directoryInfo.FullName,
				ProcessedFolders = list.Count,
				TotalFolders = 0,
				SkippedFolders = num
			});
		}
		return list;
	}

	private static int GetDepth(string rootPath, string folderPath)
	{
		string relativePath = Path.GetRelativePath(rootPath, folderPath);
		if (relativePath == ".")
		{
			return 0;
		}
		return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
	}

	private static bool IsImage(FileInfo file)
	{
		string text = file.Extension.ToLowerInvariant();
		if (text != null)
		{
			int length = text.Length;
			if (length != 4)
			{
				if (length == 5)
				{
					switch (text[2])
					{
					case 'p':
						break;
					case 'i':
						goto IL_00df;
					case 'e':
						goto IL_00ee;
					case 'f':
						goto IL_00fd;
					default:
						goto IL_010e;
					}
					if (text == ".jpeg")
					{
						goto IL_010a;
					}
				}
			}
			else
			{
				char c = text[1];
				if ((uint)c <= 103u)
				{
					if (c != 'b')
					{
						if (c == 'g' && text == ".gif")
						{
							goto IL_010a;
						}
					}
					else if (text == ".bmp")
					{
						goto IL_010a;
					}
				}
				else if (c != 'j')
				{
					if (c != 'p')
					{
						if (c == 't' && text == ".tif")
						{
							goto IL_010a;
						}
					}
					else if (text == ".png")
					{
						goto IL_010a;
					}
				}
				else if (text == ".jpg")
				{
					goto IL_010a;
				}
			}
		}
		goto IL_010e;
		IL_010a:
		return true;
		IL_010e:
		return false;
		IL_00ee:
		if (text == ".webp")
		{
			goto IL_010a;
		}
		goto IL_010e;
		IL_00df:
		if (text == ".tiff")
		{
			goto IL_010a;
		}
		goto IL_010e;
		IL_00fd:
		if (text == ".jfif")
		{
			goto IL_010a;
		}
		goto IL_010e;
	}

	private static void SortTree(FolderNode node)
	{
		node.Images.Sort((ImageItem left, ImageItem right) => StringComparer.OrdinalIgnoreCase.Compare(left.FileName, right.FileName));
		node.Children.Sort(CompareFolderNodes);
		foreach (FolderNode child in node.Children)
		{
			SortTree(child);
		}
	}

	private static int CompareFolderNodes(FolderNode left, FolderNode right)
	{
		int folderOrder = GetFolderOrder(left.DisplayName);
		int folderOrder2 = GetFolderOrder(right.DisplayName);
		if (folderOrder != folderOrder2)
		{
			return folderOrder.CompareTo(folderOrder2);
		}
		return StringComparer.OrdinalIgnoreCase.Compare(left.DisplayName, right.DisplayName);
	}

	private static int GetFolderOrder(string name)
	{
		return name.ToLowerInvariant() switch
		{
			"main" => 0, 
			"sku" => 1, 
			"detail" => 2, 
			_ => 100, 
		};
	}
}
