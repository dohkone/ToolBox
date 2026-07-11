using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;
using Microsoft.Data.Sqlite;

namespace ImageKeeper.Infrastructure.Services;

public sealed class TemplateLibraryService : ITemplateLibraryService
{
	private sealed class LayoutTemplatePackageManifest
	{
		public string Type { get; set; } = "ecomtool-layout-templates";

		public int Version { get; set; } = 2;

		public DateTimeOffset ExportedAt { get; set; }

		public ImageTemplateType ImageType { get; set; }

		public List<LayoutTemplatePackageItem> Items { get; set; } = new List<LayoutTemplatePackageItem>();
	}

	private sealed class LayoutTemplatePackageItem
	{
		public string Name { get; set; } = string.Empty;

		public string Content { get; set; } = string.Empty;

		public bool IsEnabled { get; set; }

		public string PreviewFile { get; set; } = string.Empty;

		public ImageTemplateType ImageType { get; set; }
	}

	private sealed class AllTemplatesPackageManifest
	{
		public string Type { get; set; } = "ecomtool-all-templates";

		public int Version { get; set; } = 1;

		public DateTimeOffset ExportedAt { get; set; }

		public List<AllTemplatesPackageItem> Items { get; set; } = new List<AllTemplatesPackageItem>();

		public List<AllTemplatesPackageBinding> SceneSubjectBindings { get; set; } = new List<AllTemplatesPackageBinding>();
	}

	private sealed class AllTemplatesPackageItem
	{
		public long Id { get; set; }

		public TemplateCategory Category { get; set; }

		public string Name { get; set; } = string.Empty;

		public string Content { get; set; } = string.Empty;

		public string Subject { get; set; } = string.Empty;

		public string PreviewFile { get; set; } = string.Empty;

		public ImageTemplateType ImageType { get; set; }

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

		public string[] LayoutTemplates { get; set; } = Array.Empty<string>();

		public GenerationSceneTemplatePayload[] SceneTemplates { get; set; } = Array.Empty<GenerationSceneTemplatePayload>();

		public string[] SubjectTemplates { get; set; } = Array.Empty<string>();
	}

	private sealed class GenerationSceneTemplatePayload
	{
		public string Content { get; set; } = string.Empty;

		public string[] Subjects { get; set; } = Array.Empty<string>();
	}

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

	public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_databasePath));
		Directory.CreateDirectory(GetTemplateAssetDirectory(TemplateCategory.Layout));
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "CREATE TABLE IF NOT EXISTS TemplateItems (\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\n    Category INTEGER NOT NULL,\n    Name TEXT NOT NULL DEFAULT '',\n    Content TEXT NOT NULL DEFAULT '',\n    Subject TEXT NOT NULL DEFAULT '',\n    PreviewImagePath TEXT NOT NULL DEFAULT '',\n    ImageType INTEGER NOT NULL DEFAULT 0,\n    SortOrder INTEGER NOT NULL DEFAULT 0,\n    IsEnabled INTEGER NOT NULL DEFAULT 1,\n    CreatedAt TEXT NOT NULL,\n    UpdatedAt TEXT NOT NULL\n);\n\nCREATE INDEX IF NOT EXISTS IX_TemplateItems_Category_Id\nON TemplateItems(Category, Id);\n\nCREATE TABLE IF NOT EXISTS SceneTemplateSubjectBindings (\n    Id INTEGER PRIMARY KEY AUTOINCREMENT,\n    SceneTemplateId INTEGER NOT NULL,\n    SubjectTemplateId INTEGER NOT NULL,\n    CreatedAt TEXT NOT NULL,\n    UNIQUE(SceneTemplateId, SubjectTemplateId)\n);\n\nCREATE INDEX IF NOT EXISTS IX_SceneTemplateSubjectBindings_SceneTemplateId\nON SceneTemplateSubjectBindings(SceneTemplateId);";
		await sqliteCommand.ExecuteNonQueryAsync(cancellationToken);
		await TryAddColumnAsync(connection, "ALTER TABLE TemplateItems ADD COLUMN Subject TEXT NOT NULL DEFAULT '';", cancellationToken);
		await TryAddColumnAsync(connection, "ALTER TABLE TemplateItems ADD COLUMN ImageType INTEGER NOT NULL DEFAULT 0;", cancellationToken);
		await MigrateLegacySubjectsAsync(connection, cancellationToken);
		await MigrateLegacySceneSubjectBindingsAsync(connection, cancellationToken);
	}

	public async Task<IReadOnlyList<TemplateItemRecord>> GetByCategoryAsync(TemplateCategory category, ImageTemplateType? imageType = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		await InitializeAsync(cancellationToken);
		IReadOnlyList<TemplateItemRecord> result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync(cancellationToken);
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = (imageType.HasValue ? "SELECT Id, Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt\nFROM TemplateItems\nWHERE Category = $category AND ImageType = $imageType\nORDER BY UpdatedAt DESC, Id DESC;" : "SELECT Id, Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt\nFROM TemplateItems\nWHERE Category = $category\nORDER BY UpdatedAt DESC, Id DESC;");
			sqliteCommand.Parameters.AddWithValue("$category", (int)category);
			if (imageType.HasValue)
			{
				sqliteCommand.Parameters.AddWithValue("$imageType", (int)imageType.Value);
			}
			List<TemplateItemRecord> items = new List<TemplateItemRecord>();
			IReadOnlyList<TemplateItemRecord> readOnlyList;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
				{
					items.Add(ReadRecord(reader));
				}
				readOnlyList = items;
			}
			result = readOnlyList;
		}
		return result;
	}

	public async Task<TemplateItemRecord> SaveAsync(TemplateItemRecord item, CancellationToken cancellationToken = default(CancellationToken))
	{
		await InitializeAsync(cancellationToken);
		TemplateItemRecord result;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync(cancellationToken);
			string now = DateTimeOffset.Now.ToString("O");
			TemplateItemRecord templateItemRecord;
			if (item.Id <= 0)
			{
				SqliteCommand sqliteCommand = connection.CreateCommand();
				sqliteCommand.CommandText = "INSERT INTO TemplateItems\n    (Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt)\nVALUES\n    ($category, $name, $content, $subject, $previewImagePath, $imageType, $sortOrder, $isEnabled, $now, $now)\nRETURNING Id, Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt;";
				BindParameters(sqliteCommand, item, now);
				await using SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync(cancellationToken);
				if (await reader.ReadAsync(cancellationToken))
				{
					templateItemRecord = ReadRecord(reader);
					goto IL_0341;
				}
			}
			else
			{
				SqliteCommand sqliteCommand2 = connection.CreateCommand();
				sqliteCommand2.CommandText = "UPDATE TemplateItems\nSET Category = $category,\n    Name = $name,\n    Content = $content,\n    Subject = $subject,\n    PreviewImagePath = $previewImagePath,\n    ImageType = $imageType,\n    SortOrder = $sortOrder,\n    IsEnabled = $isEnabled,\n    UpdatedAt = $now\nWHERE Id = $id\nRETURNING Id, Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt;";
				sqliteCommand2.Parameters.AddWithValue("$id", item.Id);
				BindParameters(sqliteCommand2, item, now);
				await using SqliteDataReader reader = await sqliteCommand2.ExecuteReaderAsync(cancellationToken);
				if (await reader.ReadAsync(cancellationToken))
				{
					templateItemRecord = ReadRecord(reader);
					goto IL_0564;
				}
			}
			throw new InvalidOperationException("模板保存失败。");
			IL_0341:
			result = templateItemRecord;
			goto end_IL_013d;
			IL_0564:
			result = templateItemRecord;
			end_IL_013d:;
		}
		return result;
	}

	public async Task DeleteAsync(long id, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (id <= 0)
		{
			return;
		}
		await InitializeAsync(cancellationToken);
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "DELETE FROM SceneTemplateSubjectBindings\nWHERE SceneTemplateId = $id OR SubjectTemplateId = $id;";
		sqliteCommand.Parameters.AddWithValue("$id", id);
		await sqliteCommand.ExecuteNonQueryAsync(cancellationToken);
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.CommandText = "DELETE FROM TemplateItems WHERE Id = $id;";
		sqliteCommand2.Parameters.AddWithValue("$id", id);
		await sqliteCommand2.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<IReadOnlyDictionary<long, IReadOnlyList<long>>> GetSceneSubjectBindingsAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		await InitializeAsync(cancellationToken);
		IReadOnlyDictionary<long, IReadOnlyList<long>> result2;
		await using (SqliteConnection connection = CreateConnection())
		{
			await connection.OpenAsync(cancellationToken);
			SqliteCommand sqliteCommand = connection.CreateCommand();
			sqliteCommand.CommandText = "SELECT SceneTemplateId, SubjectTemplateId\nFROM SceneTemplateSubjectBindings\nORDER BY SceneTemplateId ASC, Id ASC;";
			Dictionary<long, List<long>> result = new Dictionary<long, List<long>>();
			IReadOnlyDictionary<long, IReadOnlyList<long>> readOnlyDictionary;
			await using (SqliteDataReader reader = await sqliteCommand.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
				{
					long @int = reader.GetInt64(0);
					long int2 = reader.GetInt64(1);
					if (!result.TryGetValue(@int, out List<long> value))
					{
						value = (result[@int] = new List<long>());
					}
					value.Add(int2);
				}
				readOnlyDictionary = result.ToDictionary<KeyValuePair<long, List<long>>, long, IReadOnlyList<long>>((KeyValuePair<long, List<long>> pair) => pair.Key, (KeyValuePair<long, List<long>> pair) => pair.Value);
			}
			result2 = readOnlyDictionary;
		}
		return result2;
	}

	public async Task SetSceneSubjectBindingsAsync(long sceneTemplateId, IReadOnlyList<long> subjectTemplateIds, CancellationToken cancellationToken = default(CancellationToken))
	{
		await InitializeAsync(cancellationToken);
		await using SqliteConnection connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);
		await using SqliteTransaction transaction = (SqliteTransaction)(await connection.BeginTransactionAsync(cancellationToken));
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.Transaction = transaction;
		sqliteCommand.CommandText = "DELETE FROM SceneTemplateSubjectBindings WHERE SceneTemplateId = $sceneTemplateId;";
		sqliteCommand.Parameters.AddWithValue("$sceneTemplateId", sceneTemplateId);
		await sqliteCommand.ExecuteNonQueryAsync(cancellationToken);
		string now = DateTimeOffset.Now.ToString("O");
		foreach (long item in subjectTemplateIds.Where((long id) => id > 0).Distinct())
		{
			SqliteCommand sqliteCommand2 = connection.CreateCommand();
			sqliteCommand2.Transaction = transaction;
			sqliteCommand2.CommandText = "INSERT INTO SceneTemplateSubjectBindings\n    (SceneTemplateId, SubjectTemplateId, CreatedAt)\nVALUES\n    ($sceneTemplateId, $subjectTemplateId, $createdAt);";
			sqliteCommand2.Parameters.AddWithValue("$sceneTemplateId", sceneTemplateId);
			sqliteCommand2.Parameters.AddWithValue("$subjectTemplateId", item);
			sqliteCommand2.Parameters.AddWithValue("$createdAt", now);
			await sqliteCommand2.ExecuteNonQueryAsync(cancellationToken);
		}
		await transaction.CommitAsync(cancellationToken);
	}

	public string GetTemplateAssetDirectory(TemplateCategory category)
	{
		return Path.Combine(_assetRoot, category switch
		{
			TemplateCategory.Layout => "layout-previews", 
			TemplateCategory.Scene => "scene", 
			TemplateCategory.Subject => "subject", 
			_ => "misc", 
		});
	}

	public Task<string> ImportPreviewImageAsync(string sourceImagePath, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
		{
			throw new FileNotFoundException("预览图不存在。", sourceImagePath);
		}
		string templateAssetDirectory = GetTemplateAssetDirectory(TemplateCategory.Layout);
		Directory.CreateDirectory(templateAssetDirectory);
		string value = Path.GetExtension(sourceImagePath);
		if (string.IsNullOrWhiteSpace(value))
		{
			value = ".png";
		}
		string text = Path.Combine(templateAssetDirectory, $"layout_{DateTimeOffset.Now:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}{value}");
		File.Copy(sourceImagePath, text, overwrite: true);
		return Task.FromResult(text);
	}

	public async Task<int> ExportLayoutTemplatesAsync(string packagePath, ImageTemplateType imageType, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(packagePath))
		{
			throw new ArgumentException("导出文件路径不能为空。", "packagePath");
		}
		IReadOnlyList<TemplateItemRecord> readOnlyList = await GetByCategoryAsync(TemplateCategory.Layout, imageType, cancellationToken);
		Directory.CreateDirectory(Path.GetDirectoryName(packagePath));
		if (File.Exists(packagePath))
		{
			File.Delete(packagePath);
		}
		LayoutTemplatePackageManifest manifest = new LayoutTemplatePackageManifest
		{
			ExportedAt = DateTimeOffset.Now,
			ImageType = imageType,
			Items = new List<LayoutTemplatePackageItem>()
		};
		int result;
		await using (FileStream fileStream = new FileStream(packagePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
		{
			using ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create);
			int num = 1;
			foreach (TemplateItemRecord item in readOnlyList)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string text = string.Empty;
				if (!string.IsNullOrWhiteSpace(item.PreviewImagePath) && File.Exists(item.PreviewImagePath))
				{
					string value = Path.GetExtension(item.PreviewImagePath);
					if (string.IsNullOrWhiteSpace(value))
					{
						value = ".png";
					}
					text = $"previews/layout_{num++:000}{value}";
					archive.CreateEntryFromFile(item.PreviewImagePath, text, CompressionLevel.Optimal);
				}
				manifest.Items.Add(new LayoutTemplatePackageItem
				{
					Name = item.Name,
					Content = item.Content,
					IsEnabled = item.IsEnabled,
					PreviewFile = text,
					ImageType = item.ImageType
				});
			}
			ZipArchiveEntry zipArchiveEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
			int count;
			await using (Stream manifestStream = zipArchiveEntry.Open())
			{
				await JsonSerializer.SerializeAsync(manifestStream, manifest, new JsonSerializerOptions
				{
					WriteIndented = true
				}, cancellationToken);
				count = manifest.Items.Count;
			}
			result = count;
		}
		return result;
	}

	public async Task<int> ImportLayoutTemplatesAsync(string packagePath, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
		{
			throw new FileNotFoundException("导入文件不存在。", packagePath);
		}
		await InitializeAsync(cancellationToken);
		using ZipArchive archive = ZipFile.OpenRead(packagePath);
		ZipArchiveEntry zipArchiveEntry = archive.GetEntry("manifest.json") ?? throw new InvalidOperationException("导入文件缺少 manifest.json。");
		LayoutTemplatePackageManifest manifest;
		await using (Stream manifestStream = zipArchiveEntry.Open())
		{
			manifest = await JsonSerializer.DeserializeAsync<LayoutTemplatePackageManifest>(manifestStream, (JsonSerializerOptions?)null, cancellationToken);
		}
		if (manifest == null || !string.Equals(manifest.Type, "ecomtool-layout-templates", StringComparison.Ordinal) || manifest.Version < 1)
		{
			throw new InvalidOperationException("导入文件不是有效的布局模板包。");
		}
		TemplateLibraryService templateLibraryService = this;
		CancellationToken cancellationToken2 = cancellationToken;
		HashSet<string> existingNames = new HashSet<string>((await templateLibraryService.GetByCategoryAsync(TemplateCategory.Layout, null, cancellationToken2)).Select((TemplateItemRecord item) => item.Name), StringComparer.OrdinalIgnoreCase);
		int importedCount = 0;
		foreach (LayoutTemplatePackageItem item in manifest.Items)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Content))
			{
				string previewImagePath = ImportPreviewFromPackage(archive, item.PreviewFile);
				string text = CreateImportedTemplateName(item.Name, existingNames);
				existingNames.Add(text);
				await SaveAsync(new TemplateItemRecord
				{
					Category = TemplateCategory.Layout,
					Name = text,
					Content = item.Content,
					Subject = string.Empty,
					PreviewImagePath = previewImagePath,
					ImageType = item.ImageType,
					SortOrder = 0,
					IsEnabled = item.IsEnabled
				}, cancellationToken);
				importedCount++;
			}
		}
		return importedCount;
	}

	public async Task<int> ExportAllTemplatesAsync(string packagePath, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(packagePath))
		{
			throw new ArgumentException("Export package path cannot be empty.", "packagePath");
		}
		await InitializeAsync(cancellationToken);
		List<TemplateItemRecord> items = new List<TemplateItemRecord>();
		List<TemplateItemRecord> list = items;
		TemplateLibraryService templateLibraryService = this;
		CancellationToken cancellationToken2 = cancellationToken;
		list.AddRange(await templateLibraryService.GetByCategoryAsync(TemplateCategory.Layout, null, cancellationToken2));
		list = items;
		TemplateLibraryService templateLibraryService2 = this;
		cancellationToken2 = cancellationToken;
		list.AddRange(await templateLibraryService2.GetByCategoryAsync(TemplateCategory.Scene, null, cancellationToken2));
		list = items;
		TemplateLibraryService templateLibraryService3 = this;
		cancellationToken2 = cancellationToken;
		list.AddRange(await templateLibraryService3.GetByCategoryAsync(TemplateCategory.Subject, null, cancellationToken2));
		IReadOnlyDictionary<long, IReadOnlyList<long>> source = await GetSceneSubjectBindingsAsync(cancellationToken);
		Directory.CreateDirectory(Path.GetDirectoryName(packagePath));
		if (File.Exists(packagePath))
		{
			File.Delete(packagePath);
		}
		AllTemplatesPackageManifest manifest = new AllTemplatesPackageManifest
		{
			ExportedAt = DateTimeOffset.Now,
			Items = new List<AllTemplatesPackageItem>(),
			SceneSubjectBindings = source.SelectMany((KeyValuePair<long, IReadOnlyList<long>> pair) => pair.Value.Select((long subjectId) => new AllTemplatesPackageBinding
			{
				SceneTemplateId = pair.Key,
				SubjectTemplateId = subjectId
			})).ToList()
		};
		int result;
		await using (FileStream fileStream = new FileStream(packagePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
		{
			using ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create);
			int num = 1;
			foreach (TemplateItemRecord item in from item in items
				orderby item.Category, item.ImageType, item.Id
				select item)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string text = string.Empty;
				if (item.Category == TemplateCategory.Layout && !string.IsNullOrWhiteSpace(item.PreviewImagePath) && File.Exists(item.PreviewImagePath))
				{
					string value = Path.GetExtension(item.PreviewImagePath);
					if (string.IsNullOrWhiteSpace(value))
					{
						value = ".png";
					}
					text = $"assets/layout-previews/layout_{num++:000}{value}";
					archive.CreateEntryFromFile(item.PreviewImagePath, text, CompressionLevel.Optimal);
				}
				manifest.Items.Add(new AllTemplatesPackageItem
				{
					Id = item.Id,
					Category = item.Category,
					Name = item.Name,
					Content = item.Content,
					Subject = item.Subject,
					PreviewFile = text,
					ImageType = item.ImageType,
					SortOrder = item.SortOrder,
					IsEnabled = item.IsEnabled
				});
			}
			ZipArchiveEntry zipArchiveEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
			int count;
			await using (Stream manifestStream = zipArchiveEntry.Open())
			{
				await JsonSerializer.SerializeAsync(manifestStream, manifest, new JsonSerializerOptions
				{
					WriteIndented = true
				}, cancellationToken);
				count = manifest.Items.Count;
			}
			result = count;
		}
		return result;
	}

	public async Task<int> ImportAllTemplatesAsync(string packagePath, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
		{
			throw new FileNotFoundException("Import package does not exist.", packagePath);
		}
		await InitializeAsync(cancellationToken);
		using ZipArchive archive = ZipFile.OpenRead(packagePath);
		ZipArchiveEntry zipArchiveEntry = archive.GetEntry("manifest.json") ?? throw new InvalidOperationException("Import package is missing manifest.json.");
		AllTemplatesPackageManifest manifest;
		await using (Stream manifestStream = zipArchiveEntry.Open())
		{
			manifest = await JsonSerializer.DeserializeAsync<AllTemplatesPackageManifest>(manifestStream, (JsonSerializerOptions?)null, cancellationToken);
		}
		if (manifest == null || !string.Equals(manifest.Type, "ecomtool-all-templates", StringComparison.Ordinal) || manifest.Version < 1)
		{
			throw new InvalidOperationException("Import package is not a valid all-templates package.");
		}
		List<TemplateItemRecord> existingItems = new List<TemplateItemRecord>();
		List<TemplateItemRecord> list = existingItems;
		TemplateLibraryService templateLibraryService = this;
		CancellationToken cancellationToken2 = cancellationToken;
		list.AddRange(await templateLibraryService.GetByCategoryAsync(TemplateCategory.Layout, null, cancellationToken2));
		list = existingItems;
		TemplateLibraryService templateLibraryService2 = this;
		cancellationToken2 = cancellationToken;
		list.AddRange(await templateLibraryService2.GetByCategoryAsync(TemplateCategory.Scene, null, cancellationToken2));
		list = existingItems;
		TemplateLibraryService templateLibraryService3 = this;
		cancellationToken2 = cancellationToken;
		list.AddRange(await templateLibraryService3.GetByCategoryAsync(TemplateCategory.Subject, null, cancellationToken2));
		Dictionary<TemplateCategory, HashSet<string>> existingNames = (from item in existingItems
			group item by item.Category).ToDictionary((IGrouping<TemplateCategory, TemplateItemRecord> group) => group.Key, (IGrouping<TemplateCategory, TemplateItemRecord> group) => new HashSet<string>(group.Select((TemplateItemRecord item) => item.Name), StringComparer.OrdinalIgnoreCase));
		Dictionary<long, long> idMap = new Dictionary<long, long>();
		int importedCount = 0;
		foreach (AllTemplatesPackageItem packageItem in from item in manifest.Items
			orderby item.Category, item.Id
			select item)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!string.IsNullOrWhiteSpace(packageItem.Name) && !string.IsNullOrWhiteSpace(packageItem.Content))
			{
				if (!existingNames.TryGetValue(packageItem.Category, out var value))
				{
					value = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					existingNames[packageItem.Category] = value;
				}
				string text = CreateImportedTemplateName(packageItem.Name, value);
				value.Add(text);
				string previewImagePath = ((packageItem.Category == TemplateCategory.Layout) ? ImportPreviewFromPackage(archive, packageItem.PreviewFile) : string.Empty);
				TemplateItemRecord templateItemRecord = await SaveAsync(new TemplateItemRecord
				{
					Category = packageItem.Category,
					Name = text,
					Content = packageItem.Content,
					Subject = packageItem.Subject,
					PreviewImagePath = previewImagePath,
					ImageType = packageItem.ImageType,
					SortOrder = packageItem.SortOrder,
					IsEnabled = packageItem.IsEnabled
				}, cancellationToken);
				idMap[packageItem.Id] = templateItemRecord.Id;
				importedCount++;
			}
		}
		long value3;
		long value4;
		var importedBindings = from binding in manifest.SceneSubjectBindings
			select (!idMap.TryGetValue(binding.SceneTemplateId, out value3) || !idMap.TryGetValue(binding.SubjectTemplateId, out value4)) ? null : new
			{
				SceneId = value3,
				SubjectId = value4
			} into item
			where item != null
			group item by item.SceneId;
		IReadOnlyDictionary<long, IReadOnlyList<long>> currentBindings = await GetSceneSubjectBindingsAsync(cancellationToken);
		foreach (var item in importedBindings)
		{
			cancellationToken.ThrowIfCancellationRequested();
			IReadOnlyList<long> value2;
			long[] subjectTemplateIds = (currentBindings.TryGetValue(item.Key, out value2) ? value2.Concat(item.Select(item => item.SubjectId)).Distinct().ToArray() : item.Select(item => item.SubjectId).Distinct().ToArray());
			await SetSceneSubjectBindingsAsync(item.Key, subjectTemplateIds, cancellationToken);
		}
		return importedCount;
	}

	public async Task<string> ExportGenerationLibraryAsync(ImageTemplateType imageType, string outputPath, IReadOnlyList<long>? selectedLayoutTemplateIds = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(outputPath))
		{
			throw new ArgumentException("输出路径不能为空。", "outputPath");
		}
		IReadOnlyList<TemplateItemRecord> layouts = await GetByCategoryAsync(TemplateCategory.Layout, imageType, cancellationToken);
		TemplateLibraryService templateLibraryService = this;
		CancellationToken cancellationToken2 = cancellationToken;
		IReadOnlyList<TemplateItemRecord> scenes = await templateLibraryService.GetByCategoryAsync(TemplateCategory.Scene, null, cancellationToken2);
		TemplateLibraryService templateLibraryService2 = this;
		cancellationToken2 = cancellationToken;
		IReadOnlyList<TemplateItemRecord> subjects = await templateLibraryService2.GetByCategoryAsync(TemplateCategory.Subject, null, cancellationToken2);
		IReadOnlyDictionary<long, IReadOnlyList<long>> sceneSubjectBindings = await GetSceneSubjectBindingsAsync(cancellationToken);
		Dictionary<long, TemplateItemRecord> enabledSubjectsById = subjects.Where((TemplateItemRecord item) => item.IsEnabled).ToDictionary((TemplateItemRecord item) => item.Id);
		HashSet<long> selectedLayoutIdSet = selectedLayoutTemplateIds?.Where((long id) => id > 0).Distinct().ToHashSet();
		TemplateItemRecord[] source = (from item in layouts
			where item.IsEnabled
			where selectedLayoutIdSet == null || selectedLayoutIdSet.Count == 0 || selectedLayoutIdSet.Contains(item.Id)
			select item).ToArray();
		GenerationLibraryPayload generationLibraryPayload = new GenerationLibraryPayload
		{
			ImageType = (int)imageType,
			LayoutTemplates = (from item in source
				select item.Content.Trim() into text
				where !string.IsNullOrWhiteSpace(text)
				select text).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray(),
			SceneTemplates = (from item in scenes
				where item.IsEnabled
				select CreateGenerationSceneTemplate(item, sceneSubjectBindings, enabledSubjectsById) into item
				where !string.IsNullOrWhiteSpace(item.Content)
				where imageType != ImageTemplateType.MainImage || item.Subjects.Length != 0
				select item).ToArray(),
			SubjectTemplates = (from item in subjects
				where item.IsEnabled
				select item.Content.Trim() into text
				where !string.IsNullOrWhiteSpace(text)
				select text).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray()
		};
		if (generationLibraryPayload.LayoutTemplates.Length == 0)
		{
			throw new InvalidOperationException("当前图片类型下没有可用的布局模板。");
		}
		if (generationLibraryPayload.SceneTemplates.Length == 0)
		{
			throw new InvalidOperationException("当前没有可用的场景模板。");
		}
		if (generationLibraryPayload.SubjectTemplates.Length == 0)
		{
			throw new InvalidOperationException("当前没有可用的主体模板。");
		}
		string directoryName = Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		await using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
		{
			await JsonSerializer.SerializeAsync(stream, generationLibraryPayload, new JsonSerializerOptions
			{
				WriteIndented = true
			}, cancellationToken);
		}
		return outputPath;
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

	private static async Task TryAddColumnAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = sql;
		try
		{
			await sqliteCommand.ExecuteNonQueryAsync(cancellationToken);
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
			IsEnabled = (reader.GetInt32(8) != 0),
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
		ZipArchiveEntry entry = archive.GetEntry(previewFile.Replace('\\', '/'));
		if (entry == null)
		{
			return string.Empty;
		}
		string templateAssetDirectory = GetTemplateAssetDirectory(TemplateCategory.Layout);
		Directory.CreateDirectory(templateAssetDirectory);
		string value = Path.GetExtension(entry.Name);
		if (string.IsNullOrWhiteSpace(value))
		{
			value = ".png";
		}
		string text = Path.Combine(templateAssetDirectory, $"layout_import_{DateTimeOffset.Now:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}{value}");
		entry.ExtractToFile(text, overwrite: true);
		return text;
	}

	private static string CreateImportedTemplateName(string name, HashSet<string> existingNames)
	{
		string text = name.Trim();
		if (!existingNames.Contains(text))
		{
			return text;
		}
		int num = 1;
		string text2;
		do
		{
			text2 = $"{text} - 导入{num}";
			num++;
		}
		while (existingNames.Contains(text2));
		return text2;
	}

	private static async Task MigrateLegacySubjectsAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT COUNT(1) FROM TemplateItems WHERE Category = $category;";
		sqliteCommand.Parameters.AddWithValue("$category", 2);
		if (Convert.ToInt32(await sqliteCommand.ExecuteScalarAsync(cancellationToken)) > 0)
		{
			return;
		}
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.CommandText = "SELECT Subject\nFROM TemplateItems\nWHERE Category = $category AND TRIM(Subject) <> '';";
		sqliteCommand2.Parameters.AddWithValue("$category", 1);
		HashSet<string> subjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		await using (SqliteDataReader reader = await sqliteCommand2.ExecuteReaderAsync(cancellationToken))
		{
			while (await reader.ReadAsync(cancellationToken))
			{
				foreach (string item in SplitLegacyValues(reader.GetString(0)))
				{
					subjects.Add(item);
				}
			}
		}
		if (subjects.Count == 0)
		{
			return;
		}
		foreach (string item2 in subjects.OrderBy<string, string>((string item) => item, StringComparer.OrdinalIgnoreCase))
		{
			SqliteCommand sqliteCommand3 = connection.CreateCommand();
			sqliteCommand3.CommandText = "INSERT INTO TemplateItems\n    (Category, Name, Content, Subject, PreviewImagePath, ImageType, SortOrder, IsEnabled, CreatedAt, UpdatedAt)\nVALUES\n    ($category, $name, $content, '', '', 0, 0, 1, $now, $now);";
			string value = DateTimeOffset.Now.ToString("O");
			sqliteCommand3.Parameters.AddWithValue("$category", 2);
			sqliteCommand3.Parameters.AddWithValue("$name", item2);
			sqliteCommand3.Parameters.AddWithValue("$content", item2);
			sqliteCommand3.Parameters.AddWithValue("$now", value);
			await sqliteCommand3.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	private static async Task MigrateLegacySceneSubjectBindingsAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "SELECT COUNT(1) FROM SceneTemplateSubjectBindings;";
		if (Convert.ToInt32(await sqliteCommand.ExecuteScalarAsync(cancellationToken)) > 0)
		{
			return;
		}
		SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.CommandText = "SELECT Id, Name, Content\nFROM TemplateItems\nWHERE Category = $category;";
		sqliteCommand2.Parameters.AddWithValue("$category", 2);
		Dictionary<string, long> subjectLookup = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		await using (SqliteDataReader subjectReader = await sqliteCommand2.ExecuteReaderAsync(cancellationToken))
		{
			while (await subjectReader.ReadAsync(cancellationToken))
			{
				long @int = subjectReader.GetInt64(0);
				string text = subjectReader.GetString(1).Trim();
				string text2 = subjectReader.GetString(2).Trim();
				if (!string.IsNullOrWhiteSpace(text))
				{
					subjectLookup.TryAdd(text, @int);
				}
				if (!string.IsNullOrWhiteSpace(text2))
				{
					subjectLookup.TryAdd(text2, @int);
				}
			}
		}
		if (subjectLookup.Count == 0)
		{
			return;
		}
		SqliteCommand sqliteCommand3 = connection.CreateCommand();
		sqliteCommand3.CommandText = "SELECT Id, Subject\nFROM TemplateItems\nWHERE Category = $category AND TRIM(Subject) <> '';";
		sqliteCommand3.Parameters.AddWithValue("$category", 1);
		await using SqliteDataReader sceneReader = await sqliteCommand3.ExecuteReaderAsync(cancellationToken);
		while (await sceneReader.ReadAsync(cancellationToken))
		{
			long sceneTemplateId = sceneReader.GetInt64(0);
			string text3 = sceneReader.GetString(1);
			foreach (string item in SplitLegacyValues(text3))
			{
				if (subjectLookup.TryGetValue(item, out var value))
				{
					SqliteCommand sqliteCommand4 = connection.CreateCommand();
					sqliteCommand4.CommandText = "INSERT OR IGNORE INTO SceneTemplateSubjectBindings\n    (SceneTemplateId, SubjectTemplateId, CreatedAt)\nVALUES\n    ($sceneTemplateId, $subjectTemplateId, $createdAt);";
					sqliteCommand4.Parameters.AddWithValue("$sceneTemplateId", sceneTemplateId);
					sqliteCommand4.Parameters.AddWithValue("$subjectTemplateId", value);
					sqliteCommand4.Parameters.AddWithValue("$createdAt", DateTimeOffset.Now.ToString("O"));
					await sqliteCommand4.ExecuteNonQueryAsync(cancellationToken);
				}
			}
		}
	}

	private static GenerationSceneTemplatePayload CreateGenerationSceneTemplate(TemplateItemRecord sceneRecord, IReadOnlyDictionary<long, IReadOnlyList<long>> sceneSubjectBindings, IReadOnlyDictionary<long, TemplateItemRecord> enabledSubjectsById)
	{
		string[] subjects = Array.Empty<string>();
		if (sceneSubjectBindings.TryGetValue(sceneRecord.Id, out IReadOnlyList<long> value))
		{
			subjects = (from id in value
				select (!enabledSubjectsById.TryGetValue(id, out TemplateItemRecord value2)) ? string.Empty : value2.Content.Trim() into text
				where !string.IsNullOrWhiteSpace(text)
				select text).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		}
		return new GenerationSceneTemplatePayload
		{
			Content = sceneRecord.Content.Trim(),
			Subjects = subjects
		};
	}

	private static IEnumerable<string> SplitLegacyValues(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			yield break;
		}
		string[] array = Regex.Split(text, "[\\/\\r\\n]+");
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Trim();
			if (!string.IsNullOrWhiteSpace(text2))
			{
				yield return text2;
			}
		}
	}
}
