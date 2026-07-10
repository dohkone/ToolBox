namespace ImageKeeper.App.ViewModels;

public sealed class GenerationTemplateOptionViewModel : ViewModelBase
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    public GenerationTemplateOptionViewModel(long id, string name, bool isSelected, Action selectionChanged)
    {
        Id = id;
        Name = name;
        _isSelected = isSelected;
        _selectionChanged = selectionChanged;
    }

    public long Id { get; }

    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            _selectionChanged();
        }
    }
}
