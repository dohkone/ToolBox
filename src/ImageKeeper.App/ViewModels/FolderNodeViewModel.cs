using System.Collections.ObjectModel;
using System.Windows;
using ImageKeeper.Core.Models;
using Media = System.Windows.Media;

namespace ImageKeeper.App.ViewModels;

public sealed class FolderNodeViewModel : ViewModelBase
{
    private bool _isExpanded;
    private bool _isSelected;

    public FolderNodeViewModel(FolderNode model)
    {
        Model = model;
        _isExpanded = model.IsExpanded;
        _isSelected = model.IsSelected;

        foreach (var child in model.Children)
        {
            Children.Add(new FolderNodeViewModel(child));
        }
    }

    public FolderNode Model { get; }

    public Guid Id => Model.Id;

    public string DisplayName => Model.DisplayName;

    public string FolderPath => Model.FolderPath;

    public int Depth => Math.Max(0, Model.Depth);

    public Thickness Indent => new(Depth * 16, 0, 0, 0);

    public ObservableCollection<FolderNodeViewModel> Children { get; } = [];

    public bool HasChildren => Children.Count > 0;

    public int ImageCount => Model.Images.Count;

    public string ImageCountText => $"{ImageCount} 张";

    public string ExpandGlyph => !HasChildren ? string.Empty : IsExpanded ? "▾" : "▸";

    public bool IsRootCardNode => Depth <= 0;

    public FontWeight DisplayWeight => IsRootCardNode ? FontWeights.SemiBold : FontWeights.Medium;

    public Media.Brush RowBackground => IsSelected
        ? new Media.SolidColorBrush(Media.Color.FromRgb(238, 238, 238))
        : Media.Brushes.Transparent;

    public Media.Brush RowBorderBrush => IsSelected
        ? Media.Brushes.Transparent
        : Media.Brushes.Transparent;

    public Media.Brush NameForeground => new Media.SolidColorBrush(Media.Color.FromRgb(31, 42, 55));

    public Media.Brush CountForeground => IsSelected
        ? new Media.SolidColorBrush(Media.Color.FromRgb(96, 98, 102))
        : new Media.SolidColorBrush(Media.Color.FromRgb(140, 154, 181));

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value))
            {
                return;
            }

            Model.IsExpanded = value;
            OnPropertyChanged(nameof(ExpandGlyph));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            Model.IsSelected = value;
            OnPropertyChanged(nameof(RowBackground));
            OnPropertyChanged(nameof(RowBorderBrush));
            OnPropertyChanged(nameof(CountForeground));
        }
    }
}
