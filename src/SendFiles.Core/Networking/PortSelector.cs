using System.Net;
using System.Net.Sockets;

namespace SendFiles.Core.Networking;

/// <summary>Chooses the listening port: the remembered one if free, then a small fixed range, then any free port.</summary>
public static class PortSelector
{
    public const int DefaultPort = 47130;
    public const int RangeSize = 20;

    public static bool IsValidPort(int port) => port is >= 1024 and <= 65535;

    /// <returns>A port for which <paramref name="isFree"/> returned true, or 0 if none in the range.</returns>
    public static int Choose(int preferred, Func<int, bool> isFree)
    {
        ArgumentNullException.ThrowIfNull(isFree);
        if (IsValidPort(preferred) && isFree(preferred))
        {
            return preferred;
        }

        for (var port = DefaultPort; port < DefaultPort + RangeSize; port++)
        {
            if (port != preferred && isFree(port))
            {
                return port;
            }
        }

        return 0;
    }

    /// <summary>True if the port can be bound on every one of the given addresses right now.</summary>
    public static bool IsFree(IReadOnlyCollection<IPAddress> addresses, int port)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var listeners = new List<TcpListener>();
        try
        {
            foreach (var address in addresses)
            {
                var listener = new TcpListener(address, port);
                listener.Server.ExclusiveAddressUse = true;
                listener.Start();
                listeners.Add(listener);
            }

            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            foreach (var listener in listeners)
            {
                listener.Stop();
            }
        }
    }

    /// <summary>Asks the OS for a free port on the first address (fallback when the range is taken).</summary>
    public static int Ephemeral(IPAddress address)
    {
        var listener = new TcpListener(address, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
