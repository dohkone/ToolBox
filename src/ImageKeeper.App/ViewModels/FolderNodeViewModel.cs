using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using ImageKeeper.Core.Models;

namespace ImageKeeper.App.ViewModels;

public sealed class FolderNodeViewModel : ViewModelBase
{
	private bool _isExpanded;

	private bool _isSelected;

	public FolderNode Model { get; }

	public Guid Id => Model.Id;

	public string DisplayName => Model.DisplayName;

	public string FolderPath => Model.FolderPath;

	public int Depth => Math.Max(0, Model.Depth);

	public Thickness Indent => new Thickness(Depth * 16, 0.0, 0.0, 0.0);

	public ObservableCollection<FolderNodeViewModel> Children { get; } = new ObservableCollection<FolderNodeViewModel>();

	public bool HasChildren => Children.Count > 0;

	public int ImageCount => Model.Images.Count;

	public string ImageCountText => $"{ImageCount} 张";

	public string ExpandGlyph
	{
		get
		{
			if (HasChildren)
			{
				if (!IsExpanded)
				{
					return "▸";
				}
				return "▾";
			}
			return string.Empty;
		}
	}

	public bool IsRootCardNode => Depth <= 0;

	public FontWeight DisplayWeight
	{
		get
		{
			if (!IsRootCardNode)
			{
				return FontWeights.Medium;
			}
			return FontWeights.SemiBold;
		}
	}

	public Brush RowBackground
	{
		get
		{
			if (!IsSelected)
			{
				return Brushes.Transparent;
			}
			return new SolidColorBrush(Color.FromRgb(238, 238, 238));
		}
	}

	public Brush RowBorderBrush
	{
		get
		{
			if (!IsSelected)
			{
				return Brushes.Transparent;
			}
			return Brushes.Transparent;
		}
	}

	public Brush NameForeground => new SolidColorBrush(Color.FromRgb(31, 42, 55));

	public Brush CountForeground
	{
		get
		{
			if (!IsSelected)
			{
				return new SolidColorBrush(Color.FromRgb(140, 154, 181));
			}
			return new SolidColorBrush(Color.FromRgb(96, 98, 102));
		}
	}

	public bool IsExpanded
	{
		get
		{
			return _isExpanded;
		}
		set
		{
			if (SetProperty(ref _isExpanded, value, "IsExpanded"))
			{
				Model.IsExpanded = value;
				OnPropertyChanged("ExpandGlyph");
			}
		}
	}

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			if (SetProperty(ref _isSelected, value, "IsSelected"))
			{
				Model.IsSelected = value;
				OnPropertyChanged("RowBackground");
				OnPropertyChanged("RowBorderBrush");
				OnPropertyChanged("CountForeground");
			}
		}
	}

	public FolderNodeViewModel(FolderNode model)
	{
		Model = model;
		_isExpanded = model.IsExpanded;
		_isSelected = model.IsSelected;
		foreach (FolderNode child in model.Children)
		{
			Children.Add(new FolderNodeViewModel(child));
		}
	}

	public void RefreshImageCount()
	{
		OnPropertyChanged("ImageCount");
		OnPropertyChanged("ImageCountText");
	}
}
