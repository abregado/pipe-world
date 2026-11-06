# Piperspace → Pipe World port plan

Source: `D:\unity-projects\piperspace` (Unity 6000.5, global namespace, legacy Input, Steam + GNS).
Target: this repo (Unity 6000.4, `PipeWorld.*` namespaces, new Input System only).

Everything ported lives in `namespace PipeWorld.Piper` (not `PipeWorld`: a Piper `InputAction` would shadow `UnityEngine.InputSystem.InputAction` inside `PipeWorld.Player`). Class names are kept so the Piperspace docs stay searchable. Unity 6000.4 compiles C# 9, so namespaces are block-scoped, and `PanelRenderer` (Unity 6.5) is replaced by `UIDocument`.

## In scope
UI (HUD, tooltips, skins), main menu (UI only, no 3D background), networking (GNS only), network InputActions, interaction modes (**SuperTool + FingerInteraction only**), Props, InventoryItems.

## Out of scope
ResearchId / research gating, Steam, pipes/nodes/connection points, all other interaction modes, quest log, screens, post-game, cutscenes, music, replays, snap points, level chunks.

## Phases (each ends with a compile check; later phases add a build + screenshot / log check)

| # | Phase | Deliverable | Validation |
|---|---|---|---|
| 0 | Project prep | `allowUnsafeCode`, fixed timestep, GNS plugin + DLLs, UI resources (fonts, crosshairs, USS) copied | `build.ps1 -Check` |
| 1 | Data layer | `Vec3f`, `SUtil`, `SlabAllocator`, trimmed `Prop`/`WorldData`/`Inventory`, `PropPrototypes`, `InventoryItemPrototypes`, `World` save/load | compile; round-trip save/load self-test |
| 2 | InputActions + singleplayer | `InputAction` union, `InputActionManager`, `SingleplayerNetworkManager`, `PiperMain`/`PiperGame`, `SystemSync`/`PropSyncer`, `PropBehaviour`, pooling, `InputFixed` on Input System | compile; in-game self-test places a prop and applies actions |
| 3 | Prop prefabs | editor-generated placeholder prefabs in `Resources/PropPrefabs` (collectable, door, crate, lamp) | props appear in screenshot |
| 4 | Interaction | `InteractionMode`, `SuperTool`, `FingerInteraction`, `InteractionModeSelector`, `IInteractable`, raycast + menu refcount on `PlayerController` | scripted click collects an item (log) |
| 5 | UI | `UiPanel`/skins, `HudRegions`, `GameHud` (crosshair, prompts, inventory, tooltips), `MenuViewPanel`, main menu, escape menu, settings/join/load panels, USS | screenshots of HUD + menus |
| 6 | Networking | GNS backend, `Client`, `Server`, chunked world dumps, remote players, host/join from menus + CLI | two player instances on localhost: host places/collects, client sees it |
| 7 | Integration + docs | `WorldSetup` builds everything, debug-menu hooks, docs in `Docs/`, final build | full build + screenshots |

## Progress log

All phases are complete. Documentation is in `Docs/` starting from `piper-systems.md`.

| Phase | Result | Validated by |
|---|---|---|
| 0 | `allowUnsafeCode`, 1/60 fixed step, GNS plugin, USS, fonts, glyphs copied | `build.ps1 -Check` |
| 1 | `World`, `Prop`, `Inventory`, prototypes, save/load | `-piperSelfTest` save/reset/load round trip |
| 2 | 8 InputActions, `InputActionManager`, singleplayer, syncers, `InputFixed` | `-piperSelfTest`; `[Piper] apply` log lines |
| 3 | Placeholder prefabs for 4 props and the remote player, built by `PiperSetup` | Screenshots |
| 4 | `SuperTool`, `FingerInteraction`, selector, raycaster | Scripted click toggles the door; prompt, highlight and tooltip captured |
| 5 | HUD, main menu, escape menu, settings, join and load panels, skins | Screenshots of each; clicks and Escape through virtual devices |
| 6 | GNS backend, `Client`, `Server`, world diffs, remote avatars | Two player instances on localhost: join, world sync, a client click opening the door on both screens |
| 7 | Debug menu section, docs, screenshot harness flags | Final build |

Left out on purpose: ResearchId systems, Steam networking, every other interaction mode, pipes, quest log, screens and cutscenes. Items without an SVG icon use the coloured-square fallback.
