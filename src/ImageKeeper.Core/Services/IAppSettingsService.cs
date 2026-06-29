using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface IAppSettingsService
{
    AppUserPathsState LoadUserPaths();

    void SaveUserPaths(AppUserPathsState state);
}
