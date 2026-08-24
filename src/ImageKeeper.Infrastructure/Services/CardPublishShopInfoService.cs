using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;
using Microsoft.Data.Sqlite;

namespace ImageKeeper.Infrastructure.Services;

public sealed class CardPublishShopInfoService : ICardPublishShopInfoService
{
	private readonly string _databasePath;

	public CardPublishShopInfoService(string databasePath)
	{
		_databasePath = databasePath;
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		SqliteCommand command = connection.CreateCommand();
		command.CommandText = "CREATE TABLE IF NOT EXISTS CardPublishShopInfo (\n    CardPath TEXT PRIMARY KEY,\n    ShopNamesJson TEXT NOT NULL DEFAULT '[]',\n    UpdatedAt TEXT NOT NULL\n);";
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<IReadOnlyDictionary<string, CardPublishShopInfoRecord>> GetByCardPathsAsync(IEnumerable<string> cardPaths, CancellationToken cancellationToken = default(CancellationToken))
	{
		string[] paths = cardPaths
			.Where(path => !string.IsNullOrWhiteSpace(path))
			.Select(NormalizePath)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (paths.Length == 0)
		{
			return new Dictionary<string, CardPublishShopInfoRecord>(StringComparer.OrdinalIgnoreCase);
		}

		await InitializeAsync(cancellationToken);
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		string[] parameters = paths.Select((_, index) => $"$path{index}").ToArray();
		SqliteCommand command = connection.CreateCommand();
		command.CommandText = "SELECT CardPath, ShopNamesJson, UpdatedAt\nFROM CardPublishShopInfo\nWHERE CardPath IN (" + string.Join(", ", parameters) + ");";
		for (int index = 0; index < paths.Length; index++)
		{
			command.Parameters.AddWithValue(parameters[index], paths[index]);
		}

		Dictionary<string, CardPublishShopInfoRecord> result = new Dictionary<string, CardPublishShopInfoRecord>(StringComparer.OrdinalIgnoreCase);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			CardPublishShopInfoRecord record = new CardPublishShopInfoRecord
			{
				CardPath = reader.GetString(0),
				ShopNamesJson = reader.GetString(1),
				UpdatedAt = DateTimeOffset.Parse(reader.GetString(2))
			};
			result[record.CardPath] = record;
		}
		return result;
	}

	public async Task<CardPublishShopInfoRecord?> GetByCardPathAsync(string cardPath, CancellationToken cancellationToken = default(CancellationToken))
	{
		return (await GetByCardPathsAsync(new[] { cardPath }, cancellationToken))
			.TryGetValue(NormalizePath(cardPath), out CardPublishShopInfoRecord? record)
			? record
			: null;
	}

	public async Task UpsertAsync(CardPublishShopInfoRecord record, CancellationToken cancellationToken = default(CancellationToken))
	{
		string cardPath = NormalizePath(record.CardPath);
		if (string.IsNullOrWhiteSpace(cardPath))
		{
			return;
		}

		await InitializeAsync(cancellationToken);
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		SqliteCommand command = connection.CreateCommand();
		command.CommandText = "INSERT INTO CardPublishShopInfo\n    (CardPath, ShopNamesJson, UpdatedAt)\nVALUES\n    ($cardPath, $shopNamesJson, $updatedAt)\nON CONFLICT(CardPath) DO UPDATE SET\n    ShopNamesJson = excluded.ShopNamesJson,\n    UpdatedAt = excluded.UpdatedAt;";
		command.Parameters.AddWithValue("$cardPath", cardPath);
		command.Parameters.AddWithValue("$shopNamesJson", string.IsNullOrWhiteSpace(record.ShopNamesJson) ? "[]" : record.ShopNamesJson);
		command.Parameters.AddWithValue("$updatedAt", (record.UpdatedAt == default ? DateTimeOffset.Now : record.UpdatedAt).ToString("O"));
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task RenameShopAsync(string oldShopName, string newShopName, CancellationToken cancellationToken = default(CancellationToken))
	{
		string oldName = oldShopName.Trim();
		string newName = newShopName.Trim();
		if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)
			|| string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		await InitializeAsync(cancellationToken);
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		List<(string CardPath, string ShopNamesJson)> records = new List<(string CardPath, string ShopNamesJson)>();
		SqliteCommand selectCommand = connection.CreateCommand();
		selectCommand.CommandText = "SELECT CardPath, ShopNamesJson FROM CardPublishShopInfo;";
		await using (SqliteDataReader reader = await selectCommand.ExecuteReaderAsync(cancellationToken))
		{
			while (await reader.ReadAsync(cancellationToken))
			{
				records.Add((reader.GetString(0), reader.GetString(1)));
			}
		}

		await using SqliteTransaction transaction = connection.BeginTransaction();
		foreach ((string cardPath, string shopNamesJson) in records)
		{
			string[] names;
			try
			{
				names = JsonSerializer.Deserialize<string[]>(shopNamesJson) ?? Array.Empty<string>();
			}
			catch (JsonException)
			{
				continue;
			}

			bool changed = false;
			string[] renamedNames = names
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Select(name =>
				{
					string trimmedName = name.Trim();
					if (string.Equals(trimmedName, oldName, StringComparison.OrdinalIgnoreCase))
					{
						changed = true;
						return newName;
					}
					return trimmedName;
				})
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
			if (!changed)
			{
				continue;
			}

			SqliteCommand updateCommand = connection.CreateCommand();
			updateCommand.Transaction = transaction;
			updateCommand.CommandText = "UPDATE CardPublishShopInfo\nSET ShopNamesJson = $shopNamesJson,\n    UpdatedAt = $updatedAt\nWHERE CardPath = $cardPath;";
			updateCommand.Parameters.AddWithValue("$shopNamesJson", JsonSerializer.Serialize(renamedNames));
			updateCommand.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
			updateCommand.Parameters.AddWithValue("$cardPath", cardPath);
			await updateCommand.ExecuteNonQueryAsync(cancellationToken);
		}
		await transaction.CommitAsync(cancellationToken);
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

	private static string NormalizePath(string path)
	{
		return string.IsNullOrWhiteSpace(path)
			? string.Empty
			: Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}
}
