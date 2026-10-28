namespace SendFiles.Core.Persistence;

/// <summary>
/// Resolves where Send Files keeps its state. Not <c>%LOCALAPPDATA%\SendFiles</c>: that folder belongs to
/// Velopack and is removed on uninstall. Dev builds get a separate folder so they never touch the installed app.
/// </summary>
public sealed class DataDirectory
{
    public const string EnvironmentVariable = "SENDFILES_DATA_DIR";

    public DataDirectory(string root, bool isDev)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
        IsDev = isDev;
    }

    public string Root { get; }

    public bool IsDev { get; }

    /// <summary>Suffix for machine-wide names (mutex, pipe) so a dev build can run next to the installed one.</summary>
    public string InstanceSuffix => IsDev ? "-dev" : string.Empty;

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string DevicesFile => Path.Combine(Root, "devices.json");

    public string HistoryFile => Path.Combine(Root, "history.json");

    public string UploadsDirectory => Path.Combine(Root, "uploads");

    public string LogsDirectory => Path.Combine(Root, "logs");

    public static DataDirectory Resolve(bool isDev, string? environmentOverride = null, string? localAppData = null)
    {
        environmentOverride ??= Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            return new DataDirectory(environmentOverride, isDev);
        }

        localAppData ??= Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var name = isDev ? "SendFiles.Data-dev" : "SendFiles.Data";
        return new DataDirectory(Path.Combine(localAppData, name), isDev);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(UploadsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
