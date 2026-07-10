using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;

namespace ImageKeeper.Core.Services;

public interface ITemplateGenerationService
{
	Task<TemplateGenerateResult> GenerateAsync(TemplateGenerateRequest request, CancellationToken cancellationToken = default(CancellationToken));

	void CancelCurrentRun();
}
