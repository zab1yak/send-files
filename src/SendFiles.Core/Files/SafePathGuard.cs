namespace SendFiles.Core.Files;

/// <summary>
/// Last line of defence for writes: the final path must be a direct child of the receive folder,
/// and no folder we write through may be a symlink or junction. Cloud placeholders (OneDrive) are
/// reparse points too, but not links, so they are allowed.
/// </summary>
public static class SafePathGuard
{
    /// <summary>Full path of <paramref name="fileName"/> inside <paramref name="folder"/>, or an exception.</summary>
    public static string ChildPath(string folder, string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var full = Path.GetFullPath(Path.Combine(root, fileName));

        if (!string.Equals(Path.GetDirectoryName(full), root, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(full), fileName, StringComparison.Ordinal))
        {
            throw new UnsafePathException("Resolved path escapes the target folder.");
        }

        return full;
    }

    /// <summary>Creates the folder if needed and makes sure it is a real directory, not a link.</summary>
    public static string EnsureRealDirectory(string folder)
    {
        var full = Path.GetFullPath(folder);
        Directory.CreateDirectory(full);
        if (new DirectoryInfo(full).LinkTarget is not null)
        {
            throw new UnsafePathException("Folder is a symbolic link or junction.");
        }

        return full;
    }

    /// <summary>True if the path exists and is a symlink/junction (never follow these).</summary>
    public static bool IsLink(string path)
    {
        if (File.Exists(path))
        {
            return new FileInfo(path).LinkTarget is not null;
        }

        return Directory.Exists(path) && new DirectoryInfo(path).LinkTarget is not null;
    }
}

public sealed class UnsafePathException : IOException
{
    public UnsafePathException()
    {
    }

    public UnsafePathException(string message)
        : base(message)
    {
    }

    public UnsafePathException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
