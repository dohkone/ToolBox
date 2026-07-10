using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface IAutoPublishStateService
{
	Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task MarkIncompletePublishingAsFailedAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<IReadOnlyDictionary<string, AutoPublishCardRecord>> GetByCardPathsAsync(IEnumerable<string> cardFolderPaths, CancellationToken cancellationToken = default(CancellationToken));

	Task UpsertStatusAsync(string cardFolderPath, string displayName, AutoPublishStatus status, string lastError = "", CancellationToken cancellationToken = default(CancellationToken));
}
