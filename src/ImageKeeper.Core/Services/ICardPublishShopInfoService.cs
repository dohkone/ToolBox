using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface ICardPublishShopInfoService
{
	Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<IReadOnlyDictionary<string, CardPublishShopInfoRecord>> GetByCardPathsAsync(IEnumerable<string> cardPaths, CancellationToken cancellationToken = default(CancellationToken));

	Task<CardPublishShopInfoRecord?> GetByCardPathAsync(string cardPath, CancellationToken cancellationToken = default(CancellationToken));

	Task UpsertAsync(CardPublishShopInfoRecord record, CancellationToken cancellationToken = default(CancellationToken));

	Task RenameShopAsync(string oldShopName, string newShopName, CancellationToken cancellationToken = default(CancellationToken));
}
