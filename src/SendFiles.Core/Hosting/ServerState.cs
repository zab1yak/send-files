using System.Globalization;
using System.Net;

namespace SendFiles.Core.Hosting;

public enum ServerStatus
{
    Stopped,
    Running,

    /// <summary>No private Wi-Fi/Ethernet address: the PC is not on a usable local network.</summary>
    NoNetwork,

    /// <summary>Kestrel could not start (port taken, address vanished mid-start…).</summary>
    Failed,
}

public sealed record ServerState(ServerStatus Status, IReadOnlyList<IPAddress> Addresses, int Port, string? Error = null)
{
    public static readonly ServerState Stopped = new(ServerStatus.Stopped, [], 0);

    /// <summary>The address shown in the QR code: the best-ranked one.</summary>
    public IPAddress? Primary => Addresses.Count > 0 ? Addresses[0] : null;

    public Uri? BaseUri => Status == ServerStatus.Running && Primary is not null
        ? new Uri(string.Create(CultureInfo.InvariantCulture, $"http://{Primary}:{Port}/"))
        : null;

    /// <summary>Exact <c>Host</c> header values this server answers to (DNS-rebinding defence).</summary>
    public IReadOnlySet<string> AllowedHosts { get; } =
        Addresses.Select(a => string.Create(CultureInfo.InvariantCulture, $"{a}:{Port}")).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool SameBinding(IReadOnlyList<IPAddress> addresses) =>
        Status == ServerStatus.Running && Addresses.SequenceEqual(addresses);
}
