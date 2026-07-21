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
            .ThenBy(entry => entry.Address, NumericAddressComparer.Instance)
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

// Orders addresses by their numeric value (byte order) rather than lexically, so
// 192.168.1.99 sorts before 192.168.1.100. Inputs are always valid, scope-stripped
// IP strings produced by ToEntry.
internal sealed class NumericAddressComparer : IComparer<string>
{
    public static readonly NumericAddressComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var left = IPAddress.Parse(x).GetAddressBytes();
        var right = IPAddress.Parse(y).GetAddressBytes();

        // Shorter address families (IPv4) sort before longer ones (IPv6); within a
        // family the byte lengths are equal and the comparison is purely numeric.
        return left.Length != right.Length
            ? left.Length.CompareTo(right.Length)
            : left.AsSpan().SequenceCompareTo(right);
    }
}
