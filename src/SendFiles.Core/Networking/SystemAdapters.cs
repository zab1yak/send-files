using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SendFiles.Core.Networking;

/// <summary>Reads the real network interfaces of this machine.</summary>
public static class SystemAdapters
{
    public static IReadOnlyList<AdapterInfo> Read()
    {
        var list = new List<AdapterInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            IPInterfaceProperties props;
            try
            {
                props = nic.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }

            var addresses = props.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address)
                .ToList();
            var hasGateway = props.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(System.Net.IPAddress.Any));

            list.Add(new AdapterInfo(nic.Id, nic.Name, nic.Description, nic.NetworkInterfaceType,
                nic.OperationalStatus, addresses, hasGateway));
        }

        return list;
    }
}
