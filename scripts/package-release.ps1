<#.SYNOPSIS
    Builds the plugin and creates an installable release ZIP.

.DESCRIPTION
    Packages this plugin's compiled DLL with supporting documentation,
    example Ludusavi config, and helper scripts. Ludusavi and rclone are
    intentionally not bundled; users should install those trusted upstream
    tools separately.

.PARAMETER Version
    Release version, for example 1.0.1.

.PARAMETER DotnetPath
    Optional path to dotnet.exe. Useful when another runtime-only dotnet is
    earlier on PATH.
#>

param(
    [string]$Version = "1.0.1",
    [string]$DotnetPath = "dotnet"
)

$ErrorActionPreference = "Stop"

$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repo "src\LudusaviLaunchBox.csproj"
$releaseDir = Join-Path $repo "artifacts\release"
$publish = Join-Path $releaseDir "LudusaviLaunchBox-$Version"
$zip = Join-Path $releaseDir "LudusaviLaunchBox-$Version.zip"

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
New-Item -ItemType Directory -Path $publish | Out-Null

& $DotnetPath build $project -c Release

$build = Join-Path $repo "src\bin\Release\net9.0-windows"
Copy-Item -LiteralPath (Join-Path $build "LudusaviLaunchBox.dll") -Destination $publish
Copy-Item -LiteralPath (Join-Path $repo "README.md") -Destination $publish
Copy-Item -LiteralPath (Join-Path $repo "SECURITY.md") -Destination $publish
Copy-Item -LiteralPath (Join-Path $repo "RELEASE_NOTES.md") -Destination $publish

New-Item -ItemType Directory -Path (Join-Path $publish "examples") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $publish "scripts") | Out-Null
Copy-Item -LiteralPath (Join-Path $repo "examples\config.yaml") -Destination (Join-Path $publish "examples")
Get-ChildItem -LiteralPath (Join-Path $repo "scripts") -File -Filter "*.ps1" |
    Copy-Item -Destination (Join-Path $publish "scripts")

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip

Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $publish "LudusaviLaunchBox.dll"), $zip |
    Format-Table -AutoSize

Write-Host "Created: $zip" -ForegroundColor Green
