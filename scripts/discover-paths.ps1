<#.SYNOPSIS
    Detects emulator save folders and writes Ludusavi custom game entries.

.DESCRIPTION
    Auto-discovers RetroArch and standalone emulator installations, then
    generates Ludusavi custom game YAML entries with real filesystem paths.
    
    Supports two modes:
      1. Dry-run (default): prints entries to stdout for review
      2. In-place (-ConfigPath): reads an existing config.yaml, replaces
         %RETROARCH% / %EMUDIR% / %USERPROFILE% / %APPDATA% placeholders
         with actual detected paths, and writes the result.

.PARAMETER ConfigPath
    Path to a ludusavi config.yaml to update in-place. The script replaces
    %VARIABLE% placeholders with detected paths.

.PARAMETER RetroArchPath
    Manual RetroArch folder (skips auto-detection).

.PARAMETER EmuDir
    Manual Emulators folder (skips auto-detection of Documents\Emulation\Emulators).

.EXAMPLE
    # Print entries to stdout for review
    .\discover-paths.ps1

.EXAMPLE
    # Write discovered paths into your config (replaces %RETROARCH% etc.)
    .\discover-paths.ps1 -ConfigPath "$env:APPDATA\ludusavi\config.yaml"

.EXAMPLE
    # Specify RetroArch and emulator folders manually
    .\discover-paths.ps1 -RetroArchPath "D:\RetroArch" -EmuDir "D:\Emulation\Emulators"
#>

param(
    [string]$ConfigPath,
    [string]$RetroArchPath,
    [string]$EmuDir
)

$ErrorActionPreference = "Continue"

# ============================================================
#  Path discovery
# ============================================================

function Find-RetroArch {
    param([string]$ManualPath)
    if ($ManualPath -and (Test-Path "$ManualPath\retroarch.exe")) { return $ManualPath }

    $candidates = @(
        "C:\RetroArch-Win64",
        "C:\RetroArch",
        "$env:ProgramFiles\RetroArch",
        "D:\RetroArch",
        "E:\RetroArch"
    )
    foreach ($p in $candidates) {
        if (Test-Path "$p\retroarch.exe" -or Test-Path "$p\retroarch-plus.exe") { return $p }
    }
    return $null
}

function Find-EmuDir {
    param([string]$ManualPath)
    if ($ManualPath -and (Test-Path $ManualPath)) { return $ManualPath }

    $candidates = @(
        "$env:USERPROFILE\Documents\Emulation\Emulators",
        "C:\Emulation\Emulators",
        "D:\Emulation\Emulators"
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) { return $p }
    }
    return $null
}

$raPath = Find-RetroArch -ManualPath $RetroArchPath
$emuPath = Find-EmuDir -ManualPath $EmuDir

Write-Host "RetroArch : $($raPath ?? 'NOT FOUND')" -ForegroundColor $(if ($raPath) { 'Green' } else { 'Yellow' })
Write-Host "Emulators : $($emuPath ?? 'NOT FOUND')" -ForegroundColor $(if ($emuPath) { 'Green' } else { 'Yellow' })

# ============================================================
#  Variable replacements for config.yaml placeholders
# ============================================================

$replacements = @{
    '%APPDATA%'     = $env:APPDATA
    '%USERPROFILE%' = $env:USERPROFILE
    '%RETROARCH%'   = $raPath ?? '%RETROARCH%'    # keep placeholder if not found
    '%EMUDIR%'      = $emuPath ?? '%EMUDIR%'
}

# ============================================================
#  In-place mode: update existing config.yaml
# ============================================================

if ($ConfigPath) {
    if (-not (Test-Path $ConfigPath)) {
        Write-Host "Config not found: $ConfigPath" -ForegroundColor Red
        Write-Host "Download the example config first:" -ForegroundColor Yellow
        Write-Host "  curl -o `"$ConfigPath`" https://raw.githubusercontent.com/jackohagan94-afk/ludusavi-launchbox/main/examples/config.yaml"
        exit 1
    }

    Write-Host "`nUpdating: $ConfigPath" -ForegroundColor Cyan
    $content = Get-Content $ConfigPath -Raw

    # Replace all %VARIABLE% placeholders with real paths
    foreach ($key in $replacements.Keys) {
        $val = $replacements[$key]
        if ($val -ne $key) {  # skip if placeholder wasn't resolved
            # Use forward slashes for Ludusavi YAML compatibility
            $val = $val.Replace('\', '/')
            $content = $content.Replace($key, $val)
            Write-Host "  $key -> $val" -ForegroundColor Green
        } else {
            Write-Host "  $key -> (skipped, path not found - review manually)" -ForegroundColor Yellow
        }
    }

    # Also replace %APPDATA% in its Roaming subpath form
    $appdata = $env:APPDATA.Replace('\', '/')
    $content = $content.Replace('%APPDATA%', $appdata)

    Set-Content -Path $ConfigPath -Value $content -NoNewline
    Write-Host "`nDone! Config updated with detected paths." -ForegroundColor Green

    # Warn about rclone
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "  1. Edit $ConfigPath and set cloud.remote if using cloud sync" -ForegroundColor White
    Write-Host "  2. Run: ludusavi cloud sync --api     (verify everything works)" -ForegroundColor White
    Write-Host "  3. Install the LaunchBox plugin from GitHub releases" -ForegroundColor White
    exit 0
}

# ============================================================
#  Dry-run mode: print entries to stdout
# ============================================================

Write-Host "`n(DRY RUN - no files written. Use -ConfigPath to write to your config.)" -ForegroundColor Cyan
Write-Host ""

Write-Output "customGames:"

# RetroArch-based
if ($raPath) {
    $raSaves = "$raPath\saves"
    $raStates = "$raPath\states"

    $raEntries = @(
        @{N="Nintendo Game Boy Advance"; C="mGBA"}
        @{N="Nintendo Game Boy"; C="SameBoy"}
        @{N="Nintendo Game Boy Color"; C="SameBoy"}
        @{N="Nintendo DS"; C="melonDS"}
        @{N="Nintendo 64"; C="mupen64plus_next"}
        @{N="Sega Saturn"; C="beetle_saturn"}
        @{N="Sega Genesis / Mega Drive"; C="genesis_plus_gx"}
        @{N="Sega Master System / Game Gear / SG-1000"; C="genesis_plus_gx"}
        @{N="Virtual Boy"; C="beetle_vb"}
        @{N="WonderSwan / WonderSwan Color"; C="beetle_wswan"}
        @{N="Arcade"; C="fbneo"}
        @{N="Amiga"; C="puae"}
        @{N="Atari 2600"; C="stella"}
        @{N="Atari 5200"; C="atari800"}
        @{N="Atari 7800"; C="prosystem"}
        @{N="Atari Lynx"; C="handy"}
        @{N="Atari Jaguar"; C="virtualjaguar"}
        @{N="Commodore 64"; C="vice_x64"}
        @{N="Channel F"; C="freechaf"}
        @{N="PC Engine / TurboGrafx-16 CD"; C="beetle_pce_fast"}
    )

    foreach ($e in $raEntries) {
        $lines = @(
            "  - name: `"$($e.N)`"",
            "    integration: override",
            "    files:",
            "      - `"$($raSaves.Replace('\','/'))\$($e.C)\**`"",
            "      - `"$($raStates.Replace('\','/'))\$($e.C)\**`""
        )
        Write-Output ($lines -join [Environment]::NewLine)
        Write-Output "    registry: []"
        Write-Output "    installDir: []"
        Write-Output "    winePrefix: []"
    }
}

# Standalone emulators
$standalone = @(
    @{N="Nintendo 3DS"; F=@("%APPDATA%\Azahar\sdmc\Nintendo 3DS\**", "%APPDATA%\Azahar\nand\**")}
    @{N="Nintendo Entertainment System"; F=@("%EMUDIR%\Nestopia\saves\**", "%EMUDIR%\Nestopia\states\**")}
    @{N="Super Nintendo Entertainment System"; F=@("%EMUDIR%\Snes9x\SaveData\**")}
    @{N="Nintendo GameCube"; F=@("%APPDATA%\Dolphin Emulator\GC\**")}
    @{N="Nintendo Wii"; F=@("%APPDATA%\Dolphin Emulator\Wii\**")}
    @{N="Nintendo Wii U"; F=@("%APPDATA%\Cemu\mlc01\usr\save\**")}
    @{N="Nintendo Switch (Ryujinx)"; F=@("%APPDATA%\Ryujinx\bis\user\save\**", "%APPDATA%\Ryujinx\bis\system\save\**", "%APPDATA%\Ryujinx\sdcard\**")}
    @{N="Sony PlayStation"; F=@("%EMUDIR%\Duckstation\memcards\**")}
    @{N="Sony PlayStation 2"; F=@("%EMUDIR%\PCSX2\memcards\**")}
    @{N="Sony PlayStation 3"; F=@("%EMUDIR%\RPCS3\dev_hdd0\**")}
    @{N="Sony PlayStation Portable"; F=@("%EMUDIR%\PPSSPP\PSP\SAVEDATA\**")}
    @{N="Sony PlayStation Vita"; F=@("%EMUDIR%\Vita3K\Vita3K\**")}
    @{N="Sega Dreamcast"; F=@("%EMUDIR%\Flycast\data\**")}
    @{N="Microsoft Xbox"; F=@("%APPDATA%\xemu\xemu\**")}
    @{N="Microsoft Xbox 360"; F=@("%EMUDIR%\Xenia Canary\content\**", "%EMUDIR%\Xenia Canary\config\**")}
)

foreach ($e in $standalone) {
    $paths = @()
    foreach ($fp in $e.F) {
        $expanded = $fp
        foreach ($k in $replacements.Keys) {
            $v = $replacements[$k]
            if ($v -ne $k) { $expanded = $expanded.Replace($k, $v.Replace('\','/')) }
        }
        $paths += $expanded
    }

    Write-Output "  - name: `"$($e.N)`""
    Write-Output "    integration: override"
    Write-Output "    files:"
    foreach ($p in $paths) { Write-Output "      - `"$p`"" }
    Write-Output "    registry: []"
    Write-Output "    installDir: []"
    Write-Output "    winePrefix: []"
}

Write-Host "`n(Dry run complete. Use -ConfigPath to write changes.)" -ForegroundColor Cyan
