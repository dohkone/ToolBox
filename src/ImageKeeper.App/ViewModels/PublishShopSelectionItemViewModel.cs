namespace ImageKeeper.App.ViewModels;

public sealed class PublishShopSelectionItemViewModel : ViewModelBase
{
	private bool _isSelected;

	public string Name { get; }

	public bool IsSelected
	{
		get => _isSelected;
		set => SetProperty(ref _isSelected, value, "IsSelected");
	}

	public PublishShopSelectionItemViewModel(string name, bool isSelected = false)
	{
		Name = name;
		_isSelected = isSelected;
	}
}
