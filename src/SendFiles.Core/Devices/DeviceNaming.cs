namespace SendFiles.Core.Devices;

/// <summary>Human name for a pairing request from the User-Agent family only (never the raw string).</summary>
public static class DeviceNaming
{
    public static string FromUserAgent(string? userAgent)
    {
        var ua = userAgent ?? string.Empty;
        var device =
            Has(ua, "iPad") ? "iPad" :
            Has(ua, "iPhone") ? "iPhone" :
            Has(ua, "Android") ? "Android" :
            Has(ua, "Macintosh") && Has(ua, "Mobile") ? "iPad" :   // iPadOS requests the desktop site
            Has(ua, "Macintosh") ? "Mac" :
            Has(ua, "Windows") ? "Windows" :
            Has(ua, "Linux") ? "Linux" :
            "Device";

        var browser =
            Has(ua, "EdgA") || Has(ua, "EdgiOS") || Has(ua, "Edg/") ? "Edge" :
            Has(ua, "OPR") || Has(ua, "OPiOS") ? "Opera" :
            Has(ua, "SamsungBrowser") ? "Samsung Internet" :
            Has(ua, "YaBrowser") ? "Yandex" :
            Has(ua, "FxiOS") || Has(ua, "Firefox") ? "Firefox" :
            Has(ua, "CriOS") || Has(ua, "Chrome") ? "Chrome" :
            Has(ua, "Safari") ? "Safari" :
            null;

        return browser is null ? device : $"{device} ({browser})";
    }

    private static bool Has(string ua, string token) => ua.Contains(token, StringComparison.Ordinal);
}
