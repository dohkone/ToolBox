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

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS CardSizeInfo (
                CardPath TEXT PRIMARY KEY,
                SizeText TEXT NOT NULL DEFAULT '',
                SizeRawInput TEXT NOT NULL DEFAULT '',
                SizeImageHash TEXT NOT NULL DEFAULT '',
                SizeImageLastWriteUtc TEXT NOT NULL DEFAULT '',
                SizeUpdatedAt TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await TryAddColumnAsync(connection, "ALTER TABLE CardSizeInfo ADD COLUMN SizeText TEXT NOT NULL DEFAULT '';", cancellationToken);
        await TryAddColumnAsync(connection, "ALTER TABLE CardSizeInfo ADD COLUMN SizeRawInput TEXT NOT NULL DEFAULT '';", cancellationToken);
        await TryAddColumnAsync(connection, "ALTER TABLE CardSizeInfo ADD COLUMN SizeImageHash TEXT NOT NULL DEFAULT '';", cancellationToken);
        await TryAddColumnAsync(connection, "ALTER TABLE CardSizeInfo ADD COLUMN SizeImageLastWriteUtc TEXT NOT NULL DEFAULT '';", cancellationToken);
        await TryAddColumnAsync(connection, "ALTER TABLE CardSizeInfo ADD COLUMN SizeUpdatedAt TEXT NOT NULL DEFAULT '';", cancellationToken);
        await BackfillLegacyRowsAsync(connection, cancellationToken);
        await EnsureUniqueIndexAsync(connection, "CardSizeInfo", "CardPath", "IX_CardSizeInfo_CardPath", cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, CardSizeInfoRecord>> GetByCardPathsAsync(
        IEnumerable<string> cardPaths,
        CancellationToken cancellationToken = default)
    {
        var paths = cardPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (paths.Length == 0)
        {
            return new Dictionary<string, CardSizeInfoRecord>(StringComparer.OrdinalIgnoreCase);
        }

        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var parameters = paths.Select((_, index) => $"$path{index}").ToArray();
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT CardPath, SizeText, SizeRawInput, SizeImageHash, SizeImageLastWriteUtc, SizeUpdatedAt
            FROM CardSizeInfo
            WHERE CardPath IN ({string.Join(", ", parameters)});
            """;

        for (var index = 0; index < paths.Length; index++)
        {
            command.Parameters.AddWithValue(parameters[index], paths[index]);
        }

        var result = new Dictionary<string, CardSizeInfoRecord>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var record = ReadRecord(reader);
            result[record.CardPath] = record;
        }

        return result;
    }

    public async Task<CardSizeInfoRecord?> GetByCardPathAsync(
        string cardPath,
        CancellationToken cancellationToken = default)
    {
        var records = await GetByCardPathsAsync([cardPath], cancellationToken);
        return records.TryGetValue(NormalizePath(cardPath), out var record) ? record : null;
    }

    public async Task UpsertAsync(
        CardSizeInfoRecord record,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(record.CardPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var updatedAt = (record.SizeUpdatedAt == default ? DateTimeOffset.Now : record.SizeUpdatedAt).ToString("O");
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CardSizeInfo
                (CardPath, SizeText, SizeRawInput, SizeImageHash, SizeImageLastWriteUtc, SizeUpdatedAt)
            VALUES
                ($cardPath, $sizeText, $sizeRawInput, $sizeImageHash, $sizeImageLastWriteUtc, $sizeUpdatedAt)
            ON CONFLICT(CardPath) DO UPDATE SET
                SizeText = excluded.SizeText,
                SizeRawInput = excluded.SizeRawInput,
                SizeImageHash = excluded.SizeImageHash,
                SizeImageLastWriteUtc = excluded.SizeImageLastWriteUtc,
                SizeUpdatedAt = excluded.SizeUpdatedAt;
            """;
        command.Parameters.AddWithValue("$cardPath", normalizedPath);
        command.Parameters.AddWithValue("$sizeText", record.SizeText);
        command.Parameters.AddWithValue("$sizeRawInput", record.SizeRawInput);
        command.Parameters.AddWithValue("$sizeImageHash", record.SizeImageHash);
        command.Parameters.AddWithValue("$sizeImageLastWriteUtc", record.SizeImageLastWriteUtc);
        command.Parameters.AddWithValue("$sizeUpdatedAt", updatedAt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        return new SqliteConnection(builder.ToString());
    }

    private static async Task TryAddColumnAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static async Task EnsureUniqueIndexAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string indexName,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = connection.CreateCommand();
            command.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS {indexName} ON {tableName}({columnName});";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            var dedupe = connection.CreateCommand();
            dedupe.CommandText = $"""
                DELETE FROM {tableName}
                WHERE rowid NOT IN (
                    SELECT MIN(rowid)
                    FROM {tableName}
                    GROUP BY {columnName}
                );
                """;
            await dedupe.ExecuteNonQueryAsync(cancellationToken);

            var retry = connection.CreateCommand();
            retry.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS {indexName} ON {tableName}({columnName});";
            await retry.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task BackfillLegacyRowsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await NormalizeLegacyPathsAsync(connection, cancellationToken);

        var now = DateTimeOffset.Now.ToString("O");
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE CardSizeInfo
            SET SizeText = COALESCE(SizeText, ''),
                SizeRawInput = COALESCE(SizeRawInput, ''),
                SizeImageHash = COALESCE(SizeImageHash, ''),
                SizeImageLastWriteUtc = COALESCE(SizeImageLastWriteUtc, ''),
                SizeUpdatedAt = CASE
                    WHEN SizeUpdatedAt IS NULL OR trim(SizeUpdatedAt) = '' THEN $now
                    ELSE SizeUpdatedAt
                END
            WHERE SizeText IS NULL
               OR SizeRawInput IS NULL
               OR SizeImageHash IS NULL
               OR SizeImageLastWriteUtc IS NULL
               OR SizeUpdatedAt IS NULL
               OR trim(SizeUpdatedAt) = '';
            """;
        command.Parameters.AddWithValue("$now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task NormalizeLegacyPathsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var select = connection.CreateCommand();
        select.CommandText = "SELECT rowid, CardPath FROM CardSizeInfo;";

        var updates = new List<(long RowId, string NormalizedPath)>();
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(1))
                {
                    continue;
                }

                var originalPath = reader.GetString(1);
                var normalizedPath = NormalizePath(originalPath);
                if (!string.Equals(originalPath, normalizedPath, StringComparison.Ordinal))
                {
                    updates.Add((reader.GetInt64(0), normalizedPath));
                }
            }
        }

        foreach (var update in updates)
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE CardSizeInfo
                SET CardPath = $cardPath
                WHERE rowid = $rowId;
                """;
            command.Parameters.AddWithValue("$cardPath", update.NormalizedPath);
            command.Parameters.AddWithValue("$rowId", update.RowId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
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
