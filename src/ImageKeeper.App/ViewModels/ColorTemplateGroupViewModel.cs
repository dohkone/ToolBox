using System.Collections.ObjectModel;
using System.Linq;
using ImageKeeper.Core.Models;

namespace ImageKeeper.App.ViewModels;

public sealed class ColorTemplateGroupViewModel : ViewModelBase
{
	public ColorTemplateGroupRecord Model { get; }

	public long Id => Model.Id;

	public string Name => Model.Name;

	public string Material => Model.Material;

	public int ColorCount => Colors.Count;

	public string ColorCountText => $"{ColorCount} 个颜色";

	public string CreatedAtText => Model.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

	public string UpdatedAtText => Model.UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

	public ObservableCollection<ColorTemplateColorViewModel> Colors { get; }

	public ColorTemplateGroupViewModel(ColorTemplateGroupRecord model)
	{
		Model = model;
		Colors = new ObservableCollection<ColorTemplateColorViewModel>(model.Colors.Select(item => new ColorTemplateColorViewModel(item)));
	}
}
