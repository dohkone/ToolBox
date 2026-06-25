using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface IProductSheetService
{
    Task<ProductSheetTask> GenerateAsync(string spRootFolder, CancellationToken cancellationToken = default);
    Task RebuildSizeIndexAsync(CancellationToken cancellationToken = default);
}
