using System.Collections.Concurrent;
using ModelContextProtocol.Client;

namespace IpAddressMcp.Tests;

public sealed class McpStdioIntegrationTests
{
    [Fact]
    public async Task Server_ListsExactToolsAndInvokesLocalToolOverStdio()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var serverDll = Path.Combine(
            repositoryRoot,
            "src",
            "IpAddressMcp.Server",
            "bin",
            configuration,
            "net10.0",
            "IpAddressMcp.Server.dll");

        Assert.True(File.Exists(serverDll), $"Server assembly not found at {serverDll}.");

        var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        var standardError = new ConcurrentQueue<string>();
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ip-address-mcp-integration-test",
            Command = "dotnet",
            Arguments = [serverDll],
            WorkingDirectory = repositoryRoot,
            InheritEnvironmentVariables = false,
            EnvironmentVariables = environment,
            StandardErrorLines = line => standardError.Enqueue(line),
            ShutdownTimeout = TimeSpan.FromSeconds(5),
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var client = await McpClient.CreateAsync(
            transport,
            cancellationToken: timeout.Token);

        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);

        Assert.Equal(
            ["GetLocalIPAddress", "GetPublicIPAddress"],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));

        var result = await client.CallToolAsync(
            "GetLocalIPAddress",
            new Dictionary<string, object?>(),
            cancellationToken: timeout.Token);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        Assert.True(result.StructuredContent.Value.TryGetProperty("addresses", out _));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "IpAddressMcp.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
