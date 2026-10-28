using System.Collections.Concurrent;
using SendFiles.Core.Devices;

namespace SendFiles.Core.Security;

public enum TicketPurpose
{
    Events,
    Download,
}

/// <summary>
/// Short-lived URL tokens for what a browser cannot send a header with (WebSocket, EventSource, &lt;a href&gt;).
/// The device key itself never appears in a URL. Bound to device, purpose and (for downloads) the file.
/// </summary>
public sealed class TicketService
{
    public static readonly TimeSpan EventsLifetime = TimeSpan.FromSeconds(30);

    // iOS media players issue several Range requests for one download, so these are reusable for a while.
    public static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(10);

    private const int MaxTickets = 2048;

    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, Ticket> _tickets = new(StringComparer.Ordinal);

    public TicketService(TimeProvider time) => _time = time;

    public string Issue(string deviceId, TicketPurpose purpose, string? fileId = null)
    {
        Sweep();
        var lifetime = purpose == TicketPurpose.Events ? EventsLifetime : DownloadLifetime;
        var token = DeviceKey.RandomToken();
        _tickets[token] = new Ticket(deviceId, purpose, fileId, _time.GetUtcNow() + lifetime, SingleUse: purpose == TicketPurpose.Events);
        return token;
    }

    /// <returns>The device id, or <c>null</c> if the ticket is unknown, expired, used or for something else.</returns>
    public string? Redeem(string? token, TicketPurpose purpose, string? fileId = null)
    {
        if (string.IsNullOrEmpty(token) || !_tickets.TryGetValue(token, out var ticket))
        {
            return null;
        }

        if (ticket.ExpiresAt <= _time.GetUtcNow() || ticket.Purpose != purpose || !string.Equals(ticket.FileId, fileId, StringComparison.Ordinal))
        {
            return null;
        }

        if (ticket.SingleUse && !_tickets.TryRemove(new KeyValuePair<string, Ticket>(token, ticket)))
        {
            return null; // raced with another redemption
        }

        return ticket.DeviceId;
    }

    public void RevokeDevice(string deviceId)
    {
        foreach (var (token, ticket) in _tickets)
        {
            if (ticket.DeviceId == deviceId)
            {
                _tickets.TryRemove(token, out _);
            }
        }
    }

    private void Sweep()
    {
        var now = _time.GetUtcNow();
        foreach (var (token, ticket) in _tickets)
        {
            if (ticket.ExpiresAt <= now)
            {
                _tickets.TryRemove(token, out _);
            }
        }

        // Hard cap against a device minting tickets in a loop (rate limits apply too).
        if (_tickets.Count >= MaxTickets)
        {
            foreach (var token in _tickets.OrderBy(t => t.Value.ExpiresAt).Take(_tickets.Count - MaxTickets + 1).Select(t => t.Key))
            {
                _tickets.TryRemove(token, out _);
            }
        }
    }

    private sealed record Ticket(string DeviceId, TicketPurpose Purpose, string? FileId, DateTimeOffset ExpiresAt, bool SingleUse);
}
