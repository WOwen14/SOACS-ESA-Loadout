namespace SOACS.OfflineUpdateBuilder.Services
{
    public static class PowerShellGenerator
    {
        public static string CreateDeploymentScript()
        {
            return @"#requires -version 5.1
[CmdletBinding()]
param(
    [switch]$PreviewOnly,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('""{0}""' -f $PSCommandPath))
    if ($PreviewOnly) { $arguments += '-PreviewOnly' }
    if ($Force) { $arguments += '-Force' }
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments
    exit
}

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath = Join-Path $packageRoot 'Manifest\PackageManifest.xml'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw ""Package manifest not found: $manifestPath""
}

[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$package = $manifest.OfflineUpdatePackage
$packageId = [string]$package.PackageId
$programRoot = Join-Path $env:ProgramData 'SOACS\OfflineUpdateBuilder'
$logRoot = Join-Path $programRoot 'Logs'
$historyRoot = Join-Path $programRoot ('History\' + $packageId)
$backupRoot = Join-Path $programRoot ('Backups\' + $packageId)
$receiptPath = Join-Path $historyRoot 'DeploymentReceipt.csv'
New-Item -ItemType Directory -Force -Path $logRoot, $historyRoot | Out-Null
$logPath = Join-Path $logRoot ('Deploy_{0}_{1}.log' -f $packageId, (Get-Date -Format 'yyyyMMdd_HHmmss'))

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $line = '{0} [{1}] {2}' -f (Get-Date -Format 's'), $Level, $Message
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
    Write-Host $line
}

function Assert-SafeRelativePath {
    param([string]$RelativePath)
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw ""Unsafe relative path in manifest: $RelativePath""
    }
    if ($RelativePath.Split([char[]]'\/') -contains '..') {
        throw ""Parent traversal is not permitted in manifest paths: $RelativePath""
    }
}

Write-Log ('Starting package {0} version {1}' -f $package.Name, $package.Version)
if (-not $PreviewOnly -and (Test-Path -LiteralPath $receiptPath)) {
    throw ""This exact package ID already has a deployment receipt. Run rollback before deploying it again.""
}
Write-Log 'Verifying every packaged file before deployment.'

foreach ($item in @($package.ContentItems.Item)) {
    $dataRoot = Join-Path $packageRoot ([string]$item.DataRelativePath)
    foreach ($file in @($item.Files.File)) {
        $relativePath = [string]$file.RelativePath
        Assert-SafeRelativePath $relativePath
        $sourcePath = Join-Path $dataRoot $relativePath
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw ""Missing package file: $sourcePath""
        }
        $actualHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
        if ($actualHash -ne [string]$file.Sha256) {
            throw ""SHA-256 verification failed: $sourcePath""
        }
    }
}
Write-Log 'Package integrity verification passed.'

Write-Host ''
Write-Host 'DEPLOYMENT SUMMARY' -ForegroundColor Cyan
Write-Host ('Package : {0} {1}' -f $package.Name, $package.Version)
Write-Host ('Profile : {0}' -f $package.DeploymentProfileName)
foreach ($item in @($package.ContentItems.Item)) {
    Write-Host ('  {0} -> {1} ({2} files)' -f $item.DisplayName, $item.DestinationPath, $item.FileCount)
}
Write-Host ''

if (-not $Force) {
    $approval = Read-Host 'Type DEPLOY to continue'
    if ($approval -cne 'DEPLOY') {
        Write-Log 'Deployment cancelled by the operator.' 'WARN'
        exit 2
    }
}

foreach ($item in @($package.ContentItems.Item)) {
    $required = [string]$item.RequiresProcessesStopped
    if (-not [string]::IsNullOrWhiteSpace($required)) {
        foreach ($processName in $required.Split(';')) {
            $processName = $processName.Trim()
            if ($processName -and (Get-Process -Name $processName -ErrorAction SilentlyContinue)) {
                throw ""Close process '$processName' before deploying $($item.DisplayName).""
            }
        }
    }
}

$receipt = New-Object System.Collections.Generic.List[object]
try {
    foreach ($item in @($package.ContentItems.Item)) {
        $copyMode = [string]$item.CopyMode
        if ($copyMode -ne 'Merge') {
            throw ""Unsupported copy mode '$copyMode'. Version 0.1 supports Merge only.""
        }

        $destinationRoot = [Environment]::ExpandEnvironmentVariables([string]$item.DestinationPath)
        if ([string]::IsNullOrWhiteSpace($destinationRoot) -or $destinationRoot.StartsWith('REQUIRED:', [StringComparison]::OrdinalIgnoreCase)) {
            throw ""A destination path is not configured for $($item.DisplayName).""
        }

        $dataRoot = Join-Path $packageRoot ([string]$item.DataRelativePath)
        if (-not $PreviewOnly) {
            New-Item -ItemType Directory -Force -Path $destinationRoot | Out-Null
        }

        foreach ($file in @($item.Files.File)) {
            $relativePath = [string]$file.RelativePath
            Assert-SafeRelativePath $relativePath
            $sourcePath = Join-Path $dataRoot $relativePath
            $destinationPath = Join-Path $destinationRoot $relativePath
            $destinationDirectory = Split-Path -Parent $destinationPath
            $existedBefore = Test-Path -LiteralPath $destinationPath -PathType Leaf
            $backupPath = ''

            if ($existedBefore -and [Convert]::ToBoolean([string]$item.BackupExisting)) {
                $backupPath = Join-Path (Join-Path $backupRoot ([string]$item.CategoryId)) $relativePath
                if (-not $PreviewOnly) {
                    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backupPath) | Out-Null
                    Copy-Item -LiteralPath $destinationPath -Destination $backupPath -Force
                }
            }

            if ($PreviewOnly) {
                Write-Log (""PREVIEW: {0} -> {1}"" -f $sourcePath, $destinationPath)
            }
            else {
                New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
                Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
                $deployedHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
                if ($deployedHash -ne [string]$file.Sha256) {
                    throw ""Post-copy verification failed: $destinationPath""
                }
            }

            $receipt.Add([pscustomobject]@{
                CategoryId = [string]$item.CategoryId
                DestinationPath = $destinationPath
                ExistedBefore = $existedBefore
                BackupPath = $backupPath
                Sha256 = [string]$file.Sha256
            })
        }
        Write-Log ('Completed {0}' -f $item.DisplayName)
    }

    if (-not $PreviewOnly) {
        $receipt | Export-Csv -LiteralPath $receiptPath -NoTypeInformation -Encoding UTF8
        Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $historyRoot 'PackageManifest.xml') -Force
        Write-Log ('Deployment completed. Receipt: {0}' -f $receiptPath)
    }
    else {
        Write-Log 'Preview completed. No target files were changed.'
    }
}
catch {
    if (-not $PreviewOnly -and $receipt.Count -gt 0) {
        $receipt | Export-Csv -LiteralPath $receiptPath -NoTypeInformation -Encoding UTF8
        Write-Log ('Partial deployment receipt saved: {0}' -f $receiptPath) 'WARN'
    }
    Write-Log $_.Exception.Message 'ERROR'
    throw
}
";
        }

        public static string CreateVerificationScript()
        {
            return @"#requires -version 5.1
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath = Join-Path $packageRoot 'Manifest\PackageManifest.xml'
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$checked = 0

foreach ($item in @($manifest.OfflineUpdatePackage.ContentItems.Item)) {
    $dataRoot = Join-Path $packageRoot ([string]$item.DataRelativePath)
    foreach ($file in @($item.Files.File)) {
        $relativePath = [string]$file.RelativePath
        if ([IO.Path]::IsPathRooted($relativePath) -or $relativePath.Split([char[]]'\/') -contains '..') {
            throw ""Unsafe manifest path: $relativePath""
        }
        $path = Join-Path $dataRoot $relativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw ""Missing package file: $path""
        }
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($actual -ne [string]$file.Sha256) {
            throw ""SHA-256 mismatch: $path""
        }
        $checked++
    }
}

Write-Host (""VERIFIED: {0} files passed SHA-256 validation."" -f $checked) -ForegroundColor Green
";
        }

        public static string CreateRollbackScript()
        {
            return @"#requires -version 5.1
[CmdletBinding()]
param([switch]$Force)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('""{0}""' -f $PSCommandPath))
    if ($Force) { $arguments += '-Force' }
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments
    exit
}

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
[xml]$manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'Manifest\PackageManifest.xml') -Raw
$packageId = [string]$manifest.OfflineUpdatePackage.PackageId
$programRoot = Join-Path $env:ProgramData 'SOACS\OfflineUpdateBuilder'
$receiptPath = Join-Path $programRoot ('History\{0}\DeploymentReceipt.csv' -f $packageId)
$logRoot = Join-Path $programRoot 'Logs'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$logPath = Join-Path $logRoot ('Rollback_{0}_{1}.log' -f $packageId, (Get-Date -Format 'yyyyMMdd_HHmmss'))

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $line = '{0} [{1}] {2}' -f (Get-Date -Format 's'), $Level, $Message
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
    Write-Host $line
}

if (-not (Test-Path -LiteralPath $receiptPath)) {
    throw ""Deployment receipt not found: $receiptPath""
}

if (-not $Force) {
    $approval = Read-Host ('Type ROLLBACK to reverse package {0}' -f $packageId)
    if ($approval -cne 'ROLLBACK') {
        Write-Log 'Rollback cancelled by the operator.' 'WARN'
        exit 2
    }
}

[object[]]$rows = @(Import-Csv -LiteralPath $receiptPath)
[array]::Reverse($rows)
foreach ($row in $rows) {
    if ($row.ExistedBefore -eq 'True') {
        if ([string]::IsNullOrWhiteSpace($row.BackupPath) -or -not (Test-Path -LiteralPath $row.BackupPath)) {
            Write-Log ('Cannot restore; backup missing: {0}' -f $row.DestinationPath) 'WARN'
            continue
        }
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $row.DestinationPath) | Out-Null
        Copy-Item -LiteralPath $row.BackupPath -Destination $row.DestinationPath -Force
        Write-Log ('Restored {0}' -f $row.DestinationPath)
    }
    elseif (Test-Path -LiteralPath $row.DestinationPath -PathType Leaf) {
        Remove-Item -LiteralPath $row.DestinationPath -Force
        Write-Log ('Removed newly deployed file {0}' -f $row.DestinationPath)
    }
}

Rename-Item -LiteralPath $receiptPath -NewName ('DeploymentReceipt.rolledback_{0}.csv' -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
Write-Log 'Rollback completed.'
";
        }
    }
}
