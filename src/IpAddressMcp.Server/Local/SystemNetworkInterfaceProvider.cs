using System.Net;
using System.Net.NetworkInformation;

namespace IpAddressMcp.Local;

public sealed class SystemNetworkInterfaceProvider : INetworkInterfaceProvider
{
    public IReadOnlyList<NetworkInterfaceSnapshot> GetNetworkInterfaces()
    {
        var snapshots = new List<NetworkInterfaceSnapshot>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            IReadOnlyList<IPAddress> addresses;

            try
            {
                addresses = networkInterface
                    .GetIPProperties()
                    .UnicastAddresses
                    .Select(information => information.Address)
                    .ToArray();
            }
            catch (NetworkInformationException)
            {
                // An interface can disappear while it is being inspected.
                continue;
            }

            snapshots.Add(new NetworkInterfaceSnapshot(
                networkInterface.Id,
                networkInterface.Name,
                networkInterface.NetworkInterfaceType,
                networkInterface.OperationalStatus,
                addresses));
        }

        return snapshots;
    }
}

