using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface ISpBatchService
{
    Task<SpBatchResult> GenerateAsync(SpBatchRequest request, CancellationToken cancellationToken = default);
}
