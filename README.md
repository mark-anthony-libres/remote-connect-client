# RemoteConnect – Client

The desktop client for **RemoteConnect**, an AnyDesk/TeamViewer-style remote access app. Built with [Avalonia](https://avaloniaui.net/) (.NET 10), targeting Windows, macOS, and Linux from a single codebase.

---

## Table of Contents

1. [Branches](#branches)
2. [Architecture Overview](#architecture-overview)
3. [Build & Run](#build--run)
4. [Project Structure](#project-structure)
5. [Not Yet Implemented](#not-yet-implemented)

---

## Branches

- **`avalonia-migration`** (default/active) — the current UI, rewritten in Avalonia. All active development happens here.
- **`master`** — a checkpoint of the original WinForms UI, kept as a reference/rollback point from before the Avalonia migration. Not actively developed.

---

## Architecture Overview

- **UI is built entirely in code** — no `.axaml` files. `App.cs` sets up a `FluentTheme` and creates `MainWindow` on startup; every view/control is a plain C# class under `UI/Avalonia/`.
- **`MainWindow.cs`** — the single window: a custom title bar (native chrome is drawn off on this platform, so caption buttons/drag-to-move/double-click-to-maximize are hand-implemented), a header bar, and a scrollable content area (Recent Connections grid, connect-by-ID input).
- **`UI/Avalonia/Controls/`** — reusable custom controls: `CaptionButton`, `IconBadge`, `IconLabel`, `IconTextBox`, `Icons`, `IdChip`, `ModernButton`, `RecentDeviceCard`, `RoundedBackground` (per-corner-radius background), `StatusBadge`, `TextRendering`, `HeroIllustration`.
- **`AppTheme.cs`** — centralizes every color and font used by the custom controls, so styling stays consistent in one place instead of being scattered across controls. (Named `AppTheme`, not `Theme`, because Avalonia's `StyledElement` already declares a `Theme` property that would silently shadow it.)
- **`Core/Device/DeviceConnection.cs`** — the device/connection model used by the Recent Connections UI. Currently placeholder data; not yet wired to a real backend. (The [`backend`](../backend) project has a matching `/ws/device` WebSocket endpoint that assigns a persistent device ID — connecting this client to it is future work.)

---

## Build & Run

### Requirements

- .NET 10 SDK (the project targets `net10.0`, and `RemoteDesktopClient.slnx` uses the newer XML solution format)

### Commands

```bash
# Restore + build
dotnet restore RemoteDesktopClient.slnx
dotnet build RemoteDesktopClient.slnx

# Run
dotnet run --project RemoteDesktopClient
```

Or open `RemoteDesktopClient.slnx` directly in an IDE that supports the `.slnx` format (recent Visual Studio / Rider / VS Code with the C# Dev Kit).

---

## Project Structure

```
client/
├── RemoteDesktopClient.slnx
└── RemoteDesktopClient/
    ├── Program.cs                   # entry point, Avalonia app builder
    ├── RemoteDesktopClient.csproj
    ├── Assets/Images/                # embedded via AvaloniaResource (avares://)
    ├── Core/
    │   └── Device/DeviceConnection.cs  # device/connection model (placeholder data)
    ├── UI/Avalonia/
    │   ├── App.cs                   # Avalonia Application: theme + main window bootstrap
    │   ├── AppTheme.cs              # centralized colors/fonts
    │   ├── MainWindow.cs            # title bar, header, content area
    │   └── Controls/                # custom controls (see Architecture Overview)
    ├── Native/                      # planned: platform-specific interop (Windows/macOS) — empty so far
    ├── Services/                    # planned — empty so far
    ├── Signaling/                   # planned: WebRTC signaling — empty so far
    └── WebRTC/                      # planned — empty so far
```

---

## Not Yet Implemented

Several folders exist as scaffolding for planned work but are currently empty: `Native/Windows`, `Native/macOS` (platform-specific interop), `Services/`, `Signaling/` and `WebRTC/` (the actual remote-control/screen-share transport). The UI is functional and navigable, but connecting to a real device — including pairing with the backend's device-identity endpoint — hasn't been built yet.
