using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using SendFiles.App.Icons;
using SendFiles.App.Resources;

namespace SendFiles.App.Tray;

internal static class TrayIconFactory
{
    public static TaskbarIcon Create(MainWindow window, Action quit)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = Strings.Tray_Open, Command = new RelayCommand(window.ShowFromTray) });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = Strings.Tray_Quit, Command = new RelayCommand(quit) });

        var tray = new TaskbarIcon
        {
            Icon = RenderIcon(),
            ToolTipText = Strings.AppName,
            ContextMenu = menu,
            MenuActivation = PopupActivationMode.RightClick,
            LeftClickCommand = new RelayCommand(window.ShowFromTray),
            NoLeftClickDelay = true,
        };
        tray.ForceCreate(enablesEfficiencyMode: false);
        return tray;
    }

    /// <summary>Renders the Lucide "send" glyph to a 32 px bitmap: white glyph on the accent circle, readable on any taskbar.</summary>
    private static System.Drawing.Icon RenderIcon()
    {
        const int size = 32;
        var accent = (Brush)Application.Current.Resources["Brush.Accent"];
        var glyph = new Path
        {
            Data = LucideIcons.Send,
            Stroke = (Brush)Application.Current.Resources["Brush.Qr.Background"],
            StrokeThickness = 2.2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(7, 8, 8, 7),
        };
        var root = new Grid { Width = size, Height = size };
        root.Children.Add(new Ellipse { Fill = accent });
        root.Children.Add(glyph);
        root.Measure(new Size(size, size));
        root.Arrange(new Rect(0, 0, size, size));

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new System.IO.MemoryStream();
        encoder.Save(png);

        // ICO container with one PNG image (supported since Windows Vista).
        using var ico = new System.IO.MemoryStream();
        using (var writer = new System.IO.BinaryWriter(ico, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((short)0); // reserved
            writer.Write((short)1); // type: icon
            writer.Write((short)1); // count
            writer.Write((byte)size);
            writer.Write((byte)size);
            writer.Write((byte)0); // palette
            writer.Write((byte)0); // reserved
            writer.Write((short)1); // planes
            writer.Write((short)32); // bits per pixel
            writer.Write((int)png.Length);
            writer.Write(22); // offset: 6-byte header + 16-byte entry
            writer.Write(png.ToArray());
        }

        ico.Position = 0;
        return new System.Drawing.Icon(ico);
    }
}
