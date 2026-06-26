namespace ImageKeeper.App.ViewModels;

public sealed class GenerationPromptCardViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _promptText = string.Empty;
    private string _metaText = string.Empty;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string PromptText
    {
        get => _promptText;
        set => SetProperty(ref _promptText, value);
    }

    public string MetaText
    {
        get => _metaText;
        set => SetProperty(ref _metaText, value);
    }

    public bool HasMeta => !string.IsNullOrWhiteSpace(MetaText);
}
