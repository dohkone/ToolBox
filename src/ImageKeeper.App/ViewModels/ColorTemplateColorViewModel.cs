using System;
using System.Text.RegularExpressions;
using System.Windows.Media;
using ImageKeeper.Core.Models;

namespace ImageKeeper.App.ViewModels;

public sealed class ColorTemplateColorViewModel : ViewModelBase
{
	private string _name;

	private string _hexCode;

	private bool _isSelected;

	public long Id { get; }

	public long GroupId { get; }

	public int SortOrder { get; }

	public string Name
	{
		get => _name;
		set
		{
			SetProperty(ref _name, value, "Name");
			OnPropertyChanged("DisplayText");
		}
	}

	public string HexCode
	{
		get => _hexCode;
		set
		{
			if (SetProperty(ref _hexCode, value, "HexCode"))
			{
				OnPropertyChanged("PreviewBrush");
				OnPropertyChanged("IsHexValid");
				OnPropertyChanged("DisplayText");
			}
		}
	}

	public bool IsHexValid => IsValidHex(HexCode);

	public Brush PreviewBrush => CreateBrush(HexCode);

	public string DisplayText => $"{Name} {HexCode}";

	public bool IsSelected
	{
		get => _isSelected;
		set => SetProperty(ref _isSelected, value, "IsSelected");
	}

	public ColorTemplateColorViewModel(ColorTemplateColorRecord record)
	{
		Id = record.Id;
		GroupId = record.GroupId;
		SortOrder = record.SortOrder;
		_name = record.Name;
		_hexCode = record.HexCode;
	}

	public ColorTemplateColorViewModel(string name = "", string hexCode = "")
	{
		_name = name;
		_hexCode = hexCode;
	}

	public ColorTemplateColorRecord ToRecord(long groupId = 0, int sortOrder = 0)
	{
		return new ColorTemplateColorRecord
		{
			Id = Id,
			GroupId = groupId == 0 ? GroupId : groupId,
			Name = Name.Trim(),
			HexCode = NormalizeHex(HexCode),
			SortOrder = sortOrder
		};
	}

	public static bool IsValidHex(string? text)
	{
		return !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text.Trim(), "^#?[0-9A-Fa-f]{6}$");
	}

	public static string NormalizeHex(string text)
	{
		string trimmed = text.Trim();
		if (!IsValidHex(trimmed))
		{
			return trimmed;
		}
		return trimmed.StartsWith("#", StringComparison.Ordinal)
			? trimmed.ToUpperInvariant()
			: "#" + trimmed.ToUpperInvariant();
	}

	private static Brush CreateBrush(string? hexCode)
	{
		if (!IsValidHex(hexCode))
		{
			return new SolidColorBrush(Color.FromRgb(238, 242, 247));
		}
		return (Brush)new BrushConverter().ConvertFromString(NormalizeHex(hexCode!))!;
	}
}
