# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

`AGENTS.md` holds the full contributor guide (structure, coding style, testing, PR conventions, security notes). Read it too — this file focuses on commands and the big-picture architecture.

## Commands

Requires the **.NET 10 SDK**. Run from the repository root; the solution file is `IpAddressMcp.slnx`.

```powershell
dotnet restore IpAddressMcp.slnx
dotnet build IpAddressMcp.slnx -c Release --no-restore
dotnet test IpAddressMcp.slnx -c Release --no-build
dotnet format IpAddressMcp.slnx --no-restore --verify-no-changes   # style gate
dotnet run --project src/IpAddressMcp.Server/IpAddressMcp.Server.csproj -c Release
```

Run a single test by fully-qualified name:

```powershell
dotnet test IpAddressMcp.slnx -c Release --no-build --filter "FullyQualifiedName~GetPublicIpAddressesAsync_ReturnsPartialResultWhenIpv6IsUnavailable"
```

`TreatWarningsAsErrors` is on globally (`Directory.Build.props`), so builds fail on any warning.

## Architecture

A single stdio MCP server exposing two read-only tools. `Program.cs` is the composition root: it clears logging providers and routes **all** logs to stderr (`LogToStandardErrorThreshold = Trace`) because **stdout is reserved exclusively for MCP JSON-RPC** — never `Console.WriteLine` to stdout. Tools are auto-discovered via `.WithToolsFromAssembly()`.

Two independent feature slices, each behind an injectable interface for deterministic tests:

- **`Local/`** — `IpAddressTools.GetLocalIPAddress` → `ILocalIpAddressService` → `INetworkInterfaceProvider`. The provider (`SystemNetworkInterfaceProvider`) wraps `System.Net.NetworkInformation` into `NetworkInterfaceSnapshot` records so the service's filtering/sorting logic is testable against fakes without real hardware. The service filters to operational, non-loopback IPv4/IPv6 unicast addresses and splits an IPv6 zone index into a separate `scopeId` field. Synchronous — no I/O beyond the OS query.

- **`Public/`** — `IpAddressTools.GetPublicIPAddress` → `IPublicIpAddressService` (`PublicIpAddressService`), a typed `HttpClient` (10s timeout, 1 KB response cap) hitting ipify. IPv4 and IPv6 are looked up **concurrently and independently**: one family failing yields a partial result (`status: available|unavailable`), and only if **both** fail does the service throw `PublicIpLookupException`, which the tool converts to an `McpException`. Provider exception details and response bodies are never surfaced to the client. Endpoints are configurable via `PublicIpOptions` (section `PublicIp`, keys `PublicIp__Ipv4Endpoint` / `PublicIp__Ipv6Endpoint`) and validated at startup to be absolute HTTPS URLs.

## Invariants to preserve

- Tool names `GetLocalIPAddress` and `GetPublicIPAddress` are the public contract — do not rename.
- Keep stdout clean of anything but JSON-RPC; diagnostics go to stderr.
- Public endpoints must stay absolute HTTPS; partial-result semantics (either family may be unavailable) must hold.
- When changing tool registration, schemas, serialization, or transport, update/extend the stdio integration test (`tests/.../McpStdioIntegrationTests.cs`), which launches the built server DLL and asserts the exact tool list — so **build before running that test**.
- A registered/running instance of this MCP server locks its output DLL, so `dotnet build -c Release` fails the copy step (`MSB3027`, "being used by another process") while the server is running. Stop the server before a Release rebuild, or build/test in `-c Debug` (a separate output path) when the Release instance is live.
