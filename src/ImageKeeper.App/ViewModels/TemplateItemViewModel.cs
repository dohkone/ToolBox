using System.IO;
using System.Windows;
using System.Windows.Input;
using ImageKeeper.App.Utilities;
using ImageKeeper.Core.Models;

namespace ImageKeeper.App.ViewModels;

public sealed class TemplateItemViewModel : ViewModelBase
{
    private bool _isSelected;
    private string? _subjectSummaryOverride;

    public TemplateItemViewModel(TemplateItemRecord model, string? subjectSummaryOverride = null)
    {
        Model = model;
        _subjectSummaryOverride = subjectSummaryOverride;
        OpenPreviewImageCommand = new RelayCommand(_ => OpenPreviewImage(), _ => HasPreviewImage);
    }

    public TemplateItemRecord Model { get; private set; }

    public long Id => Model.Id;

    public TemplateCategory Category => Model.Category;

    public string Name => Model.Name;

    public string Content => Model.Content;

    public string Subject => Model.Subject;

    public string PreviewImagePath => Model.PreviewImagePath;

    public ImageTemplateType ImageType => Model.ImageType;

    public string ImageTypeText => ImageType switch
    {
        ImageTemplateType.SceneImage => "场景图",
        ImageTemplateType.CompareImage => "对比图",
        _ => "主图"
    };

    public bool HasPreviewImage => !string.IsNullOrWhiteSpace(PreviewImagePath) && File.Exists(PreviewImagePath);

    public bool IsEnabled => Model.IsEnabled;

    public string EnabledText => IsEnabled ? "启用" : "停用";

    public string EnabledBadgeBackground => IsEnabled ? "#67C23A" : "#909399";

    public string EnabledBadgeBorder => IsEnabled ? "#67C23A" : "#909399";

    public string EnabledBadgeForeground => "White";

    public string CreatedAtText => Model.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

    public string UpdatedAtText => Model.UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

    public string ContentSummary
    {
        get
        {
            var text = Content.ReplaceLineEndings(" ").Trim();
            return text.Length <= 80 ? text : $"{text[..80]}...";
        }
    }

    public string SubjectSummary
    {
        get
        {
            var text = (_subjectSummaryOverride ?? Subject).ReplaceLineEndings(" ").Trim();
            return string.IsNullOrWhiteSpace(text)
                ? "-"
                : text.Length <= 60 ? text : $"{text[..60]}...";
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ICommand OpenPreviewImageCommand { get; }

    public void Update(TemplateItemRecord model, string? subjectSummaryOverride = null)
    {
        Model = model;
        _subjectSummaryOverride = subjectSummaryOverride;
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Content));
        OnPropertyChanged(nameof(Subject));
        OnPropertyChanged(nameof(PreviewImagePath));
        OnPropertyChanged(nameof(ImageType));
        OnPropertyChanged(nameof(ImageTypeText));
        OnPropertyChanged(nameof(HasPreviewImage));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(EnabledText));
        OnPropertyChanged(nameof(EnabledBadgeBackground));
        OnPropertyChanged(nameof(EnabledBadgeBorder));
        OnPropertyChanged(nameof(EnabledBadgeForeground));
        OnPropertyChanged(nameof(CreatedAtText));
        OnPropertyChanged(nameof(UpdatedAtText));
        OnPropertyChanged(nameof(ContentSummary));
        OnPropertyChanged(nameof(SubjectSummary));
    }

    private void OpenPreviewImage()
    {
        if (!HasPreviewImage)
        {
            return;
        }

        try
        {
            ShellOpenHelper.OpenFile(PreviewImagePath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"打开预览图失败：{ex.Message}",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
