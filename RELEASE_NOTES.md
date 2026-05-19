# v1.0.2

## Added

- Added optional `scripts/setup-tools.ps1` to install/update Ludusavi and rclone from upstream, write plugin defaults, create a Ludusavi config, run path discovery, and copy local HLTB theme/dataset assets into LaunchBox.
- Added `scripts/update-maintenance.ps1` for monthly maintenance updates.
- Added `scripts/register-monthly-maintenance.ps1` to register a Windows Scheduled Task for monthly maintenance.

# v1.0.1

## Fixed

- Rebuilt the installable LaunchBox plugin from the repository source instead of the older loose `0.1.0` local project.
- Removed developer-specific path detection and temp marker file writes.
- Passed Ludusavi CLI arguments with `ProcessStartInfo.ArgumentList` to avoid shell-style argument construction.
- Fixed `RestoreAll` so the optional backup path is passed correctly.

## Documentation

- Clarified that users should download `LudusaviLaunchBox.dll`, not GitHub's automatic source archives.
- Added security policy and audit notes.
