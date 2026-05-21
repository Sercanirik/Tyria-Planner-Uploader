# Tyria Uploader

> Open-source Windows companion app for [Tyria Planner](https://tyriaplanner.com).
> Watches your arcdps log folder and uploads each new combat log to your account
> in the background.

[![Build](https://img.shields.io/github/actions/workflow/status/Sercanirik/Tyria-Planner-Uploader/build.yml?branch=main)](../../actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

---

## What it does

1. Watches `Documents\Guild Wars 2\addons\arcdps\arcdps.cbtlogs\` (configurable)
   for new `.evtc` / `.zevtc` files.
2. Waits for the file to finish writing, arcdps streams as the fight runs.
3. Hashes the raw bytes (SHA-256) so the same fight is never uploaded twice.
4. Spawns the [Elite Insights CLI](https://github.com/baaron4/GW2-Elite-Insights-Parser)
   to parse the log into JSON locally on your machine.
5. POSTs the JSON to `https://tyriaplanner.com/api/logs/eijson` with your
   OAuth bearer token.
6. Records the hash so it isn't re-uploaded after a restart.

The uploader **never** sends your raw `.evtc` file off your machine, only the
JSON Elite Insights produces. You can inspect that JSON yourself in
`%TEMP%\TyriaUploader\` while a parse is in flight.

## Why open source?

So you don't have to trust the binary. The project is intentionally tiny
(~10 source files) and every network call lives in `Api/ApiClient.cs` and
`OAuth/OAuthClient.cs`. Audit them in 15 minutes; build it yourself if you
want.

## Install

Two options:

### A) Pre-built release (recommended)

1. Download the latest `TyriaUploader.exe` from the [Releases](../../releases) page.
2. Download the latest [Elite Insights CLI](https://github.com/baaron4/GW2-Elite-Insights-Parser/releases)
   release. Extract it anywhere, you'll point the uploader at the
   `GuildWars2EliteInsights-CLI.exe` inside.
3. Run `TyriaUploader.exe`. The Settings window opens on first launch:
   * Click **Sign In…** to authorize against your Tyria Planner account.
   * Set the **GW2EI CLI executable** path.
   * Confirm the **arcdps log folder** is correct.
   * Click **Save**.

The uploader minimizes to the system tray (right side of the taskbar).
Right-click the icon for menu options.

> **First-run SmartScreen warning**: the exe is unsigned. Click *More info* →
> *Run anyway*. Reputation builds up automatically over time. EV code
> signing is on the roadmap but adds a recurring cost (~$200/yr).

### B) Build from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```cmd
git clone https://github.com/Sercanirik/Tyria-Planner-Uploader.git
cd Tyria-Uploader
dotnet publish TyriaUploader -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The single-file exe lands at `TyriaUploader\bin\Release\net8.0-windows\win-x64\publish\TyriaUploader.exe`.

## Security model

Short version: minimal scope, no password storage, encrypted token, audited
boundaries. See [SECURITY.md](SECURITY.md) for the full threat model.

| Risk | Mitigation |
|---|---|
| Stolen token used by attacker | Server-side POV-account check, token can only upload your own logs |
| Disk image stolen | Token encrypted with Windows DPAPI (CurrentUser scope), unusable on another machine |
| Database leak | Tokens stored only as SHA-256 hashes, leak yields no usable tokens |
| Compromised uploader build | Build from source; or audit `Api/` and `OAuth/` (~300 LOC total) |
| Token leak after revoke | Revoke from `tyriaplanner.com/profile`; server invalidates immediately |

## Settings file

Stored at `%APPDATA%\TyriaUploader\settings.json`:

```json
{
  "apiBaseUrl": "https://tyriaplanner.com",
  "logFolder": "C:\\Users\\you\\Documents\\Guild Wars 2\\addons\\arcdps\\arcdps.cbtlogs",
  "gw2EiCliPath": "C:\\Tools\\GW2EI\\GuildWars2EliteInsights-CLI.exe",
  "rescanIntervalSeconds": 60,
  "uploadOnlyIfGw2Running": false,
  "startWithWindows": true,
  "signedInUsername": "your-username",
  "signedInDisplayName": "Your Display Name"
}
```

Editable while the uploader is closed.

## Logs

* Diagnostic log: `%APPDATA%\TyriaUploader\uploader.log` (auto-rotates at 5 MB)
* Encrypted token: `%APPDATA%\TyriaUploader\token.bin`
* Already-uploaded hashes: `%APPDATA%\TyriaUploader\uploaded.json`
* Settings: `%APPDATA%\TyriaUploader\settings.json`

## Uninstall

1. Right-click tray icon → **Quit**.
2. Delete `%APPDATA%\TyriaUploader\`.
3. (Optional) Revoke the OAuth token at `https://tyriaplanner.com/profile`.
4. (Optional) Delete the registry value
   `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\TyriaUploader`.

## Project layout

```
TyriaUploader.sln
TyriaUploader/
├── TyriaUploader.csproj
├── Program.cs                  # entry point + single-instance mutex
├── Api/
│   └── ApiClient.cs            # POSTs to /api/logs/eijson
├── Config/
│   ├── Settings.cs             # config model
│   ├── SettingsStore.cs        # JSON file load/save + paths
│   └── FileLogger.cs           # rotating log writer
├── Gw2Ei/
│   ├── Gw2EiRunner.cs          # spawns GW2EI CLI
│   └── LogHash.cs              # SHA-256 of EVTC bytes
├── OAuth/
│   ├── OAuthClient.cs          # PKCE + loopback redirect
│   ├── PkceUtil.cs             # verifier / challenge / state
│   └── TokenStore.cs           # DPAPI-encrypted token storage
├── UI/
│   ├── TrayApplicationContext.cs  # NotifyIcon + menu
│   ├── SettingsForm.cs         # WinForms settings window
│   └── Autostart.cs            # HKCU Run registry entry
└── Watcher/
    └── LogWatcher.cs           # FileSystemWatcher → parse → upload pipeline
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Security issues: see [SECURITY.md](SECURITY.md).

## License

MIT. See [LICENSE](LICENSE).
