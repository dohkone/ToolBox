using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageKeeper.Core.Models;
using ImageKeeper.Core.Services;

namespace ImageKeeper.PythonBridge;

public sealed class ProductSheetService : IProductSheetService
{
	private readonly IPythonScriptRunner _scriptRunner;

	private readonly string _fillProductSheetScriptPath;

	private readonly string _buildSizeIndexScriptPath;

	public ProductSheetService(IPythonScriptRunner scriptRunner, string fillProductSheetScriptPath, string buildSizeIndexScriptPath)
	{
		_scriptRunner = scriptRunner;
		_fillProductSheetScriptPath = fillProductSheetScriptPath;
		_buildSizeIndexScriptPath = buildSizeIndexScriptPath;
	}

	public async Task<ProductSheetTask> GenerateAsync(string spRootFolder, IReadOnlyList<string>? sizes = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		ProductSheetTask task = new ProductSheetTask
		{
			SpRootFolder = spRootFolder,
			Status = "Running",
			StartedAt = DateTime.Now
		};
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToolBox", "output", "products");
			Directory.CreateDirectory(text);
			string productsJsonPath = Path.Combine(text, Path.GetFileName(spRootFolder) + ".product.json");
			List<string> list = new List<string>
			{
				"--sp-dir",
				spRootFolder,
				"--product-id",
				Path.GetFileName(spRootFolder),
				"--output-dir",
				text,
				"--products-json",
				productsJsonPath
			};
			if (sizes != null && sizes.Count > 0)
			{
				list.Add("--sizes");
				list.AddRange(sizes.Where((string size) => !string.IsNullOrWhiteSpace(size)));
			}
			task.Status = ((await _scriptRunner.RunAsync(_fillProductSheetScriptPath, list, cancellationToken) == 0) ? "Completed" : "Failed");
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

	public async Task RebuildSizeIndexAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		await _scriptRunner.RunAsync(_buildSizeIndexScriptPath, Array.Empty<string>(), cancellationToken);
	}
}
