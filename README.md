# Ludusavi for LaunchBox

Cloud save backup & restore for LaunchBox, powered by [Ludusavi](https://github.com/mtkennerly/ludusavi).  
Inspired by and modeled after [ludusavi-playnite](https://github.com/mtkennerly/ludusavi-playnite).

## Credits

- **[mtkennerly](https://github.com/mtkennerly)** — creator of [Ludusavi](https://github.com/mtkennerly/ludusavi) (the save backup engine) and [ludusavi-playnite](https://github.com/mtkennerly/ludusavi-playnite) (the Playnite plugin this project is based on)
- **[Unbroken Software](https://www.launchbox-app.com/)** — creators of [LaunchBox](https://www.launchbox-app.com/), the frontend this plugin integrates with
- **Community** — plugin development by LaunchBox users

## Features

- **Automatic cloud save sync** — restore saves before playing, back them up when you exit
- **No prompts (optional)** — fully automatic mode: restore on launch, backup on exit, no dialogs
- **Known save locations for 10,000+ PC games** — via Ludusavi's manifest (sourced from [PCGamingWiki](https://www.pcgamingwiki.com))
- **Platform-level emulator support** — back up ALL saves for a console platform (GBA, DS, PS2, etc.) using Ludusavi custom entries
- **Right-click game menu** — manual backup/restore per game or per platform
- **Game badges** — green dot on games recognized by Ludusavi
- **Per-game tag overrides** — `[Ludusavi] Skip`, `[Ludusavi] Game: backup`, etc.
- **Cloud sync via rclone** — supports Google Drive, Dropbox, OneDrive, etc.

## Requirements

| Component | Details |
|---|---|
| **LaunchBox** | Version 13.x or newer (uses .NET 9.0 plugin host) |
| **Windows** | Windows 10/11 64-bit |
| **Ludusavi** | v0.24.0 or newer (v0.31.0+ recommended); installed separately |
| **rclone** | Optional and installed separately — only if using cloud sync |

## Installation

GitHub automatically shows "Source code" downloads on every release. Those are for developers and are not the plugin.

For normal installation, download the release asset named `LudusaviLaunchBox.dll`.

### 1. Install Ludusavi

Download the latest Ludusavi release from [github.com/mtkennerly/ludusavi/releases](https://github.com/mtkennerly/ludusavi/releases).

Extract `ludusavi.exe` somewhere permanent (e.g. `C:\Tools\ludusavi\`). Launch it once to generate the default config at `%APPDATA%\ludusavi\config.yaml`.

### 2. Configure Ludusavi

Copy `examples/config.yaml` to `%APPDATA%\ludusavi\config.yaml` as a starting point. Customize:

- The **backup path** — where your save backups will be stored
- **Custom game entries** — one per emulator platform you want to back up (edit the paths to match your emulator save folders)
- **Cloud sync** — set the `remote` field to your rclone remote name if using cloud storage

### 3. (Recommended) Auto-Detect Emulator Paths

Run the discovery script to scan your system and fill in emulator save paths:

```powershell
.\scripts\discover-paths.ps1 -ConfigPath "$env:APPDATA\ludusavi\config.yaml"
```

This replaces `%RETROARCH%`, `%EMUDIR%`, `%USERPROFILE%`, and `%APPDATA%` placeholders with actual detected paths. Run it without `-ConfigPath` first to preview what it finds.

### 4. Set Up Cloud Sync (Optional)

Ludusavi uses [rclone](https://rclone.org) for cloud sync. Install it first:

```powershell
# Via scoop (recommended)
scoop install rclone

# Or download from https://rclone.org/downloads/
```

Configure a remote (example for Google Drive):

```powershell
rclone config
# n) New remote
# name> gdrive
# type> drive
# Follow the prompts to authenticate in your browser
```

Then set the remote in your ludusavi config:

```yaml
cloud:
  remote: "gdrive:"        # the colon is required
  path: ludusavi-backup
  synchronize: true
```

Test it works:

```powershell
ludusavi cloud sync --api
```

### 5. Install the Plugin

1. Download `LudusaviLaunchBox.dll` from the [latest release](https://github.com/johagan94/ludusavi-launchbox/releases)
2. Drop it into your LaunchBox `Plugins` folder:
   ```
   C:\Users\<You>\LaunchBox\Plugins\LudusaviLaunchBox.dll
   ```
3. Restart LaunchBox

Do not install the release source `.zip` or `.tar.gz`; LaunchBox needs the compiled `.dll`.

### 6. Configure the Plugin

On first run, the plugin creates `Plugins\ludusavi_settings.json`. Edit it to set:

```json
{
  "ExePath": "C:\\Tools\\ludusavi\\ludusavi.exe",
  "BackupOnGameExited": true,
  "AskBackupOnGameExited": false,
  "RestoreOnGameStarting": true,
  "OnlyBackupOnGameExitedIfPc": false,
  "BackupByPlatformForNonPc": true
}
```

| Setting | Default | Description |
|---|---|---|
| `ExePath` | `ludusavi` | Full path to `ludusavi.exe` |
| `BackupOnGameExited` | `true` | Auto-backup after exiting a game |
| `AskBackupOnGameExited` | `true` | Prompt before backing up (set `false` for silent) |
| `RestoreOnGameStarting` | `false` | Auto-restore before launching a game |
| `OnlyBackupOnGameExitedIfPc` | `false` | Skip auto-backup for emulated games |
| `BackupByPlatformForNonPc` | `true` | Use platform name for non-PC games (matches Ludusavi custom entries) |
| `AddSuffixForNonPcGameNames` | `true` | Append `" (<platform>)"` to game names when resolving in Ludusavi |

### Optional Setup Helper

The release ZIP includes helper scripts for users who want a guided setup:

```powershell
.\scripts\setup-tools.ps1
```

It can install/update Ludusavi and rclone from upstream, write plugin defaults, copy the example Ludusavi config, run path discovery, and copy local HLTB theme/dataset assets when those files are available.

To register monthly maintenance:

```powershell
.\scripts\register-monthly-maintenance.ps1
```

The monthly task runs `scripts\update-maintenance.ps1`, which checks the latest GitHub release asset for this plugin and refreshes Ludusavi/rclone from their upstream downloads.

## How It Works

### PC Games

When you launch a PC game from LaunchBox, the plugin looks it up in Ludusavi's manifest (10,000+ known games from PCGamingWiki). If found, Ludusavi knows exactly where the game stores its save files and backs them up.

### Emulated (Non-PC) Games

For emulator platforms, you create custom entries in Ludusavi's config — one per console. Name them to match your LaunchBox platform names (e.g. `"Nintendo Game Boy Advance"`). The plugin detects non-PC games and backs up / restores by platform name, capturing all saves for that console.

### Tags

Add tags to any game in LaunchBox to override the global behavior:

| Tag | Effect |
|---|---|
| `[Ludusavi] Skip` | Never back up or restore this game |
| `[Ludusavi] Game: backup` | Always back up, no prompt |
| `[Ludusavi] Game: no backup` | Never back up |
| `[Ludusavi] Game: backup and restore` | Always back up AND restore |
| `[Ludusavi] Platform: backup` | Always back up by platform name |
| `[Ludusavi] Platform: no backup` | Never back up by platform |
| `[Ludusavi] Backed up` | Informational — marks game as backed up |
| `[Ludusavi] Unknown save data` | Informational — marks game with unknown saves |

## Building from Source

Requires [.NET 9.0 SDK](https://dotnet.microsoft.com/download).

```powershell
# Clone
git clone https://github.com/YOUR_USER/ludusavi-launchbox.git
cd ludusavi-launchbox

# Build
dotnet build src/LudusaviLaunchBox.csproj -c Release

# Output: src/bin/Release/net9.0-windows/LudusaviLaunchBox.dll
```

The project references `Unbroken.LaunchBox.Plugins.dll` from your LaunchBox install. The stub in `stubs/` is a compile-time substitute — it is not shipped.

## Releasing

Release builds are signed via **Azure Trusted Signing** to prevent LaunchBox from blocking the plugin.

Push a tag to trigger the release workflow:

```powershell
git tag v1.0.3
git push origin v1.0.3
```

### Required GitHub Secrets

| Secret | Description |
|---|---|
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_CLIENT_ID` | Service principal with Trusted Signing Certificate Profile Signer role |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
| `TRUSTED_SIGNING_ACCOUNT_NAME` | Azure Trusted Signing account name |
| `TRUSTED_SIGNING_CERT_PROFILE` | Certificate profile name |

### Setup Azure Trusted Signing

1. Create an [Azure subscription](https://azure.microsoft.com/free) (free tier works)
2. Follow [Microsoft's guide](https://learn.microsoft.com/en-us/azure/trusted-signing/how-to-signing-integrations) to create a Trusted Signing account
3. Create a certificate profile (Public Trust or Private)
4. Create a service principal with `Trusted Signing Certificate Profile Signer` role
5. Add the secrets above to your GitHub repo settings

## License

MIT — see [LICENSE](LICENSE) file.

Ludusavi and ludusavi-playnite are MIT-licensed projects by [mtkennerly](https://github.com/mtkennerly).
