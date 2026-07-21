using IpAddressMcp.Public;

namespace IpAddressMcp.Tests;

public sealed class PublicIpOptionsTests
{
    [Fact]
    public void HasValidEndpoints_AcceptsAbsoluteHttpsEndpoints()
    {
        var options = new PublicIpOptions
        {
            Ipv4Endpoint = new Uri("https://api.ipify.org"),
            Ipv6Endpoint = new Uri("https://api6.ipify.org"),
        };

        Assert.True(PublicIpOptions.HasValidEndpoints(options));
    }

    [Fact]
    public void HasValidEndpoints_DefaultsAreValid()
    {
        Assert.True(PublicIpOptions.HasValidEndpoints(new PublicIpOptions()));
    }

    [Theory]
    [InlineData("http://api.ipify.org")]
    [InlineData("ftp://api.ipify.org")]
    public void HasValidEndpoints_RejectsNonHttpsSchemes(string endpoint)
    {
        var options = new PublicIpOptions
        {
            Ipv4Endpoint = new Uri(endpoint),
            Ipv6Endpoint = new Uri("https://api6.ipify.org"),
        };

        Assert.False(PublicIpOptions.HasValidEndpoints(options));
    }

    [Fact]
    public void HasValidEndpoints_RejectsRelativeEndpoints()
    {
        var options = new PublicIpOptions
        {
            Ipv4Endpoint = new Uri("https://api.ipify.org"),
            Ipv6Endpoint = new Uri("/relative", UriKind.Relative),
        };

        Assert.False(PublicIpOptions.HasValidEndpoints(options));
    }
}
