namespace IpAddressMcp.Public;

public sealed class PublicIpOptions
{
    public const string SectionName = "PublicIp";

    public Uri Ipv4Endpoint { get; set; } = new("https://api.ipify.org");

    public Uri Ipv6Endpoint { get; set; } = new("https://api6.ipify.org");

    public static bool HasValidEndpoints(PublicIpOptions options) =>
        IsAbsoluteHttps(options.Ipv4Endpoint) && IsAbsoluteHttps(options.Ipv6Endpoint);

    private static bool IsAbsoluteHttps(Uri? endpoint) =>
        endpoint is { IsAbsoluteUri: true } && endpoint.Scheme == Uri.UriSchemeHttps;
}

