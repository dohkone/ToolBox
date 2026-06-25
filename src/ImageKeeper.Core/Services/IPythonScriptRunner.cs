namespace ImageKeeper.Core.Services;

public interface IPythonScriptRunner
{
    Task<int> RunAsync(string scriptPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
