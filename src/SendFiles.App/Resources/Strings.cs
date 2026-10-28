using System.Globalization;
using System.Resources;

namespace SendFiles.App.Resources;

/// <summary>
/// Typed access to Strings.resx. Written by hand because the WPF markup pass runs before resgen;
/// LocalizationTests checks that every resx key has a property here and that ru/en keys match.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Property names mirror resx keys.")]
public static class Strings
{
    private static readonly ResourceManager Manager = new("SendFiles.App.Resources.Strings", typeof(Strings).Assembly);

    public static string AppName => Get(nameof(AppName));

    public static string Status_Waiting => Get(nameof(Status_Waiting));

    public static string Status_Starting => Get(nameof(Status_Starting));

    public static string Status_NoNetwork => Get(nameof(Status_NoNetwork));

    public static string Status_NoNetworkHint => Get(nameof(Status_NoNetworkHint));

    public static string Status_Failed => Get(nameof(Status_Failed));

    public static string Status_FailedHint => Get(nameof(Status_FailedHint));

    public static string ScanHint => Get(nameof(ScanHint));

    public static string AddressHint => Get(nameof(AddressHint));

    public static string CopyAddress => Get(nameof(CopyAddress));

    public static string Copied => Get(nameof(Copied));

    public static string Retry => Get(nameof(Retry));

    public static string DropHint => Get(nameof(DropHint));

    public static string Window_Close => Get(nameof(Window_Close));

    public static string Window_Minimize => Get(nameof(Window_Minimize));

    public static string Tray_Open => Get(nameof(Tray_Open));

    public static string Tray_Quit => Get(nameof(Tray_Quit));

    public static string Tray_Tooltip => Get(nameof(Tray_Tooltip));

    public static string Qr_Name => Get(nameof(Qr_Name));

    public static string Format(string template, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, template, args);

    private static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
