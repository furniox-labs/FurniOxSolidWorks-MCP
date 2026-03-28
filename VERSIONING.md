# Versioning Strategy

This project uses Semantic Versioning with prerelease tags.

## Version Format

`MAJOR.MINOR.PATCH[-PRERELEASE.NUMBER]`

Examples:
- `0.1.0-alpha.1`
- `0.1.0-alpha.2`
- `0.1.0-beta.1`
- `0.1.0-rc.1`
- `0.1.0`

## Release Stages

### Alpha
- Format: `0.x.y-alpha.n`
- Meaning: working release, still under active validation
- Expectation: core workflows should work, but broader SolidWorks environment testing is still needed
- Allowed changes: breaking changes are acceptable between alpha releases

### Beta
- Format: `0.x.y-beta.n`
- Meaning: feature scope is mostly settled and external testing should expand
- Expectation: fewer breaking changes, stronger compatibility expectations

### Release Candidate
- Format: `0.x.y-rc.n`
- Meaning: release candidate for stable launch
- Expectation: no intentional breaking changes without strong reason

### Stable
- Format: `1.x.y`
- Meaning: supported stable release
- Expectation: standard Semantic Versioning rules apply

## How Versions Move

- Increase `MAJOR` for incompatible public contract changes after `1.0.0`
- Increase `MINOR` for backward-compatible capability additions
- Increase `PATCH` for backward-compatible fixes
- Increase prerelease number for the next alpha, beta, or rc cut

Typical path:

```text
0.1.0-alpha.1 -> 0.1.0-alpha.2 -> 0.1.0-beta.1 -> 0.1.0-rc.1 -> 0.1.0
```

## Current Release Policy

The public repo is currently in alpha.

- Current version: `0.1.0-alpha.2`
- Status: working public/basic MCP
- Validation note: more testing is needed across SolidWorks installs, templates, and real-world assemblies

## Tagging Convention

All releases are tagged:

```bash
git tag -a v0.1.0-alpha.1 -m "Alpha release 0.1.0-alpha.1"
git tag -a v0.1.0-beta.1 -m "Beta release 0.1.0-beta.1"
git tag -a v0.1.0 -m "Stable release 0.1.0"
```

## Changelog Policy

Maintain [CHANGELOG.md](CHANGELOG.md) in Keep a Changelog style:

- `Added`
- `Changed`
- `Deprecated`
- `Removed`
- `Fixed`
- `Security`
