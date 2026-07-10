using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface ISkuOptimizeService
{
    Task<SkuOptimizeResult> GenerateAsync(SkuOptimizeRequest request, CancellationToken cancellationToken = default);
    void CancelCurrentRun();
}
