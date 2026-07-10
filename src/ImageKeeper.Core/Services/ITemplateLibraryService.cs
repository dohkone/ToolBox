using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface ITemplateLibraryService
{
	Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<IReadOnlyList<TemplateItemRecord>> GetByCategoryAsync(TemplateCategory category, ImageTemplateType? imageType = null, CancellationToken cancellationToken = default(CancellationToken));

	Task<TemplateItemRecord> SaveAsync(TemplateItemRecord item, CancellationToken cancellationToken = default(CancellationToken));

	Task DeleteAsync(long id, CancellationToken cancellationToken = default(CancellationToken));

	Task<IReadOnlyDictionary<long, IReadOnlyList<long>>> GetSceneSubjectBindingsAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task SetSceneSubjectBindingsAsync(long sceneTemplateId, IReadOnlyList<long> subjectTemplateIds, CancellationToken cancellationToken = default(CancellationToken));

	string GetTemplateAssetDirectory(TemplateCategory category);

	Task<string> ImportPreviewImageAsync(string sourceImagePath, CancellationToken cancellationToken = default(CancellationToken));

	Task<int> ExportLayoutTemplatesAsync(string packagePath, ImageTemplateType imageType, CancellationToken cancellationToken = default(CancellationToken));

	Task<int> ImportLayoutTemplatesAsync(string packagePath, CancellationToken cancellationToken = default(CancellationToken));

	Task<int> ExportAllTemplatesAsync(string packagePath, CancellationToken cancellationToken = default(CancellationToken));

	Task<int> ImportAllTemplatesAsync(string packagePath, CancellationToken cancellationToken = default(CancellationToken));

	Task<string> ExportGenerationLibraryAsync(ImageTemplateType imageType, string outputPath, IReadOnlyList<long>? selectedLayoutTemplateIds = null, CancellationToken cancellationToken = default(CancellationToken));
}
