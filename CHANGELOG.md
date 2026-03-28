# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/compare/v0.1.0-alpha.1...HEAD
[0.1.0-alpha.1]: https://github.com/furniox-labs/FurniOxSolidWorks-MCP/releases/tag/v0.1.0-alpha.1
