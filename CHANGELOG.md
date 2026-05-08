# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-alpha.3] - 2026-05-08

### Added
- Public single-document tools for analysis, custom properties, summary information, document governance, cross-reference scans, and equation-reference scans/repairs.
- Private batch wrappers for cross-reference, equation-reference, document-governance, SWOOD, add-in, and diagnostic workflows.
- Public boundary tests covering private tool-family leaks and private core implementation leaks.

### Changed
- Reworked the public/private split so public tools stay available through direct COM while batch, bridge/add-in, diagnostic, and SWOOD tooling remains private.
- Moved shared reference-scan runners into the core adapter layer so public single-document tools and private batch tools use the same scan logic without duplicate implementations.
- Hardened the public export pipeline to strip private project references, private compile includes, bridge/add-in tools, batch tool wrappers, and bridge parity tests from exported public source.
- Split diagnostic operation names from the SWOOD catalog and made bridge-only private operations fail fast when the add-in is unavailable.
- Updated direct COM `open_model` and `save_model` handling so public fallback behavior honors the same user-facing options exposed by the tools.

### Fixed
- Public host registration now includes the single-document document-governance tools.
- Public tests no longer depend on private projects, making clean public checkouts build and test independently.
- Batch document-governance tests now use matching file and class names.

## [0.1.0-alpha.2] - 2026-03-28

### Changed
- Windows release packaging is now built from the exported public source tree instead of private build output
- Packaged Windows releases are now self-contained single-file builds for easier installation on non-technical machines
- The packaged release now contains only the runtime files users actually need:
  - `FurniOx.SolidWorks.MCP.exe`
  - `appsettings.json`
  - `appsettings.local.example.json`
  - `.mcp.exe.example.json`
  - `README.md`
  - `LICENSE`

### Fixed
- Removed the redundant direct `Shared` project reference from the MCP host so self-contained publish works reliably
- Removed unnecessary COM hosting from the public `Core` project, eliminating the self-contained publish warning
- Installation docs now match the real packaged release behavior

## [0.1.0-alpha.1] - 2026-03-28

### Added
- First public open-source release under `furniox-labs/FurniOxSolidWorks-MCP`
- Public/basic MCP surface for:
  - document lifecycle and document info
  - configuration management
  - assembly browsing with `list_assembly_components`
  - selection helpers
  - sketch creation and editing
  - feature creation for extrusion, revolve, fillet, and shell
  - export to STEP, IGES, STL, PDF, and DXF
  - sorting and folder inspection helpers
- Deterministic public export pipeline and boundary checks
- `.mcp.example.json` for source-based MCP setup
- `.mcp.exe.example.json` for executable-based MCP setup
- `scripts/publish-public-release.ps1` for single-file Windows release builds

### Changed
- Versioning now uses SemVer prerelease tags instead of the old internal `0.0.x` progression
- Public README now documents:
  - alpha release status
  - working scope
  - testing expectations
  - installation from source
  - installation from a packaged Windows executable
- Public repo metadata now targets `furniox-labs/FurniOxSolidWorks-MCP`

### Fixed
- Public export no longer leaks local launcher files or private-only code
- Public release validation now requires a clean build and test pass from the exported source tree
- Public exported solution is warning-clean and buildable on its own

### Notes
- This is a working alpha release.
- More testing is still needed across SolidWorks versions, template setups, and larger real-world assemblies.

[Unreleased]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/compare/v0.1.0-alpha.3...HEAD
[0.1.0-alpha.3]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/releases/tag/v0.1.0-alpha.3
[0.1.0-alpha.2]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/releases/tag/v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/releases/tag/v0.1.0-alpha.1
