using System.Windows;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SendFiles.App.Theming;
using SendFiles.App.Tray;
using SendFiles.Core.Hosting;
using Serilog;

namespace SendFiles.App;

public partial class App : Application
{
    private readonly IHost _host;
    private TaskbarIcon? _tray;

    internal App(IHost host) => _host = host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Initialize(this);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            args.Handled = true;
        };

        await _host.StartAsync().ConfigureAwait(true);

        var window = _host.Services.GetRequiredService<MainWindow>();
        _tray = TrayIconFactory.Create(window, Quit);

        var instance = _host.Services.GetRequiredService<SingleInstance>();
        instance.ArgumentsReceived += (_, _) => Dispatcher.BeginInvoke(window.ShowFromTray);
        instance.Listen();

        window.Show();
        var server = _host.Services.GetRequiredService<SendFilesServer>();
        await server.StartAsync().ConfigureAwait(true);

        if (DevScreenshot.Folder is { } folder)
        {
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            DevScreenshot.Capture((FrameworkElement)window.Content, folder, $"pc-{server.State.Status.ToString().ToLowerInvariant()}-{(ThemeManager.IsDark ? "dark" : "light")}");
            Quit();
        }
    }

    private async void Quit()
    {
        _tray?.Dispose();
        // The container disposes the server; stop it first so connections close cleanly.
        await _host.Services.GetRequiredService<SendFilesServer>().StopAsync().ConfigureAwait(true);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            await _host.StopAsync(timeout.Token).ConfigureAwait(true);
        }

        Shutdown();
    }
}
