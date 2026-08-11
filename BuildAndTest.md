# Build and test procedure

## Build

1. Open `Source\SOACS.OfflineUpdateBuilder.sln` in Visual Studio 2019.
2. Confirm `.NET Framework 4.8` is installed in Visual Studio Installer.
3. Select `Release` and `Any CPU`.
4. Choose **Build > Rebuild Solution**.
5. Confirm the output contains both `SOACS.OfflineUpdateBuilder.exe` and `Config\DeploymentProfiles.xml`.

No NuGet restore is required.

## Safe functional test

1. Place representative non-operational files in several `Sample-Input` subfolders.
2. Launch the application and scan `Sample-Input`.
3. Keep the **Lab / Staging Validation** profile selected.
4. Build a ZIP and expanded package folder.
5. Extract the ZIP to a separate test location.
6. Run `Verify-Package.ps1`; confirm every file passes.
7. Run `Deploy-OfflineUpdates.ps1 -PreviewOnly`; confirm no target files change.
8. Run the deployment script and type `DEPLOY`.
9. Confirm files are under `%ProgramData%\SOACS\OfflineUpdateStaging`.
10. Run rollback and type `ROLLBACK`.
11. Confirm newly added files were removed and overwritten test files were restored.

## Operational acceptance inputs still required

- Verified destination path for every application/data type
- Confirmed process name(s) that must be closed during update
- Representative production folder structure and file sizes
- Decision on whether any category requires delete/mirror semantics instead of Merge
- Confirmation of the product spelling and folder convention for DISCORT
