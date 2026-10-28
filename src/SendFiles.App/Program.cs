using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SendFiles.App.ViewModels;
using SendFiles.Core.Hosting;
using SendFiles.Core.Persistence;
using SendFiles.Core.Settings;
using Serilog;
using Serilog.Events;

namespace SendFiles.App;

internal static class Program
{
#if DEBUG
    private const bool IsDev = true;
#else
    private const bool IsDev = false;
#endif

    [STAThread]
    private static int Main(string[] args)
    {
        var data = DataDirectory.Resolve(IsDev);
        data.EnsureCreated();

        using var instance = SingleInstance.TryAcquire(data.InstanceSuffix);
        if (instance is null)
        {
            // Already running: hand our arguments (files to send, or nothing = "show window") to it.
            return SingleInstance.SendToPrimary(args, data.InstanceSuffix) ? 0 : 1;
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.File(
                System.IO.Path.Combine(data.LogsDirectory, "sendfiles-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        try
        {
            var settings = new SettingsStore(data.SettingsFile);
            ApplyLanguage(settings.Current.Language);

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                DisableDefaults = true,
                ContentRootPath = AppContext.BaseDirectory,
            });
            builder.Services.AddSerilog();
            builder.Services.AddSingleton(data);
            builder.Services.AddSingleton(settings);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton(sp => new SendFilesServer(new SendFilesServerOptions
            {
                Data = data,
                Settings = settings,
                LoggerFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>(),
                Time = sp.GetRequiredService<TimeProvider>(),
                AddressSource = DevOverrides.AddressSource(),
            }));
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddSingleton<MainWindow>();
            builder.Services.AddSingleton(instance);

            using var host = builder.Build();
            var app = new App(host);
            app.InitializeComponent();
            return app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled startup failure");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ApplyLanguage(string language)
    {
        if (language is "ru" or "en")
        {
            var culture = CultureInfo.GetCultureInfo(language);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
    }
}
