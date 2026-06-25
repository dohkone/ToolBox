namespace ImageKeeper.Core.Services;

public interface IThumbnailService
{
    Task<object?> GetThumbnailAsync(string filePath, CancellationToken cancellationToken = default);
}
