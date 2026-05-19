<#.SYNOPSIS
    Optional first-run setup helper for Ludusavi LaunchBox users.

.DESCRIPTION
    Offers to install Ludusavi and rclone into LaunchBox\ThirdParty, create a
    default Ludusavi config, run emulator path discovery, and copy optional
    local HLTB theme/dataset assets into the LaunchBox folders that use them.

    Third-party binaries are downloaded from their upstream release locations
    at setup time instead of being bundled with this plugin.

.PARAMETER LaunchBoxPath
    Path to the LaunchBox installation.

.PARAMETER ThemeSource
    Optional theme folder or archive. Defaults to auto-detecting a neighboring
    hltb-dataset-plugin\LBThemes\HLTB folder when present.

.PARAMETER DatasetSource
    Optional folder containing hltb_dataset*.csv files. Defaults to
    auto-detecting a neighboring hltb-dataset-plugin\dataset folder.

.PARAMETER Yes
    Run non-interactively and accept all default actions.

.PARAMETER AddToUserPath
    Add LaunchBox\ThirdParty\Ludusavi and LaunchBox\ThirdParty\rclone to the
    current user's PATH so Ludusavi can find rclone for cloud sync.

.PARAMETER UpdateTools
    Replace existing Ludusavi/rclone installs when running maintenance.
#>

param(
    [string]$LaunchBoxPath = "$env:USERPROFILE\LaunchBox",
    [string]$ThemeSource,
    [string]$DatasetSource,
    [switch]$Yes,
    [switch]$AddToUserPath,
    [switch]$UpdateTools
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$examplesConfig = Join-Path $repoRoot "examples\config.yaml"
$discoverScript = Join-Path $PSScriptRoot "discover-paths.ps1"

function Ask-YesNo {
    param(
        [string]$Prompt,
        [bool]$Default = $true
    )
    if ($Yes) { return $Default }

    $suffix = if ($Default) { "[Y/n]" } else { "[y/N]" }
    $answer = Read-Host "$Prompt $suffix"
    if ([string]::IsNullOrWhiteSpace($answer)) { return $Default }
    return $answer.Trim().ToLowerInvariant().StartsWith("y")
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Backup-ExistingPath {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $backup = "$Path.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Move-Item -LiteralPath $Path -Destination $backup
    Write-Host "Backed up existing path: $backup" -ForegroundColor DarkCyan
}

function Find-Executable {
    param(
        [string]$Root,
        [string]$Name
    )
    Get-ChildItem -LiteralPath $Root -Recurse -File -Filter $Name |
        Select-Object -First 1 -ExpandProperty FullName
}

function Download-File {
    param(
        [string]$Uri,
        [string]$OutFile
    )
    Write-Host "Downloading: $Uri" -ForegroundColor Cyan
    Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing
}

function Install-Ludusavi {
    param([string]$InstallRoot)

    $target = Join-Path $InstallRoot "Ludusavi"
    $exe = Join-Path $target "ludusavi.exe"
    if ((Test-Path -LiteralPath $exe) -and -not ($UpdateTools -or (Ask-YesNo "Ludusavi already exists. Replace it?" $false))) {
        return $exe
    }

    Ensure-Directory $InstallRoot
    if (Test-Path -LiteralPath $target) { Backup-ExistingPath $target }
    Ensure-Directory $target

    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/mtkennerly/ludusavi/releases/latest" -Headers @{ "User-Agent" = "ludusavi-launchbox-setup" }
    $asset = $release.assets |
        Where-Object { $_.name -match '(?i)(win|windows).*(64|x64).*\.zip$' } |
        Select-Object -First 1
    if (-not $asset) { throw "Could not find a Windows x64 Ludusavi ZIP in the latest GitHub release." }

    $tmp = Join-Path $env:TEMP $asset.name
    $extract = Join-Path $env:TEMP ("ludusavi-extract-" + [guid]::NewGuid())
    Ensure-Directory $extract
    Download-File $asset.browser_download_url $tmp
    Expand-Archive -LiteralPath $tmp -DestinationPath $extract -Force

    $found = Find-Executable $extract "ludusavi.exe"
    if (-not $found) { throw "Downloaded Ludusavi archive did not contain ludusavi.exe." }
    Copy-Item -LiteralPath $found -Destination $exe -Force
    Remove-Item -LiteralPath $tmp -Force
    Remove-Item -LiteralPath $extract -Recurse -Force
    return $exe
}

function Install-Rclone {
    param([string]$InstallRoot)

    $target = Join-Path $InstallRoot "rclone"
    $exe = Join-Path $target "rclone.exe"
    if ((Test-Path -LiteralPath $exe) -and -not ($UpdateTools -or (Ask-YesNo "rclone already exists. Replace it?" $false))) {
        return $exe
    }

    Ensure-Directory $InstallRoot
    if (Test-Path -LiteralPath $target) { Backup-ExistingPath $target }
    Ensure-Directory $target

    $tmp = Join-Path $env:TEMP "rclone-current-windows-amd64.zip"
    $extract = Join-Path $env:TEMP ("rclone-extract-" + [guid]::NewGuid())
    Ensure-Directory $extract
    Download-File "https://downloads.rclone.org/rclone-current-windows-amd64.zip" $tmp
    Expand-Archive -LiteralPath $tmp -DestinationPath $extract -Force

    $found = Find-Executable $extract "rclone.exe"
    if (-not $found) { throw "Downloaded rclone archive did not contain rclone.exe." }
    Copy-Item -LiteralPath $found -Destination $exe -Force
    Remove-Item -LiteralPath $tmp -Force
    Remove-Item -LiteralPath $extract -Recurse -Force
    return $exe
}

function Add-DirectoriesToUserPath {
    param([string[]]$Paths)

    $current = [Environment]::GetEnvironmentVariable("Path", "User")
    $parts = @()
    if ($current) { $parts = $current -split ';' | Where-Object { $_ } }
    $changed = $false

    foreach ($path in $Paths) {
        if (-not ($parts | Where-Object { $_ -ieq $path })) {
            $parts += $path
            $changed = $true
        }
        if (-not (($env:Path -split ';') | Where-Object { $_ -ieq $path })) {
            $env:Path = "$env:Path;$path"
        }
    }

    if ($changed) {
        [Environment]::SetEnvironmentVariable("Path", ($parts -join ';'), "User")
        Write-Host "Updated user PATH. Restart LaunchBox after setup." -ForegroundColor Green
    }
}

function Copy-PluginSettings {
    param([string]$LudusaviExe)

    $settingsPath = Join-Path $LaunchBoxPath "Plugins\ludusavi_settings.json"
    Ensure-Directory (Split-Path -Parent $settingsPath)

    $settings = [ordered]@{
        ExePath = $LudusaviExe
        OverrideBackupPath = $false
        BackupPath = ""
        BackupOnGameExited = $true
        AskBackupOnGameExited = $false
        RestoreOnGameStarting = $true
        OnlyBackupOnGameExitedIfPc = $false
        BackupByPlatformForNonPc = $true
        RetryUnrecognizedWithNormalization = $true
        AddSuffixForNonPcGameNames = $true
        SuffixForNonPcGameNames = " (<platform>)"
        TagGamesWithBackups = $false
        TagGamesWithUnknownSaveData = $false
        AlternativeTitles = @{}
    }

    if ((Test-Path -LiteralPath $settingsPath) -and -not (Ask-YesNo "Update existing ludusavi_settings.json ExePath/defaults?" $true)) {
        return
    }

    if (Test-Path -LiteralPath $settingsPath) {
        Copy-Item -LiteralPath $settingsPath -Destination "$settingsPath.bak" -Force
    }
    $settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    Write-Host "Wrote: $settingsPath" -ForegroundColor Green
}

function Setup-LudusaviConfig {
    $configDir = Join-Path $env:APPDATA "ludusavi"
    $configPath = Join-Path $configDir "config.yaml"
    Ensure-Directory $configDir

    if ((Test-Path -LiteralPath $configPath) -and -not (Ask-YesNo "Update existing Ludusavi config.yaml from example?" $false)) {
        return
    }

    if (Test-Path -LiteralPath $configPath) {
        Copy-Item -LiteralPath $configPath -Destination "$configPath.bak" -Force
    }
    Copy-Item -LiteralPath $examplesConfig -Destination $configPath -Force
    Write-Host "Wrote: $configPath" -ForegroundColor Green

    if (Ask-YesNo "Run emulator path discovery against config.yaml?" $true) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $discoverScript -ConfigPath $configPath
    }
}

function Resolve-DefaultThemeSource {
    if ($ThemeSource) { return $ThemeSource }
    $candidate = Join-Path $repoRoot "..\hltb-dataset-plugin\LBThemes\HLTB"
    if (Test-Path -LiteralPath $candidate) { return (Resolve-Path $candidate).Path }
    return $null
}

function Resolve-DefaultDatasetSource {
    if ($DatasetSource) { return $DatasetSource }
    $candidate = Join-Path $repoRoot "..\hltb-dataset-plugin\dataset"
    if (Test-Path -LiteralPath $candidate) { return (Resolve-Path $candidate).Path }
    return $null
}

function Install-Theme {
    $source = Resolve-DefaultThemeSource
    if (-not $source) { return }
    if (-not (Ask-YesNo "Install LaunchBox theme from '$source'?" $true)) { return }

    $themeRoot = Join-Path $LaunchBoxPath "LBThemes"
    Ensure-Directory $themeRoot

    if ((Get-Item -LiteralPath $source).PSIsContainer) {
        $destination = Join-Path $themeRoot (Split-Path -Leaf $source)
        if (Test-Path -LiteralPath $destination) { Backup-ExistingPath $destination }
        Copy-Item -LiteralPath $source -Destination $destination -Recurse
        Write-Host "Installed theme: $destination" -ForegroundColor Green
        return
    }

    $extension = [IO.Path]::GetExtension($source).ToLowerInvariant()
    if ($extension -eq ".zip") {
        Expand-Archive -LiteralPath $source -DestinationPath $themeRoot -Force
        Write-Host "Extracted theme archive into: $themeRoot" -ForegroundColor Green
    } elseif ($extension -eq ".7z") {
        $sevenZip = Get-Command 7z.exe -ErrorAction SilentlyContinue
        if (-not $sevenZip) { throw "Cannot extract .7z theme archive because 7z.exe was not found on PATH." }
        & $sevenZip.Source x "-o$themeRoot" -y $source
        Write-Host "Extracted theme archive into: $themeRoot" -ForegroundColor Green
    } else {
        throw "Unsupported theme source: $source"
    }
}

function Install-Dataset {
    $source = Resolve-DefaultDatasetSource
    if (-not $source) { return }
    if (-not (Ask-YesNo "Copy HLTB dataset CSVs from '$source' to LaunchBox\Plugins?" $true)) { return }

    $plugins = Join-Path $LaunchBoxPath "Plugins"
    Ensure-Directory $plugins

    $files = Get-ChildItem -LiteralPath $source -File -Filter "hltb_dataset*.csv"
    if (-not $files) { throw "No hltb_dataset*.csv files found in: $source" }

    foreach ($file in $files) {
        $destination = Join-Path $plugins $file.Name
        if (Test-Path -LiteralPath $destination) {
            Copy-Item -LiteralPath $destination -Destination "$destination.bak" -Force
        }
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        Write-Host "Copied dataset: $destination" -ForegroundColor Green
    }
}

if (-not (Test-Path -LiteralPath $LaunchBoxPath)) {
    throw "LaunchBox path not found: $LaunchBoxPath"
}

$thirdParty = Join-Path $LaunchBoxPath "ThirdParty"
$ludusaviExe = $null
$rcloneExe = $null

if (Ask-YesNo "Install or update Ludusavi into LaunchBox\ThirdParty?" $true) {
    $ludusaviExe = Install-Ludusavi $thirdParty
    Write-Host "Ludusavi: $ludusaviExe" -ForegroundColor Green
}

if (Ask-YesNo "Install or update rclone into LaunchBox\ThirdParty?" $true) {
    $rcloneExe = Install-Rclone $thirdParty
    Write-Host "rclone  : $rcloneExe" -ForegroundColor Green
}

if ($AddToUserPath -or (Ask-YesNo "Add Ludusavi/rclone folders to user PATH?" $false)) {
    $pathCandidates = @()
    if ($ludusaviExe) { $pathCandidates += Split-Path -Parent $ludusaviExe }
    if ($rcloneExe) { $pathCandidates += Split-Path -Parent $rcloneExe }
    if ($pathCandidates) { Add-DirectoriesToUserPath $pathCandidates }
}

if ($ludusaviExe) { Copy-PluginSettings $ludusaviExe }
Setup-LudusaviConfig
Install-Theme
Install-Dataset

Write-Host "Setup complete. Restart LaunchBox before testing the plugin." -ForegroundColor Green
