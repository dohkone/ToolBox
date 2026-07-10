using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Services;

namespace ImageKeeper.Infrastructure.Services;

public sealed class NullThumbnailService : IThumbnailService
{
	public Task<object?> GetThumbnailAsync(string filePath, CancellationToken cancellationToken = default(CancellationToken))
	{
		return Task.FromResult<object>(null);
	}
}
