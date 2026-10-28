using System.Net;
using Microsoft.Extensions.Logging;
using SendFiles.Core.Networking;
using SendFiles.Core.Persistence;
using SendFiles.Core.Qr;
using SendFiles.Core.Settings;

namespace SendFiles.Core.Hosting;

/// <summary>
/// Host-agnostic facade: everything a desktop shell (WPF window now, Dynamic Island later) needs.
/// Events are raised on background threads; the host marshals them to its UI thread.
/// </summary>
public sealed partial class SendFilesServer : IAsyncDisposable
{
    private readonly ServerSupervisor _supervisor;
    private readonly NetworkChangeMonitor _monitor;
    private readonly SettingsStore _settings;
    private readonly ILogger _logger;
    private int _disposed;

    public SendFilesServer(SendFilesServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Data = options.Data;
        _settings = options.Settings;
        _logger = options.LoggerFactory.CreateLogger<SendFilesServer>();
        _monitor = new NetworkChangeMonitor(options.Time);
        var addressSource = options.AddressSource ?? DefaultAddresses;
        _supervisor = new ServerSupervisor(_settings, addressSource, options.LoggerFactory);

        _supervisor.StateChanged += (_, state) => StateChanged?.Invoke(this, state);
        _monitor.Changed += (_, _) => RefreshInBackground();
        _settings.Changed += OnSettingsChanged;
    }

    public event EventHandler<ServerState>? StateChanged;

    public DataDirectory Data { get; }

    public ServerState State => _supervisor.State;

    /// <summary>The URL encoded in the QR code, or <c>null</c> when the server is not reachable.</summary>
    public Uri? ConnectUri => State.BaseUri;

    public QrMatrix? CreateQr() => ConnectUri is { } uri ? QrMatrix.Create(uri.AbsoluteUri) : null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _monitor.Start();
        await _supervisor.RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Re-check the network now (e.g. the user pressed "Retry").</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default) => _supervisor.RefreshAsync(cancellationToken);

    public Task StopAsync() => _supervisor.StopAsync();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _settings.Changed -= OnSettingsChanged;
        _monitor.Dispose();
        await _supervisor.DisposeAsync().ConfigureAwait(false);
    }

    private static IReadOnlyList<IPAddress> DefaultAddresses(AppSettings settings) =>
        NetworkAdapterSelector.Select(SystemAdapters.Read(), settings.AllowVirtualAdapters)
            .Select(c => c.Address)
            .ToList();

    private void OnSettingsChanged(object? sender, AppSettings settings) => RefreshInBackground();

    private void RefreshInBackground() => _ = Task.Run(async () =>
    {
        try
        {
            await _supervisor.RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogRefreshFailed(_logger, ex);
        }
    });

    [LoggerMessage(Level = LogLevel.Error, Message = "Network refresh failed")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);
}

public sealed class SendFilesServerOptions
{
    public required DataDirectory Data { get; init; }

    public required SettingsStore Settings { get; init; }

    public required ILoggerFactory LoggerFactory { get; init; }

    public TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>Overrides adapter discovery (tests bind to loopback).</summary>
    public Func<AppSettings, IReadOnlyList<IPAddress>>? AddressSource { get; init; }
}
