namespace ImageKeeper.App.ViewModels;

public sealed class SpBatchResultCardViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _summaryText = string.Empty;
    private string _detailText = string.Empty;
    private string _statusText = string.Empty;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value);
    }

    public string DetailText
    {
        get => _detailText;
        set => SetProperty(ref _detailText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
}
