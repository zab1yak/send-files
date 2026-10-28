using SendFiles.Core.Networking;

namespace SendFiles.Core.Settings;

public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>Folder for received files; <c>null</c> means the default (Downloads\Send Files).</summary>
    public string? ReceiveFolder { get; init; }

    public int Port { get; init; } = PortSelector.DefaultPort;

    /// <summary>"auto", "ru" or "en".</summary>
    public string Language { get; init; } = "auto";

    public bool Autostart { get; init; }

    public bool AllowVirtualAdapters { get; init; }

    /// <summary>The first-run firewall explanation has been shown.</summary>
    public bool FirewallPromptShown { get; init; }

    public static readonly string[] Languages = ["auto", "ru", "en"];

    /// <summary>Replaces invalid values (hand-edited file, older version) with defaults.</summary>
    public AppSettings Normalize() => this with
    {
        Version = CurrentVersion,
        Port = PortSelector.IsValidPort(Port) ? Port : PortSelector.DefaultPort,
        Language = Languages.Contains(Language) ? Language : "auto",
        ReceiveFolder = string.IsNullOrWhiteSpace(ReceiveFolder) ? null : ReceiveFolder,
    };
}
