using System;

namespace ImageKeeper.Core.Models;

public sealed class CardPublishShopInfoRecord
{
	public string CardPath { get; init; } = string.Empty;

	public string ShopNamesJson { get; init; } = "[]";

	public DateTimeOffset UpdatedAt { get; init; }
}
