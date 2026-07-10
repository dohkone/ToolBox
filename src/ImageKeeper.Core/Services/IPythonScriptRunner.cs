using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImageKeeper.Core.Services;

public interface IPythonScriptRunner
{
	Task<int> RunAsync(string scriptPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default(CancellationToken));
}
