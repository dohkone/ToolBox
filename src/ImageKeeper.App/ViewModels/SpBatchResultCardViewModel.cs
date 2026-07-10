using Media = System.Windows.Media;

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
        set
        {
            if (!SetProperty(ref _statusText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StatusForeground));
            OnPropertyChanged(nameof(StatusBackground));
            OnPropertyChanged(nameof(StatusBorderBrush));
        }
    }

    public Media.Brush StatusForeground => StatusText switch
    {
        "失败" => new Media.SolidColorBrush(Media.Color.FromRgb(255, 255, 255)),
        "成功" => new Media.SolidColorBrush(Media.Color.FromRgb(255, 255, 255)),
        _ => new Media.SolidColorBrush(Media.Color.FromRgb(96, 98, 102))
    };

    public Media.Brush StatusBackground => StatusText switch
    {
        "失败" => new Media.SolidColorBrush(Media.Color.FromRgb(245, 108, 108)),
        "成功" => new Media.SolidColorBrush(Media.Color.FromRgb(103, 194, 58)),
        _ => new Media.SolidColorBrush(Media.Color.FromRgb(248, 250, 253))
    };

    public Media.Brush StatusBorderBrush => StatusText switch
    {
        "失败" => new Media.SolidColorBrush(Media.Color.FromRgb(245, 108, 108)),
        "成功" => new Media.SolidColorBrush(Media.Color.FromRgb(103, 194, 58)),
        _ => new Media.SolidColorBrush(Media.Color.FromRgb(227, 234, 244))
    };
}
