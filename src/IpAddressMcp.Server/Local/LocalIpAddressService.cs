using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace IpAddressMcp.Local;

public interface ILocalIpAddressService
{
    LocalIpAddressResult GetLocalIpAddresses();
}

public sealed class LocalIpAddressService(INetworkInterfaceProvider networkInterfaceProvider)
    : ILocalIpAddressService
{
    public LocalIpAddressResult GetLocalIpAddresses()
    {
        var entries = networkInterfaceProvider
            .GetNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.InterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(networkInterface => networkInterface.UnicastAddresses.Select(address =>
                (NetworkInterface: networkInterface, Address: address)))
            .Where(candidate => IsSupported(candidate.Address))
            .DistinctBy(candidate => new
            {
                candidate.NetworkInterface.Id,
                Address = candidate.Address.ToString(),
            })
            .Select(candidate => ToEntry(candidate.NetworkInterface, candidate.Address))
            .OrderBy(entry => entry.AddressFamily == "IPv4" ? 0 : 1)
            .ThenBy(entry => entry.InterfaceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Address, StringComparer.Ordinal)
            .ThenBy(entry => entry.ScopeId)
            .ToArray();

        return new LocalIpAddressResult(entries);
    }

    private static bool IsSupported(IPAddress address) =>
        (address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6) &&
        !IPAddress.IsLoopback(address) &&
        !address.Equals(IPAddress.Any) &&
        !address.Equals(IPAddress.IPv6Any) &&
        !address.Equals(IPAddress.None) &&
        !address.Equals(IPAddress.IPv6None);

    private static LocalIpAddressEntry ToEntry(
        NetworkInterfaceSnapshot networkInterface,
        IPAddress address)
    {
        var isIpv6 = address.AddressFamily == AddressFamily.InterNetworkV6;
        long? scopeId = isIpv6 && address.ScopeId != 0 ? address.ScopeId : null;
        var addressWithoutScope = isIpv6
            ? new IPAddress(address.GetAddressBytes()).ToString()
            : address.ToString();

        return new LocalIpAddressEntry(
            addressWithoutScope,
            isIpv6 ? "IPv6" : "IPv4",
            networkInterface.Name,
            networkInterface.InterfaceType.ToString(),
            scopeId);
    }
}
