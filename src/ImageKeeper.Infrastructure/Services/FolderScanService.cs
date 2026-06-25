using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.Infrastructure.Services;

public sealed class FolderScanService : IFolderScanService
{
    public Task<IReadOnlyList<FolderNode>> ScanAsync(
        string rootFolder,
        bool recursive,
        IProgress<FolderScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanCore(rootFolder, recursive, progress, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<FolderNode> ScanCore(
        string rootFolder,
        bool recursive,
        IProgress<FolderScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
        {
            return [];
        }

        progress?.Report(new FolderScanProgress
        {
            Stage = "正在准备扫描",
            CurrentFolder = rootFolder
        });

        var rootDirectory = new DirectoryInfo(rootFolder);
        var directories = CollectDirectories(rootDirectory, recursive, progress, cancellationToken);
        var nodeByPath = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
        var processedFolders = 0;
        var imageCount = 0;
        var skippedFolders = 0;

        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processedFolders++;

            var parentNode = directory.Parent is null
                ? null
                : nodeByPath.GetValueOrDefault(directory.Parent.FullName);

            var depth = GetDepth(rootDirectory.FullName, directory.FullName) - 1;
            var node = new FolderNode
            {
                FolderPath = directory.FullName,
                DisplayName = directory.Name,
                Parent = parentNode,
                Depth = depth,
                IsExpanded = depth <= 0
            };

            try
            {
                foreach (var file in directory.EnumerateFiles().Where(IsImage))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    node.Images.Add(new ImageItem
                    {
                        FilePath = file.FullName,
                        FileName = file.Name,
                        FileSize = file.Length,
                        LastWriteTime = file.LastWriteTime
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                skippedFolders++;
            }
            catch (IOException)
            {
                skippedFolders++;
            }

            imageCount += node.Images.Count;
            nodeByPath[directory.FullName] = node;
            parentNode?.Children.Add(node);

            progress?.Report(new FolderScanProgress
            {
                Stage = "正在扫描文件夹",
                CurrentFolder = directory.FullName,
                ProcessedFolders = processedFolders,
                TotalFolders = directories.Count,
                ImageCount = imageCount,
                SkippedFolders = skippedFolders
            });
        }

        if (!nodeByPath.TryGetValue(rootDirectory.FullName, out var rootNode))
        {
            return [];
        }

        SortTree(rootNode);

        progress?.Report(new FolderScanProgress
        {
            Stage = "扫描完成",
            CurrentFolder = rootFolder,
            ProcessedFolders = directories.Count,
            TotalFolders = directories.Count,
            ImageCount = imageCount,
            SkippedFolders = skippedFolders
        });

        return rootNode.Children.Count > 0 ? rootNode.Children : [rootNode];
    }

    private static List<DirectoryInfo> CollectDirectories(
        DirectoryInfo rootDirectory,
        bool recursive,
        IProgress<FolderScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = new List<DirectoryInfo> { rootDirectory };
        if (!recursive)
        {
            return result;
        }

        var pending = new Queue<DirectoryInfo>();
        pending.Enqueue(rootDirectory);
        var skippedFolders = 0;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Dequeue();
            IEnumerable<DirectoryInfo> children;

            try
            {
                children = current.EnumerateDirectories()
                    .Where(child => (child.Attributes & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
                    .ToArray();
            }
            catch (UnauthorizedAccessException)
            {
                skippedFolders++;
                continue;
            }
            catch (IOException)
            {
                skippedFolders++;
                continue;
            }

            foreach (var child in children)
            {
                result.Add(child);
                pending.Enqueue(child);
            }

            progress?.Report(new FolderScanProgress
            {
                Stage = "正在读取目录结构",
                CurrentFolder = current.FullName,
                ProcessedFolders = result.Count,
                TotalFolders = 0,
                SkippedFolders = skippedFolders
            });
        }

        return result;
    }

    private static int GetDepth(string rootPath, string folderPath)
    {
        var relative = Path.GetRelativePath(rootPath, folderPath);
        if (relative == ".")
        {
            return 0;
        }

        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
    }

    private static bool IsImage(FileInfo file)
    {
        var ext = file.Extension.ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp" or ".jfif";
    }

    private static void SortTree(FolderNode node)
    {
        node.Images.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.FileName, right.FileName));
        node.Children.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.DisplayName, right.DisplayName));

        foreach (var child in node.Children)
        {
            SortTree(child);
        }
    }
}
