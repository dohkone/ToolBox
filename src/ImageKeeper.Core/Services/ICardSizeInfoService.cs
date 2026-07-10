using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface ICardSizeInfoService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, CardSizeInfoRecord>> GetByCardPathsAsync(
        IEnumerable<string> cardPaths,
        CancellationToken cancellationToken = default);

    Task<CardSizeInfoRecord?> GetByCardPathAsync(
        string cardPath,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        CardSizeInfoRecord record,
        CancellationToken cancellationToken = default);
}
