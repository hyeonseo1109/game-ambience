# Minecraft segmented-heart calibration

The Segmented Hearts detector supports the ordinary single-row red-heart HUD. It treats every calibrated slot independently as full, half, empty, or unknown and never converts an unknown slot into zero health.

## Calibrate from screenshots

1. Import a screenshot with the normal red-heart HUD visible.
2. Select a tight ROI around the heart row only.
3. Choose **Segmented Hearts** under Detection method.
4. Confirm the numbered slot boxes. The app estimates the count from ROI aspect ratio; adjust **Heart slots** if necessary.
5. Review the colored boxes and `FULL / HALF / EMPTY / UNKNOWN` summary.
6. Optionally select a representative slot and use **Mark Full**, **Mark Half**, or **Mark Empty**. Register all three states before template matching replaces structural classification.
7. Save the profile. Slot rectangles and compact 8×8 redness/structure feature templates are stored; the source screenshot is not embedded.

The two 1680×1050 test screenshots supplied during development use approximately `x=476, y=895, width=326, height=38`. Offline validation measured the first as seven full hearts, one half heart, and two empty hearts (15/20, 75%), and the second as ten full hearts (20/20, 100%). These images are not redistributed by the project.

## Classification

Each calibrated slot is downsampled to an 8×8 feature grid. Every cell stores red dominance and darkness/structure. When user templates for full, half, and empty are all available, normalized template similarity and the margin between the top candidates determine state and confidence. Without a complete template set, the fallback classifier compares red coverage in the left and right halves while requiring visible dark heart structure for an empty slot.

Any ambiguous slot makes the frame unknown by default. Low-confidence frames therefore do not create a false critical warning. The existing median and hysteresis pipeline handles valid readings and brief damage-animation gaps.

## Current limitations

- Normal red hearts in one row are the primary supported case.
- Poison, wither, absorption, hardcore textures, blinking damage frames, custom resource packs, and multi-row automatic discovery are not guaranteed.
- Multiple rows can be represented by individual normalized slots in the profile, but the current UI automatically lays out a single row.
- For custom textures, register all three templates from screenshots captured with the same resource pack and GUI scale.
