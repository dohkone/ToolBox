namespace ImageKeeper.App.ViewModels;

public sealed class AutoPublishShopTagViewModel
{
	public string Text { get; }

	public bool IsAddButton { get; }

	public bool IsInput { get; }

	public bool IsTag => !IsAddButton && !IsInput;

	public string Background => "#F4F4F5";

	public string BorderBrush => "#E4E7ED";

	public string Foreground => "#303133";

	private AutoPublishShopTagViewModel(string text, bool isAddButton, bool isInput)
	{
		Text = text;
		IsAddButton = isAddButton;
		IsInput = isInput;
	}

	public static AutoPublishShopTagViewModel CreateTag(string text)
	{
		return new AutoPublishShopTagViewModel(text, isAddButton: false, isInput: false);
	}

	public static AutoPublishShopTagViewModel CreateInput()
	{
		return new AutoPublishShopTagViewModel(string.Empty, isAddButton: false, isInput: true);
	}

	public static AutoPublishShopTagViewModel CreateAddButton()
	{
		return new AutoPublishShopTagViewModel(string.Empty, isAddButton: true, isInput: false);
	}
}
