# FurniOx SolidWorks MCP

Public/basic SolidWorks MCP server for day-to-day CAD editing and file operations over native COM.

## Public Surface

Public MCP tools include:
- document lifecycle and document info
- configuration management
- assembly browsing through `list_assembly_components`
- selection helpers
- sketch creation, editing, parametric, inspection, and specialized sketch tools
- feature creation for extrusion, revolve, fillet, and shell
- export to STEP, IGES, STL, PDF, and DXF
- sorting and folder inspection helpers

Public resources include:
- `solidworks://connection/status`
- `solidworks://document/active`
- `solidworks://metrics/performance`
- `solidworks://project/about`

## Not In The Public Repo

The public/basic distribution does not include:
- deep model analysis tools such as `analyze_part`, `analyze_assembly`, and `analyze_drawing`
- batch analysis or batch property workflows
- custom-property and summary-info tools
- rename/governance workflows
- bridge/add-in integration and other internal acceleration layers

## Requirements

- Windows 10/11
- .NET SDK `10.0.100` or a compatible minor roll-forward
- SolidWorks installed for real server usage and manual integration testing

## Build

```powershell
dotnet restore
dotnet build FurniOxSolidWorks-MCP.sln
```

## Test

Unit and wrapper tests run without SolidWorks. COM-backed integration tests are opt-in and only execute when `SOLIDWORKS_INTEGRATION_TESTS=1`.

```powershell
dotnet test FurniOxSolidWorks-MCP.sln
```

To run the COM-backed integration tests manually:

```powershell
$env:SOLIDWORKS_INTEGRATION_TESTS = '1'
dotnet test tests/FurniOx.SolidWorks.Integration.Tests
```

To verify the public boundary:

```powershell
pwsh ./scripts/check-public-boundary.ps1
```

## Run

```powershell
dotnet run --project src/FurniOx.SolidWorks.MCP
```

The server uses stdio transport and is intended to be launched by an MCP client.

An MCP client configuration example is provided in `.mcp.example.json`. Replace `<ABSOLUTE_PATH_TO_REPO>` with your local checkout path.

## Configuration

Runtime settings live in `src/FurniOx.SolidWorks.MCP/appsettings.json` under `SolidWorks`.

Important settings:
- `ProgIdVersion`: optional SolidWorks major revision hint for version-specific COM ProgIDs
- `TemplateVersion`: optional installation year used only for conventional template path fallbacks
- `PartTemplatePath`, `AssemblyTemplatePath`, `DrawingTemplatePath`: explicit template paths when you do not want to rely on SolidWorks user preferences
- `ComParameterLimit`: guardrail for oversized tool payloads before they reach COM
- `CircuitBreaker`: failure threshold and reset timeout
- `PublicProfile:Contact`: GitHub and LinkedIn links used by `solidworks://project/about`

Template resolution order for document creation:
1. explicit template path from configuration
2. conventional path derived from `TemplateVersion`
3. SolidWorks user preference default template

## Architecture

The public/basic repo follows a consistent structure:
- `SolidWorks2023Adapter` is the public routing and composition root
- public operation catalogs live in `src/FurniOx.SolidWorks.Core/Operations/`
- large domains use thin coordinators plus focused sub-handlers
- MCP tool classes are split by capability family
- the public surface is intentionally kept free of internal bridge, batch, governance, and deep-analysis code

## Project Layout

```text
src/
  FurniOx.SolidWorks.Shared/       Public shared models and configuration
  FurniOx.SolidWorks.Core/         Public COM adapter, operations, routing, and helpers
  FurniOx.SolidWorks.MCP/          Public MCP host, tools, and resources

tests/
  FurniOx.SolidWorks.Core.Tests/
  FurniOx.SolidWorks.Tools.Tests/
  FurniOx.SolidWorks.Integration.Tests/
```

## Open-Core Model

This repository is the public/basic MCP surface.

Internal/private extensions are maintained separately and are not required to build or use the public server. The public boundary is enforced by the solution layout, project references, and `scripts/check-public-boundary.ps1`.

## Contact

Recommended public contact channels:
- GitHub organization or public repository
- LinkedIn profile for direct outreach

Populate these in `SolidWorks:PublicProfile:Contact` so both the README workflow and the MCP `solidworks://project/about` resource point to the same locations.
