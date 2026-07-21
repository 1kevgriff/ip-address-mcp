using System.Reflection;
using IpAddressMcp.Tools;
using ModelContextProtocol.Server;

namespace IpAddressMcp.Tests;

public sealed class IpAddressToolsTests
{
    [Fact]
    public void ToolType_ExposesExactlyTheRequestedStructuredTools()
    {
        var tools = typeof(IpAddressTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetCustomAttribute<McpServerToolAttribute>(),
            })
            .Where(tool => tool.Attribute is not null)
            .OrderBy(tool => tool.Attribute!.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, tools.Length);
        Assert.Equal("GetLocalIPAddress", tools[0].Attribute!.Name);
        Assert.Equal("GetPublicIPAddress", tools[1].Attribute!.Name);
        Assert.All(tools, tool =>
        {
            Assert.True(tool.Attribute!.ReadOnly);
            Assert.False(tool.Attribute.Destructive);
            Assert.True(tool.Attribute.Idempotent);
            Assert.True(tool.Attribute.UseStructuredContent);
        });
    }
}

