# Game Ambience

Game Ambience is a Windows desktop accessibility/awareness tool that turns a visible game HUD signal into subtle peripheral light. The MVP reads a user-selected health-bar region and shows a soft red edge pulse when health becomes dangerous.

## Run

### Portable Windows build

1. Open the latest successful GitHub Actions run.
2. Download the `GameAmbient-win-x64` artifact.
3. Extract the archive and run `GameAmbient.exe`.

The build is self-contained, so users do not need Visual Studio or a separate .NET installation. It is currently unsigned, so Windows may display a first-run warning.

### Development

```powershell
dotnet restore GameAmbient.sln
dotnet run --project src/GameAmbient.Windows/GameAmbient.Windows.csproj
```

### Minecraft overlay test

1. Run Minecraft in windowed or borderless-windowed mode.
2. Import a screenshot and select the HUD ROI.
3. Click **Choose game window** and select Minecraft.
4. Use **Warning** and **Critical** to verify the click-through overlay.
5. Click **Start**, return to Minecraft, then test Alt+Tab, window movement, and resize.

Vanilla Minecraft uses segmented hearts. The current `ColorBarDetector` can validate capture and overlay behavior but cannot report those hearts accurately. See [the Windows smoke-test guide](docs/windows-smoke-test.md) for the supplied screenshots and exact steps.

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

## Build locally

Requirements: Windows 10 version 2004+ or Windows 11, Visual Studio 2022 with the .NET desktop workload, and .NET 8 SDK.

```powershell
dotnet restore GameAmbient.sln
dotnet build GameAmbient.sln -c Release
dotnet test GameAmbient.sln -c Release
dotnet run --project src/GameAmbient.Windows/GameAmbient.Windows.csproj
dotnet publish src/GameAmbient.Windows/GameAmbient.Windows.csproj -c Release -r win-x64 --self-contained true
```

Borderless-windowed or windowed game mode is recommended. Ordinary overlays may not appear above exclusive fullscreen applications; Game Ambience deliberately does not work around that limitation with injection or hooks.

## Current scope

The MVP focuses on horizontal or vertical single-color bars, screenshot calibration, detector diagnostics, safe/warning/critical state stabilization, profile persistence, a full-pipeline simulator, game-window tracking, tray behavior, and a click-through edge-pulse overlay. OCR, automatic HUD discovery, cloud sync, accounts, and plugins are out of scope.
