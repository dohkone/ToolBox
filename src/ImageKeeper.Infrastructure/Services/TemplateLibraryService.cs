using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;
using Microsoft.Data.Sqlite;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ImageKeeper.Infrastructure.Services;

public sealed class TemplateLibraryService : ITemplateLibraryService
{
    private const string LayoutPackageType = "ecomtool-layout-templates";
    private const string AllTemplatesPackageType = "ecomtool-all-templates";
    private const int LayoutPackageVersion = 2;
    private const int AllTemplatesPackageVersion = 1;
    private readonly string _databasePath;
    private readonly string _assetRoot;

    public TemplateLibraryService(string databasePath, string assetRoot)
    {
        _databasePath = databasePath;
        _assetRoot = assetRoot;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        Directory.CreateDirectory(GetTemplateAssetDirectory(TemplateCategory.Layout));

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS TemplateItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Category INTEGER NOT NULL,
                Name TEXT NOT NULL DEFAULT '',
                Content TEXT NOT NULL DEFAULT '',
                Subject TEXT NOT NULL DEFAULT '',
                PreviewImagePath TEXT NOT NULL DEFAULT '',
                ImageType INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_TemplateItems_Category_Id
            ON TemplateItems(Category, Id);

            CREATE TABLE IF NOT EXISTS SceneTemplateSubjectBindings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SceneTemplateId INTEGER NOT NULL,
                SubjectTemplateId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UNIQUE(SceneTemplateId, SubjectTemplateId)
            );

            CREATE INDEX IF NOT EXISTS IX_SceneTemplateSubjectBindings_SceneTemplateId
            ON SceneTemplateSubjectBindings(SceneTemplateId);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await TryAddColumnAsync(connection, "ALTER TABLE TemplateItems ADD COLUMN Subject TEXT NOT NULL DEFAULT '';", cancellationToken);
        await TryAddColumnAsync(connection, "ALTER TABLE TemplateItems ADD COLUMN ImageType INTEGER NOT NULL DEFAULT 0;", cancellationToken);
        await MigrateLegacySubjectsAsync(connection, cancellationToken);
        await MigrateLegacySceneSubjectBindingsAsync(connection, cancellationToken);
    }

    public async Task<IReadOnlyList<TemplateItemRecord>> GetByCategoryAsync(
        TemplateCategory category,
        ImageTemplateType? imageType = null,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = imageType.HasValue
            ? """
                SELECT Id, Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt
                FROM TemplateItems
                WHERE Category = $category AND ImageType = $imageType
                ORDER BY Id ASC;
                """
            : """
                SELECT Id, Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt
                FROM TemplateItems
                WHERE Category = $category
                ORDER BY Id ASC;
                """;
        command.Parameters.AddWithValue("$category", (int)category);
        if (imageType.HasValue)
        {
            command.Parameters.AddWithValue("$imageType", (int)imageType.Value);
        }

        var items = new List<TemplateItemRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadRecord(reader));
        }

        return items;
    }

    public async Task<TemplateItemRecord> SaveAsync(
        TemplateItemRecord item,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var now = DateTimeOffset.Now.ToString("O");
        if (item.Id <= 0)
        {
            var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO TemplateItems
                    (Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt)
                VALUES
                    ($category, $name, $content, $subject, $previewImagePath, $imageType, $sortOrder, $isEnabled, $now, $now)
                RETURNING Id, Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt;
                """;
            BindParameters(insert, item, now);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return ReadRecord(reader);
            }
        }
        else
        {
            var update = connection.CreateCommand();
            update.CommandText = """
                UPDATE TemplateItems
                SET Category = $category,
                    Name = $name,
                    Content = $content,
                    Subject = $subject,
                    PreviewImagePath = $previewImagePath,
                    ImageType = $imageType,
                    SortOrder = $sortOrder,
                    IsEnabled = $isEnabled,
                    UpdatedAt = $now
                WHERE Id = $id
                RETURNING Id, Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt;
                """;
            update.Parameters.AddWithValue("$id", item.Id);
            BindParameters(update, item, now);
            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return ReadRecord(reader);
            }
        }

        throw new InvalidOperationException("模板保存失败。");
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var deleteBindings = connection.CreateCommand();
        deleteBindings.CommandText = """
            DELETE FROM SceneTemplateSubjectBindings
            WHERE SceneTemplateId = $id OR SubjectTemplateId = $id;
            """;
        deleteBindings.Parameters.AddWithValue("$id", id);
        await deleteBindings.ExecuteNonQueryAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM TemplateItems WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, IReadOnlyList<long>>> GetSceneSubjectBindingsAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT SceneTemplateId, SubjectTemplateId
            FROM SceneTemplateSubjectBindings
            ORDER BY SceneTemplateId ASC, Id ASC;
            """;

        var result = new Dictionary<long, List<long>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var sceneTemplateId = reader.GetInt64(0);
            var subjectTemplateId = reader.GetInt64(1);
            if (!result.TryGetValue(sceneTemplateId, out var list))
            {
                list = [];
                result[sceneTemplateId] = list;
            }

            list.Add(subjectTemplateId);
        }

        return result.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<long>)pair.Value);
    }

    public async Task SetSceneSubjectBindingsAsync(
        long sceneTemplateId,
        IReadOnlyList<long> subjectTemplateIds,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = "DELETE FROM SceneTemplateSubjectBindings WHERE SceneTemplateId = $sceneTemplateId;";
        deleteCommand.Parameters.AddWithValue("$sceneTemplateId", sceneTemplateId);
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

        var now = DateTimeOffset.Now.ToString("O");
        foreach (var subjectTemplateId in subjectTemplateIds
                     .Where(static id => id > 0)
                     .Distinct())
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO SceneTemplateSubjectBindings
                    (SceneTemplateId, SubjectTemplateId, CreatedAt)
                VALUES
                    ($sceneTemplateId, $subjectTemplateId, $createdAt);
                """;
            insertCommand.Parameters.AddWithValue("$sceneTemplateId", sceneTemplateId);
            insertCommand.Parameters.AddWithValue("$subjectTemplateId", subjectTemplateId);
            insertCommand.Parameters.AddWithValue("$createdAt", now);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public string GetTemplateAssetDirectory(TemplateCategory category)
    {
        var folderName = category switch
        {
            TemplateCategory.Layout => "layout-previews",
            TemplateCategory.Scene => "scene",
            TemplateCategory.Subject => "subject",
            _ => "misc"
        };

        return Path.Combine(_assetRoot, folderName);
    }

    public Task<string> ImportPreviewImageAsync(
        string sourceImagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
        {
            throw new FileNotFoundException("预览图不存在。", sourceImagePath);
        }

        var targetDirectory = GetTemplateAssetDirectory(TemplateCategory.Layout);
        Directory.CreateDirectory(targetDirectory);

        var extension = Path.GetExtension(sourceImagePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        var targetPath = Path.Combine(targetDirectory, $"layout_{DateTimeOffset.Now:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}{extension}");
        File.Copy(sourceImagePath, targetPath, overwrite: true);
        return Task.FromResult(targetPath);
    }

    public async Task<int> ExportLayoutTemplatesAsync(
        string packagePath,
        ImageTemplateType imageType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new ArgumentException("导出文件路径不能为空。", nameof(packagePath));
        }

        var items = await GetByCategoryAsync(TemplateCategory.Layout, imageType, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);

        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        var manifest = new LayoutTemplatePackageManifest
        {
            ExportedAt = DateTimeOffset.Now,
            ImageType = imageType,
            Items = []
        };

        await using var fileStream = new FileStream(packagePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        var previewIndex = 1;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var previewFile = string.Empty;
            if (!string.IsNullOrWhiteSpace(item.PreviewImagePath) && File.Exists(item.PreviewImagePath))
            {
                var extension = Path.GetExtension(item.PreviewImagePath);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".png";
                }

                previewFile = $"previews/layout_{previewIndex++:000}{extension}";
                archive.CreateEntryFromFile(item.PreviewImagePath, previewFile, CompressionLevel.Optimal);
            }

            manifest.Items.Add(new LayoutTemplatePackageItem
            {
                Name = item.Name,
                Content = item.Content,
                IsEnabled = item.IsEnabled,
                PreviewFile = previewFile,
                ImageType = item.ImageType
            });
        }

        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(
            manifestStream,
            manifest,
            new JsonSerializerOptions { WriteIndented = true },
            cancellationToken);

        return manifest.Items.Count;
    }

    public async Task<int> ImportLayoutTemplatesAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            throw new FileNotFoundException("导入文件不存在。", packagePath);
        }

        await InitializeAsync(cancellationToken);
        using var archive = ZipFile.OpenRead(packagePath);
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException("导入文件缺少 manifest.json。");

        LayoutTemplatePackageManifest? manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<LayoutTemplatePackageManifest>(
                manifestStream,
                cancellationToken: cancellationToken);
        }

        if (manifest is null
            || !string.Equals(manifest.Type, LayoutPackageType, StringComparison.Ordinal)
            || manifest.Version < 1)
        {
            throw new InvalidOperationException("导入文件不是有效的布局模板包。");
        }

        var existingItems = await GetByCategoryAsync(TemplateCategory.Layout, cancellationToken: cancellationToken);
        var existingNames = new HashSet<string>(existingItems.Select(item => item.Name), StringComparer.OrdinalIgnoreCase);
        var importedCount = 0;

        foreach (var packageItem in manifest.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(packageItem.Name) || string.IsNullOrWhiteSpace(packageItem.Content))
            {
                continue;
            }

            var previewImagePath = ImportPreviewFromPackage(archive, packageItem.PreviewFile);
            var uniqueName = CreateImportedTemplateName(packageItem.Name, existingNames);
            existingNames.Add(uniqueName);

            await SaveAsync(new TemplateItemRecord
            {
                Category = TemplateCategory.Layout,
                Name = uniqueName,
                Content = packageItem.Content,
                Subject = string.Empty,
                PreviewImagePath = previewImagePath,
                ImageType = packageItem.ImageType,
                SortOrder = 0,
                IsEnabled = packageItem.IsEnabled
            }, cancellationToken);

            importedCount++;
        }

        return importedCount;
    }

    public async Task<int> ExportAllTemplatesAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new ArgumentException("Export package path cannot be empty.", nameof(packagePath));
        }

        await InitializeAsync(cancellationToken);
        var items = new List<TemplateItemRecord>();
        items.AddRange(await GetByCategoryAsync(TemplateCategory.Layout, cancellationToken: cancellationToken));
        items.AddRange(await GetByCategoryAsync(TemplateCategory.Scene, cancellationToken: cancellationToken));
        items.AddRange(await GetByCategoryAsync(TemplateCategory.Subject, cancellationToken: cancellationToken));
        var bindings = await GetSceneSubjectBindingsAsync(cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        var manifest = new AllTemplatesPackageManifest
        {
            ExportedAt = DateTimeOffset.Now,
            Items = [],
            SceneSubjectBindings = bindings
                .SelectMany(static pair => pair.Value.Select(subjectId => new AllTemplatesPackageBinding
                {
                    SceneTemplateId = pair.Key,
                    SubjectTemplateId = subjectId
                }))
                .ToList()
        };

        await using var fileStream = new FileStream(packagePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        var previewIndex = 1;
        foreach (var item in items.OrderBy(static item => item.Category).ThenBy(static item => item.ImageType).ThenBy(static item => item.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var previewFile = string.Empty;
            if (item.Category == TemplateCategory.Layout
                && !string.IsNullOrWhiteSpace(item.PreviewImagePath)
                && File.Exists(item.PreviewImagePath))
            {
                var extension = Path.GetExtension(item.PreviewImagePath);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".png";
                }

                previewFile = $"assets/layout-previews/layout_{previewIndex++:000}{extension}";
                archive.CreateEntryFromFile(item.PreviewImagePath, previewFile, CompressionLevel.Optimal);
            }

            manifest.Items.Add(new AllTemplatesPackageItem
            {
                Id = item.Id,
                Category = item.Category,
                Name = item.Name,
                Content = item.Content,
                Subject = item.Subject,
                PreviewFile = previewFile,
                ImageType = item.ImageType,
                SortOrder = item.SortOrder,
                IsEnabled = item.IsEnabled
            });
        }

        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(
            manifestStream,
            manifest,
            new JsonSerializerOptions { WriteIndented = true },
            cancellationToken);

        return manifest.Items.Count;
    }

    public async Task<int> ImportAllTemplatesAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            throw new FileNotFoundException("Import package does not exist.", packagePath);
        }

        await InitializeAsync(cancellationToken);
        using var archive = ZipFile.OpenRead(packagePath);
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException("Import package is missing manifest.json.");

        AllTemplatesPackageManifest? manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<AllTemplatesPackageManifest>(
                manifestStream,
                cancellationToken: cancellationToken);
        }

        if (manifest is null
            || !string.Equals(manifest.Type, AllTemplatesPackageType, StringComparison.Ordinal)
            || manifest.Version < 1)
        {
            throw new InvalidOperationException("Import package is not a valid all-templates package.");
        }

        var existingItems = new List<TemplateItemRecord>();
        existingItems.AddRange(await GetByCategoryAsync(TemplateCategory.Layout, cancellationToken: cancellationToken));
        existingItems.AddRange(await GetByCategoryAsync(TemplateCategory.Scene, cancellationToken: cancellationToken));
        existingItems.AddRange(await GetByCategoryAsync(TemplateCategory.Subject, cancellationToken: cancellationToken));
        var existingNames = existingItems
            .GroupBy(static item => item.Category)
            .ToDictionary(
                static group => group.Key,
                static group => new HashSet<string>(group.Select(static item => item.Name), StringComparer.OrdinalIgnoreCase));

        var idMap = new Dictionary<long, long>();
        var importedCount = 0;

        foreach (var packageItem in manifest.Items.OrderBy(static item => item.Category).ThenBy(static item => item.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(packageItem.Name) || string.IsNullOrWhiteSpace(packageItem.Content))
            {
                continue;
            }

            if (!existingNames.TryGetValue(packageItem.Category, out var categoryNames))
            {
                categoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                existingNames[packageItem.Category] = categoryNames;
            }

            var uniqueName = CreateImportedTemplateName(packageItem.Name, categoryNames);
            categoryNames.Add(uniqueName);

            var previewImagePath = packageItem.Category == TemplateCategory.Layout
                ? ImportPreviewFromPackage(archive, packageItem.PreviewFile)
                : string.Empty;

            var saved = await SaveAsync(new TemplateItemRecord
            {
                Category = packageItem.Category,
                Name = uniqueName,
                Content = packageItem.Content,
                Subject = packageItem.Subject,
                PreviewImagePath = previewImagePath,
                ImageType = packageItem.ImageType,
                SortOrder = packageItem.SortOrder,
                IsEnabled = packageItem.IsEnabled
            }, cancellationToken);

            idMap[packageItem.Id] = saved.Id;
            importedCount++;
        }

        var importedBindings = manifest.SceneSubjectBindings
            .Select(binding =>
                idMap.TryGetValue(binding.SceneTemplateId, out var newSceneId)
                && idMap.TryGetValue(binding.SubjectTemplateId, out var newSubjectId)
                    ? new { SceneId = newSceneId, SubjectId = newSubjectId }
                    : null)
            .Where(static item => item is not null)
            .GroupBy(static item => item!.SceneId);

        var currentBindings = await GetSceneSubjectBindingsAsync(cancellationToken);
        foreach (var group in importedBindings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subjectIds = currentBindings.TryGetValue(group.Key, out var ids)
                ? ids.Concat(group.Select(static item => item!.SubjectId)).Distinct().ToArray()
                : group.Select(static item => item!.SubjectId).Distinct().ToArray();
            await SetSceneSubjectBindingsAsync(group.Key, subjectIds, cancellationToken);
        }

        return importedCount;
    }

    public async Task<string> ExportGenerationLibraryAsync(
        ImageTemplateType imageType,
        string outputPath,
        IReadOnlyList<long>? selectedLayoutTemplateIds = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("输出路径不能为空。", nameof(outputPath));
        }

        var layouts = await GetByCategoryAsync(TemplateCategory.Layout, imageType, cancellationToken);
        var scenes = await GetByCategoryAsync(TemplateCategory.Scene, cancellationToken: cancellationToken);
        var subjects = await GetByCategoryAsync(TemplateCategory.Subject, cancellationToken: cancellationToken);
        var sceneSubjectBindings = await GetSceneSubjectBindingsAsync(cancellationToken);
        var enabledSubjectsById = subjects
            .Where(static item => item.IsEnabled)
            .ToDictionary(static item => item.Id);
        var selectedLayoutIdSet = selectedLayoutTemplateIds?
            .Where(static id => id > 0)
            .Distinct()
            .ToHashSet();
        var filteredLayouts = layouts
            .Where(static item => item.IsEnabled)
            .Where(item => selectedLayoutIdSet is null || selectedLayoutIdSet.Count == 0 || selectedLayoutIdSet.Contains(item.Id))
            .ToArray();

        var payload = new GenerationLibraryPayload
        {
            ImageType = (int)imageType,
            LayoutTemplates = filteredLayouts
                .Select(static item => item.Content.Trim())
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SceneTemplates = scenes
                .Where(static item => item.IsEnabled)
                .Select(item => CreateGenerationSceneTemplate(item, sceneSubjectBindings, enabledSubjectsById))
                .Where(static item => !string.IsNullOrWhiteSpace(item.Content))
                .Where(item => imageType != ImageTemplateType.MainImage || item.Subjects.Length > 0)
                .ToArray(),
            SubjectTemplates = subjects
                .Where(static item => item.IsEnabled)
                .Select(static item => item.Content.Trim())
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        if (payload.LayoutTemplates.Length == 0)
        {
            throw new InvalidOperationException("当前图片类型下没有可用的布局模板。");
        }

        if (payload.SceneTemplates.Length == 0)
        {
            throw new InvalidOperationException("当前没有可用的场景模板。");
        }

        if (payload.SubjectTemplates.Length == 0)
        {
            throw new InvalidOperationException("当前没有可用的主体模板。");
        }

        var targetDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, payload, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        return outputPath;
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
        var command = connection.CreateCommand();
        command.CommandText = sql;
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static void BindParameters(SqliteCommand command, TemplateItemRecord item, string now)
    {
        command.Parameters.AddWithValue("$category", (int)item.Category);
        command.Parameters.AddWithValue("$name", item.Name.Trim());
        command.Parameters.AddWithValue("$content", item.Content.Trim());
        command.Parameters.AddWithValue("$subject", item.Subject.Trim());
        command.Parameters.AddWithValue("$previewImagePath", item.PreviewImagePath.Trim());
        command.Parameters.AddWithValue("$imageType", (int)item.ImageType);
        command.Parameters.AddWithValue("$sortOrder", item.SortOrder);
        command.Parameters.AddWithValue("$isEnabled", item.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$now", now);
    }

    private static TemplateItemRecord ReadRecord(SqliteDataReader reader)
    {
        return new TemplateItemRecord
        {
            Id = reader.GetInt64(0),
            Category = (TemplateCategory)reader.GetInt32(1),
            Name = reader.GetString(2),
            Content = reader.GetString(3),
            Subject = reader.GetString(4),
            PreviewImagePath = reader.GetString(5),
            ImageType = (ImageTemplateType)reader.GetInt32(6),
            SortOrder = reader.GetInt32(7),
            IsEnabled = reader.GetInt32(8) != 0,
            CreatedAt = DateTimeOffset.Parse(reader.GetString(9)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(10))
        };
    }

    private string ImportPreviewFromPackage(ZipArchive archive, string previewFile)
    {
        if (string.IsNullOrWhiteSpace(previewFile))
        {
            return string.Empty;
        }

        var entry = archive.GetEntry(previewFile.Replace('\\', '/'));
        if (entry is null)
        {
            return string.Empty;
        }

        var targetDirectory = GetTemplateAssetDirectory(TemplateCategory.Layout);
        Directory.CreateDirectory(targetDirectory);

        var extension = Path.GetExtension(entry.Name);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        var targetPath = Path.Combine(targetDirectory, $"layout_import_{DateTimeOffset.Now:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}{extension}");
        entry.ExtractToFile(targetPath, overwrite: true);
        return targetPath;
    }

    private static string CreateImportedTemplateName(string name, HashSet<string> existingNames)
    {
        var trimmedName = name.Trim();
        if (!existingNames.Contains(trimmedName))
        {
            return trimmedName;
        }

        var index = 1;
        string candidate;
        do
        {
            candidate = $"{trimmedName} - 导入{index}";
            index++;
        }
        while (existingNames.Contains(candidate));

        return candidate;
    }

    private static async Task MigrateLegacySubjectsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(1) FROM TemplateItems WHERE Category = $category;";
        countCommand.Parameters.AddWithValue("$category", (int)TemplateCategory.Subject);
        var subjectCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        if (subjectCount > 0)
        {
            return;
        }

        var sceneCommand = connection.CreateCommand();
        sceneCommand.CommandText = """
            SELECT Subject
            FROM TemplateItems
            WHERE Category = $category AND TRIM(Subject) <> '';
            """;
        sceneCommand.Parameters.AddWithValue("$category", (int)TemplateCategory.Scene);

        var subjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await sceneCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                foreach (var subject in SplitLegacyValues(reader.GetString(0)))
                {
                    subjects.Add(subject);
                }
            }
        }

        if (subjects.Count == 0)
        {
            return;
        }

        foreach (var subject in subjects.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase))
        {
            var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO TemplateItems
                    (Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt)
                VALUES
                    ($category, $name, $content, '', '', 0, 0, 1, $now, $now);
                """;
            var now = DateTimeOffset.Now.ToString("O");
            insert.Parameters.AddWithValue("$category", (int)TemplateCategory.Subject);
            insert.Parameters.AddWithValue("$name", subject);
            insert.Parameters.AddWithValue("$content", subject);
            insert.Parameters.AddWithValue("$now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task MigrateLegacySceneSubjectBindingsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var bindingCountCommand = connection.CreateCommand();
        bindingCountCommand.CommandText = "SELECT COUNT(1) FROM SceneTemplateSubjectBindings;";
        var bindingCount = Convert.ToInt32(await bindingCountCommand.ExecuteScalarAsync(cancellationToken));
        if (bindingCount > 0)
        {
            return;
        }

        var subjectLookupCommand = connection.CreateCommand();
        subjectLookupCommand.CommandText = """
            SELECT Id, Name, Content
            FROM TemplateItems
            WHERE Category = $category;
            """;
        subjectLookupCommand.Parameters.AddWithValue("$category", (int)TemplateCategory.Subject);

        var subjectLookup = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await using (var subjectReader = await subjectLookupCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await subjectReader.ReadAsync(cancellationToken))
            {
                var id = subjectReader.GetInt64(0);
                var name = subjectReader.GetString(1).Trim();
                var content = subjectReader.GetString(2).Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    subjectLookup.TryAdd(name, id);
                }

                if (!string.IsNullOrWhiteSpace(content))
                {
                    subjectLookup.TryAdd(content, id);
                }
            }
        }

        if (subjectLookup.Count == 0)
        {
            return;
        }

        var sceneCommand = connection.CreateCommand();
        sceneCommand.CommandText = """
            SELECT Id, Subject
            FROM TemplateItems
            WHERE Category = $category AND TRIM(Subject) <> '';
            """;
        sceneCommand.Parameters.AddWithValue("$category", (int)TemplateCategory.Scene);

        await using var sceneReader = await sceneCommand.ExecuteReaderAsync(cancellationToken);
        while (await sceneReader.ReadAsync(cancellationToken))
        {
            var sceneTemplateId = sceneReader.GetInt64(0);
            var subjectText = sceneReader.GetString(1);
            foreach (var subject in SplitLegacyValues(subjectText))
            {
                if (!subjectLookup.TryGetValue(subject, out var subjectTemplateId))
                {
                    continue;
                }

                var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT OR IGNORE INTO SceneTemplateSubjectBindings
                        (SceneTemplateId, SubjectTemplateId, CreatedAt)
                    VALUES
                        ($sceneTemplateId, $subjectTemplateId, $createdAt);
                    """;
                insert.Parameters.AddWithValue("$sceneTemplateId", sceneTemplateId);
                insert.Parameters.AddWithValue("$subjectTemplateId", subjectTemplateId);
                insert.Parameters.AddWithValue("$createdAt", DateTimeOffset.Now.ToString("O"));
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    private static GenerationSceneTemplatePayload CreateGenerationSceneTemplate(
        TemplateItemRecord sceneRecord,
        IReadOnlyDictionary<long, IReadOnlyList<long>> sceneSubjectBindings,
        IReadOnlyDictionary<long, TemplateItemRecord> enabledSubjectsById)
    {
        var subjectContents = Array.Empty<string>();
        if (sceneSubjectBindings.TryGetValue(sceneRecord.Id, out var boundSubjectIds))
        {
            subjectContents = boundSubjectIds
                .Select(id => enabledSubjectsById.TryGetValue(id, out var subject) ? subject.Content.Trim() : string.Empty)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return new GenerationSceneTemplatePayload
        {
            Content = sceneRecord.Content.Trim(),
            Subjects = subjectContents
        };
    }

    private static IEnumerable<string> SplitLegacyValues(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var part in Regex.Split(text, @"[\/\r\n]+"))
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    private sealed class LayoutTemplatePackageManifest
    {
        public string Type { get; set; } = LayoutPackageType;

        public int Version { get; set; } = LayoutPackageVersion;

        public DateTimeOffset ExportedAt { get; set; }

        public ImageTemplateType ImageType { get; set; } = ImageTemplateType.MainImage;

        public List<LayoutTemplatePackageItem> Items { get; set; } = [];
    }

    private sealed class LayoutTemplatePackageItem
    {
        public string Name { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }

        public string PreviewFile { get; set; } = string.Empty;

        public ImageTemplateType ImageType { get; set; } = ImageTemplateType.MainImage;
    }

    private sealed class AllTemplatesPackageManifest
    {
        public string Type { get; set; } = AllTemplatesPackageType;

        public int Version { get; set; } = AllTemplatesPackageVersion;

        public DateTimeOffset ExportedAt { get; set; }

        public List<AllTemplatesPackageItem> Items { get; set; } = [];

        public List<AllTemplatesPackageBinding> SceneSubjectBindings { get; set; } = [];
    }

    private sealed class AllTemplatesPackageItem
    {
        public long Id { get; set; }

        public TemplateCategory Category { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string PreviewFile { get; set; } = string.Empty;

        public ImageTemplateType ImageType { get; set; } = ImageTemplateType.MainImage;

        public int SortOrder { get; set; }

        public bool IsEnabled { get; set; }
    }

    private sealed class AllTemplatesPackageBinding
    {
        public long SceneTemplateId { get; set; }

        public long SubjectTemplateId { get; set; }
    }

    private sealed class GenerationLibraryPayload
    {
        public int ImageType { get; set; }

        public string[] LayoutTemplates { get; set; } = [];

        public GenerationSceneTemplatePayload[] SceneTemplates { get; set; } = [];

        public string[] SubjectTemplates { get; set; } = [];
    }

    private sealed class GenerationSceneTemplatePayload
    {
        public string Content { get; set; } = string.Empty;

        public string[] Subjects { get; set; } = [];
    }
}
