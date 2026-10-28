using System.Windows.Media;

namespace SendFiles.App.Theming;

/// <summary>
/// The single source of every colour in the PC app (both themes). XAML references the brush keys;
/// <see cref="ThemeManager"/> fills them from here.
/// </summary>
internal static class Palette
{
    public static readonly IReadOnlyDictionary<string, Color> Dark = new Dictionary<string, Color>
    {
        ["Window.Background"] = Rgb(0x1C, 0x1C, 0x1E),
        ["Window.Border"] = Argb(0x26, 0xFF, 0xFF, 0xFF),
        ["Surface"] = Rgb(0x2C, 0x2C, 0x2E),
        ["Surface.Hover"] = Rgb(0x3A, 0x3A, 0x3C),
        ["Surface.Pressed"] = Rgb(0x48, 0x48, 0x4A),
        ["Separator"] = Argb(0x1F, 0xFF, 0xFF, 0xFF),
        ["Text.Primary"] = Rgb(0xF5, 0xF5, 0xF7),
        ["Text.Secondary"] = Rgb(0x98, 0x98, 0x9D),
        ["Text.Tertiary"] = Rgb(0x63, 0x63, 0x66),
        ["Accent"] = Rgb(0x0A, 0x84, 0xFF),
        ["Accent.Soft"] = Argb(0x33, 0x0A, 0x84, 0xFF),
        ["Status.Ok"] = Rgb(0x30, 0xD1, 0x58),
        ["Status.Warning"] = Rgb(0xFF, 0x9F, 0x0A),
        ["Status.Error"] = Rgb(0xFF, 0x45, 0x3A),
        ["Shadow"] = Rgb(0x00, 0x00, 0x00),
        // QR stays dark-on-white in both themes: phone cameras read it most reliably.
        ["Qr.Foreground"] = Rgb(0x00, 0x00, 0x00),
        ["Qr.Background"] = Rgb(0xFF, 0xFF, 0xFF),
    };

    public static readonly IReadOnlyDictionary<string, Color> Light = new Dictionary<string, Color>
    {
        ["Window.Background"] = Rgb(0xF5, 0xF5, 0xF7),
        ["Window.Border"] = Argb(0x1A, 0x00, 0x00, 0x00),
        ["Surface"] = Rgb(0xFF, 0xFF, 0xFF),
        ["Surface.Hover"] = Rgb(0xE8, 0xE8, 0xED),
        ["Surface.Pressed"] = Rgb(0xD8, 0xD8, 0xDD),
        ["Separator"] = Argb(0x1F, 0x00, 0x00, 0x00),
        ["Text.Primary"] = Rgb(0x1D, 0x1D, 0x1F),
        ["Text.Secondary"] = Rgb(0x6E, 0x6E, 0x73),
        ["Text.Tertiary"] = Rgb(0xAE, 0xAE, 0xB2),
        ["Accent"] = Rgb(0x00, 0x7A, 0xFF),
        ["Accent.Soft"] = Argb(0x26, 0x00, 0x7A, 0xFF),
        ["Status.Ok"] = Rgb(0x34, 0xC7, 0x59),
        ["Status.Warning"] = Rgb(0xFF, 0x95, 0x00),
        ["Status.Error"] = Rgb(0xFF, 0x3B, 0x30),
        ["Shadow"] = Rgb(0x00, 0x00, 0x00),
        ["Qr.Foreground"] = Rgb(0x00, 0x00, 0x00),
        ["Qr.Background"] = Rgb(0xFF, 0xFF, 0xFF),
    };

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    private static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
}
