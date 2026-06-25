namespace ImageKeeper.Core.Utilities;

public static class SpRootResolver
{
    public static string? Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var current = new DirectoryInfo(path);
        if (!current.Exists && current.Parent is not null)
        {
            current = current.Parent;
        }

        while (current is not null)
        {
            if (current.Name.StartsWith("SP", StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
