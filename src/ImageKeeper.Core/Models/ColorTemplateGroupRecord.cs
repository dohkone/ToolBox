using System;
using System.Collections.Generic;

namespace ImageKeeper.Core.Models;

public sealed class ColorTemplateGroupRecord
{
	public long Id { get; init; }

	public string Name { get; init; } = string.Empty;

	public string Material { get; init; } = "荔枝纹";

	public int SortOrder { get; init; }

	public bool IsEnabled { get; init; } = true;

	public DateTimeOffset CreatedAt { get; init; }

	public DateTimeOffset UpdatedAt { get; init; }

	public IReadOnlyList<ColorTemplateColorRecord> Colors { get; init; } = Array.Empty<ColorTemplateColorRecord>();
}
