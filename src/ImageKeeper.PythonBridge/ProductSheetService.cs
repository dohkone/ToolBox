using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class ProductSheetService : IProductSheetService
{
    private readonly IPythonScriptRunner _scriptRunner;
    private readonly string _fillProductSheetScriptPath;
    private readonly string _buildSizeIndexScriptPath;

    public ProductSheetService(
        IPythonScriptRunner scriptRunner,
        string fillProductSheetScriptPath,
        string buildSizeIndexScriptPath)
    {
        _scriptRunner = scriptRunner;
        _fillProductSheetScriptPath = fillProductSheetScriptPath;
        _buildSizeIndexScriptPath = buildSizeIndexScriptPath;
    }

    public async Task<ProductSheetTask> GenerateAsync(string spRootFolder, CancellationToken cancellationToken = default)
    {
        var task = new ProductSheetTask
        {
            SpRootFolder = spRootFolder,
            Status = "Running",
            StartedAt = DateTime.Now
        };

        try
        {
            var outputDir = Path.Combine(AppContext.BaseDirectory, "output", "products");
            Directory.CreateDirectory(outputDir);
            var productsJsonPath = Path.Combine(outputDir, $"{Path.GetFileName(spRootFolder)}.product.json");
            var exitCode = await _scriptRunner.RunAsync(
                _fillProductSheetScriptPath,
                ["--sp-dir", spRootFolder, "--output-dir", outputDir, "--products-json", productsJsonPath],
                cancellationToken);
            task.Status = exitCode == 0 ? "Completed" : "Failed";
            if (task.Status == "Completed")
            {
                task.ProductsJsonPath = productsJsonPath;
            }
        }
        catch (Exception ex)
        {
            task.Status = "Failed";
            task.ErrorMessage = ex.Message;
            throw;
        }

        task.FinishedAt = DateTime.Now;
        return task;
    }

    public async Task RebuildSizeIndexAsync(CancellationToken cancellationToken = default)
    {
        await _scriptRunner.RunAsync(_buildSizeIndexScriptPath, [], cancellationToken);
    }
}
