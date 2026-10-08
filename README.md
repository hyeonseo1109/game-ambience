# Game Ambience

Game Ambience is a Windows desktop accessibility/awareness tool that turns a visible game HUD signal into subtle peripheral light. The MVP reads a user-selected health-bar region and shows a soft red edge pulse when health becomes dangerous.

The app observes pixels already visible on the screen. It does **not** read or modify game memory, inject DLLs, hook graphics APIs, inspect network traffic, automate input, or bypass anti-cheat software. Captured pixels remain in local memory and are not uploaded or retained by default.

## Technology

- **C# / .NET 8** for a long-supported native toolchain and testable core logic.
- **WPF** for a lightweight settings/calibration UI and mature transparent-window support.
- **Windows Graphics Capture** for GPU-friendly window/monitor capture without game-process access.
- **Win32 interop** only for target enumeration and the non-activating, click-through overlay.

WPF was selected over a browser shell because capture, pixel analysis, and overlay animation remain in the native process without transferring full frames into JavaScript. The reusable `GameAmbient.Core` assembly has no Windows dependency.

## Data flow

`Window/monitor -> Windows Graphics Capture -> normalized ROI -> ColorBarDetector -> median + hysteresis -> HUD state -> ambient overlay`

Only the latest frame is retained; if analysis is busy, stale frames are replaced rather than queued. The default Balanced preset analyzes at 8 Hz. When the target is not foreground or disappears, capture processing pauses and the overlay hides.

See [docs/architecture.md](docs/architecture.md) for responsibilities and design decisions.

## Build

Requirements: Windows 10 version 2004+ or Windows 11, Visual Studio 2022 with the .NET desktop workload, and .NET 8 SDK.

```powershell
dotnet restore GameAmbient.sln
dotnet build GameAmbient.sln -c Release
dotnet test GameAmbient.sln -c Release
dotnet run --project src/GameAmbient.Windows/GameAmbient.Windows.csproj
```

Borderless-windowed or windowed game mode is recommended. Ordinary overlays may not appear above exclusive fullscreen applications; Game Ambience deliberately does not work around that limitation with injection or hooks.

## Current scope

The MVP focuses on horizontal or vertical single-color bars, screenshot calibration, detector diagnostics, safe/warning/critical state stabilization, profile persistence, a full-pipeline simulator, and a click-through edge-pulse overlay. OCR, automatic HUD discovery, cloud sync, accounts, and plugins are out of scope.
