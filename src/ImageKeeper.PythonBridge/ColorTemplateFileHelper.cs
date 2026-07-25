using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ImageKeeper.Core.Models;

namespace ImageKeeper.PythonBridge;

internal static class ColorTemplateFileHelper
{
	public static string? Write(IReadOnlyList<ColorTemplateColorRecord> colors, IReadOnlyCollection<string>? selectedColorNames = null)
	{
		HashSet<string>? selectedNames = null;
		if (selectedColorNames != null && selectedColorNames.Count > 0)
		{
			selectedNames = new HashSet<string>(selectedColorNames.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()), StringComparer.OrdinalIgnoreCase);
		}

		ColorTemplateColorRecord[] validColors = colors
			.Where(item => selectedNames == null || selectedNames.Contains(item.Name.Trim()))
			.Where(item => !string.IsNullOrWhiteSpace(item.Name) && ColorTemplateColorView.IsValidHex(item.HexCode))
			.ToArray();
		if (validColors.Length == 0)
		{
			return null;
		}
		string directory = Path.Combine(Path.GetTempPath(), "EcomToolStudio", "color-templates");
		Directory.CreateDirectory(directory);
		string path = Path.Combine(directory, $"colors_{Guid.NewGuid():N}.json");
		string json = JsonSerializer.Serialize(validColors.Select(item => new
		{
			name = item.Name.Trim(),
			hex = item.HexCode.Trim().ToUpperInvariant()
		}));
		File.WriteAllText(path, json);
		return path;
	}

	private static class ColorTemplateColorView
	{
		public static bool IsValidHex(string? text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			string value = text.Trim();
			return value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
		}
	}
}
