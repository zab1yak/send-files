using Microsoft.Extensions.Time.Testing;
using SendFiles.Core.Networking;

namespace SendFiles.Tests.Networking;

public class NetworkChangeMonitorTests
{
    [Fact]
    public void Burst_of_events_raises_once_after_debounce()
    {
        var time = new FakeTimeProvider();
        using var monitor = new NetworkChangeMonitor(time, TimeSpan.FromSeconds(1.5));
        var count = 0;
        monitor.Changed += (_, _) => count++;

        monitor.Signal();
        time.Advance(TimeSpan.FromSeconds(1));
        monitor.Signal();
        time.Advance(TimeSpan.FromSeconds(1));
        monitor.Signal();

        Assert.Equal(0, count);
        time.Advance(TimeSpan.FromSeconds(1.5));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Separate_changes_raise_separately()
    {
        var time = new FakeTimeProvider();
        using var monitor = new NetworkChangeMonitor(time, TimeSpan.FromSeconds(1));
        var count = 0;
        monitor.Changed += (_, _) => count++;

        monitor.Signal();
        time.Advance(TimeSpan.FromSeconds(2));
        monitor.Signal();
        time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(2, count);
    }
}
