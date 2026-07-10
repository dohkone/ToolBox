namespace ImageKeeper.App.ViewModels;

public sealed class SceneContentLineViewModel : ViewModelBase
{
	private string _text;

	private bool _canAdd = true;

	private bool _canRemove;

	public string Text
	{
		get
		{
			return _text;
		}
		set
		{
			SetProperty(ref _text, value, "Text");
		}
	}

	public bool CanRemove
	{
		get
		{
			return _canRemove;
		}
		set
		{
			SetProperty(ref _canRemove, value, "CanRemove");
		}
	}

	public bool CanAdd
	{
		get
		{
			return _canAdd;
		}
		set
		{
			SetProperty(ref _canAdd, value, "CanAdd");
		}
	}

	public SceneContentLineViewModel(string text)
	{
		_text = text;
	}
}
