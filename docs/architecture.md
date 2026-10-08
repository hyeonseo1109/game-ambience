# Architecture

## Principles

The product name is confined to presentation and assembly metadata. Core types describe generic HUD signals so future mana, oxygen, stamina, and icon-state detectors do not require changes to capture or overlay infrastructure.

```text
Capture source
  -> ROI extractor
  -> Detector
  -> DetectionResult(value, confidence)
  -> Stabilizer
  -> HUD state
  -> Ambient state
  -> Overlay renderer
```

## Project boundaries

- `GameAmbient.Core`: platform-neutral pixels, detector, stabilization, rules, pipeline, profile schema, and metrics.
- `GameAmbient.Windows`: WPF composition root, Windows Graphics Capture adapter, target discovery, focus tracking, screenshot/ROI UI, and overlay window.
- `GameAmbient.Core.Tests`: generated-image detector tests, stabilization tests, profile validation, and pipeline behavior.

## Core contracts

- `ICaptureSource` produces frames and reports target loss. A source owns native textures and releases them deterministically.
- `IDetector` turns a small ROI into a normalized value plus confidence. `ColorBarDetector` follows a contiguous fill direction; `SegmentedHeartDetector` classifies cached normalized slots and can additionally return per-heart diagnostics. Neither knows about capture or effects.
- `ISignalStabilizer` rejects low-confidence samples, computes a bounded-window median, and applies state hysteresis.
- The monitoring pipeline has one replaceable frame slot: latest frame wins and memory cannot grow with capture rate.

## Coordinates and DPI

Profiles store ROI as `NormalizedRect` relative to the capture content, never absolute screen coordinates. Conversion to pixels occurs for each current frame. Overlay placement uses physical window/monitor bounds and is updated when size, position, monitor, or DPI changes.

## Color-bar detection

Pixels are converted to HSV and compared with independent hue, saturation, and value tolerances. Each column (or row) receives a match ratio. The detector follows the configured fill direction, accepts a small bounded gap, and estimates the contiguous filled extent rather than dividing matching pixels by all ROI pixels. Confidence combines interior coverage, boundary contrast, and continuity. No color match yields an unknown result rather than zero health.

## State safety

Low confidence and capture failure are `Unknown`; they never create a warning. Accepted values pass through a median window. Warning enters at 30% and exits at 40%; critical enters at 15% and exits at 20%. This prevents flicker around thresholds.

For segmented hearts, an unknown slot invalidates the whole frame by default. Compact user templates contain only 8×8 redness and structure features, not copyrighted source textures or full screenshots. Profile schema v2 adds a detector discriminator and optional segmented-heart configuration; schema v1 profiles continue to default to Color Bar.

## Overlay

The overlay is a separate transparent, borderless WPF window. Win32 extended styles make it click-through (`WS_EX_TRANSPARENT`), non-activating (`WS_EX_NOACTIVATE`), hidden from Alt+Tab (`WS_EX_TOOLWINDOW`), and topmost. Four gradient brushes create soft inward falloff; only brush opacity is animated. Safe/unknown hides the window and stops animation.

## Privacy and safety

Frames are processed locally, are not logged, and are discarded after the ROI is analyzed. Profiles contain calibration and target-matching hints, not screenshots. The app does not access the target process beyond standard top-level-window metadata.
