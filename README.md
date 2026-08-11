# SOACS ESA Loadout

**Offline ESA Data and Update-Package Builder**

SOACS ESA Loadout is a Windows desktop application for building verified, portable update packages for disconnected ESA client systems. It scans prepared mission-data folders, classifies supported content, applies deployment profiles, generates integrity manifests, and produces offline deployment packages with PowerShell-based verification, deployment, and rollback tooling.

> **Status:** Working Software / Early Baseline  
> **Current Source Version:** v0.1.0  
> **Platform:** Windows  
> **Application:** WPF  
> **Framework:** .NET Framework 4.8

## Purpose

ESA Loadout reduces the manual effort involved in preparing repeatable update packages for disconnected systems. Rather than hand-building deployment media and scripts for each update cycle, the application packages selected data into a controlled structure with verification, operator review, deployment safeguards, and rollback support.

## Supported Content

The current source recognizes:

- WinTAK maps
- WinTAK charts
- VVOD
- IMOM parametrics
- IMOM data
- DISCORT
- TRAX
- AKA files
- Additional operator-assigned folders through XML configuration

## Operator Workflow

1. Select the root folder containing update data.
2. Scan the source folder.
3. Review detected categories, file counts, and sizes.
4. Assign any unrecognized content to the correct category.
5. Select a deployment profile and verify destination paths.
6. Enter package identification and choose an output location.
7. Build the deployment package.

## Generated Deployment Package

A completed package can include:

```text
Data/
Manifest/PackageManifest.xml
Manifest/SHA256SUMS.txt
Config/DeploymentProfile.xml
Docs/DEPLOYMENT_README.txt
Deploy-OfflineUpdates.ps1
Verify-Package.ps1
Rollback-OfflineUpdates.ps1
```

The builder can create both an expanded package folder and a ZIP for transfer to disconnected systems.

## Deployment Safeguards

- SHA-256 validation before target files are changed
- Windows UAC elevation
- Operator-visible deployment summary
- Typed deployment approval
- Merge-only deployment behavior in v0.1
- Backup of files that will be overwritten
- Post-copy SHA-256 verification
- Deployment receipts and logs
- Preview-only deployment mode
- Rollback support for overwritten and newly added package files

## Deployment Profiles

The repository includes two safe baseline approaches:

- **Operational Template** — uses `REQUIRED:` placeholders that must be replaced with verified destination paths before operational use.
- **Lab / Staging Validation** — deploys only beneath `%ProgramData%\SOACS\OfflineUpdateStaging` for testing.

Do not commit site-specific or operational destination paths to the public repository.

## Architecture

ESA Loadout is implemented in C# using WPF and targets **.NET Framework 4.8**. The source is compatible with Visual Studio 2019 and requires no NuGet restore or internet dependency for the current build.

The internal solution and assembly currently retain the original project name `SOACS.OfflineUpdateBuilder`; the repository and product portfolio name are **SOACS ESA Loadout**.

## Build

Open `SOACS.OfflineUpdateBuilder.sln`, select **Release / Any CPU**, and build the solution.

The Release output should contain:

- `SOACS.OfflineUpdateBuilder.exe`
- `Config/DeploymentProfiles.xml`

See [Build and Test](Docs/BuildAndTest.md) for the validation procedure.

## Documentation

- [Operator Guide](Docs/OperatorGuide.md)
- [Build and Test](Docs/BuildAndTest.md)
- [Package Format](Docs/PackageFormat.md)
- [Representative Sample Input](Docs/SampleInput.md)
- [Changelog](CHANGELOG.md)

## Repository Layout

```text
Config/        Deployment-profile configuration
Docs/          Operator, build, test, and package documentation
Models/        Application data models
Properties/    Assembly metadata
Samples/       Non-operational representative sample files
Services/      Scanning, packaging, configuration, and script generation
```

## Development Workflow

```text
feature/* or fix/*
        |
        v
     develop
        |
   testing/review
        |
        v
       main
        |
        v
   tagged release
```

- `main` represents the stable portfolio/release baseline.
- `develop` is used for integrated development.
- New work should be performed in feature or fix branches and merged into `develop`.
- Tested changes are promoted from `develop` to `main` through pull requests.

See [CONTRIBUTING.md](CONTRIBUTING.md) for repository workflow and data-handling rules.

## About SOACS

ESA Loadout is part of the SOACS software suite, a set of mission-focused applications developed around real operational workflows and disconnected-system requirements.
