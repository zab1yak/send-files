using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using SendFiles.Core.Hosting;
using SendFiles.Core.Settings;

namespace SendFiles.Tests.Hosting;

public class ServerSupervisorTests
{
    [Fact]
    public async Task Starts_on_given_address_and_answers_ping()
    {
        using var temp = new TempDir();
        var settings = new SettingsStore(temp.File("settings.json"));
        await using var supervisor = new ServerSupervisor(settings, _ => [IPAddress.Loopback], NullLoggerFactory.Instance);

        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServerStatus.Running, supervisor.State.Status);
        using var http = new HttpClient { BaseAddress = supervisor.State.BaseUri };
        var body = await http.GetStringAsync("api/v1/ping", TestContext.Current.CancellationToken);
        Assert.Contains("\"ok\":true", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Remembers_chosen_port()
    {
        using var temp = new TempDir();
        var settings = new SettingsStore(temp.File("settings.json"));
        await using var supervisor = new ServerSupervisor(settings, _ => [IPAddress.Loopback], NullLoggerFactory.Instance);

        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(supervisor.State.Port, settings.Current.Port);
    }

    [Fact]
    public async Task No_addresses_means_no_network_state()
    {
        using var temp = new TempDir();
        var settings = new SettingsStore(temp.File("settings.json"));
        await using var supervisor = new ServerSupervisor(settings, _ => [], NullLoggerFactory.Instance);

        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServerStatus.NoNetwork, supervisor.State.Status);
        Assert.Null(supervisor.State.BaseUri);
    }

    [Fact]
    public async Task Same_addresses_do_not_rebind_but_new_ones_do()
    {
        using var temp = new TempDir();
        var settings = new SettingsStore(temp.File("settings.json"));
        IReadOnlyList<IPAddress> current = [IPAddress.Loopback];
        await using var supervisor = new ServerSupervisor(settings, _ => current, NullLoggerFactory.Instance);
        var changes = 0;
        supervisor.StateChanged += (_, _) => changes++;

        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);
        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, changes);

        current = [];
        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, changes);
        Assert.Equal(ServerStatus.NoNetwork, supervisor.State.Status);

        current = [IPAddress.Loopback];
        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ServerStatus.Running, supervisor.State.Status);
    }

    [Fact]
    public void Allowed_hosts_are_exact_address_and_port()
    {
        var state = new ServerState(ServerStatus.Running, [IPAddress.Parse("192.168.1.42"), IPAddress.Parse("10.0.0.5")], 47130);

        Assert.Contains("192.168.1.42:47130", state.AllowedHosts);
        Assert.Contains("10.0.0.5:47130", state.AllowedHosts);
        Assert.DoesNotContain("192.168.1.42", state.AllowedHosts);
        Assert.Equal(new Uri("http://192.168.1.42:47130/"), state.BaseUri);
    }

    [Fact]
    public async Task Dispose_twice_is_safe()
    {
        using var temp = new TempDir();
        var settings = new SettingsStore(temp.File("settings.json"));
        var supervisor = new ServerSupervisor(settings, _ => [IPAddress.Loopback], NullLoggerFactory.Instance);
        await supervisor.RefreshAsync(TestContext.Current.CancellationToken);

        await supervisor.DisposeAsync();
        await supervisor.DisposeAsync();

        Assert.Equal(ServerStatus.Stopped, supervisor.State.Status);
    }
}
