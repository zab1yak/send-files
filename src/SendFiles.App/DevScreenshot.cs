using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SendFiles.App;

/// <summary>
/// Debug-only: SENDFILES_DEV_SCREENSHOT=&lt;folder&gt; renders the window to PNG at 100 % and 150 % scale
/// (WPF re-renders vectors at that DPI, as on a scaled display) without capturing the desktop behind it.
/// </summary>
internal static class DevScreenshot
{
    public static string? Folder =>
#if DEBUG
        Environment.GetEnvironmentVariable("SENDFILES_DEV_SCREENSHOT") is { Length: > 0 } dir ? dir : null;
#else
        null;
#endif

    public static void Capture(FrameworkElement root, string folder, string name)
    {
        Directory.CreateDirectory(folder);
        foreach (var (scale, suffix) in new[] { (1.0, "100"), (1.5, "150") })
        {
            root.UpdateLayout();
            // The visual is rendered at its layout offset, so the outer margin (shadow room) must fit too.
            var width = (int)Math.Ceiling((root.ActualWidth + root.Margin.Left + root.Margin.Right) * scale);
            var height = (int)Math.Ceiling((root.ActualHeight + root.Margin.Top + root.Margin.Bottom) * scale);
            var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(System.IO.Path.Combine(folder, $"{name}-{suffix}.png"));
            encoder.Save(file);
        }
    }
}
