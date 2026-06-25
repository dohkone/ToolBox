namespace ImageKeeper.Core.Services;

public interface IYingdaoLauncher
{
    Task<string> LaunchMiaoshouAsync(CancellationToken cancellationToken = default);
}
