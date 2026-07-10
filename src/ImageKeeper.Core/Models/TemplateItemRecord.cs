namespace ImageKeeper.Core.Models;

public sealed class TemplateItemRecord
{
    public long Id { get; init; }

    public TemplateCategory Category { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string PreviewImagePath { get; init; } = string.Empty;

    public ImageTemplateType ImageType { get; init; } = ImageTemplateType.MainImage;

    public int SortOrder { get; init; }

    public bool IsEnabled { get; init; } = true;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
