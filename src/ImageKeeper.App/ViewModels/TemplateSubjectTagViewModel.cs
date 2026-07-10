namespace ImageKeeper.App.ViewModels;

public sealed class TemplateSubjectTagViewModel
{
    private TemplateSubjectTagViewModel(long id, string text, bool isEnabled, bool isAddButton, bool isInput)
    {
        Id = id;
        Text = text;
        IsEnabled = isEnabled;
        IsAddButton = isAddButton;
        IsInput = isInput;
    }

    public long Id { get; }

    public string Text { get; }

    public bool IsEnabled { get; }

    public bool IsAddButton { get; }

    public bool IsInput { get; }

    public bool IsTag => !IsAddButton && !IsInput;

    public string DisplayText => IsEnabled ? Text : $"{Text}（停用）";

    public string Background => IsEnabled ? "#F4F4F5" : "#F3F4F6";

    public string BorderBrush => IsEnabled ? "#E4E7ED" : "#D1D5DB";

    public string Foreground => IsEnabled ? "#303133" : "#909399";

    public static TemplateSubjectTagViewModel Create(long id, string text, bool isEnabled)
    {
        return new TemplateSubjectTagViewModel(id, text, isEnabled, false, false);
    }

    public static TemplateSubjectTagViewModel CreateTag(string text)
    {
        return new TemplateSubjectTagViewModel(0, text, true, false, false);
    }

    public static TemplateSubjectTagViewModel CreateAddButton()
    {
        return new TemplateSubjectTagViewModel(0, string.Empty, true, true, false);
    }

    public static TemplateSubjectTagViewModel CreateInput()
    {
        return new TemplateSubjectTagViewModel(0, string.Empty, true, false, true);
    }
}
