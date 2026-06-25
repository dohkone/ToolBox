using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class ProductSheetService : IProductSheetService
{
    private readonly IPythonScriptRunner _scriptRunner;
    private readonly string _fillProductSheetScriptPath;
    private readonly string _buildSizeIndexScriptPath;
    private readonly IYingdaoLauncher? _yingdaoLauncher;

    public ProductSheetService(
        IPythonScriptRunner scriptRunner,
        string fillProductSheetScriptPath,
        string buildSizeIndexScriptPath,
        IYingdaoLauncher? yingdaoLauncher = null)
    {
        _scriptRunner = scriptRunner;
        _fillProductSheetScriptPath = fillProductSheetScriptPath;
        _buildSizeIndexScriptPath = buildSizeIndexScriptPath;
        _yingdaoLauncher = yingdaoLauncher;
    }

    public async Task<ProductSheetTask> GenerateAsync(string spRootFolder, CancellationToken cancellationToken = default)
    {
        var task = new ProductSheetTask
        {
            SpRootFolder = spRootFolder,
            Status = "Running",
            StartedAt = DateTime.Now
        };

        var exitCode = await _scriptRunner.RunAsync(_fillProductSheetScriptPath, ["--sp-dir", spRootFolder], cancellationToken);
        task.Status = exitCode == 0 ? "Completed" : "Failed";
        if (task.Status == "Completed" && _yingdaoLauncher is not null)
        {
            task.OutputPath = await _yingdaoLauncher.LaunchMiaoshouAsync(cancellationToken);
        }

        task.FinishedAt = DateTime.Now;
        return task;
    }

    public async Task RebuildSizeIndexAsync(CancellationToken cancellationToken = default)
    {
        await _scriptRunner.RunAsync(_buildSizeIndexScriptPath, [], cancellationToken);
    }
}
