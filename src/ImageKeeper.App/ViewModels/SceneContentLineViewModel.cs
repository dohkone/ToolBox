namespace ImageKeeper.App.ViewModels;

public sealed class SceneContentLineViewModel : ViewModelBase
{
    private string _text;
    private bool _canAdd = true;
    private bool _canRemove;

    public SceneContentLineViewModel(string text)
    {
        _text = text;
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public bool CanRemove
    {
        get => _canRemove;
        set => SetProperty(ref _canRemove, value);
    }

    public bool CanAdd
    {
        get => _canAdd;
        set => SetProperty(ref _canAdd, value);
    }
}
