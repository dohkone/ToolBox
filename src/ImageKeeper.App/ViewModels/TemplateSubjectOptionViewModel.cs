namespace ImageKeeper.App.ViewModels;

public sealed class TemplateSubjectOptionViewModel : ViewModelBase
{
    private bool _isSelected;

    public TemplateSubjectOptionViewModel(long id, string name, bool isEnabled)
    {
        Id = id;
        Name = name;
        IsEnabled = isEnabled;
    }

    public long Id { get; }

    public string Name { get; }

    public bool IsEnabled { get; }

    public string DisplayName => IsEnabled ? Name : $"{Name}（已停用）";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
