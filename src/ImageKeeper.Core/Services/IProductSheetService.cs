using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface IProductSheetService
{
	Task<ProductSheetTask> GenerateAsync(string spRootFolder, IReadOnlyList<string>? sizes = null, bool titleChineseOnly = false, CancellationToken cancellationToken = default(CancellationToken));

	Task RebuildSizeIndexAsync(CancellationToken cancellationToken = default(CancellationToken));
}
