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

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS AutoPublishCards (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CardFolderPath TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL DEFAULT '',
                Status INTEGER NOT NULL DEFAULT 0,
                LastError TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                LastRunAt TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_AutoPublishCards_Status
            ON AutoPublishCards(Status);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, AutoPublishCardRecord>> GetByCardPathsAsync(
        IEnumerable<string> cardFolderPaths,
        CancellationToken cancellationToken = default)
    {
        var paths = cardFolderPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (paths.Length == 0)
        {
            return new Dictionary<string, AutoPublishCardRecord>(StringComparer.OrdinalIgnoreCase);
        }

        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var parameters = paths.Select((_, index) => $"$path{index}").ToArray();
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT Id, CardFolderPath, DisplayName, Status, LastError, CreatedAt, UpdatedAt, LastRunAt
            FROM AutoPublishCards
            WHERE CardFolderPath IN ({string.Join(", ", parameters)});
            """;

        for (var index = 0; index < paths.Length; index++)
        {
            command.Parameters.AddWithValue(parameters[index], paths[index]);
        }

        var result = new Dictionary<string, AutoPublishCardRecord>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var record = ReadRecord(reader);
            result[record.CardFolderPath] = record;
        }

        return result;
    }

    public async Task UpsertStatusAsync(
        string cardFolderPath,
        string displayName,
        AutoPublishStatus status,
        string lastError = "",
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(cardFolderPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var now = DateTimeOffset.Now.ToString("O");
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AutoPublishCards
                (CardFolderPath, DisplayName, Status, LastError, CreatedAt, UpdatedAt, LastRunAt)
            VALUES
                ($cardFolderPath, $displayName, $status, $lastError, $now, $now, $now)
            ON CONFLICT(CardFolderPath) DO UPDATE SET
                DisplayName = excluded.DisplayName,
                Status = excluded.Status,
                LastError = excluded.LastError,
                UpdatedAt = excluded.UpdatedAt,
                LastRunAt = excluded.LastRunAt;
            """;
        command.Parameters.AddWithValue("$cardFolderPath", normalizedPath);
        command.Parameters.AddWithValue("$displayName", displayName);
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$lastError", lastError);
        command.Parameters.AddWithValue("$now", now);

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
            LastRunAt = reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7))
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
