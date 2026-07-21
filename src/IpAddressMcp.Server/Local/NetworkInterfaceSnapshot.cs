using System.Net;
using System.Net.NetworkInformation;

namespace IpAddressMcp.Local;

public sealed record NetworkInterfaceSnapshot(
    string Id,
    string Name,
    NetworkInterfaceType InterfaceType,
    OperationalStatus OperationalStatus,
    IReadOnlyList<IPAddress> UnicastAddresses);

public interface INetworkInterfaceProvider
{
    IReadOnlyList<NetworkInterfaceSnapshot> GetNetworkInterfaces();
}

