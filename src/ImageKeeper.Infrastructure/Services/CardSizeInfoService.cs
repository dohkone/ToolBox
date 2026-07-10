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

public sealed class CardSizeInfoService : ICardSizeInfoService
{
	private readonly string _databasePath;

	public CardSizeInfoService(string databasePath)
	{
		_databasePath = databasePath;
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_databasePath));
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "CREATE TABLE IF NOT EXISTS CardSizeInfo (\n    CardPath TEXT PRIMARY KEY,\n    SizeText TEXT NOT NULL DEFAULT '',\n    SizeRawInput TEXT NOT NULL DEFAULT '',\n    SizeImageHash TEXT NOT NULL DEFAULT '',\n    SizeImageLastWriteUtc TEXT NOT NULL DEFAULT '',\n    SizeUpdatedAt TEXT NOT NULL\n);";
		await sqliteCommand.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<IReadOnlyDictionary<string, CardSizeInfoRecord>> GetByCardPathsAsync(IEnumerable<string> cardPaths, CancellationToken cancellationToken = default(CancellationToken))
	{
		string[] paths = cardPaths.Where((string path) => !string.IsNullOrWhiteSpace(path)).Select(NormalizePath).Distinct<string>(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (paths.Length == 0)
		{
			return new Dictionary<string, CardSizeInfoRecord>(StringComparer.OrdinalIgnoreCase);
		}
		await InitializeAsync(cancellationToken);
		IReadOnlyDictionary<string, CardSizeInfoRecord> result2;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync(cancellationToken);
			string[] array = paths.Select((string _, int index) => $"$path{index}").ToArray();
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT CardPath, SizeText, SizeRawInput, SizeImageHash, SizeImageLastWriteUtc, SizeUpdatedAt\nFROM CardSizeInfo\nWHERE CardPath IN (" + string.Join(", ", array) + ");";
			for (int num = 0; num < paths.Length; num++)
			{
				sqliteCommand.Parameters.AddWithValue(array[num], paths[num]);
			}
			Dictionary<string, CardSizeInfoRecord> result = new Dictionary<string, CardSizeInfoRecord>(StringComparer.OrdinalIgnoreCase);
			IReadOnlyDictionary<string, CardSizeInfoRecord> readOnlyDictionary;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
				{
					CardSizeInfoRecord cardSizeInfoRecord = ReadRecord(reader);
					result[cardSizeInfoRecord.CardPath] = cardSizeInfoRecord;
				}
				readOnlyDictionary = result;
			}
			result2 = readOnlyDictionary;
		}
		return result2;
	}

	public async Task<CardSizeInfoRecord?> GetByCardPathAsync(string cardPath, CancellationToken cancellationToken = default(CancellationToken))
	{
		CardSizeInfoRecord value;
		return (await GetByCardPathsAsync(new[] { cardPath }, cancellationToken)).TryGetValue(NormalizePath(cardPath), out value) ? value : null;
	}

	public async Task UpsertAsync(CardSizeInfoRecord record, CancellationToken cancellationToken = default(CancellationToken))
	{
		string normalizedPath = NormalizePath(record.CardPath);
		if (string.IsNullOrWhiteSpace(normalizedPath))
		{
			return;
		}
		await InitializeAsync(cancellationToken);
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		string value = ((record.SizeUpdatedAt == default(DateTimeOffset)) ? DateTimeOffset.Now : record.SizeUpdatedAt).ToString("O");
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "INSERT INTO CardSizeInfo\n    (CardPath, SizeText, SizeRawInput, SizeImageHash, SizeImageLastWriteUtc, SizeUpdatedAt)\nVALUES\n    ($cardPath, $sizeText, $sizeRawInput, $sizeImageHash, $sizeImageLastWriteUtc, $sizeUpdatedAt)\nON CONFLICT(CardPath) DO UPDATE SET\n    SizeText = excluded.SizeText,\n    SizeRawInput = excluded.SizeRawInput,\n    SizeImageHash = excluded.SizeImageHash,\n    SizeImageLastWriteUtc = excluded.SizeImageLastWriteUtc,\n    SizeUpdatedAt = excluded.SizeUpdatedAt;";
		sqliteCommand.Parameters.AddWithValue("$cardPath", normalizedPath);
		sqliteCommand.Parameters.AddWithValue("$sizeText", record.SizeText);
		sqliteCommand.Parameters.AddWithValue("$sizeRawInput", record.SizeRawInput);
		sqliteCommand.Parameters.AddWithValue("$sizeImageHash", record.SizeImageHash);
		sqliteCommand.Parameters.AddWithValue("$sizeImageLastWriteUtc", record.SizeImageLastWriteUtc);
		sqliteCommand.Parameters.AddWithValue("$sizeUpdatedAt", value);
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

	private static CardSizeInfoRecord ReadRecord(SqliteDataReader reader)
	{
		return new CardSizeInfoRecord
		{
			CardPath = reader.GetString(0),
			SizeText = reader.GetString(1),
			SizeRawInput = reader.GetString(2),
			SizeImageHash = reader.GetString(3),
			SizeImageLastWriteUtc = reader.GetString(4),
			SizeUpdatedAt = DateTimeOffset.Parse(reader.GetString(5))
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
