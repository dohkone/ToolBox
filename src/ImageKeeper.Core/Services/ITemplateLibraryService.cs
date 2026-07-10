using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface ITemplateLibraryService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TemplateItemRecord>> GetByCategoryAsync(
        TemplateCategory category,
        ImageTemplateType? imageType = null,
        CancellationToken cancellationToken = default);

    Task<TemplateItemRecord> SaveAsync(
        TemplateItemRecord item,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, IReadOnlyList<long>>> GetSceneSubjectBindingsAsync(
        CancellationToken cancellationToken = default);

    Task SetSceneSubjectBindingsAsync(
        long sceneTemplateId,
        IReadOnlyList<long> subjectTemplateIds,
        CancellationToken cancellationToken = default);

    string GetTemplateAssetDirectory(TemplateCategory category);

    Task<string> ImportPreviewImageAsync(
        string sourceImagePath,
        CancellationToken cancellationToken = default);

    Task<int> ExportLayoutTemplatesAsync(
        string packagePath,
        ImageTemplateType imageType,
        CancellationToken cancellationToken = default);

    Task<int> ImportLayoutTemplatesAsync(
        string packagePath,
        CancellationToken cancellationToken = default);

    Task<int> ExportAllTemplatesAsync(
        string packagePath,
        CancellationToken cancellationToken = default);

    Task<int> ImportAllTemplatesAsync(
        string packagePath,
        CancellationToken cancellationToken = default);

    Task<string> ExportGenerationLibraryAsync(
        ImageTemplateType imageType,
        string outputPath,
        IReadOnlyList<long>? selectedLayoutTemplateIds = null,
        CancellationToken cancellationToken = default);
}
