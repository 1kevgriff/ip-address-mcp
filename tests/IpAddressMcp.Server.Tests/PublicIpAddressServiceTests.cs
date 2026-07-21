using System.Net;
using IpAddressMcp.Public;
using Microsoft.Extensions.Options;

namespace IpAddressMcp.Tests;

public sealed class PublicIpAddressServiceTests
{
    [Fact]
    public async Task GetPublicIpAddressesAsync_ReturnsBothAddressFamilies()
    {
        using var client = CreateClient((request, _) => Task.FromResult(
            TextResponse(request.RequestUri!.Host == "ipv4.test"
                ? "203.0.113.10"
                : "2001:db8::10")));
        var service = CreateService(client);

        var result = await service.GetPublicIpAddressesAsync(CancellationToken.None);

        Assert.Equal("available", result.Ipv4.Status);
        Assert.Equal("203.0.113.10", result.Ipv4.Address);
        Assert.Null(result.Ipv4.Message);
        Assert.Equal("available", result.Ipv6.Status);
        Assert.Equal("2001:db8::10", result.Ipv6.Address);
        Assert.Null(result.Ipv6.Message);
    }

    [Fact]
    public async Task GetPublicIpAddressesAsync_ReturnsPartialResultWhenIpv6IsUnavailable()
    {
        using var client = CreateClient((request, _) =>
            request.RequestUri!.Host == "ipv4.test"
                ? Task.FromResult(TextResponse("198.51.100.5"))
                : Task.FromException<HttpResponseMessage>(new HttpRequestException("sensitive")));
        var service = CreateService(client);

        var result = await service.GetPublicIpAddressesAsync(CancellationToken.None);

        Assert.Equal("available", result.Ipv4.Status);
        Assert.Equal("unavailable", result.Ipv6.Status);
        Assert.Null(result.Ipv6.Address);
        Assert.Equal("The IPv6 provider could not be reached.", result.Ipv6.Message);
        Assert.DoesNotContain("sensitive", result.Ipv6.Message);
    }

    [Fact]
    public async Task GetPublicIpAddressesAsync_ReturnsPartialResultWhenIpv4IsUnavailable()
    {
        using var client = CreateClient((request, _) =>
            request.RequestUri!.Host == "ipv6.test"
                ? Task.FromResult(TextResponse("2001:db8::5"))
                : Task.FromException<HttpResponseMessage>(new HttpRequestException("sensitive")));
        var service = CreateService(client);

        var result = await service.GetPublicIpAddressesAsync(CancellationToken.None);

        Assert.Equal("available", result.Ipv6.Status);
        Assert.Equal("2001:db8::5", result.Ipv6.Address);
        Assert.Equal("unavailable", result.Ipv4.Status);
        Assert.Null(result.Ipv4.Address);
        Assert.Equal("The IPv4 provider could not be reached.", result.Ipv4.Message);
        Assert.DoesNotContain("sensitive", result.Ipv4.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPublicIpAddressesAsync_RejectsEmptyResponses(string ipv6Response)
    {
        using var client = CreateClient((request, _) => Task.FromResult(
            TextResponse(request.RequestUri!.Host == "ipv4.test"
                ? "192.0.2.10"
                : ipv6Response)));
        var service = CreateService(client);

        var result = await service.GetPublicIpAddressesAsync(CancellationToken.None);

        Assert.Equal("available", result.Ipv4.Status);
        Assert.Equal("unavailable", result.Ipv6.Status);
        Assert.Equal("The IPv6 provider returned an invalid IP address.", result.Ipv6.Message);
    }

    [Fact]
    public async Task GetPublicIpAddressesAsync_RejectsOverlyLongResponses()
    {
        var oversized = new string('9', 129);
        using var client = CreateClient((request, _) => Task.FromResult(
            TextResponse(request.RequestUri!.Host == "ipv4.test"
                ? "192.0.2.10"
                : oversized)));
        var service = CreateService(client);

        var result = await service.GetPublicIpAddressesAsync(CancellationToken.None);

        Assert.Equal("available", result.Ipv4.Status);
        Assert.Equal("The IPv6 provider returned an invalid IP address.", result.Ipv6.Message);
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("192.0.2.12")]
    public async Task GetPublicIpAddressesAsync_RejectsInvalidIpv6Responses(string ipv6Response)
    {
        using var client = CreateClient((request, _) => Task.FromResult(
            TextResponse(request.RequestUri!.Host == "ipv4.test"
                ? "192.0.2.10"
                : ipv6Response)));
        var service = CreateService(client);

        var result = await service.GetPublicIpAddressesAsync(CancellationToken.None);

        Assert.Equal("available", result.Ipv4.Status);
        Assert.Equal("unavailable", result.Ipv6.Status);
        Assert.Equal("The IPv6 provider returned an invalid IP address.", result.Ipv6.Message);
    }

    [Fact]
    public async Task GetPublicIpAddressesAsync_ReportsHttpFailureWithoutLeakingContent()
    {
        using var client = CreateClient((request, _) => Task.FromResult(
            request.RequestUri!.Host == "ipv4.test"
                ? TextResponse("203.0.113.20")
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("provider internals"),
                }));
        var service = CreateService(client);

        var result = await service.GetPublicIpAddressesAsync(CancellationToken.None);

        Assert.Equal("The IPv6 provider returned HTTP status 503.", result.Ipv6.Message);
        Assert.DoesNotContain("provider internals", result.Ipv6.Message);
    }

    [Fact]
    public async Task GetPublicIpAddressesAsync_SanitizesUnexpectedProviderFailures()
    {
        using var client = CreateClient((request, _) =>
            request.RequestUri!.Host == "ipv4.test"
                ? Task.FromResult(TextResponse("203.0.113.40"))
                : Task.FromException<HttpResponseMessage>(
                    new InvalidOperationException("secret internal detail")));
        var service = CreateService(client);

        var result = await service.GetPublicIpAddressesAsync(CancellationToken.None);

        Assert.Equal("available", result.Ipv4.Status);
        Assert.Equal("unavailable", result.Ipv6.Status);
        Assert.Equal("The IPv6 lookup failed.", result.Ipv6.Message);
        Assert.DoesNotContain("secret internal detail", result.Ipv6.Message);
    }

    [Fact]
    public async Task GetPublicIpAddressesAsync_ReturnsTimeoutAsPartialFailure()
    {
        using var client = CreateClient(
            async (request, cancellationToken) =>
            {
                if (request.RequestUri!.Host == "ipv4.test")
                {
                    return TextResponse("203.0.113.30");
                }

                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable");
            },
            TimeSpan.FromMilliseconds(50));
        var service = CreateService(client);

        var result = await service.GetPublicIpAddressesAsync(CancellationToken.None);

        Assert.Equal("available", result.Ipv4.Status);
        Assert.Equal("The IPv6 lookup timed out.", result.Ipv6.Message);
    }

    [Fact]
    public async Task GetPublicIpAddressesAsync_PropagatesCallerCancellation()
    {
        using var client = CreateClient(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        });
        var service = CreateService(client);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetPublicIpAddressesAsync(cancellation.Token));
    }

    [Fact]
    public async Task GetPublicIpAddressesAsync_ThrowsSanitizedErrorWhenBothFamiliesFail()
    {
        using var client = CreateClient((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("secret host details")));
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<PublicIpLookupException>(() =>
            service.GetPublicIpAddressesAsync(CancellationToken.None));

        Assert.Contains("Neither public address family could be resolved.", exception.Message);
        Assert.DoesNotContain("secret host details", exception.Message);
    }

    private static PublicIpAddressService CreateService(HttpClient client) =>
        new(client, Options.Create(new PublicIpOptions
        {
            Ipv4Endpoint = new Uri("https://ipv4.test"),
            Ipv6Endpoint = new Uri("https://ipv6.test"),
        }));

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        TimeSpan? timeout = null) =>
        new(new StubHttpMessageHandler(handler))
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(5),
        };

    private static HttpResponseMessage TextResponse(string value) =>
        new(HttpStatusCode.OK) { Content = new StringContent(value) };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
