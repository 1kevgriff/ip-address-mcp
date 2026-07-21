using IpAddressMcp.Local;
using IpAddressMcp.Public;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    // Standard output belongs exclusively to the stdio MCP transport.
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddOptions<PublicIpOptions>()
    .Bind(builder.Configuration.GetSection(PublicIpOptions.SectionName))
    .Validate(PublicIpOptions.HasValidEndpoints, "Public IP endpoints must be absolute HTTPS URLs.")
    .ValidateOnStart();

builder.Services.AddSingleton<INetworkInterfaceProvider, SystemNetworkInterfaceProvider>();
builder.Services.AddSingleton<ILocalIpAddressService, LocalIpAddressService>();
builder.Services
    .AddHttpClient<IPublicIpAddressService, PublicIpAddressService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        client.MaxResponseContentBufferSize = 1024;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ip-address-mcp/1.0");
    });

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

