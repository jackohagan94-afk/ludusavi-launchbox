<#.SYNOPSIS
    Registers a monthly Windows Scheduled Task for plugin maintenance.

.EXAMPLE
    .\scripts\register-monthly-maintenance.ps1 -Yes
#>

param(
    [string]$LaunchBoxPath = "$env:USERPROFILE\LaunchBox",
    [string]$TaskName = "Ludusavi LaunchBox Monthly Maintenance",
    [int]$DayOfMonth = 1,
    [string]$At = "09:00",
    [switch]$Yes
)

$ErrorActionPreference = "Stop"

$script = Join-Path $PSScriptRoot "update-maintenance.ps1"
if (-not (Test-Path -LiteralPath $script)) {
    throw "Maintenance script not found: $script"
}

if (-not $Yes) {
    $answer = Read-Host "Register monthly task '$TaskName' for day $DayOfMonth at $At? [Y/n]"
    if ($answer -and -not $answer.Trim().ToLowerInvariant().StartsWith("y")) {
        Write-Host "Cancelled."
        exit 0
    }
}

$powershell = Join-Path $env:SystemRoot "System32\WindowsPowerShell\v1.0\powershell.exe"
$arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$script`" -LaunchBoxPath `"$LaunchBoxPath`" -Yes"

$action = New-ScheduledTaskAction -Execute $powershell -Argument $arguments
$trigger = New-ScheduledTaskTrigger -Monthly -DaysOfMonth $DayOfMonth -At ([datetime]::Parse($At))
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 1)

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Description "Updates Ludusavi LaunchBox plugin support tools monthly." -Force | Out-Null

Write-Host "Registered scheduled task: $TaskName" -ForegroundColor Green
