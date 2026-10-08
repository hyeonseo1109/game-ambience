# Windows and Minecraft smoke test

Use Minecraft in windowed or borderless-windowed mode. Exclusive fullscreen is intentionally unsupported because Game Ambient does not inject into or hook the game.

## Prepare the supplied screenshots

The supplied Minecraft screenshots are 1680 × 1050. Their vanilla heart HUD is approximately:

- pixels: `x=476, y=895, width=326, height=38`
- normalized: `x=0.283, y=0.852, width=0.194, height=0.036`

Select only the heart row, excluding hunger and the experience bar. The current Color Bar detector will not report vanilla hearts accurately; this selection is for validating live capture and lifecycle. A SegmentedDetector is required for actual heart counting.

## Test procedure

1. Extract the `GameAmbient-win-x64` artifact and run `GameAmbient.exe`.
2. Open Minecraft world **게임 앰비언스 테스트용** in windowed or borderless-windowed mode.
3. In Game Ambient, choose **Import PNG / JPEG** and open either supplied screenshot.
4. Drag a tight rectangle around the ten-heart row using the coordinates above as a guide.
5. Click **Choose game window**, then select the Minecraft window in the Windows capture picker.
6. Confirm that the target title, process name, and resolution appear in Game Ambient.
7. Click **Warning**. Confirm a soft red pulse follows the Minecraft window bounds.
8. Click inside Minecraft and verify mouse input passes through the overlay.
9. Move with WASD and verify keyboard focus remains in Minecraft.
10. Click **Critical** and verify the stronger, faster pulse.
11. Change **Intensity** and **Glow width**; the preview should update without recreating the target.
12. Click **Start**, switch to Minecraft, and verify status changes to monitoring.
13. Alt+Tab away. The live overlay must hide and detection must pause.
14. Return to Minecraft. Detection and the most recent ambient state must resume.
15. Move and resize the Minecraft window. The overlay should follow within about 250 ms.
16. Move Minecraft to a monitor with a different DPI scale and verify the physical edges remain aligned.
17. Minimize the Game Ambient window. Monitoring should continue and the tray icon should remain.
18. From the tray, test Open, Pause/Resume, Stop Monitoring, and Exit.
19. Close Minecraft. Game Ambient should show Target Lost, hide the overlay, and remain running.
20. Choose the newly opened Minecraft window again to reconnect.

## Expected limitations

- Color Bar detection is intended for continuous bars and does not count vanilla Minecraft hearts.
- Minecraft heart detection needs a future `SegmentedDetector` that classifies full, half, and empty slots.
- Windows may warn on first launch because the portable build is not code-signed.
