# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) with custom pre-release phases.

## [Unreleased]

## [0.0.5] - 2025-11-12

### Added
- Full MCP .NET SDK integration (ModelContextProtocol v1.0.0)
  - SolidWorksTools.cs with `[McpServerToolType]` and `[McpServerTool]` attributes
  - Automatic tool discovery via `.WithToolsFromAssembly()`
  - stdio transport configuration
  - ISmartRouter dependency injection into MCP tools
- Real SolidWorks COM automation in SolidWorks2023Adapter
  - CreatePartAsync, OpenModelAsync, SaveModelAsync
  - CreateSketchAsync, ExitSketchAsync, SketchCircleAsync
  - CreateExtrusionAsync with depth and reverse parameters
  - GetMassPropertiesAsync
- SolidWorksConnection class for COM lifecycle management
- Configurable Part template path in SolidWorksSettings
  - `PartTemplatePath` property with `GetPartTemplatePath()` helper
  - Defaults to standard SolidWorks installation path

### Changed
- Program.cs to use MCP SDK hosting extensions
  - `.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`
  - Logging configured to stderr (MCP protocol uses stdout)
  - Background services disabled in production
- CircuitBreaker to track consecutive failures correctly
  - FailureRatio changed from 0.5 to 1.0 (100% failure rate)
  - SamplingDuration changed from 30s to 5s (short window)
  - Interface signature updated to forward CancellationToken
- SmartRouter to forward cancellation tokens through execution chain
  - Lambda signature: `async () =>` changed to `async (ct) =>`
  - VBA fallback now adds diagnostic message
- VbaGenerator completely rewritten
  - Parameterized script generation (no more dummy scripts)
  - Category-specific generators (Document, Sketch, Feature, Analysis)
  - Input validation before VBA generation
  - Template paths now configurable via settings
- ExecutionResult.SuccessResult signature
  - Added optional `message` parameter for diagnostic info
- SolidWorks2023Adapter constructor
  - Added ICircuitBreaker and ILoggerFactory dependencies
  - Integrated with SolidWorksConnection for COM management
- SmartRouterHostedService sample operation
  - Changed from "Sketch.CreateCircle" to "Sketch.SketchCircle"
  - Parameters changed from SketchName/Radius to CenterX/CenterY/Radius

### Fixed
- **CRITICAL**: Parameter casing mismatch between tools and adapter
  - SolidWorksTools.cs now uses PascalCase keys (Path, Plane, CenterX, etc.)
  - Previously used camelCase (path, plane, centerX) which adapter couldn't find
  - This was preventing ALL parameters from being passed correctly
- Hardcoded template path in CreatePartAsync
  - Now uses `_settings.GetPartTemplatePath()` for configurability
- Missing Reverse parameter in CreateExtrusionAsync
  - Now reads and applies the `Reverse` boolean parameter
  - Returned in success result for verification
- Nullable value type coalescing errors
  - Changed from `??` operator to pattern matching with `is double x ? x : default`
- Circuit breaker type inference failures
  - Updated all lambda signatures to include CancellationToken parameter
- Missing packages in Integration.Tests
  - Added Microsoft.Extensions.Logging and Microsoft.Extensions.Logging.Console
- Wrong namespace in tool files
  - Changed from `ModelContextProtocol.SDK` to `ModelContextProtocol.Server`

### Removed
- McpServer.cs hosted service (replaced by MCP SDK)
- McpServerSettings configuration class (replaced by SDK)
- McpServer section in appsettings.json (no longer needed)
- Dead test files (UnitTest1.cs references to non-existent ToolRegistry)

### Technical Details
- MCP SDK: ModelContextProtocol v1.0.0
- Tool count: 5 working tools (CreatePart, OpenModel, CreateSketch, SketchCircle, CreateExtrusion)
- All operations route through ISmartRouter with circuit breaker protection
- VBA fallback generates executable scripts with parameter validation
- Build: Successful (0 errors, 2 warnings about System.Text.Json redundancy)

### Known Issues
- System.Text.Json package redundancy warning (NU1510) - minor, does not affect functionality
- VBA script execution not yet implemented (scripts generated but not auto-executed)
- Only 5 of 88+ planned tools implemented

### Notes
- **Phase 3 (MCP SDK Integration) complete**
- MCP server successfully connects and registers tools with Claude Code
- Real SolidWorks COM automation working
- Parameter passing verified and fixed
- Ready for Phase 4 (Tool Implementation & Testing) towards beta v0.1.0

## [0.0.4] - 2025-11-11

### Added
- MCP tool wrappers for 8 SolidWorks operations
  - **DocumentTools**: CreatePart, OpenModel, SaveModel
  - **SketchTools**: CreateSketch, ExitSketch, SketchCircle
  - **FeatureTools**: CreateExtrusion
  - **AnalysisTools**: GetMassProperties
- ToolRegistry with dependency injection support
  - Registers all 8 tools with ISolidWorksAdapter
  - Organized by category (Document, Sketch, Feature, Analysis)
- MCP Server hosted service (McpServer.cs)
  - Logs registered tools on startup
  - Configured for stdio transport
  - Ready for MCP .NET SDK integration
- McpServerSettings configuration class
  - Name, Version, Transport properties
- SolidWorks setup documentation (SOLIDWORKS_SETUP.md)
  - DLL reference requirements
  - Windows targeting configuration
  - STA threading requirements
  - Development setup steps
  - Troubleshooting guide
- appsettings.json McpServer section
  - Server name, version, transport configuration

### Changed
- Updated FurniOx.SolidWorks.Tools.csproj
  - Target framework: net10.0 → net10.0-windows
  - Added ProjectReference to FurniOx.SolidWorks.Core
- Updated FurniOx.SolidWorks.Tools.Tests.csproj
  - Target framework: net10.0 → net10.0-windows
- Updated Program.cs with explicit Main method
  - Added [STAThread] attribute for STA apartment state
  - Fixed threading model for COM automation
- Updated ToolRegistry test to verify all 8 tools
  - Uses Moq to mock ISolidWorksAdapter
  - Verifies tool count and categories

### Fixed
- STA threading issue in Program.cs
  - Replaced `Thread.CurrentThread.SetApartmentState()` with `[STAThread]` attribute
  - Fixed "Failed to set the specified COM apartment state" error
- Tools project missing Core reference
  - Added ProjectReference to FurniOx.SolidWorks.Core
  - Fixed ISolidWorksAdapter namespace errors

### Technical Details
- All 8 tools delegate to ISolidWorksAdapter
- Pattern: `_adapter.ExecuteAsync("Category.Operation", parameters, cancellationToken)`
- Tools injected via constructor dependency injection
- Build: Successful (0 errors, 2 warnings)

### Notes
- Phase 2 (MCP Tool Wrappers) complete
- MCP server runs and registers all 8 tools
- Ready for Phase 3 (Integration and Testing)

## [0.0.3] - 2025-11-11

### Added
- Complete .NET 10.0 solution structure with 7 projects
  - FurniOx.SolidWorks.MCP (Console application, entry point)
  - FurniOx.SolidWorks.Core (Class library, COM adapter)
  - FurniOx.SolidWorks.Tools (Class library, tool implementations)
  - FurniOx.SolidWorks.Shared (Class library, shared utilities)
  - FurniOx.SolidWorks.Core.Tests (xUnit test project)
  - FurniOx.SolidWorks.Tools.Tests (xUnit test project)
  - FurniOx.SolidWorks.Integration.Tests (xUnit test project)
- Directory.Build.props with shared MSBuild properties
- Directory.Packages.props with Central Package Management
  - SolidWorks Interop packages (v31.5.0)
  - Microsoft.Extensions.* packages (v8.0.0)
  - Serilog for structured logging (v3.1.1)
  - Polly for resilience patterns (v8.2.1)
  - xUnit, Moq, FluentAssertions for testing
- global.json pinning .NET SDK to 10.0.100
- .editorconfig with .NET coding conventions
- ARCHITECTURE.md comprehensive documentation
  - System architecture diagrams
  - Component responsibilities
  - Design patterns (DI, Circuit Breaker, Retry, Repository)
  - Data flow documentation
  - Threading model (STA for COM)
  - Performance targets
  - Testing strategy

### Changed
- Updated README.md to reflect C# implementation
  - Changed from C to C# throughout
  - Updated prerequisites (removed C compiler, added .NET 8 SDK)
  - Updated build instructions (CMake → dotnet build)
  - Updated project structure to show C# multi-project layout
  - Updated roadmap to show C# phases
- Updated version to 0.0.3 across all configuration files
- System.Text.Json updated to 8.0.5 (fixes security vulnerabilities)

### Technical Details
- Target Framework: .NET 10.0
- Language Version: C# 12
- Nullable Reference Types: Enabled
- Implicit Usings: Enabled
- Documentation Generation: Enabled
- Build: Successful (0 errors, 2 warnings)

### Notes
- Phase 1 (Foundation) complete
- All 7 projects build successfully
- 88+ NuGet packages installed and configured
- Ready for Phase 2 (Core Architecture implementation)

## [0.0.2] - 2025-11-11

### Added
- Reference implementations directory structure
- Comprehensive reference/README.md documentation
- Cloned three reference repositories:
  - vespo-solidworks-mcp-ts (TypeScript SolidWorks patterns)
  - eyfel-version-aware-mcp (Version awareness strategies)
  - mcp-sdk-dotnet (Official C# MCP SDK)
- .gitignore rules for reference directory
- reference/.gitkeep to track directory structure

### Changed
- Renamed "Reference MCP" to "reference" (removed spaces, lowercase)
- Renamed subdirectories to kebab-case naming convention
- Updated README.md with reference implementations section
- Updated project structure diagram in README.md
- Version bump to 0.0.2

### Notes
- Reference repositories are for learning/architectural guidance only
- Not dependencies or submodules
- Full git histories preserved for easy browsing

## [0.0.1] - 2025-11-11

### Added
- Project initialization
- Documentation structure (README.md, VERSIONING.md, CHANGELOG.md)
- Git repository setup
- Initial project scaffold

### Notes
- First alpha release
- Development phase begins

---

## Version History Legend

- **Added**: New features
- **Changed**: Changes in existing functionality
- **Deprecated**: Soon-to-be removed features
- **Removed**: Removed features
- **Fixed**: Bug fixes
- **Security**: Security fixes

## Version Phases

- `0.0.x` - Alpha (unstable, experimental)
- `0.x.0` - Beta (feature complete, testing)
- `1.0.0+` - Production (stable releases)

[Unreleased]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/compare/v0.0.5...HEAD
[0.0.5]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/compare/v0.0.4...v0.0.5
[0.0.4]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/compare/v0.0.3...v0.0.4
[0.0.3]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/compare/v0.0.2...v0.0.3
[0.0.2]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/compare/v0.0.1...v0.0.2
[0.0.1]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/releases/tag/v0.0.1
