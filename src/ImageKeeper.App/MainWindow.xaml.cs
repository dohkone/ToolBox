using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageKeeper.App.ViewModels;
using ImageKeeper.Core.Services;
using ImageKeeper.Infrastructure.Services;
using ImageKeeper.PythonBridge;

namespace ImageKeeper.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel(
            CreateFolderScanService(),
            CreateWorkspaceService(),
            CreateWorkspaceStateService(),
            CreateProductSheetService());
        DataContext = _viewModel;
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAsync;
        await _viewModel.InitializeAsync();
    }

    private static IFolderScanService CreateFolderScanService()
    {
        return new FolderScanService();
    }

    private static IImageWorkspaceService CreateWorkspaceService()
    {
        return new ImageWorkspaceService();
    }

    private static IWorkspaceStateService CreateWorkspaceStateService()
    {
        return new WorkspaceStateService();
    }

    private static IProductSheetService CreateProductSheetService()
    {
        var pythonRunner = new PythonScriptRunner("python");
        var fillProductSheetScript = ResolveToolPath("fill_product_sheet.py");
        var buildSizeIndexScript = ResolveToolPath("build_size_index.py");
        var yingdaoLauncher = CreateYingdaoLauncher();
        return new ProductSheetService(pythonRunner, fillProductSheetScript, buildSizeIndexScript, yingdaoLauncher);
    }

    private static IYingdaoLauncher CreateYingdaoLauncher()
    {
        var appBase = AppContext.BaseDirectory;
        var scriptPath = Path.Combine(appBase, "tools", "node", "yingdao", "start_miaoshou.js");
        var configPath = Path.Combine(appBase, "config", "yingdao.json");
        return new YingdaoLauncher("node", scriptPath, configPath);
    }

    private static string ResolveToolPath(string fileName)
    {
        var appBase = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appBase, "tools", "python", "temu-product-sheet", fileName),
            Path.Combine(@"D:\temu_auto\tools", fileName),
            Path.Combine(@"D:\temu_auto", fileName)
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private async void WorkspaceContentBorder_OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not Border { DataContext: RootCardViewModel card })
        {
            return;
        }

        card.SetDropTarget(false);
        var files = GetDroppedImageFiles(e.Data);

        if (files.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "只能拖入图片文件到当前目录。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            e.Handled = true;
            return;
        }

        if (!card.CanAcceptDrop(files))
        {
            System.Windows.MessageBox.Show(this, "请先从左侧资源树选择一个文件夹。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            e.Handled = true;
            return;
        }

        try
        {
            await card.AddImageFilesAsync(files, showStatusMessage: true);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"添加图片失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        e.Handled = true;
    }

    private void WorkspaceContentBorder_OnDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        HandleWorkspaceDragState(sender, e);
    }

    private void WorkspaceContentBorder_OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        HandleWorkspaceDragState(sender, e);
    }

    private void WorkspaceContentBorder_OnDragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is Border { DataContext: RootCardViewModel card })
        {
            card.SetDropTarget(false);
        }

        e.Handled = true;
    }

    private void HandleWorkspaceDragState(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not Border { DataContext: RootCardViewModel card })
        {
            return;
        }

        var files = GetDroppedImageFiles(e.Data);
        var canAccept = card.CanAcceptDrop(files);
        card.SetDropTarget(canAccept);
        e.Effects = canAccept ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private static IReadOnlyList<string> GetDroppedImageFiles(System.Windows.IDataObject dataObject)
    {
        if (!dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return [];
        }

        var dropped = dataObject.GetData(System.Windows.DataFormats.FileDrop) as string[] ?? [];
        return dropped
            .Where(File.Exists)
            .Where(IsImageFile)
            .ToArray();
    }

    private static bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp" or ".jfif";
    }

    private void NestedScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var canScrollUp = e.Delta > 0 && scrollViewer.VerticalOffset > 0;
        var canScrollDown = e.Delta < 0 && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;

        if (canScrollUp || canScrollDown)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
            return;
        }

        var reroutedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };

        var parent = scrollViewer.Parent as UIElement;
        parent?.RaiseEvent(reroutedEvent);
        e.Handled = true;
    }
}
