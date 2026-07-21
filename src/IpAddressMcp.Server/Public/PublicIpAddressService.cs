using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace IpAddressMcp.Public;

public interface IPublicIpAddressService
{
    Task<PublicIpAddressResult> GetPublicIpAddressesAsync(CancellationToken cancellationToken);
}

public sealed class PublicIpLookupException(string message) : Exception(message);

public sealed class PublicIpAddressService(
    HttpClient httpClient,
    IOptions<PublicIpOptions> options) : IPublicIpAddressService
{
    private readonly PublicIpOptions _options = options.Value;

    public async Task<PublicIpAddressResult> GetPublicIpAddressesAsync(
        CancellationToken cancellationToken)
    {
        var ipv4Task = LookupAsync(
            _options.Ipv4Endpoint,
            AddressFamily.InterNetwork,
            "IPv4",
            cancellationToken);
        var ipv6Task = LookupAsync(
            _options.Ipv6Endpoint,
            AddressFamily.InterNetworkV6,
            "IPv6",
            cancellationToken);

        await Task.WhenAll(ipv4Task, ipv6Task).ConfigureAwait(false);

        var result = new PublicIpAddressResult(
            await ipv4Task.ConfigureAwait(false),
            await ipv6Task.ConfigureAwait(false));

        if (result.Ipv4.Status == "unavailable" && result.Ipv6.Status == "unavailable")
        {
            throw new PublicIpLookupException(
                $"Neither public address family could be resolved. " +
                $"IPv4: {result.Ipv4.Message} IPv6: {result.Ipv6.Message}");
        }

        return result;
    }

    private async Task<PublicAddressLookupResult> LookupAsync(
        Uri endpoint,
        AddressFamily expectedFamily,
        string familyName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                endpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return PublicAddressLookupResult.Unavailable(
                    $"The {familyName} provider returned HTTP status {(int)response.StatusCode}.");
            }

            var value = (await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false)).Trim();

            if (value.Length is 0 or > 128 ||
                !IPAddress.TryParse(value, out var address) ||
                address.AddressFamily != expectedFamily)
            {
                return PublicAddressLookupResult.Unavailable(
                    $"The {familyName} provider returned an invalid IP address.");
            }

            return PublicAddressLookupResult.Available(address.ToString());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PublicAddressLookupResult.Unavailable(
                $"The {familyName} lookup timed out.");
        }
        catch (HttpRequestException)
        {
            return PublicAddressLookupResult.Unavailable(
                $"The {familyName} provider could not be reached.");
        }
    }
}

