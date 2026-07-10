using System;

namespace ImageKeeper.App.ViewModels;

public sealed class GenerationTemplateOptionViewModel : ViewModelBase
{
	private readonly Action _selectionChanged;

	private bool _isSelected;

	public long Id { get; }

	public string Name { get; }

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
				_selectionChanged();
			}
		}
	}

	public GenerationTemplateOptionViewModel(long id, string name, bool isSelected, Action selectionChanged)
	{
		Id = id;
		Name = name;
		_isSelected = isSelected;
		_selectionChanged = selectionChanged;
	}
}
