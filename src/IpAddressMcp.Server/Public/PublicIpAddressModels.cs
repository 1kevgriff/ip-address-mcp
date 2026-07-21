namespace IpAddressMcp.Public;

public sealed record PublicIpAddressResult(
    PublicAddressLookupResult Ipv4,
    PublicAddressLookupResult Ipv6);

public sealed record PublicAddressLookupResult(
    string Status,
    string? Address,
    string? Message)
{
    public static PublicAddressLookupResult Available(string address) =>
        new("available", address, null);

    public static PublicAddressLookupResult Unavailable(string message) =>
        new("unavailable", null, message);
}

