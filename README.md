# WinGetMGui

A minimal Windows GUI wrapper for [winget](https://learn.microsoft.com/windows/package-manager/), the Windows Package Manager.
Search, install, uninstall and update packages from a simple grid-based interface.

## Features

- **Tab-based modes** (the tab picks the action, grid double-click and context menu):
  - **Update** — installed packages that have updates available; can ignore a specific version or a whole package
  - **Install** — search the winget catalog and install; **Scope** selector (Default/User/Machine) passes `--scope` to winget
  - **Uninstall** — all installed packages
  - **Ignored** — managed ignore entries (unignore via action button, double-click or Delete)
  - **Settings** — winget behavior checkboxes: Silent, Disable interactivity, Accept source agreements, Accept package agreements, Include unknown, Wait, Force (Wait and Force off by default)
- **Search box** filters Update/Uninstall/Ignored rows client-side, searches the catalog on Install
- **Refresh** (or F5) runs `winget source update` first, then reloads the current tab
- **Ignore rules** (per package or per available version)
- **App data** — settings and ignore rules are persisted in `%AppData%\WinGetMGui`
- **Localized winget output** handled: probes `--output-locale en-US` (auto-enabled once winget supports it) and falls back to positional column mapping for non-English winget output
- **Info** — package details from `winget show` in a modal window (Info button or context menu on any grid tab)
- Install/uninstall/upgrade run in their own visible winget console window (the **Wait** setting adds `--wait` to keep it open at the end so you can read output and errors)
- Status bar with package count

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+F` | Focus the search box |
| `F5` | Refresh (when idle) |
| `Delete` | Unignore entry (Ignored tab) |
| Double-click | Current tab's verb on the selected row |
| Right-click | Context menu (Info, verb + ignore options on Update tab) |

## Requirements

- Windows with winget (`winget` available on `PATH`)
- .NET 8 Desktop Runtime to run
- .NET 8 SDK to build

## Build

```powershell
dotnet build
```

## License

Copyright (C) 2026 Maxim Chirikov.

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version — see [LICENSE](LICENSE).

Anyone who distributes this program or a modified version must release it under the same GPL-3.0 license, including the corresponding source code.
