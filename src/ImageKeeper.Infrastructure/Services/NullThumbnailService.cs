using ImageKeeper.Core.Services;

namespace ImageKeeper.Infrastructure.Services;

public sealed class NullThumbnailService : IThumbnailService
{
    public Task<object?> GetThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<object?>(null);
    }
}
