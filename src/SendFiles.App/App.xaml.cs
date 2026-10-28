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
    private int _quitting;

    internal App(IHost host) => _host = host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            args.Handled = true;
        };

        try
        {
            ThemeManager.Initialize(this);
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
        catch (Exception ex)
        {
            // Without a window or tray the user could not even close a half-started app.
            Log.Fatal(ex, "Startup failed");
            Shutdown(1);
        }
    }

    private async void Quit()
    {
        if (Interlocked.Exchange(ref _quitting, 1) != 0)
        {
            return;
        }

        try
        {
            _tray?.Dispose();
            // Disposing stops network monitoring first, then the server; the container's later dispose is a no-op.
            await _host.Services.GetRequiredService<SendFilesServer>().DisposeAsync().ConfigureAwait(true);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _host.StopAsync(timeout.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Shutdown was not clean");
        }
        finally
        {
            Shutdown();
        }
    }
}
