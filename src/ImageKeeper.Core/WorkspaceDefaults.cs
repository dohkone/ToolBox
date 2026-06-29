using System.IO;

namespace ImageKeeper.Core;

public static class WorkspaceDefaults
{
    private static readonly string LegacyRoot = @"D:\temu_auto";

    private static string PackagedWorkspaceRoot => Path.Combine(AppContext.BaseDirectory, "data", "workspace");

    public static string DefaultOpenFolder => ResolveWorkspaceFolder("review");

    public static string DefaultBackupFolder => ResolveWorkspaceFolder("backup");

    public static string DefaultExcelFolder => ResolveWorkspaceFolder("excel");

    public static string DefaultSpBatchOutputFolder => ResolveWorkspaceFolder("assert");

    public static string DefaultTempFolder => ResolveWorkspaceFolder("temp");

    private static string ResolveWorkspaceFolder(string folderName)
    {
        if (Directory.Exists(PackagedWorkspaceRoot))
        {
            return Path.Combine(PackagedWorkspaceRoot, folderName);
        }

        return Path.Combine(LegacyRoot, folderName);
    }
}
