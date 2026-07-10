using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface ICardSizeInfoService
{
	Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<IReadOnlyDictionary<string, CardSizeInfoRecord>> GetByCardPathsAsync(IEnumerable<string> cardPaths, CancellationToken cancellationToken = default(CancellationToken));

	Task<CardSizeInfoRecord?> GetByCardPathAsync(string cardPath, CancellationToken cancellationToken = default(CancellationToken));

	Task UpsertAsync(CardSizeInfoRecord record, CancellationToken cancellationToken = default(CancellationToken));
}
