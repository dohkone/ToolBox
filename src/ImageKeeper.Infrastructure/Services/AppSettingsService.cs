using System.Text.Json;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.Infrastructure.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "config", "user-settings.json");
    private readonly object _syncRoot = new();

    public AppUserPathsState LoadUserPaths()
    {
        lock (_syncRoot)
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return new AppUserPathsState();
                }

                var json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<AppUserPathsState>(json, JsonOptions) ?? new AppUserPathsState();
            }
            catch
            {
                return new AppUserPathsState();
            }
        }
    }

    public void SaveUserPaths(AppUserPathsState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_syncRoot)
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
    }
}
