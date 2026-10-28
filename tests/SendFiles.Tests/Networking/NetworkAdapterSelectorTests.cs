using System.Net;
using System.Net.NetworkInformation;
using SendFiles.Core.Networking;

namespace SendFiles.Tests.Networking;

public class NetworkAdapterSelectorTests
{
    private static AdapterInfo Adapter(string name, string description, string ip,
        NetworkInterfaceType type = NetworkInterfaceType.Ethernet, bool gateway = true,
        OperationalStatus status = OperationalStatus.Up) =>
        new(name, name, description, type, status, [IPAddress.Parse(ip)], gateway);

    [Fact]
    public void Picks_WiFi_with_gateway_as_primary()
    {
        var adapters = new[]
        {
            Adapter("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter", "172.24.0.1", gateway: false),
            Adapter("Wi-Fi", "Intel Wi-Fi 6 AX201", "192.168.1.42", NetworkInterfaceType.Wireless80211),
            Adapter("Ethernet 2", "Realtek PCIe GbE", "10.0.0.5", gateway: false),
        };

        var result = NetworkAdapterSelector.Select(adapters);

        Assert.Equal("192.168.1.42", NetworkAdapterSelector.Primary(result)!.Address.ToString());
        Assert.DoesNotContain(result, c => c.Address.ToString() == "172.24.0.1");
        Assert.Contains(result, c => c.Address.ToString() == "10.0.0.5");
    }

    [Theory]
    [InlineData("vEthernet (Default Switch)", "Hyper-V Virtual Ethernet Adapter")]
    [InlineData("Ethernet 3", "VirtualBox Host-Only Ethernet Adapter")]
    [InlineData("VMware Network Adapter VMnet8", "VMware Virtual Ethernet Adapter for VMnet8")]
    [InlineData("vEthernet (WSL (Hyper-V firewall))", "Hyper-V Virtual Ethernet Adapter #2")]
    [InlineData("vEthernet (DockerNAT)", "Hyper-V Virtual Ethernet Adapter #3")]
    [InlineData("OpenVPN TAP", "TAP-Windows Adapter V9")]
    [InlineData("wg0", "WireGuard Tunnel")]
    [InlineData("Tailscale", "Tailscale Tunnel")]
    [InlineData("ZeroTier One", "ZeroTier Virtual Port")]
    [InlineData("Cisco", "Cisco AnyConnect Secure Mobility Client Virtual Miniport Adapter")]
    public void Excludes_virtual_and_vpn_adapters(string name, string description)
    {
        var result = NetworkAdapterSelector.Select([Adapter(name, description, "192.168.56.1")]);

        Assert.Empty(result);
    }

    [Fact]
    public void Virtual_adapters_allowed_when_setting_is_on()
    {
        var result = NetworkAdapterSelector.Select(
            [Adapter("vEthernet (Default Switch)", "Hyper-V Virtual Ethernet Adapter", "172.20.0.1")],
            allowVirtual: true);

        Assert.Single(result);
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("100.64.0.5")] // CGNAT / Tailscale range — not RFC 1918
    [InlineData("172.32.0.1")]
    [InlineData("127.0.0.1")]
    public void Excludes_non_private_addresses(string ip)
    {
        Assert.Empty(NetworkAdapterSelector.Select([Adapter("Ethernet", "Realtek PCIe GbE", ip)]));
    }

    [Theory]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.0.10")]
    public void Accepts_rfc1918_addresses(string ip)
    {
        Assert.Single(NetworkAdapterSelector.Select([Adapter("Ethernet", "Realtek PCIe GbE", ip)]));
    }

    [Fact]
    public void Link_local_is_lowest_priority()
    {
        var result = NetworkAdapterSelector.Select(
        [
            Adapter("Ethernet", "Realtek PCIe GbE", "169.254.10.20", gateway: false),
            Adapter("Wi-Fi", "Intel Wi-Fi", "192.168.0.7", NetworkInterfaceType.Wireless80211, gateway: false),
        ]);

        Assert.Equal("192.168.0.7", result[0].Address.ToString());
        Assert.Equal("169.254.10.20", result[1].Address.ToString());
    }

    [Fact]
    public void Skips_adapters_that_are_down()
    {
        var result = NetworkAdapterSelector.Select(
            [Adapter("Wi-Fi", "Intel Wi-Fi", "192.168.0.7", status: OperationalStatus.Down)]);

        Assert.Empty(result);
    }

    [Fact]
    public void Skips_tunnel_and_ppp_types()
    {
        var result = NetworkAdapterSelector.Select(
        [
            Adapter("Teredo", "Microsoft Teredo", "192.168.5.1", NetworkInterfaceType.Tunnel),
            Adapter("Dial-up", "WAN Miniport (PPP)", "192.168.6.1", NetworkInterfaceType.Ppp),
        ]);

        Assert.Empty(result);
    }

    [Fact]
    public void Windows_mobile_hotspot_is_allowed()
    {
        var result = NetworkAdapterSelector.Select(
        [
            new AdapterInfo("h", "Подключение по локальной сети* 10", "Microsoft Wi-Fi Direct Virtual Adapter #2",
                NetworkInterfaceType.Wireless80211, OperationalStatus.Up, [IPAddress.Parse("192.168.137.1")], false),
        ]);

        Assert.Single(result);
    }

    [Fact]
    public void Gateway_beats_no_gateway()
    {
        var result = NetworkAdapterSelector.Select(
        [
            Adapter("Ethernet", "Realtek", "10.0.0.2", gateway: false),
            Adapter("Ethernet 2", "Intel I219", "192.168.1.2", gateway: true),
        ]);

        Assert.Equal("192.168.1.2", result[0].Address.ToString());
    }

    [Fact]
    public void No_adapters_means_no_primary()
    {
        Assert.Null(NetworkAdapterSelector.Primary(NetworkAdapterSelector.Select([])));
    }
}
