# Screenshots

How to capture a frame from the built game, and drive the player with virtual key presses, so rendering and movement can be checked without the editor.

---

## Usage

`shot.ps1` launches `Builds/Windows/PipeWorld.exe` in a 1280x720 window, captures one frame (post-processing included), and quits. Build first with [build-pipeline.md](build-pipeline.md); `-Build` and `-Setup` do that in the same command.

| Parameter | Meaning |
|---|---|
| `-Out` | Output PNG, relative to the repo root. Default `Builds\Shots\shot.png`. |
| `-Time` | Time of day, 0 to 1 (see [lighting-and-day-night.md](lighting-and-day-night.md#time-of-day)). Freezes the cycle. Omitted: the scene's default time. |
| `-Pos` | Player position as `x,y,z`. |
| `-Look` | Player view as `yaw,pitch` in degrees. |
| `-Keys` | Comma-separated Input System key names (`W`, `A`, `Space`, `LeftShift`) held down. |
| `-Hold` | Seconds to hold `-Keys`. Default 1.5. |
| `-Frames` | Frames to wait before the hold or the capture. Default 30. |
| `-Respawn` | `centre` or `outer`: calls the matching [debug menu](debug-menu.md#respawn) respawn and logs the position. |
| `-Menu` | Opens the debug menu before the capture. |
| `-Click` | `x,y` in screen pixels, origin bottom-left: injects a left click there after the warm-up frames, to drive the menu. |
| `-Build`, `-Setup` | Run [build.ps1](../build.ps1) (with `-Setup` for `-Setup`) first. |

```powershell
.\shot.ps1 -Time 0.5 -Pos "0,0.05,0" -Look "0,8" -Out Builds\Shots\noon.png
.\shot.ps1 -Pos "0,0.05,0" -Keys "W,LeftShift" -Hold 2
.\shot.ps1 -Pos "0,60,-120" -Look "0,25"         # aerial overview of the crater
.\shot.ps1 -Pos "0,420,0" -Look "0,90" -Width 1280 -Height 1280   # top-down
```

`-Look` yaw is a compass heading around Y: 0 faces +Z, 90 faces +X. To aim at a feature, stand on the ground beside it (`-Pos` Y is the player's feet) and use the heading toward it. Mesa positions are logged as `[DemoWorld] mesa at (x, z) ... ground y` (see [landscape.md](landscape.md#regenerating)).

---

## Implementation

[ScreenshotRig.cs](../Assets/Scripts/Tools/ScreenshotRig.cs) is a component on the `Screenshot Rig` object in `World`. It does nothing unless the command line contains `-shot <path>`. The `-shot*` arguments map one-to-one to the table above (`-shotTime`, `-shotPos`, `-shotLook`, `-shotKeys`, `-shotHold`, `-shotFrames`).

- Position and view are applied with [PlayerController.Teleport](../Assets/Scripts/Player/PlayerController.cs).
- With `-Click`, the rig adds a virtual `Mouse` device and queues a move, a press and a release. The virtual mouse becomes `Mouse.current`, which Dear ImGui reads.
- With `-Keys`, the rig adds a virtual `Keyboard` device and queues a key state every frame for `-Hold` seconds, then logs `[ShotRig] player position ... maxY ... crouching ...` to `Player.log`.
- Without `-Keys`, the rig disables `PlayerController` immediately. Disabling releases the cursor lock, so an unattended capture never takes over the mouse.

**Key holds are measured in seconds, not frames.** The player runs uncapped, so a fixed frame count can span a tiny fraction of a second.

With `-Frames` above 20, the rig also logs `[ShotRig] average frame N ms`, measured after the first ten frames, as a quick performance check.

The screenshot is written by `ScreenCapture.CaptureScreenshot` at the end of a frame, so the rig waits two frames before calling `Application.Quit`.

Screenshots under `Builds/` are ignored by git.
