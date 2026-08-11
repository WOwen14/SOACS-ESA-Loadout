# Offline deployment package format

## Package root

| Item | Purpose |
| --- | --- |
| `Data\<CategoryId>` | Data copied from each selected source category with subfolders preserved |
| `Manifest\PackageManifest.xml` | Package identity, profile, destinations, file sizes, and per-file SHA-256 values |
| `Manifest\SHA256SUMS.txt` | SHA-256 list for all package artifacts except the checksum list itself |
| `Config\DeploymentProfile.xml` | Exact profile embedded at build time |
| `Deploy-OfflineUpdates.ps1` | Elevated verification, backup, copy, and post-copy verification workflow |
| `Verify-Package.ps1` | Read-only integrity test |
| `Rollback-OfflineUpdates.ps1` | Receipt-based reversal workflow |
| `Docs\DEPLOYMENT_README.txt` | Package-specific operator instructions |

## Manifest behavior

Every content item records its logical category, original source-relative folder, package data location, target destination, copy mode, backup policy, stopped-process requirement, total size, and file count.

Every file records its relative path, size in bytes, and SHA-256 hash. Deployment is blocked if a file is missing or its hash differs.

## Target-side state

Deployment creates no internet dependency. Local state is written under:

```text
%ProgramData%\SOACS\OfflineUpdateBuilder
├── Logs
├── History\<PackageId>
│   ├── PackageManifest.xml
│   └── DeploymentReceipt.csv
└── Backups\<PackageId>\<CategoryId>
```

The receipt identifies whether each destination file existed before deployment and, when applicable, where its backup was stored. This makes rollback deterministic for files handled by the package.

## Trust boundary

SHA-256 detects file corruption or accidental modification. It is not a digital signature. A later release can add certificate-based signing if packages must be authenticated to a specific publisher.
