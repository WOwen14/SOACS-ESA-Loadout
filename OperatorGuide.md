# SOACS Offline Update Builder — Operator Guide

## 1. Purpose

The builder packages map, chart, and mission-application data for Windows computers that cannot reach an online update service. The builder does not download content. It packages only the files placed in the selected source folder.

## 2. Prepare source data

Use the recommended folder structure shown in `Sample-Input`. Folder names are matched using aliases in `Config\DeploymentProfiles.xml`. The scan is read-only.

If an immediate child folder is not recognized, it appears as **Unassigned** and is excluded. Select it, choose the correct category, click **Apply Category**, and verify that its status changes to **Ready**.

## 3. Configure destinations

Select a deployment profile. Each included category must have a destination path that is not blank and does not begin with `REQUIRED:`.

Paths may contain Windows environment variables, including `%ProgramData%`, `%PUBLIC%`, and `%USERPROFILE%`. Use machine-wide paths for content that must be available to all Windows users.

Click **Save Paths** after making changes. The application writes `DeploymentProfiles.xml` and preserves the previous version as `DeploymentProfiles.xml.bak`.

Process names that must be closed are configured directly in the XML using the `RequiresProcessesStopped` attribute. Separate multiple process names with semicolons. Use executable process names without `.exe`.

## 4. Build a package

Enter a short package name and a controlled version, such as `CONUS_Mission_Data` and `2026.07`. Select an output folder that is not inside the source-data folder.

Click **Build Deployment Package**. The application displays the current file, percentage complete, and activity log. Cancel stops after the current file and removes the temporary staging folder.

Successful builds preserve the expanded deployment folder. When **Create ZIP package** is selected, a ZIP is also created beside the folder.

## 5. Verify and deploy

Extract the entire ZIP before deploying it.

Run:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\Verify-Package.ps1
```

Preview the deployment:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\Deploy-OfflineUpdates.ps1 -PreviewOnly
```

Deploy:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\Deploy-OfflineUpdates.ps1
```

The deployment script requests elevation, verifies all data hashes, shows category destinations, verifies required processes are closed, and requires the operator to type `DEPLOY`.

## 6. Roll back

Run the rollback script from the same extracted package:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\Rollback-OfflineUpdates.ps1
```

Type `ROLLBACK` when prompted. Rollback uses the deployment receipt stored under `%ProgramData%\SOACS\OfflineUpdateBuilder\History` and backups under `%ProgramData%\SOACS\OfflineUpdateBuilder\Backups`.

## 7. Adding a future data type

Add a new `<Category>` to the configuration, including a unique ID, display name, order, and one or more source-folder aliases. Then add a matching `<Target>` to every deployment profile. Restart the application to reload the XML.

Example:

```xml
<Category Id="NewData" DisplayName="New Data" Order="90" Enabled="true">
  <SourceAliases>
    <Alias Path="NewData" />
  </SourceAliases>
</Category>
```

```xml
<Target CategoryId="NewData"
        DestinationPath="%ProgramData%\Vendor\NewData"
        CopyMode="Merge"
        BackupExisting="true"
        RequiresProcessesStopped="VendorApp" />
```

## 8. Current release limitation

Version 0.1 supports the **Merge** copy mode only. It adds new files and overwrites same-path files after backing them up. It does not delete unrelated files already present at a destination.
