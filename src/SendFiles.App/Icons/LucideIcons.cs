using System.Windows.Media;

namespace SendFiles.App.Icons;

/// <summary>
/// Path data from Lucide (https://lucide.dev, ISC license; see THIRD_PARTY_NOTICES.md).
/// 24×24 viewbox, drawn as a 2 px round-capped stroke.
/// </summary>
internal static class LucideIcons
{
    public static readonly Geometry Send = Parse(
        "M14.536 21.686a.5 .5 0 0 0 .937-.024l6.5-19a.496 .496 0 0 0-.635-.635l-19 6.5a.5 .5 0 0 0-.024.937l7.93 3.18a2 2 0 0 1 1.112 1.11z M21.854 2.147 10.914 13.086");

    public static readonly Geometry X = Parse("M18 6 6 18 M6 6 18 18");

    public static readonly Geometry Minus = Parse("M5 12h14");

    public static readonly Geometry Copy = Parse(
        "M10 8h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H10a2 2 0 0 1-2-2V10a2 2 0 0 1 2-2z M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2");

    public static readonly Geometry Check = Parse("M20 6 9 17l-5-5");

    public static readonly Geometry WifiOff = Parse(
        "M12 20h.01 M8.5 16.429a5 5 0 0 1 7 0 M5 12.859a10 10 0 0 1 5.17-2.69 M19 12.859a10 10 0 0 0-2.007-1.523 M2 8.82a15 15 0 0 1 4.177-2.643 M22 8.82a15 15 0 0 0-11.288-3.764 M2 2l20 20");

    public static readonly Geometry RotateCw = Parse(
        "M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8 M21 3v5h-5");

    private static Geometry Parse(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}
