using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface IAutoPublishStateService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, AutoPublishCardRecord>> GetByCardPathsAsync(
        IEnumerable<string> cardFolderPaths,
        CancellationToken cancellationToken = default);

    Task UpsertStatusAsync(
        string cardFolderPath,
        string displayName,
        AutoPublishStatus status,
        string lastError = "",
        CancellationToken cancellationToken = default);
}
