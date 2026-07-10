using System.Threading;
using System.Threading.Tasks;

namespace ImageKeeper.Core.Services;

public interface IThumbnailService
{
	Task<object?> GetThumbnailAsync(string filePath, CancellationToken cancellationToken = default(CancellationToken));
}
