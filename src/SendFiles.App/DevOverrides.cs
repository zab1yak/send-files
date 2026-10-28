using System.Net;
using SendFiles.Core.Settings;

namespace SendFiles.App;

/// <summary>
/// Debug-only switches for screenshots and local checks on neutral data:
/// SENDFILES_DEV_ADDRESS binds to that address only (e.g. 127.0.0.1 — no firewall prompt; "none" = no network),
/// SENDFILES_DEV_NAME replaces the computer name. Release builds ignore both.
/// </summary>
internal static class DevOverrides
{
#if DEBUG
    public static string? ComputerName => Environment.GetEnvironmentVariable("SENDFILES_DEV_NAME") is { Length: > 0 } name ? name : null;

    public static Func<AppSettings, IReadOnlyList<IPAddress>>? AddressSource()
    {
        var value = Environment.GetEnvironmentVariable("SENDFILES_DEV_ADDRESS");
        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return _ => []; // simulate "no local network"
        }

        if (IPAddress.TryParse(value, out var address))
        {
            return _ => [address];
        }

        // Automated screenshot runs must never listen on the real LAN (that triggers the Windows firewall prompt).
        return Environment.GetEnvironmentVariable("SENDFILES_DEV_SCREENSHOT") is { Length: > 0 }
            ? _ => [IPAddress.Loopback]
            : null;
    }
#else
    public static string? ComputerName => null;

    public static Func<AppSettings, IReadOnlyList<IPAddress>>? AddressSource() => null;
#endif
}
