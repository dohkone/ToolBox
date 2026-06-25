using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface IFolderScanService
{
    Task<IReadOnlyList<FolderNode>> ScanAsync(
        string rootFolder,
        bool recursive,
        IProgress<FolderScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
