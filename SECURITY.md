# Security Policy

## Supported Versions

Security fixes are applied to the latest GitHub release.

## Reporting

Please open a private report or contact the maintainer before publishing exploit details. Include:

- LaunchBox version
- Plugin version
- Ludusavi version
- Steps to reproduce
- Any relevant `ludusavi_settings.json` values, with private paths or remote names redacted

## Current Audit Notes

The plugin is a local LaunchBox extension. Its main security boundary is the local user account running LaunchBox.

- The plugin executes the configured Ludusavi binary directly with `UseShellExecute = false`.
- Command-line values are passed through `ProcessStartInfo.ArgumentList` instead of shell-joined strings.
- The plugin writes only its local settings file, `Plugins\ludusavi_settings.json`.
- Restore operations require user confirmation in the manual menu flow.
- Cloud synchronization is handled by Ludusavi/rclone, not by this plugin.
- Release packages do not bundle Ludusavi or rclone binaries; users install those tools directly from their upstream projects.

## User Guidance

- Download `LudusaviLaunchBox.dll` only from the GitHub release assets.
- Do not install GitHub's automatic source archives as the plugin.
- Set `ExePath` to a trusted `ludusavi.exe` location or make sure `ludusavi` on `PATH` is trusted.
- Review restore prompts carefully, because restoring save data can overwrite local saves.
