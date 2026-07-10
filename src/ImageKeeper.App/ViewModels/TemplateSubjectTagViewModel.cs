namespace ImageKeeper.App.ViewModels;

public sealed class TemplateSubjectTagViewModel
{
	public long Id { get; }

	public string Text { get; }

	public bool IsEnabled { get; }

	public bool IsAddButton { get; }

	public bool IsInput { get; }

	public bool IsTag => !IsAddButton && !IsInput;

	public string DisplayText
	{
		get
		{
			if (!IsEnabled)
			{
				return Text + "（停用）";
			}
			return Text;
		}
	}

	public string Background
	{
		get
		{
			if (!IsEnabled)
			{
				return "#F3F4F6";
			}
			return "#F4F4F5";
		}
	}

	public string BorderBrush
	{
		get
		{
			if (!IsEnabled)
			{
				return "#D1D5DB";
			}
			return "#E4E7ED";
		}
	}

	public string Foreground
	{
		get
		{
			if (!IsEnabled)
			{
				return "#909399";
			}
			return "#303133";
		}
	}

	private TemplateSubjectTagViewModel(long id, string text, bool isEnabled, bool isAddButton, bool isInput = false)
	{
		Id = id;
		Text = text;
		IsEnabled = isEnabled;
		IsAddButton = isAddButton;
		IsInput = isInput;
	}

	public static TemplateSubjectTagViewModel Create(long id, string text, bool isEnabled)
	{
		return new TemplateSubjectTagViewModel(id, text, isEnabled, isAddButton: false);
	}

	public static TemplateSubjectTagViewModel CreateTag(string text)
	{
		return Create(0L, text, isEnabled: true);
	}

	public static TemplateSubjectTagViewModel CreateInput()
	{
		return new TemplateSubjectTagViewModel(0L, string.Empty, isEnabled: true, isAddButton: false, isInput: true);
	}

	public static TemplateSubjectTagViewModel CreateAddButton()
	{
		return new TemplateSubjectTagViewModel(0L, string.Empty, isEnabled: true, isAddButton: true);
	}
}
