using System.Net;
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
    private volatile bool _shutdown;

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
        if (_shutdown)
        {
            return;
        }

        ServerState? publish = null;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_shutdown)
            {
                return;
            }

            publish = await RebindAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        Publish(publish);
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopAppAsync().ConfigureAwait(false);
            _state = ServerState.Stopped;
        }
        finally
        {
            _gate.Release();
        }

        Publish(ServerState.Stopped);
    }

    /// <summary>Stops for good: later refreshes (queued or from network events) do nothing.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _shutdown = true;
        await StopAsync().ConfigureAwait(false);
        // _gate is not disposed: refreshes queued behind us must still be able to release it.
    }

    private async Task<ServerState?> RebindAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Current;
        var addresses = _addressSource(settings).Distinct().ToList();

        if (_state.Status == ServerStatus.Running && _state.Port == settings.Port && _state.HasSameAddresses(addresses))
        {
            // Same set, maybe a different best address: update the QR without cutting transfers.
            return _state.Addresses.SequenceEqual(addresses) ? null : SetState(_state with { Addresses = addresses });
        }

        await StopAppAsync().ConfigureAwait(false);
        _state = ServerState.Stopped; // never leave a stale "Running" behind if the restart fails

        if (addresses.Count == 0)
        {
            LogNoNetwork(_logger);
            return SetState(new ServerState(ServerStatus.NoNetwork, [], 0));
        }

        try
        {
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
            RememberPort(settings, port);
            LogListening(_logger, addresses.Count, port);
            return SetState(new ServerState(ServerStatus.Running, addresses, port));
        }
        catch (OperationCanceledException)
        {
            SetState(ServerState.Stopped);
            throw;
        }
        catch (Exception ex)
        {
            LogStartFailed(_logger, ex);
            return SetState(new ServerState(ServerStatus.Failed, [], 0, ex.GetType().Name));
        }
    }

    private void RememberPort(AppSettings settings, int port)
    {
        if (port == settings.Port)
        {
            return;
        }

        try
        {
            _settings.Update(s => s with { Port = port });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The server is running; only the preference for next time is lost.
            LogSettingsWriteFailed(_logger, ex);
        }
    }

    private ServerState SetState(ServerState state)
    {
        _state = state;
        return state;
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
        if (_app is not { } app)
        {
            return;
        }

        _app = null;
        try
        {
            using var timeout = new CancellationTokenSource(StopTimeout);
            await app.StopAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Open connections are cut; uploads resume by id.
        }
        finally
        {
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Raised outside the gate so a subscriber can call back into the supervisor.</summary>
    private void Publish(ServerState? state)
    {
        if (state is null)
        {
            return;
        }

        try
        {
            StateChanged?.Invoke(this, state);
        }
        catch (Exception ex)
        {
            LogSubscriberFailed(_logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Listening on {Count} address(es), port {Port}")]
    private static partial void LogListening(ILogger logger, int count, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No private Wi-Fi/Ethernet address; server not started")]
    private static partial void LogNoNetwork(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remember the chosen port")]
    private static partial void LogSettingsWriteFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Server state subscriber failed")]
    private static partial void LogSubscriberFailed(ILogger logger, Exception exception);

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
