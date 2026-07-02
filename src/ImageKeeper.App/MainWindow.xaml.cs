using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using System.Windows.Interop;
using ImageKeeper.App.ViewModels;
using ImageKeeper.Core.Services;
using ImageKeeper.Infrastructure.Services;
using ImageKeeper.PythonBridge;

namespace ImageKeeper.App;

public partial class MainWindow : Window
{
    private const int DwmaBorderColor = 34;
    private const int DwmaCaptionColor = 35;
    private const int DwmaTextColor = 36;

    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel(
            CreateFolderScanService(),
            CreateWorkspaceService(),
            CreateWorkspaceStateService(),
            CreateAppSettingsService(),
            CreateProductSheetService(),
            CreateTemplateGenerationService(),
            CreateSpBatchService(),
            CreateMiaoshouPublishService(),
            CreateAutoPublishStateService());
        DataContext = _viewModel;
        SizeToCurrentWorkArea();
        Loaded += OnLoadedAsync;
        SourceInitialized += OnSourceInitialized;
        PreviewKeyDown += OnPreviewKeyDownAsync;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref uint pvAttribute, int cbAttribute);

    private void SizeToCurrentWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        var targetWidth = Math.Min(Width, Math.Max(MinWidth, workArea.Width - 32));
        var targetHeight = Math.Min(Height, Math.Max(MinHeight, workArea.Height - 32));

        Width = targetWidth;
        Height = targetHeight;
        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAsync;
        await _viewModel.InitializeAsync();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyWindowChromeTheme();
    }

    private async void OnPreviewKeyDownAsync(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.F5)
        {
            return;
        }

        e.Handled = true;
        await _viewModel.RefreshCurrentPageAsync();
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

    private static IAppSettingsService CreateAppSettingsService()
    {
        return new AppSettingsService();
    }

    private static IProductSheetService CreateProductSheetService()
    {
        var pythonExePath = ResolvePythonExecutable();
        var pythonRunner = new PythonScriptRunner(pythonExePath);
        var fillProductSheetScript = ResolveToolPath("temu-product-sheet", "fill_product_sheet.py");
        var buildSizeIndexScript = ResolveToolPath("temu-product-sheet", "build_size_index.py");
        return new ProductSheetService(pythonRunner, fillProductSheetScript, buildSizeIndexScript);
    }

    private static ITemplateGenerationService CreateTemplateGenerationService()
    {
        var scriptPath = ResolveToolPath("template-random-generate", "random_generate_from_template.py");
        return new TemplateGenerationService(ResolvePythonExecutable(), scriptPath);
    }

    private static ISpBatchService CreateSpBatchService()
    {
        var scriptPath = ResolveToolPath("sp-batch", "SP_Batch.py");
        return new SpBatchService(ResolvePythonExecutable(), scriptPath);
    }

    private static IMiaoshouPublishService CreateMiaoshouPublishService()
    {
        var appBase = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appBase, "tools", "node", "miaoshou-playwright"),
            Path.Combine(@"D:\new_project\tools\node\miaoshou-playwright")
        };
        var workingDirectory = candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
        return new MiaoshouPublishService(ResolveNodeExecutable(), workingDirectory);
    }

    private static IAutoPublishStateService CreateAutoPublishStateService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var databasePath = Path.Combine(localAppData, "ToolBox", "toolbox.db");
        MigrateLegacyDatabaseIfNeeded(databasePath);
        return new AutoPublishStateService(databasePath);
    }

    private static void MigrateLegacyDatabaseIfNeeded(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            return;
        }

        var legacyDatabasePath = Path.Combine(AppContext.BaseDirectory, "data", "toolbox.db");
        if (!File.Exists(legacyDatabasePath))
        {
            return;
        }

        var targetDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        File.Copy(legacyDatabasePath, databasePath, overwrite: false);
    }

    private static string ResolveToolPath(string toolFolder, string fileName)
    {
        var appBase = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appBase, "tools", "python", toolFolder, fileName),
            Path.Combine(@"D:\new_project\tools\python", toolFolder, fileName),
            Path.Combine(@"D:\temu_auto\tools", fileName),
            Path.Combine(@"D:\temu_auto", fileName)
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static string ResolvePythonExecutable()
    {
        var appBase = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appBase, "runtime", "python", "python.exe"),
            Path.Combine(appBase, "python", "python.exe"),
            "python"
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[^1];
    }

    private static string ResolveNodeExecutable()
    {
        var appBase = AppContext.BaseDirectory;
        var bundledNode = Path.Combine(appBase, "runtime", "node", "node.exe");
        if (File.Exists(bundledNode))
        {
            return bundledNode;
        }

        var legacyBundledNode = Path.Combine(appBase, "node", "node.exe");
        if (File.Exists(legacyBundledNode))
        {
            return legacyBundledNode;
        }

        throw new FileNotFoundException(
            "便携包缺少 Node.js 运行时，请确认 runtime\\node\\node.exe 已随安装包一起复制。",
            bundledNode);
    }

    private async void WorkspaceContentBorder_OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!TryGetRootCardFromSender(sender, out var card))
        {
            return;
        }

        card.SetDropTarget(false);
        var files = GetDroppedImageFiles(e.Data);

        if (files.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "只能拖入图片文件到当前目录。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            e.Handled = true;
            return;
        }

        if (!card.CanAcceptDrop(files))
        {
            System.Windows.MessageBox.Show(this, "请先从左侧资源树选择一个文件夹。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            e.Handled = true;
            return;
        }

        try
        {
            await card.AddImageFilesAsync(files, showStatusMessage: true);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"添加图片失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
        if (TryGetRootCardFromSender(sender, out var card))
        {
            card.SetDropTarget(false);
        }

        e.Handled = true;
    }

    private void HandleWorkspaceDragState(object sender, System.Windows.DragEventArgs e)
    {
        if (!TryGetRootCardFromSender(sender, out var card))
        {
            return;
        }

        var files = GetDroppedImageFiles(e.Data);
        var canAccept = card.CanAcceptDrop(files);
        card.SetDropTarget(canAccept);
        e.Effects = canAccept ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private static bool TryGetRootCardFromSender(object sender, out RootCardViewModel card)
    {
        if (sender is FrameworkElement { DataContext: RootCardViewModel dataContext })
        {
            card = dataContext;
            return true;
        }

        card = null!;
        return false;
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

    private void GeneratedImageCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsFromButton(e.OriginalSource))
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: GeneratedImageResultCardViewModel card })
        {
            _viewModel.HandleGeneratedImageCardClick(card, e.ClickCount);
            e.Handled = true;
        }
    }

    private void SpBatchSourceCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsFromButton(e.OriginalSource))
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: GeneratedImageResultCardViewModel card })
        {
            _viewModel.HandleSpBatchSourceImageCardClick(card, e.ClickCount);
            e.Handled = true;
        }
    }

    private void ReviewImageCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || IsFromButton(e.OriginalSource))
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: ImageItemViewModel card }
            && card.OpenFileCommand.CanExecute(null))
        {
            card.OpenFileCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void PreviewImageArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || IsFromButton(e.OriginalSource))
        {
            return;
        }

        if (_viewModel.OpenCurrentPreviewImage())
        {
            e.Handled = true;
        }
    }

    private void SpBatchStagingArea_OnDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        HandleSpBatchStagingDragState(e);
    }

    private void SpBatchStagingArea_OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        HandleSpBatchStagingDragState(e);
    }

    private void SpBatchStagingArea_OnDragLeave(object sender, System.Windows.DragEventArgs e)
    {
        _viewModel.SetSpBatchStagingDropTarget(false);
        e.Handled = true;
    }

    private void SpBatchStagingArea_OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        _viewModel.SetSpBatchStagingDropTarget(false);
        var files = GetDroppedImageFiles(e.Data);
        if (files.Count > 0)
        {
            _viewModel.AddDroppedImagesToSpBatch(files);
        }

        e.Handled = true;
    }

    private void HandleSpBatchStagingDragState(System.Windows.DragEventArgs e)
    {
        var files = GetDroppedImageFiles(e.Data);
        var hasFiles = files.Count > 0;
        _viewModel.SetSpBatchStagingDropTarget(hasFiles);
        e.Effects = hasFiles ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void ApplyWindowChromeTheme()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var captionColor = ToColorRef(246, 246, 246);
            var borderColor = ToColorRef(230, 230, 230);
            var textColor = ToColorRef(51, 51, 51);

            DwmSetWindowAttribute(hwnd, DwmaCaptionColor, ref captionColor, sizeof(uint));
            DwmSetWindowAttribute(hwnd, DwmaBorderColor, ref borderColor, sizeof(uint));
            DwmSetWindowAttribute(hwnd, DwmaTextColor, ref textColor, sizeof(uint));
        }
        catch
        {
            // Ignore failures on systems that do not support caption color overrides.
        }
    }

    private static uint ToColorRef(byte red, byte green, byte blue)
    {
        return (uint)(red | (green << 8) | (blue << 16));
    }

    private static bool IsFromButton(object originalSource)
    {
        DependencyObject? current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is System.Windows.Controls.Button)
            {
                return true;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
