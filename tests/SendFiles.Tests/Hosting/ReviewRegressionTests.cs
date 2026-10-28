using System.IO.Pipes;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using SendFiles.Core.Hosting;
using SendFiles.Core.Settings;

namespace SendFiles.Tests.Hosting;

/// <summary>Regressions for the task 1 review (stale state, restart after shutdown, stuck pipe).</summary>
public class ReviewRegressionTests
{
    [Fact]
    public async Task Refresh_after_dispose_does_not_restart_the_server()
    {
        using var temp = new TempDir();
        var settings = new SettingsStore(temp.File("settings.json"));
        var supervisor = new ServerSupervisor(settings, _ => [IPAddress.Loopback], NullLoggerFactory.Instance);
        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);

        await supervisor.DisposeAsync();
        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServerStatus.Stopped, supervisor.State.Status);
    }

    [Fact]
    public async Task Reordered_addresses_update_primary_without_restart()
    {
        using var temp = new TempDir();
        var settings = new SettingsStore(temp.File("settings.json"));
        var other = IPAddress.Parse("127.0.0.2");
        IReadOnlyList<IPAddress> current = [IPAddress.Loopback, other];
        await using var supervisor = new ServerSupervisor(settings, _ => current, NullLoggerFactory.Instance);
        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);
        var port = supervisor.State.Port;

        // A connection kept open across the reorder proves Kestrel was not restarted.
        using var http = new HttpClient { BaseAddress = supervisor.State.BaseUri };
        Assert.True((await http.GetAsync("api/v1/ping", TestContext.Current.CancellationToken)).IsSuccessStatusCode);

        current = [other, IPAddress.Loopback];
        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServerStatus.Running, supervisor.State.Status);
        Assert.Equal(other, supervisor.State.Primary);
        Assert.Equal(port, supervisor.State.Port);
        Assert.True((await http.GetAsync("api/v1/ping", TestContext.Current.CancellationToken)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Failed_start_does_not_leave_stale_running_state()
    {
        using var temp = new TempDir();
        var settings = new SettingsStore(temp.File("settings.json"));
        IReadOnlyList<IPAddress> current = [IPAddress.Loopback];
        await using var supervisor = new ServerSupervisor(settings, _ => current, NullLoggerFactory.Instance);
        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);

        // 192.0.2.1 (TEST-NET-1) is not assigned to this machine, so binding fails.
        current = [IPAddress.Parse("192.0.2.1")];
        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(ServerStatus.Running, supervisor.State.Status);
        Assert.Null(supervisor.State.BaseUri);
    }

    [Fact]
    public async Task Silent_pipe_client_does_not_block_later_launches()
    {
        var suffix = "-test-" + Guid.NewGuid().ToString("N");
        using var primary = SingleInstance.TryAcquire(suffix)!;
        var received = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.ArgumentsReceived += (_, args) => received.TrySetResult(args);
        primary.Listen();

        // Connects and never writes or closes.
        await using var silent = new NamedPipeClientStream(".", SingleInstance.BaseName(suffix), PipeDirection.Out, PipeOptions.CurrentUserOnly);
        await silent.ConnectAsync(TestContext.Current.CancellationToken);

        var sent = await Task.Run(() => SingleInstance.SendToPrimary(["--show"], suffix, TimeSpan.FromSeconds(6)), TestContext.Current.CancellationToken);

        Assert.True(sent);
        var args = await received.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
        Assert.Equal(["--show"], args);
    }
}
