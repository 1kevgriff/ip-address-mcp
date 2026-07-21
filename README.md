# IP Address MCP Server

A local, cross-platform MCP server built with .NET 10 and the official MCP C# SDK. It exposes two read-only tools over stdio:

- `GetLocalIPAddress` returns all active, non-loopback IPv4 and IPv6 addresses with interface metadata.
- `GetPublicIPAddress` returns the public IPv4 and IPv6 addresses observed by ipify. Either family can be unavailable without failing the other.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An MCP client that supports stdio servers
- Internet access for public-address lookups

## Build and test

```powershell
dotnet restore IpAddressMcp.slnx
dotnet build IpAddressMcp.slnx -c Release --no-restore
dotnet test IpAddressMcp.slnx -c Release --no-build
```

Run the server directly with:

```powershell
dotnet run --project src/IpAddressMcp.Server/IpAddressMcp.Server.csproj -c Release
```

The process waits for MCP JSON-RPC messages on standard input. Standard output is reserved for protocol traffic; application logs are written to standard error.

## MCP client configuration

Build the server, then configure your client to launch the resulting DLL. Replace the path below with the absolute path on your machine.

```json
{
  "mcpServers": {
    "ip-address": {
      "command": "dotnet",
      "args": [
        "<absolute-path-to-repo>\\src\\IpAddressMcp.Server\\bin\\Release\\net10.0\\IpAddressMcp.Server.dll"
      ]
    }
  }
}
```

## Tool results

`GetLocalIPAddress` has no arguments and returns:

```json
{
  "addresses": [
    {
      "address": "192.168.1.25",
      "addressFamily": "IPv4",
      "interfaceName": "Ethernet",
      "interfaceType": "Ethernet",
      "scopeId": null
    }
  ]
}
```

The local result includes private, APIPA, link-local, physical, virtual, and tunnel-interface addresses when their interfaces are operational. A scoped IPv6 address reports its numeric zone separately as `scopeId`.

`GetPublicIPAddress` has no arguments and returns independent family results:

```json
{
  "ipv4": {
    "status": "available",
    "address": "203.0.113.10",
    "message": null
  },
  "ipv6": {
    "status": "unavailable",
    "address": null,
    "message": "The IPv6 provider could not be reached."
  }
}
```

If neither family can be resolved, the tool returns an MCP error. Provider exception details and response bodies are not exposed.

## Public IP configuration

The defaults are `https://api.ipify.org` for IPv4 and `https://api6.ipify.org` for IPv6. Override them through standard .NET configuration, including environment variables:

- `PublicIp__Ipv4Endpoint`
- `PublicIp__Ipv6Endpoint`

Overrides must be absolute HTTPS URLs. Requests run concurrently with a 10-second timeout and responses must contain an address of the expected family.

Calling `GetPublicIPAddress` sends HTTPS requests to the configured providers. Those services can observe the server's source address and normal HTTP request metadata; review the provider's privacy policy before use.
