# SOACS Offline Update Builder

Working source release: **v0.1.0**

SOACS Offline Update Builder is a Windows desktop application that converts a prepared folder of mission-data updates into a verified, offline deployment package. The first release recognizes:

- WinTAK maps
- WinTAK charts
- VVOD
- IMOM parametrics
- IMOM data
- DISCORT
- TRAX
- AKA files
- Additional folders assigned by the operator or added through XML configuration

## Operator workflow

1. Select the root folder containing the update data.
2. Click **Scan Folder**.
3. Review the detected categories, file counts, and sizes.
4. Assign any unrecognized folder to the correct category.
5. Select a deployment profile and verify each destination path.
6. Enter a package name/version and output folder.
7. Click **Build Deployment Package**.

The application produces an expanded package folder and, by default, a ZIP containing:

```text
Data\
Manifest\PackageManifest.xml
Manifest\SHA256SUMS.txt
Config\DeploymentProfile.xml
Docs\DEPLOYMENT_README.txt
Deploy-OfflineUpdates.ps1
Verify-Package.ps1
Rollback-OfflineUpdates.ps1
```

## Deployment safeguards

- SHA-256 verification before any target file is changed
- Self-elevation through Windows UAC
- Operator-visible deployment summary and typed approval
- Merge-only deployment in v0.1; unrelated destination files are retained
- Backup of every existing file that will be overwritten
- Post-copy SHA-256 verification
- Deployment receipt and logs under `%ProgramData%\SOACS\OfflineUpdateBuilder`
- Rollback restores overwritten files and removes newly added package files
- Preview mode performs validation and reports planned file operations without changing target data

## Build requirements

- Visual Studio 2019 16.11 or later
- .NET Framework 4.8 Developer Pack
- Windows 10 or Windows 11, x64
- No NuGet packages and no internet restore

Open `Source\SOACS.OfflineUpdateBuilder.sln`, select **Release / Any CPU**, and build the solution. The executable and `Config\DeploymentProfiles.xml` will be under `bin\Release`.

## Important configuration note

The included **Lab / Staging Validation** profile deploys only under `%ProgramData%\SOACS\OfflineUpdateStaging`. The operational profile intentionally contains `REQUIRED:` placeholders. Replace those with verified paths for the actual WinTAK, VVOD, IMOM, DISCORT, TRAX, and AKA installations before building an operational package.

See `Docs\OperatorGuide.md` and `Docs\PackageFormat.md` for additional details.
