<#.SYNOPSIS
    Monthly maintenance updater for Ludusavi LaunchBox.

.DESCRIPTION
    Updates the installed Ludusavi LaunchBox plugin from GitHub release assets,
    refreshes Ludusavi/rclone from upstream, and re-copies optional local HLTB
    theme/dataset assets when present.
#>

param(
    [string]$LaunchBoxPath = "$env:USERPROFILE\LaunchBox",
    [string]$Repo = "jackohagan94-afk/ludusavi-launchbox",
    [switch]$Yes
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Get-VersionFromFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return [version]"0.0.0" }
    $raw = (Get-Item -LiteralPath $Path).VersionInfo.FileVersion
    $parsed = [version]"0.0.0"
    if ([version]::TryParse($raw, [ref]$parsed)) { return $parsed }
    return [version]"0.0.0"
}

function Get-VersionFromTag {
    param([string]$Tag)
    $clean = $Tag.TrimStart("v", "V")
    return [version]$clean
}

function Update-PluginFromGitHub {
    $plugins = Join-Path $LaunchBoxPath "Plugins"
    Ensure-Directory $plugins

    $installedDll = Join-Path $plugins "LudusaviLaunchBox.dll"
    $currentVersion = Get-VersionFromFile $installedDll
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers @{ "User-Agent" = "ludusavi-launchbox-maintenance" }
    $latestVersion = Get-VersionFromTag $release.tag_name

    if ($latestVersion -le $currentVersion) {
        Write-Host "Plugin already current: $currentVersion" -ForegroundColor Green
        return
    }

    $asset = $release.assets | Where-Object { $_.name -eq "LudusaviLaunchBox.dll" } | Select-Object -First 1
    if (-not $asset) { throw "Latest release does not include LudusaviLaunchBox.dll." }

    $tmp = Join-Path $env:TEMP "LudusaviLaunchBox-$($release.tag_name).dll"
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tmp -UseBasicParsing

    if (Test-Path -LiteralPath $installedDll) {
        $backup = "$installedDll.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        Copy-Item -LiteralPath $installedDll -Destination $backup -Force
        Write-Host "Backed up plugin: $backup" -ForegroundColor DarkCyan
    }

    Copy-Item -LiteralPath $tmp -Destination $installedDll -Force
    Remove-Item -LiteralPath $tmp -Force
    Write-Host "Updated plugin to $latestVersion" -ForegroundColor Green
}

if (-not (Test-Path -LiteralPath $LaunchBoxPath)) {
    throw "LaunchBox path not found: $LaunchBoxPath"
}

Update-PluginFromGitHub

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "setup-tools.ps1") `
    -LaunchBoxPath $LaunchBoxPath `
    -Yes `
    -UpdateTools

Write-Host "Maintenance complete." -ForegroundColor Green
