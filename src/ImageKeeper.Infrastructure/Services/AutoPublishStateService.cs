using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;
using Microsoft.Data.Sqlite;

namespace ImageKeeper.Infrastructure.Services;

public sealed class AutoPublishStateService : IAutoPublishStateService
{
	private readonly string _databasePath;

	public AutoPublishStateService(string databasePath)
	{
		_databasePath = databasePath;
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_databasePath));
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "CREATE TABLE IF NOT EXISTS AutoPublishCards (\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\n    CardFolderPath TEXT NOT NULL UNIQUE,\n    DisplayName TEXT NOT NULL DEFAULT '',\n    Status INTEGER NOT NULL DEFAULT 0,\n    LastError TEXT NOT NULL DEFAULT '',\n    CreatedAt TEXT NOT NULL,\n    UpdatedAt TEXT NOT NULL,\n    LastRunAt TEXT NULL\n);\n\nCREATE INDEX IF NOT EXISTS IX_AutoPublishCards_Status\nON AutoPublishCards(Status);";
		await sqliteCommand.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task MarkIncompletePublishingAsFailedAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		await InitializeAsync(cancellationToken);
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		string value = DateTimeOffset.Now.ToString("O");
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "UPDATE AutoPublishCards\nSET Status = $failedStatus,\n    LastError = $lastError,\n    UpdatedAt = $now,\n    LastRunAt = $now\nWHERE Status = $publishingStatus;";
		sqliteCommand.Parameters.AddWithValue("$failedStatus", 3);
		sqliteCommand.Parameters.AddWithValue("$publishingStatus", 1);
		sqliteCommand.Parameters.AddWithValue("$lastError", "上次自动上架未正常结束，请重新执行。");
		sqliteCommand.Parameters.AddWithValue("$now", value);
		await sqliteCommand.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<IReadOnlyDictionary<string, AutoPublishCardRecord>> GetByCardPathsAsync(IEnumerable<string> cardFolderPaths, CancellationToken cancellationToken = default(CancellationToken))
	{
		string[] paths = cardFolderPaths.Where((string path) => !string.IsNullOrWhiteSpace(path)).Select(NormalizePath).Distinct<string>(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (paths.Length == 0)
		{
			return new Dictionary<string, AutoPublishCardRecord>(StringComparer.OrdinalIgnoreCase);
		}
		await InitializeAsync(cancellationToken);
		IReadOnlyDictionary<string, AutoPublishCardRecord> result2;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync(cancellationToken);
			string[] array = paths.Select((string _, int index) => $"$path{index}").ToArray();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT Id, CardFolderPath, DisplayName, Status, LastError, CreatedAt, UpdatedAt, LastRunAt\nFROM AutoPublishCards\nWHERE CardFolderPath IN (" + string.Join(", ", array) + ");";
			for (int num = 0; num < paths.Length; num++)
			{
				sqliteCommand.Parameters.AddWithValue(array[num], paths[num]);
			}
			Dictionary<string, AutoPublishCardRecord> result = new Dictionary<string, AutoPublishCardRecord>(StringComparer.OrdinalIgnoreCase);
			IReadOnlyDictionary<string, AutoPublishCardRecord> readOnlyDictionary;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
				{
					AutoPublishCardRecord autoPublishCardRecord = ReadRecord(reader);
					result[autoPublishCardRecord.CardFolderPath] = autoPublishCardRecord;
				}
				readOnlyDictionary = result;
			}
			result2 = readOnlyDictionary;
		}
		return result2;
	}

	public async Task UpsertStatusAsync(string cardFolderPath, string displayName, AutoPublishStatus status, string lastError = "", CancellationToken cancellationToken = default(CancellationToken))
	{
		string normalizedPath = NormalizePath(cardFolderPath);
		if (string.IsNullOrWhiteSpace(normalizedPath))
		{
			return;
		}
		await InitializeAsync(cancellationToken);
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		string value = DateTimeOffset.Now.ToString("O");
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "INSERT INTO AutoPublishCards\n    (CardFolderPath, DisplayName, Status, LastError, CreatedAt, UpdatedAt, LastRunAt)\nVALUES\n    ($cardFolderPath, $displayName, $status, $lastError, $now, $now, $now)\nON CONFLICT(CardFolderPath) DO UPDATE SET\n    DisplayName = excluded.DisplayName,\n    Status = excluded.Status,\n    LastError = excluded.LastError,\n    UpdatedAt = excluded.UpdatedAt,\n    LastRunAt = excluded.LastRunAt;";
		sqliteCommand.Parameters.AddWithValue("$cardFolderPath", normalizedPath);
		sqliteCommand.Parameters.AddWithValue("$displayName", displayName);
		sqliteCommand.Parameters.AddWithValue("$status", (int)status);
		sqliteCommand.Parameters.AddWithValue("$lastError", lastError);
		sqliteCommand.Parameters.AddWithValue("$now", value);
		await sqliteCommand.ExecuteNonQueryAsync(cancellationToken);
	}

	private SqliteConnection CreateConnection()
	{
		return new SqliteConnection(new SqliteConnectionStringBuilder
		{
			DataSource = _databasePath,
			Mode = SqliteOpenMode.ReadWriteCreate,
			Cache = SqliteCacheMode.Shared
		}.ToString());
	}

	private static AutoPublishCardRecord ReadRecord(SqliteDataReader reader)
	{
		return new AutoPublishCardRecord
		{
			Id = reader.GetInt64(0),
			CardFolderPath = reader.GetString(1),
			DisplayName = reader.GetString(2),
			Status = (AutoPublishStatus)reader.GetInt32(3),
			LastError = reader.GetString(4),
			CreatedAt = DateTimeOffset.Parse(reader.GetString(5)),
			UpdatedAt = DateTimeOffset.Parse(reader.GetString(6)),
			LastRunAt = (reader.IsDBNull(7) ? ((DateTimeOffset?)null) : new DateTimeOffset?(DateTimeOffset.Parse(reader.GetString(7))))
		};
	}

	private static string NormalizePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}
}
