# Contributing to SOACS ESA Loadout

## Branch Model

- `main` — stable portfolio/release baseline
- `develop` — integrated development branch
- `feature/<description>` — isolated feature development
- `fix/<description>` — defect correction

New work should normally branch from `develop` and return to `develop` through a pull request. Tested integrated changes are promoted from `develop` to `main` through a separate pull request.

## Development Requirements

The current source baseline is:

- Visual Studio 2019-compatible
- C# / WPF
- .NET Framework 4.8
- Any CPU
- No NuGet packages required
- Designed for offline/disconnected development and deployment

Before promoting changes, verify that the solution builds from a clean checkout and that the Release output includes the application executable and `Config/DeploymentProfiles.xml`.

## Functional Validation

At minimum, test:

- Application startup
- Source-folder selection
- Content scanning and category detection
- Manual category assignment
- Deployment-profile loading
- Lab / Staging profile behavior
- Package build progress and cancellation
- Expanded-folder package generation
- ZIP package generation
- Manifest generation
- SHA-256 validation
- Preview-only deployment
- Deployment approval flow
- Backup behavior
- Post-copy verification
- Receipt/log generation
- Rollback

Use only representative non-operational data for repository and public test content.

## Public Repository Data Rules

Do not commit:

- Real mission-data update packages
- Site-specific deployment paths
- Operational configuration files
- Customer or unit identifiers
- Credentials, API keys, certificates, tokens, or passwords
- Logs or deployment receipts containing operational information
- Generated deployment ZIPs
- Backups or staging data

The checked-in operational deployment profile must continue to use placeholder destination paths. Lab/testing profiles should write only to safe staging locations.

## Versioning

Update the assembly metadata, visible application version, README, and changelog together when establishing a new release baseline. Release tags should match the verified source version.
