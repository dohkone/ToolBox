using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface IProductSheetService
{
    Task<ProductSheetTask> GenerateAsync(
        string spRootFolder,
        IReadOnlyList<string>? sizes = null,
        CancellationToken cancellationToken = default);

    Task RebuildSizeIndexAsync(CancellationToken cancellationToken = default);
}
