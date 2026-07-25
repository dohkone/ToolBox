using System;

namespace ImageKeeper.Core.Models;

public sealed class ColorTemplateColorRecord
{
	public long Id { get; init; }

	public long GroupId { get; init; }

	public string Name { get; init; } = string.Empty;

	public string HexCode { get; init; } = string.Empty;

	public int SortOrder { get; init; }

	public DateTimeOffset CreatedAt { get; init; }

	public DateTimeOffset UpdatedAt { get; init; }
}
