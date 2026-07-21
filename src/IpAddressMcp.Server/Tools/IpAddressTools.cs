using System.ComponentModel;
using IpAddressMcp.Local;
using IpAddressMcp.Public;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IpAddressMcp.Tools;

[McpServerToolType]
public static class IpAddressTools
{
    [McpServerTool(
        Name = "GetLocalIPAddress",
        Title = "Get local IP addresses",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Gets all active non-loopback IPv4 and IPv6 addresses on the local machine, including network interface metadata.")]
    public static LocalIpAddressResult GetLocalIPAddress(ILocalIpAddressService service) =>
        service.GetLocalIpAddresses();

    [McpServerTool(
        Name = "GetPublicIPAddress",
        Title = "Get public IP addresses",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Gets the public IPv4 and IPv6 addresses observed by external HTTPS services. Either family may be unavailable.")]
    public static async Task<PublicIpAddressResult> GetPublicIPAddress(
        IPublicIpAddressService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return await service
                .GetPublicIpAddressesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PublicIpLookupException exception)
        {
            throw new McpException(exception.Message);
        }
    }
}

