namespace ImageKeeper.App.ViewModels;

public sealed class GenerationPromptCardViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _promptText = string.Empty;
    private string _metaText = string.Empty;
    private bool _isCopied;
    private int _copyFeedbackVersion;

    public GenerationPromptCardViewModel()
    {
        CopyPromptCommand = new RelayCommand(async _ =>
        {
            if (!string.IsNullOrWhiteSpace(PromptText))
            {
                System.Windows.Clipboard.SetText(PromptText);
                var currentVersion = ++_copyFeedbackVersion;
                IsCopied = true;
                await Task.Delay(1200);
                if (currentVersion == _copyFeedbackVersion)
                {
                    IsCopied = false;
                }
            }
        }, _ => !string.IsNullOrWhiteSpace(PromptText));
    }

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

    public bool IsCopied
    {
        get => _isCopied;
        private set => SetProperty(ref _isCopied, value);
    }

    public bool HasMeta => !string.IsNullOrWhiteSpace(MetaText);

    public RelayCommand CopyPromptCommand { get; }
}
