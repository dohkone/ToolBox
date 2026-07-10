using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageKeeper.Core.Models;

public sealed class SkuOptimizeResult
{
	public bool Success { get; init; }

	public string InputDirectory { get; init; } = string.Empty;

	public string OutputDirectory { get; init; } = string.Empty;

	public string ResultRoot { get; init; } = string.Empty;

	public int Concurrency { get; init; }

	public double LengthMultiplier { get; init; }

	public double DiameterMultiplier { get; init; }

	public IReadOnlyList<SkuOptimizeJobResult> Results { get; init; } = Array.Empty<SkuOptimizeJobResult>();

	public int SuccessCount => Results.Count((SkuOptimizeJobResult item) => string.Equals(item.Status, "generated", StringComparison.OrdinalIgnoreCase));

	public int SkippedCount => Results.Count((SkuOptimizeJobResult item) => string.Equals(item.Status, "skipped", StringComparison.OrdinalIgnoreCase));

	public int FailedCount => Results.Count((SkuOptimizeJobResult item) => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase));
}
