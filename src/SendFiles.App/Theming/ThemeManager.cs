using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace SendFiles.App.Theming;

/// <summary>Follows the Windows app theme (light/dark) and publishes brushes as dynamic resources.</summary>
internal static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsDark { get; private set; }

    public static void Initialize(Application app)
    {
        Apply(app);
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                app.Dispatcher.BeginInvoke(() => Apply(app));
            }
        };
    }

    private static void Apply(Application app)
    {
        IsDark = ReadSystemIsDark();
        var colors = IsDark ? Palette.Dark : Palette.Light;
        foreach (var (key, color) in colors)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            app.Resources[$"Brush.{key}"] = brush;
            app.Resources[$"Color.{key}"] = color;
        }
    }

    private static bool ReadSystemIsDark()
    {
#if DEBUG
        // SENDFILES_DEV_THEME=light|dark: screenshots of both themes without touching Windows settings.
        switch (Environment.GetEnvironmentVariable("SENDFILES_DEV_THEME"))
        {
            case "light":
                return false;
            case "dark":
                return true;
        }
#endif
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }
}
