using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SendFiles.Core.Networking;
using SendFiles.Core.Settings;

namespace SendFiles.Core.Hosting;

/// <summary>
/// Owns the inner Kestrel app. Listens on concrete private addresses only (never 0.0.0.0) and is rebuilt
/// when the set of addresses changes. Services shared with the outer host survive a rebuild.
/// </summary>
public sealed partial class ServerSupervisor : IAsyncDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

    private readonly SettingsStore _settings;
    private readonly Func<AppSettings, IReadOnlyList<IPAddress>> _addressSource;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Action<IServiceCollection>? _shareServices;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WebApplication? _app;
    private volatile ServerState _state = ServerState.Stopped;
    private int _disposed;

    public ServerSupervisor(
        SettingsStore settings,
        Func<AppSettings, IReadOnlyList<IPAddress>> addressSource,
        ILoggerFactory loggerFactory,
        Action<IServiceCollection>? shareServices = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _addressSource = addressSource ?? throw new ArgumentNullException(nameof(addressSource));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _shareServices = shareServices;
        _logger = loggerFactory.CreateLogger<ServerSupervisor>();
    }

    public event EventHandler<ServerState>? StateChanged;

    public ServerState State => _state;

    /// <summary>Re-reads the network and (re)binds if the address set changed. Safe to call repeatedly.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = _settings.Current;
            var addresses = _addressSource(settings).Distinct().ToList();
            if (_state.SameBinding(addresses))
            {
                return;
            }

            await StopAppAsync().ConfigureAwait(false);

            if (addresses.Count == 0)
            {
                LogNoNetwork(_logger);
                Publish(new ServerState(ServerStatus.NoNetwork, [], 0));
                return;
            }

            var port = PortSelector.Choose(settings.Port, p => PortSelector.IsFree(addresses, p));
            if (port == 0)
            {
                port = PortSelector.Ephemeral(addresses[0]);
            }

            var app = Build(addresses, port);
            try
            {
                await app.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await app.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            _app = app;
            if (port != settings.Port)
            {
                _settings.Update(s => s with { Port = port });
            }

            LogListening(_logger, addresses.Count, port);
            Publish(new ServerState(ServerStatus.Running, addresses, port));
        }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidOperationException)
        {
            LogStartFailed(_logger, ex);
            Publish(new ServerState(ServerStatus.Failed, [], 0, ex.GetType().Name));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopAppAsync().ConfigureAwait(false);
            Publish(ServerState.Stopped);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private WebApplication Build(IReadOnlyList<IPAddress> addresses, int port)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(ServerSupervisor).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new ForwardingLoggerProvider(_loggerFactory));
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            foreach (var address in addresses)
            {
                options.Listen(address, port);
            }

            KestrelLimits.Apply(options.Limits);
        });

        _shareServices?.Invoke(builder.Services);

        var app = builder.Build();
        SendFilesEndpoints.Configure(app);
        return app;
    }

    private async Task StopAppAsync()
    {
        if (_app is null)
        {
            return;
        }

        using (var timeout = new CancellationTokenSource(StopTimeout))
        {
            try
            {
                await _app.StopAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Open connections are cut; uploads resume by id.
            }
        }

        await _app.DisposeAsync().ConfigureAwait(false);
        _app = null;
    }

    private void Publish(ServerState state)
    {
        _state = state;
        StateChanged?.Invoke(this, state);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Listening on {Count} address(es), port {Port}")]
    private static partial void LogListening(ILogger logger, int count, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No private Wi-Fi/Ethernet address; server not started")]
    private static partial void LogNoNetwork(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Server failed to start")]
    private static partial void LogStartFailed(ILogger logger, Exception exception);

    private sealed class ForwardingLoggerProvider(ILoggerFactory factory) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => factory.CreateLogger(categoryName);

        public void Dispose()
        {
            // The outer host owns the factory.
        }
    }
}
