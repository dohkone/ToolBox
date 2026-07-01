namespace ImageKeeper.Core.Models;

public sealed class ProductSheetTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string SpRootFolder { get; init; } = string.Empty;
    public string Mode { get; init; } = "Single";
    public string OutputPath { get; set; } = string.Empty;
    public string ProductsJsonPath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
