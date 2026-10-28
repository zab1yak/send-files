using System.Net.NetworkInformation;

namespace SendFiles.Core.Networking;

/// <summary>
/// Raises <see cref="Changed"/> once after a burst of OS network events settles
/// (Wi-Fi switching fires many address-changed events in a row).
/// </summary>
public sealed class NetworkChangeMonitor : IDisposable
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(1.5);

    private readonly ITimer _timer;
    private readonly TimeSpan _debounce;
    private bool _subscribed;

    public NetworkChangeMonitor(TimeProvider time, TimeSpan? debounce = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        _debounce = debounce ?? DefaultDebounce;
        _timer = time.CreateTimer(_ => Changed?.Invoke(this, EventArgs.Empty), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event EventHandler? Changed;

    public void Start()
    {
        if (_subscribed)
        {
            return;
        }

        NetworkChange.NetworkAddressChanged += OnSystemEvent;
        NetworkChange.NetworkAvailabilityChanged += OnSystemEvent;
        _subscribed = true;
    }

    /// <summary>Restarts the debounce window; public so tests and the host can trigger a re-check.</summary>
    public void Signal() => _timer.Change(_debounce, Timeout.InfiniteTimeSpan);

    public void Dispose()
    {
        if (_subscribed)
        {
            NetworkChange.NetworkAddressChanged -= OnSystemEvent;
            NetworkChange.NetworkAvailabilityChanged -= OnSystemEvent;
            _subscribed = false;
        }

        _timer.Dispose();
    }

    private void OnSystemEvent(object? sender, EventArgs e) => Signal();
}
