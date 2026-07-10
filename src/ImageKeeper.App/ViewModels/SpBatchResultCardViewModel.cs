using System.Windows.Media;

namespace ImageKeeper.App.ViewModels;

public sealed class SpBatchResultCardViewModel : ViewModelBase
{
	private string _title = string.Empty;

	private string _summaryText = string.Empty;

	private string _detailText = string.Empty;

	private string _statusText = string.Empty;

	public string Title
	{
		get
		{
			return _title;
		}
		set
		{
			SetProperty(ref _title, value, "Title");
		}
	}

	public string SummaryText
	{
		get
		{
			return _summaryText;
		}
		set
		{
			SetProperty(ref _summaryText, value, "SummaryText");
		}
	}

	public string DetailText
	{
		get
		{
			return _detailText;
		}
		set
		{
			SetProperty(ref _detailText, value, "DetailText");
		}
	}

	public string StatusText
	{
		get
		{
			return _statusText;
		}
		set
		{
			if (SetProperty(ref _statusText, value, "StatusText"))
			{
				OnPropertyChanged("StatusForeground");
				OnPropertyChanged("StatusBackground");
				OnPropertyChanged("StatusBorderBrush");
			}
		}
	}

	public Brush StatusForeground
	{
		get
		{
			string statusText = StatusText;
			if (!(statusText == "失败"))
			{
				if (statusText == "成功")
				{
					return new SolidColorBrush(Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue));
				}
				return new SolidColorBrush(Color.FromRgb(96, 98, 102));
			}
			return new SolidColorBrush(Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue));
		}
	}

	public Brush StatusBackground
	{
		get
		{
			string statusText = StatusText;
			if (!(statusText == "失败"))
			{
				if (statusText == "成功")
				{
					return new SolidColorBrush(Color.FromRgb(103, 194, 58));
				}
				return new SolidColorBrush(Color.FromRgb(248, 250, 253));
			}
			return new SolidColorBrush(Color.FromRgb(245, 108, 108));
		}
	}

	public Brush StatusBorderBrush
	{
		get
		{
			string statusText = StatusText;
			if (!(statusText == "失败"))
			{
				if (statusText == "成功")
				{
					return new SolidColorBrush(Color.FromRgb(103, 194, 58));
				}
				return new SolidColorBrush(Color.FromRgb(227, 234, 244));
			}
			return new SolidColorBrush(Color.FromRgb(245, 108, 108));
		}
	}
}
