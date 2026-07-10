using System.Threading.Tasks;
using System.Windows;

namespace ImageKeeper.App.ViewModels;

public sealed class GenerationPromptCardViewModel : ViewModelBase
{
	private string _title = string.Empty;

	private string _promptText = string.Empty;

	private string _metaText = string.Empty;

	private bool _isCopied;

	private int _copyFeedbackVersion;

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

	public string PromptText
	{
		get
		{
			return _promptText;
		}
		set
		{
			SetProperty(ref _promptText, value, "PromptText");
		}
	}

	public string MetaText
	{
		get
		{
			return _metaText;
		}
		set
		{
			SetProperty(ref _metaText, value, "MetaText");
		}
	}

	public bool IsCopied
	{
		get
		{
			return _isCopied;
		}
		private set
		{
			SetProperty(ref _isCopied, value, "IsCopied");
		}
	}

	public bool HasMeta => !string.IsNullOrWhiteSpace(MetaText);

	public RelayCommand CopyPromptCommand { get; }

	public GenerationPromptCardViewModel()
	{
		CopyPromptCommand = new RelayCommand(async delegate
		{
			if (!string.IsNullOrWhiteSpace(PromptText))
			{
				Clipboard.SetText(PromptText);
				int currentVersion = ++_copyFeedbackVersion;
				IsCopied = true;
				await Task.Delay(1200);
				if (currentVersion == _copyFeedbackVersion)
				{
					IsCopied = false;
				}
			}
		}, (object? _) => !string.IsNullOrWhiteSpace(PromptText));
	}
}
