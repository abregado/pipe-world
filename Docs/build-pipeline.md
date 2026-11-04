# Build Pipeline

How to build and run pipe-world for Windows from the command line, without opening the Unity editor.

---

## Commands

Run from the repo root in PowerShell. All Unity invocations are headless (`-batchmode -nographics`).

| Command | What it does |
|---|---|
| `.\build.ps1` | Builds the Windows player to `Builds/Windows/PipeWorld.exe`. |
| `.\build.ps1 -Run` | Builds, then launches the game. |
| `.\build.ps1 -Setup` | Runs [WorldSetup](#world-generation-script) first, then builds. |
| `.\build.ps1 -Check` | Compiles scripts only; no player is built. |
| `.\shot.ps1` | Captures a screenshot of the built player. See [screenshots.md](screenshots.md). |

Unity's output goes to `Builds/build.log`. The script prints only compile errors (`error CS...`), shader errors (`Shader error ...`), `[WorldSetup]` lines, and the build result. A non-zero exit code means the Unity step failed.

**Unity cannot open a project that another editor instance has open.** [build.ps1:24](../build.ps1) refuses to run while a non-batch `Unity.exe` is running and names its PID.

---

## How It Works

[build.ps1:31](../build.ps1) (`Invoke-Unity`) reads the editor version from `ProjectSettings/ProjectVersion.txt`, locates it under `C:\Program Files\Unity\Hub\Editor\<version>`, and starts `Unity.exe` with `-executeMethod`. It waits with a timeout (`-TimeoutMinutes`, default 30) and kills Unity if the timeout passes.

`Unity.exe` is a GUI-subsystem executable, so PowerShell returns immediately unless the process is started with `Start-Process -PassThru` and waited on explicitly.

| Entry point | Role |
|---|---|
| [BuildScript.cs](../Assets/Editor/BuildScript.cs) `BuildWindows` | Builds every enabled scene in Build Settings as a Development, Windows x64, Mono player. Exits with code 1 if the build fails. |
| [WorldSetup.cs:24](../Assets/Editor/WorldSetup.cs) `Run` | Regenerates the URP asset settings, night and warm LUTs, volume profile, skybox material, renderer features (edge highlight, light grade, Dear ImGui), terrain textures and materials, scatter, grass and flame materials, and the `World` scene. |

Development builds write the console log to `%USERPROFILE%\AppData\LocalLow\DefaultCompany\pipe-world\Player.log`.

---

## World Generation Script

`WorldSetup.Run` builds [World.unity](../Assets/Scenes/World.unity) from scratch every time it runs. **Hand edits made to `World.unity` in the editor are overwritten the next time `-Setup` runs.** Anything that must persist belongs in `WorldSetup.cs` or in a runtime script.

`World.unity` is the only scene in Build Settings; `WorldSetup` sets that list.

**Rerun `-Setup` after changing a terrain texture generator, a material setting in `WorldSetup`, or `Walkable.SlopeLimitDegrees`.** Script and shader changes do not need it; a plain build picks them up. The world's geometry is generated at runtime by `DemoWorld`, not stored in the scene.

Scene contents are described in [lighting-and-day-night.md](lighting-and-day-night.md), [player-controller.md](player-controller.md), [landscape.md](landscape.md) and [terrain-shader.md](terrain-shader.md).
