# Versioning Strategy

This project follows **Semantic Versioning** with specific pre-release phase definitions.

## Version Format

`MAJOR.MINOR.PATCH[-PRERELEASE]`

## Development Phases

### Alpha Phase (0.0.x)
- **Version Range**: `0.0.1` to `0.0.99`
- **Purpose**: Initial development, experimental features, API design
- **Stability**: Unstable, breaking changes expected
- **Testing**: Internal testing only
- **Increment**: Bump PATCH version for each alpha release

**Example progression:**
```
0.0.1 → 0.0.2 → 0.0.3 → ... → 0.0.99
```

### Beta Phase (0.x.0)
- **Version Range**: `0.1.0` to `0.99.0`
- **Purpose**: Feature complete for testing, API stabilization
- **Stability**: Mostly stable, minor breaking changes possible
- **Testing**: External beta testing, community feedback
- **Increment**: Bump MINOR version for each beta release

**Example progression:**
```
0.1.0 → 0.2.0 → 0.3.0 → ... → 0.99.0
```

**Beta patches:**
```
0.1.0 → 0.1.1 (bugfix) → 0.1.2 (bugfix) → 0.2.0 (next beta)
```

### Production Phase (1.0.0+)
- **Version Range**: `1.0.0` and above
- **Purpose**: Production-ready, stable releases
- **Stability**: Stable, follows strict semantic versioning
- **Testing**: Full QA, production testing

**Semantic Versioning Rules:**
- **MAJOR**: Increment for incompatible API changes
- **MINOR**: Increment for backwards-compatible functionality additions
- **PATCH**: Increment for backwards-compatible bug fixes

**Example progression:**
```
1.0.0 → 1.0.1 (bugfix) → 1.1.0 (new feature) → 2.0.0 (breaking change)
```

## Version Lifecycle

```
Development → Alpha → Beta → Release Candidate → Production
  (main)      (0.0.x)  (0.x.0)      (x.y.z-rc.n)      (1.0.0+)
```

## Release Criteria

### Alpha → Beta (0.0.x → 0.1.0)
- Core architecture implemented
- Basic functionality working
- Ready for external testing

### Beta → Production (0.x.0 → 1.0.0)
- All planned features implemented
- API stable and documented
- Comprehensive test coverage
- No critical bugs
- Documentation complete

## Branching Strategy

### Main Branches
- `main` - Production-ready code (1.0.0+)
- `develop` - Integration branch for next release
- `alpha` - Alpha development (0.0.x)
- `beta` - Beta testing (0.x.0)

### Supporting Branches
- `feature/*` - New features
- `bugfix/*` - Bug fixes
- `hotfix/*` - Production hotfixes
- `release/*` - Release preparation

## Tagging Convention

All releases must be tagged:

```bash
# Alpha releases
git tag -a v0.0.1 -m "Alpha release 0.0.1 - Initial MCP server structure"

# Beta releases
git tag -a v0.1.0 -m "Beta release 0.1.0 - Feature complete for testing"

# Production releases
git tag -a v1.0.0 -m "Production release 1.0.0 - First stable release"
```

## Changelog

Maintain a [CHANGELOG.md](CHANGELOG.md) following [Keep a Changelog](https://keepachangelog.com/) format:

- **Added** - New features
- **Changed** - Changes in existing functionality
- **Deprecated** - Soon-to-be removed features
- **Removed** - Removed features
- **Fixed** - Bug fixes
- **Security** - Security fixes

## Current Version

**Version**: `0.0.1`
**Phase**: Alpha
**Status**: Initial development
