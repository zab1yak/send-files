using System.Net;
using System.Net.NetworkInformation;

namespace SendFiles.Core.Networking;

/// <summary>Snapshot of one network interface, decoupled from <see cref="NetworkInterface"/> so selection can be tested on fake data.</summary>
public sealed record AdapterInfo(
    string Id,
    string Name,
    string Description,
    NetworkInterfaceType Type,
    OperationalStatus Status,
    IReadOnlyList<IPAddress> IPv4Addresses,
    bool HasGateway);
