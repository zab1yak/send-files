using Microsoft.Extensions.Time.Testing;
using SendFiles.Core.Devices;
using SendFiles.Core.Pairing;
using SendFiles.Core.Security;

namespace SendFiles.Tests.Pairing;

/// <summary>Approver that answers when the test says so.</summary>
public sealed class ManualApprover : IPairingApprover
{
    private TaskCompletionSource<bool> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PairingRequest? LastRequest { get; private set; }

    public bool? AutoAnswer { get; set; }

    public Task<bool> RequestAsync(PairingRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (AutoAnswer is { } auto)
        {
            return Task.FromResult(auto);
        }

        return _answer.Task.WaitAsync(cancellationToken);
    }

    public void Answer(bool approved)
    {
        _answer.TrySetResult(approved);
        _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public sealed class PairingTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-10-29T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
    private readonly ManualApprover _approver = new();
    private readonly DeviceStore _devices;
    private readonly PairingService _pairing;

    public PairingTests()
    {
        _devices = new DeviceStore(_temp.File("devices.json"), _time);
        _pairing = new PairingService(_devices, _time, () => _approver);
    }

    public void Dispose()
    {
        _pairing.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task Approved_request_delivers_key_once_and_device_is_trusted()
    {
        var start = _pairing.Start(_pairing.CurrentToken, "iPhone (Safari)");
        Assert.Equal(PairingStartError.None, start.Error);
        Assert.Matches("^[0-9]{4}$", start.Code);
        Assert.Equal(start.Code, _approver.LastRequest?.Code);

        _approver.Answer(true);
        var result = await _pairing.WaitAsync(start.RequestId!, start.RequestSecret!, TestContext.Current.CancellationToken);

        Assert.Equal(PairingStatus.Approved, result!.Status);
        Assert.NotNull(_devices.Authenticate(result.Key));
        Assert.Null(await _pairing.WaitAsync(start.RequestId!, start.RequestSecret!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Token_is_single_use()
    {
        var token = _pairing.CurrentToken;
        _approver.AutoAnswer = false;

        Assert.Equal(PairingStartError.None, _pairing.Start(token, "a").Error);
        Assert.Equal(PairingStartError.TokenInvalid, _pairing.Start(token, "b").Error);
        Assert.NotEqual(token, _pairing.CurrentToken);
    }

    [Fact]
    public void Token_expires_after_five_minutes()
    {
        var token = _pairing.CurrentToken;
        var changed = 0;
        _pairing.TokenChanged += (_, _) => changed++;

        _time.Advance(PairingService.TokenLifetime + TimeSpan.FromSeconds(1));

        Assert.Equal(PairingStartError.TokenInvalid, _pairing.Start(token, "a").Error);
        Assert.Equal(1, changed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    public void Wrong_token_is_rejected(string? token)
    {
        Assert.Equal(PairingStartError.TokenInvalid, _pairing.Start(token, "a").Error);
    }

    [Fact]
    public void Second_request_while_first_pending_is_busy()
    {
        Assert.Equal(PairingStartError.None, _pairing.Start(_pairing.CurrentToken, "a").Error);

        Assert.Equal(PairingStartError.Busy, _pairing.Start(_pairing.CurrentToken, "b").Error);
    }

    [Fact]
    public void Guessing_pauses_pairing_and_rotates_token()
    {
        var token = _pairing.CurrentToken;
        for (var i = 0; i < 20; i++)
        {
            _pairing.Start("guess" + i, "x");
        }

        Assert.Equal(PairingStartError.Paused, _pairing.Start(_pairing.CurrentToken, "a").Error);
        Assert.NotEqual(token, _pairing.CurrentToken);

        _time.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(PairingStartError.None, _pairing.Start(_pairing.CurrentToken, "a").Error);
    }

    [Fact]
    public async Task Denied_request_gives_no_key()
    {
        var start = _pairing.Start(_pairing.CurrentToken, "a");
        _approver.Answer(false);

        var result = await _pairing.WaitAsync(start.RequestId!, start.RequestSecret!, TestContext.Current.CancellationToken);

        Assert.Equal(PairingStatus.Denied, result!.Status);
        Assert.Null(result.Key);
        Assert.Empty(_devices.List());
    }

    [Fact]
    public async Task Wrong_request_secret_cannot_collect_the_key()
    {
        var start = _pairing.Start(_pairing.CurrentToken, "a");
        _approver.Answer(true);

        Assert.Null(await _pairing.WaitAsync(start.RequestId!, "not-the-secret", TestContext.Current.CancellationToken));
        var real = await _pairing.WaitAsync(start.RequestId!, start.RequestSecret!, TestContext.Current.CancellationToken);
        Assert.Equal(PairingStatus.Approved, real!.Status);
    }

    [Fact]
    public void Codes_are_four_digits_and_vary()
    {
        _approver.AutoAnswer = false;
        var codes = new HashSet<string>();
        for (var i = 0; i < 30; i++)
        {
            var start = _pairing.Start(_pairing.CurrentToken, "a");
            codes.Add(start.Code!);
            _pairing.Cancel(start.RequestId!, start.RequestSecret!);
        }

        Assert.All(codes, c => Assert.Matches("^[0-9]{4}$", c));
        Assert.True(codes.Count > 20);
    }
}

public class DeviceTests
{
    [Fact]
    public void Key_roundtrip_and_tamper_detection()
    {
        var (id, secret, key) = DeviceKey.Generate();

        Assert.True(DeviceKey.TryParse(key, out var parsedId, out var parsedSecret));
        Assert.Equal(id, parsedId);
        Assert.True(DeviceKey.Verify(parsedSecret, DeviceKey.Hash(secret)));
        Assert.False(DeviceKey.Verify(parsedSecret[..^1] + (parsedSecret[^1] == 'A' ? 'B' : 'A'), DeviceKey.Hash(secret)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sf1")]
    [InlineData("sf2.AAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("sf1.short.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("sf1.AAAAAAAAAAAAAAAAAAAAAA.AAAA")]
    [InlineData("sf1.AAAAAAAAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.extra")]
    public void Malformed_keys_do_not_parse(string? key)
    {
        Assert.False(DeviceKey.TryParse(key, out _, out _));
    }

    [Fact]
    public void Store_keeps_only_hashes_and_survives_restart()
    {
        using var temp = new TempDir();
        var time = new FakeTimeProvider();
        var store = new DeviceStore(temp.File("devices.json"), time);
        var (device, key) = store.Add("iPhone (Safari)");

        var json = File.ReadAllText(temp.File("devices.json"));
        Assert.True(DeviceKey.TryParse(key, out _, out var secret));
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);

        var reloaded = new DeviceStore(temp.File("devices.json"), time);
        Assert.Equal(device.Id, reloaded.Authenticate(key)?.Id);
    }

    [Fact]
    public void Removed_device_cannot_authenticate_and_raises_revoked()
    {
        using var temp = new TempDir();
        var store = new DeviceStore(temp.File("devices.json"), new FakeTimeProvider());
        var (device, key) = store.Add("a");
        string? revoked = null;
        store.Revoked += (_, id) => revoked = id;

        store.Remove(device.Id);

        Assert.Null(store.Authenticate(key));
        Assert.Equal(device.Id, revoked);
    }

    [Fact]
    public void Remove_all_revokes_everyone()
    {
        using var temp = new TempDir();
        var store = new DeviceStore(temp.File("devices.json"), new FakeTimeProvider());
        var (_, a) = store.Add("a");
        var (_, b) = store.Add("b");

        store.RemoveAll();

        Assert.Null(store.Authenticate(a));
        Assert.Null(store.Authenticate(b));
        Assert.Empty(store.List());
    }

    [Theory]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "iPhone (Safari)")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/130.0 Mobile/15E148 Safari/604.1", "iPhone (Chrome)")]
    [InlineData("Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36", "Android (Chrome)")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/26.0 Chrome/122.0 Mobile Safari/537.36", "Android (Samsung Internet)")]
    [InlineData("<script>alert(1)</script>", "Device")]
    [InlineData(null, "Device")]
    public void Device_name_comes_from_user_agent_family(string? ua, string expected)
    {
        Assert.Equal(expected, DeviceNaming.FromUserAgent(ua));
    }
}

public class TicketTests
{
    [Fact]
    public void Events_ticket_is_single_use_and_short_lived()
    {
        var time = new FakeTimeProvider();
        var tickets = new TicketService(time);
        var t1 = tickets.Issue("dev", TicketPurpose.Events);

        Assert.Equal("dev", tickets.Redeem(t1, TicketPurpose.Events));
        Assert.Null(tickets.Redeem(t1, TicketPurpose.Events));

        var t2 = tickets.Issue("dev", TicketPurpose.Events);
        time.Advance(TicketService.EventsLifetime);
        Assert.Null(tickets.Redeem(t2, TicketPurpose.Events));
    }

    [Fact]
    public void Download_ticket_is_bound_to_file_and_reusable()
    {
        var tickets = new TicketService(new FakeTimeProvider());
        var t = tickets.Issue("dev", TicketPurpose.Download, "file-1");

        Assert.Equal("dev", tickets.Redeem(t, TicketPurpose.Download, "file-1"));
        Assert.Equal("dev", tickets.Redeem(t, TicketPurpose.Download, "file-1"));
        Assert.Null(tickets.Redeem(t, TicketPurpose.Download, "file-2"));
        Assert.Null(tickets.Redeem(t, TicketPurpose.Events));
    }

    [Fact]
    public void Revoking_device_kills_its_tickets()
    {
        var tickets = new TicketService(new FakeTimeProvider());
        var t = tickets.Issue("dev", TicketPurpose.Download, "f");

        tickets.RevokeDevice("dev");

        Assert.Null(tickets.Redeem(t, TicketPurpose.Download, "f"));
    }
}
