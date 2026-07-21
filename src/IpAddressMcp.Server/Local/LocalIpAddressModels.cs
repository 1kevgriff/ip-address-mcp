namespace IpAddressMcp.Local;

public sealed record LocalIpAddressResult(IReadOnlyList<LocalIpAddressEntry> Addresses);

public sealed record LocalIpAddressEntry(
    string Address,
    string AddressFamily,
    string InterfaceName,
    string InterfaceType,
    long? ScopeId);

