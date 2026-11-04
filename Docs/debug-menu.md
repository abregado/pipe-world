# Debug Menu

The in-game debug window for jumping through the day/night cycle and respawning, built on Dear ImGui. Read this before adding a debug control or changing how ImGui is set up.

---

## Usage

Press **F1** (or the backquote key), or click the window's close button, to toggle the menu in the built game.

[DebugMenu.cs](../Assets/Scripts/Tools/DebugMenu.cs) draws the window from `UImGuiUtility.Layout`, the event the UImGui package raises once per frame. Controls are plain ImGui calls (`ImGui.Button`, `ImGui.SliderFloat`); adding one is a call in a `Draw...` method. Public float and bool fields (`cycle.timeOfDay`, `cycle.paused`) are passed to ImGui with `ref`, so a slider edits the field directly.

| Section | Controls |
|---|---|
| Readout | Frame rate, player position, distance from the crater centre |
| Time of day | Clock, fog end, night-factor bar, time slider |
| Time buttons | Start of day, Noon, Start of night, Midnight |
| Time options | Pause, day length (30 to 1800 s) |
| Fog start / end | Day and night fog start and end sliders |
| Light grade | Global strength of the [light grade](realtime-lights.md#light-grade) |
| Respawn | Centre of crater, Inside outer wall |

Start of day and start of night use [DayNightCycle.StartOfDayTime](../Assets/Scripts/World/DayNightCycle.cs) and `StartOfNightTime`: the times at which sun elevation crosses `nightStart`, the point where the night grade begins to fade in.

---

## Cursor and Input

Opening the menu disables the `PlayerController`, which releases the cursor lock so the menu can be clicked. Closing it re-enables the controller. The `Visible` setter ([DebugMenu.cs:22](../Assets/Scripts/Tools/DebugMenu.cs)) is the only place that does this.

**While the menu is open the player does not move or look.** Time keeps advancing unless Pause is ticked.

The window uses `NoSavedSettings` and `AlwaysAutoResize`, so ImGui never stores its position or size.

---

## Respawn

[RespawnAtCentre](../Assets/Scripts/Tools/DebugMenu.cs) places the player at the crater centre. [RespawnInsideOuterWall](../Assets/Scripts/Tools/DebugMenu.cs) places the player 25 m inside the outer ring at 225°, on the terrace's actual ground height from `WorldShape.Height`, facing the crater centre.

Both go through `PlayerController.Teleport`, which also resets velocity and crouch (see [player-controller.md](player-controller.md#teleport)).

[screenshots.md](screenshots.md) describes `-Respawn`, `-Menu` and `-Click`, which exercise these from the command line.

---

## Dear ImGui Setup

The library is the `com.psydack.uimgui` package, pinned to an exact commit in `Packages/manifest.json`. It wraps ImGui.NET and ships native libraries (`cimgui.dll` and add-on libraries) for several platforms, which is most of the roughly 36 MB it adds to the player.

[WorldSetup.AddDearImGui](../Assets/Editor/WorldSetup.cs) builds the scene's `Dear ImGui` object. The `UImGui` component needs:

| Field | Value |
|---|---|
| Camera | The main camera |
| Render feature | A `RenderImGui` feature added to `PC_Renderer` by `EnsureRendererFeature` |
| Shaders, Style, Cursor shapes | The package's `DefaultShader`, `DefaultStyle` and `DefaultCursorShape` assets |
| Ini settings | `Assets/Settings/ImGuiIni.asset` |

**The ini asset stops ImGui writing `imgui.ini` into the working directory.** With it assigned, the package keeps window layout in memory. Its type is internal to the package, so `LoadOrCreateImGuiIniAsset` creates it by name. The Unity editor itself may still write an `imgui.ini` in the project root when the project loads; it is listed in `.gitignore`.

`RenderImGui` draws after post-processing, so ImGui is not tonemapped, tinted by the night grade, or outlined by the [edge highlight](visibility-and-depth.md#edge-highlight).

The component reads input through the Input System platform, using `Mouse.current`.
