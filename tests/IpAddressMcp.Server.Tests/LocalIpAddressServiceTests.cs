using System.Net;
using System.Net.NetworkInformation;
using IpAddressMcp.Local;

namespace IpAddressMcp.Tests;

public sealed class LocalIpAddressServiceTests
{
    [Fact]
    public void GetLocalIpAddresses_FiltersAndDescribesActiveUnicastAddresses()
    {
        var scopedIpv6 = IPAddress.Parse("fe80::1234%7");
        var provider = new StubNetworkInterfaceProvider(
        [
            Snapshot(
                "ethernet",
                "Ethernet",
                NetworkInterfaceType.Ethernet,
                OperationalStatus.Up,
                IPAddress.Parse("192.168.1.25"),
                scopedIpv6,
                IPAddress.Loopback,
                IPAddress.IPv6Any),
            Snapshot(
                "down",
                "Disconnected",
                NetworkInterfaceType.Wireless80211,
                OperationalStatus.Down,
                IPAddress.Parse("10.0.0.5")),
            Snapshot(
                "loopback",
                "Loopback",
                NetworkInterfaceType.Loopback,
                OperationalStatus.Up,
                IPAddress.Parse("127.0.0.2")),
        ]);

        var result = new LocalIpAddressService(provider).GetLocalIpAddresses();

        Assert.Collection(
            result.Addresses,
            ipv4 =>
            {
                Assert.Equal("192.168.1.25", ipv4.Address);
                Assert.Equal("IPv4", ipv4.AddressFamily);
                Assert.Equal("Ethernet", ipv4.InterfaceName);
                Assert.Equal("Ethernet", ipv4.InterfaceType);
                Assert.Null(ipv4.ScopeId);
            },
            ipv6 =>
            {
                Assert.Equal("fe80::1234", ipv6.Address);
                Assert.Equal("IPv6", ipv6.AddressFamily);
                Assert.Equal(7, ipv6.ScopeId);
            });
    }

    [Fact]
    public void GetLocalIpAddresses_DeduplicatesPerInterfaceAndSortsDeterministically()
    {
        var provider = new StubNetworkInterfaceProvider(
        [
            Snapshot(
                "z-id",
                "Zeta",
                NetworkInterfaceType.Ethernet,
                OperationalStatus.Up,
                IPAddress.Parse("10.0.0.2"),
                IPAddress.Parse("10.0.0.2")),
            Snapshot(
                "a-id",
                "Alpha",
                NetworkInterfaceType.Wireless80211,
                OperationalStatus.Up,
                IPAddress.Parse("2001:db8::2"),
                IPAddress.Parse("10.0.0.3")),
        ]);

        var result = new LocalIpAddressService(provider).GetLocalIpAddresses();

        Assert.Equal(
            ["10.0.0.3", "10.0.0.2", "2001:db8::2"],
            result.Addresses.Select(address => address.Address));
    }

    private static NetworkInterfaceSnapshot Snapshot(
        string id,
        string name,
        NetworkInterfaceType type,
        OperationalStatus status,
        params IPAddress[] addresses) =>
        new(id, name, type, status, addresses);

    private sealed class StubNetworkInterfaceProvider(
        IReadOnlyList<NetworkInterfaceSnapshot> networkInterfaces) : INetworkInterfaceProvider
    {
        public IReadOnlyList<NetworkInterfaceSnapshot> GetNetworkInterfaces() => networkInterfaces;
    }
}

