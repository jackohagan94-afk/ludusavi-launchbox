# v1.0.1

## Fixed

- Rebuilt the installable LaunchBox plugin from the repository source instead of the older loose `0.1.0` local project.
- Removed developer-specific path detection and temp marker file writes.
- Passed Ludusavi CLI arguments with `ProcessStartInfo.ArgumentList` to avoid shell-style argument construction.
- Fixed `RestoreAll` so the optional backup path is passed correctly.

## Documentation

- Clarified that users should download `LudusaviLaunchBox.dll`, not GitHub's automatic source archives.
- Added security policy and audit notes.
