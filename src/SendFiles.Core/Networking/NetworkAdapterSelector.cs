using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SendFiles.Core.Networking;

/// <summary>An address the server may listen on, with the adapter it belongs to.</summary>
public sealed record ListenCandidate(IPAddress Address, AdapterInfo Adapter, int Rank);

/// <summary>
/// Picks the local addresses the server is allowed to listen on: private (RFC 1918) or link-local IPv4
/// of physical Wi-Fi/Ethernet adapters that are up. VPN and virtual adapters are excluded unless allowed.
/// </summary>
public static class NetworkAdapterSelector
{
    // Description/name fragments of virtual, VPN and tunnel adapters.
    private static readonly string[] VirtualMarkers =
    [
        "hyper-v", "vethernet", "virtualbox", "vmware", "vmnet", "wsl", "docker", "container",
        "tap-", "tap adapter", "tun", "wintun", "wireguard", "openvpn", "tailscale", "zerotier",
        "vpn", "anyconnect", "fortinet", "globalprotect", "pangp", "npcap", "loopback", "parallels",
        "bluetooth", "teredo", "isatap", "6to4", "kernel debug",
    ];

    // Windows "Mobile hotspot": the phone joins the PC's own network. Virtual, but a legitimate LAN.
    private const string HotspotMarker = "wi-fi direct virtual adapter";

    public static IReadOnlyList<ListenCandidate> Select(IEnumerable<AdapterInfo> adapters, bool allowVirtual = false)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var result = new List<ListenCandidate>();
        foreach (var adapter in adapters)
        {
            if (adapter.Status != OperationalStatus.Up)
            {
                continue;
            }

            var isHotspot = Contains(adapter, HotspotMarker);
            if (!IsAllowedType(adapter.Type) && !isHotspot)
            {
                continue;
            }

            if (!isHotspot && !allowVirtual && IsVirtual(adapter))
            {
                continue;
            }

            foreach (var address in adapter.IPv4Addresses)
            {
                if (address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                var rank = Rank(address, adapter, isHotspot);
                if (rank >= 0)
                {
                    result.Add(new ListenCandidate(address, adapter, rank));
                }
            }
        }

        // Highest rank first; stable by adapter name and address for deterministic output.
        return result
            .OrderByDescending(c => c.Rank)
            .ThenBy(c => c.Adapter.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Address.ToString(), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The address to show in the QR code, or <c>null</c> if no suitable network.</summary>
    public static ListenCandidate? Primary(IReadOnlyList<ListenCandidate> candidates) =>
        candidates.Count > 0 ? candidates[0] : null;

    public static bool IsPrivate(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = address.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
    }

    public static bool IsLinkLocal(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var b = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork && b[0] == 169 && b[1] == 254;
    }

    private static int Rank(IPAddress address, AdapterInfo adapter, bool isHotspot)
    {
        int rank;
        if (IsPrivate(address))
        {
            rank = 100;
        }
        else if (IsLinkLocal(address))
        {
            rank = 10;
        }
        else
        {
            return -1; // public or otherwise unsuitable
        }

        if (adapter.HasGateway)
        {
            rank += 50;
        }

        if (isHotspot)
        {
            rank += 20;
        }
        else if (adapter.Type == NetworkInterfaceType.Wireless80211)
        {
            rank += 5; // phones are almost always on Wi-Fi
        }

        return rank;
    }

    private static bool IsAllowedType(NetworkInterfaceType type) => type is
        NetworkInterfaceType.Ethernet or
        NetworkInterfaceType.Wireless80211 or
        NetworkInterfaceType.GigabitEthernet or
        NetworkInterfaceType.FastEthernetT or
        NetworkInterfaceType.FastEthernetFx or
        NetworkInterfaceType.Ethernet3Megabit;

    private static bool IsVirtual(AdapterInfo adapter) =>
        VirtualMarkers.Any(marker => Contains(adapter, marker));

    private static bool Contains(AdapterInfo adapter, string marker) =>
        adapter.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)
        || adapter.Description.Contains(marker, StringComparison.OrdinalIgnoreCase);
}
