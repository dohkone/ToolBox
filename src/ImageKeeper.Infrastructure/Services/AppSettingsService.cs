using System;
using System.IO;
using System.Text.Json;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.Infrastructure.Services;

public sealed class AppSettingsService : IAppSettingsService
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly string _settingsFilePath = ResolveUserSettingsPath();

	private readonly string _legacySettingsFilePath = Path.Combine(AppContext.BaseDirectory, "config", "user-settings.json");

	private readonly object _syncRoot = new object();

	public AppUserPathsState LoadUserPaths()
	{
		lock (_syncRoot)
		{
			try
			{
				if (!File.Exists(_settingsFilePath))
				{
					TryMigrateLegacySettings();
				}
				if (!File.Exists(_settingsFilePath))
				{
					return new AppUserPathsState();
				}
				return JsonSerializer.Deserialize<AppUserPathsState>(File.ReadAllText(_settingsFilePath), JsonOptions) ?? new AppUserPathsState();
			}
			catch
			{
				return new AppUserPathsState();
			}
		}
	}

	public void SaveUserPaths(AppUserPathsState state)
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		lock (_syncRoot)
		{
			string directoryName = Path.GetDirectoryName(_settingsFilePath);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string contents = JsonSerializer.Serialize(state, JsonOptions);
			File.WriteAllText(_settingsFilePath, contents);
		}
	}

	private static string ResolveUserSettingsPath()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		if (string.IsNullOrWhiteSpace(folderPath))
		{
			folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		}
		return Path.Combine(folderPath, "EcomTool Studio", "user-settings.json");
	}

	private void TryMigrateLegacySettings()
	{
		try
		{
			if (File.Exists(_legacySettingsFilePath))
			{
				string directoryName = Path.GetDirectoryName(_settingsFilePath);
				if (!string.IsNullOrWhiteSpace(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				File.Copy(_legacySettingsFilePath, _settingsFilePath, overwrite: false);
			}
		}
		catch
		{
		}
	}
}
