using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface ITemplateGenerationService
{
    Task<TemplateGenerateResult> GenerateAsync(TemplateGenerateRequest request, CancellationToken cancellationToken = default);

    void CancelCurrentRun();
}
