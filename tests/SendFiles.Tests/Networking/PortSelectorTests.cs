using System.Net;
using System.Net.Sockets;
using SendFiles.Core.Networking;

namespace SendFiles.Tests.Networking;

public class PortSelectorTests
{
    [Fact]
    public void Prefers_remembered_port()
    {
        Assert.Equal(51000, PortSelector.Choose(51000, _ => true));
    }

    [Fact]
    public void Falls_back_to_range_when_remembered_is_taken()
    {
        Assert.Equal(PortSelector.DefaultPort, PortSelector.Choose(51000, p => p != 51000));
    }

    [Fact]
    public void Skips_taken_ports_in_range()
    {
        var taken = new HashSet<int> { PortSelector.DefaultPort, PortSelector.DefaultPort + 1 };

        Assert.Equal(PortSelector.DefaultPort + 2, PortSelector.Choose(PortSelector.DefaultPort, p => !taken.Contains(p)));
    }

    [Fact]
    public void Returns_zero_when_everything_is_taken()
    {
        Assert.Equal(0, PortSelector.Choose(51000, _ => false));
    }

    [Fact]
    public void IsFree_detects_a_bound_port()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            Assert.False(PortSelector.IsFree([IPAddress.Loopback], port));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void Ephemeral_port_is_free()
    {
        var port = PortSelector.Ephemeral(IPAddress.Loopback);

        Assert.True(PortSelector.IsFree([IPAddress.Loopback], port));
    }
}
