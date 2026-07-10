namespace ImageKeeper.App.ViewModels;

public sealed class TemplateSubjectOptionViewModel : ViewModelBase
{
	private bool _isSelected;

	public long Id { get; }

	public string Name { get; }

	public bool IsEnabled { get; }

	public string DisplayName
	{
		get
		{
			if (!IsEnabled)
			{
				return Name + "（已停用）";
			}
			return Name;
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
			SetProperty(ref _isSelected, value, "IsSelected");
		}
	}

	public TemplateSubjectOptionViewModel(long id, string name, bool isEnabled)
	{
		Id = id;
		Name = name;
		IsEnabled = isEnabled;
	}
}
