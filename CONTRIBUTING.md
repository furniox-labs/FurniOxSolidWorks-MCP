# Contributing

This repository is the public/basic MCP surface.

## Scope

Good public contributions:
- generic SolidWorks MCP tooling
- document, selection, sketch, feature, export, configuration, sorting, and assembly-browser improvements
- bug fixes that do not depend on internal bridge, batch, or governance workflows
- tests, docs, and boundary validation for the public surface

Out of scope for the public repo:
- bridge/add-in internals
- batch processing workflows
- custom-property and summary-info governance flows
- internal deployment or customer-specific automation

## Workflow

1. Open an issue or pull request in the public repository.
2. Keep the change generic and runnable without private projects.
3. Run the public validation before submitting:
   - `dotnet build FurniOxSolidWorks-MCP.sln`
   - `dotnet test FurniOxSolidWorks-MCP.sln`
   - `pwsh ./scripts/check-public-boundary.ps1`

Do not commit local launcher files like `.mcp.json` or build artifacts under `bin/`, `obj/`, or `.artifacts/`.

Maintainers validate accepted public changes in the private source-of-truth repo before exporting them back to the public repo.

## Style

- Keep public contracts stable unless the change clearly requires a breaking change.
- Prefer focused handlers and thin coordinators over adding more logic to central dispatchers.
- Add tests for new public operations and routing changes.
