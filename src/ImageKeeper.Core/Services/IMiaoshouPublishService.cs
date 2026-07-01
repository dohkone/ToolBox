using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface IMiaoshouPublishService
{
    Task<MiaoshouPublishResult> PublishAsync(
        MiaoshouPublishRequest request,
        CancellationToken cancellationToken = default);
}
