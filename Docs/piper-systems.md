# Piper Systems

The game layer that sits on top of the landscape: a fixed-tick world of props and inventory, local and networked play, interaction with props, and the HUD and menus. This doc maps the pieces and how a frame flows through them. It is the entry point for [netcode.md](netcode.md), [props-and-inventory.md](props-and-inventory.md), [interaction-modes.md](interaction-modes.md) and [ui-and-menus.md](ui-and-menus.md).

---

## Namespace and Layout

All game-layer code lives in `PipeWorld.Piper` under `Assets/Scripts/Piper/`. **It is kept out of `PipeWorld` itself because `InputAction` and `World` would otherwise shadow `UnityEngine.InputSystem.InputAction` in [PlayerController.cs](../Assets/Scripts/Player/PlayerController.cs) and the `PipeWorld.World` namespace.**

| Folder | Contents |
|---|---|
| `Systems/` | Unmanaged world data (props in [props-and-inventory.md](props-and-inventory.md), buildings in [buildings.md](buildings.md)): [SystemTypes.cs](../Assets/Scripts/Piper/Systems/SystemTypes.cs), [World.cs](../Assets/Scripts/Piper/Systems/World.cs), [SlabAllocator.cs](../Assets/Scripts/Piper/Systems/SlabAllocator.cs), prototypes and [PropLogic.cs](../Assets/Scripts/Piper/Systems/PropLogic.cs) |
| `Net/` | InputActions, network managers, the GNS backend and the world-diff codec |
| `Core/` | [PiperMain.cs](../Assets/Scripts/Piper/Core/PiperMain.cs), [PiperGame.cs](../Assets/Scripts/Piper/Core/PiperGame.cs), prop and remote-player syncers, input latching, settings |
| `Props/` | One `PropBehaviour` view per prop prototype |
| `Interaction/` | Interaction modes and the view raycaster |
| `UI/` | HUD, menus, skins and USS-driven elements |

Scene objects are created by [PiperSetup.cs](../Assets/Editor/PiperSetup.cs), which [WorldSetup.cs](../Assets/Editor/WorldSetup.cs) calls on every `-Setup` ([build-pipeline.md](build-pipeline.md)):

| Object | Components |
|---|---|
| `Piper` | `PiperMain`, `PiperGame` |
| `UI` | `ScreenUIHolder`, `GameHud`, `MainMenu`, `EscapeMenu`, `EventSystem`, `InputSystemUIInputModule` |
| `Interaction Modes` | `InteractionModeSelector`, `SuperTool`, `FingerInteraction` |
| `Player` | the [player controller](player-controller.md) plus `PlayerRaycaster` |

---

## Startup

`PiperMain` runs first (execution order -1000). It sets `Application.runInBackground` so a host keeps ticking while another window has focus, loads [Settings.cs](../Assets/Scripts/Piper/Core/Settings.cs), builds the inventory and prop prototypes (`SystemInit.init`), starts the GNS backend, and asserts the struct sizes the wire format depends on ([netcode.md](netcode.md#struct-sizes)).

**`Time.fixedDeltaTime` must be 1/60.** [PiperMain.cs](../Assets/Scripts/Piper/Core/PiperMain.cs) asserts it against `Constants.TICKS_PER_SECOND`, and `ProjectSettings/TimeManager.asset` sets it.

`PiperGame` (execution order -900) creates the `World`, the `InputActionManager`, a `SingleplayerNetworkManager` and the `SystemSync`. Its `Start` finds the other scene objects, builds the HUD and both menus, and opens the main menu unless a command-line flag skips it.

---

## The Fixed Tick

`PiperGame.FixedUpdate` runs once per tick, in this order:

1. `NetworkingBackend.gns.runCallbacks()` and any queued operation (new game, load, save, host, join, return to menu).
2. `networkManager.update`: applies the tick's `InputAction`s to the `World` and advances it ([netcode.md](netcode.md)).
3. World seeding, once the local player has an allocator slot.
4. `syncer.sync`: creates and recycles prop views near the player ([props-and-inventory.md](props-and-inventory.md#views)).
5. `PlayerRaycaster.updateRaycast`, then the interaction mode update ([interaction-modes.md](interaction-modes.md)), or `Suspend` while any menu is open.
6. `GameHud.doUpdate`, then the active menu's `doUpdate`.
7. `InputFixed.update`.

`PiperGame.Update` only calls `InputFixed.accumulateUpdate`. [InputFixed.cs](../Assets/Scripts/Piper/Core/InputFixed.cs) samples keys and mouse buttons every rendered frame and latches presses, so a click between two ticks is still seen exactly once. It reads the new Input System; `KeyCode` names map to `Key` by name, with `Alpha0`-`Alpha9` mapped to `Digit0`-`Digit9`.

---

## Game Flow

There is one scene. Menu choices do not reload it; `PiperGame` replaces the world in place.

| Operation | Effect |
|---|---|
| New game | Destroys every prop view, creates a fresh `World` and syncer, restarts singleplayer, seeds the props and teleports the player to the crater centre |
| Load | As new game without seeding, then `World.load` of the chosen file and the player moves to their saved pose |
| Save | Writes `World.save()` to `Documents/PipeWorld/Saves/save####.bin` (`PipeWorldInEditor` in the editor) |
| Host | Replaces the network manager with a `Server`; the current world carries on |
| Join | Resets to an empty world without moving the player, then connects a `Client`; the first world dump fills it |
| Return to menu | Resets to an empty singleplayer world and opens the main menu |

`PiperGame.SeedWorld` queues `PlacePropData` and `PlaceBuildingData` actions rather than writing the world directly, so a host's seed reaches clients through the normal path.

---

## Command-Line Flags

| Flag | Effect |
|---|---|
| `-piperPlay` | Skips the main menu |
| `-piperMenu` | With `-shot`, keeps the main menu, which a screenshot run otherwise skips |
| `-piperHost` | Skips the menu and hosts |
| `-piperJoin <address>` | Skips the menu and joins `address` |
| `-piperName <name>` | Overrides the player name for this run |
| `-piperPlayerId <guid>` | Overrides the player id for this run, so several instances on one machine are distinct players. Never saved |
| `-piperEscape` | Skips the menu and opens the escape menu |
| `-piperEdit` | Skips the menu and enters the [building editor](interaction-modes.md#editing-buildings) on the first building |
| `-piperEditTool <0-4>` | With `-piperEdit`, the tool to start with |
| `-piperDecorate` | Once the world is available, raises the first building's floor and ceiling and places a table, chair, ceiling lamp and painting in it |
| `-piperExtrude` | Once the world is available, sends an `ExtrudeBuildingEdge` action on the first building's entrance wall |
| `-piperSelfTest` | Runs [PiperSelfTest.cs](../Assets/Scripts/Piper/Core/PiperSelfTest.cs): every `InputAction` and a save, reset and load round trip, logged as `[Piper] selftest ...`, then quits |

`shot.ps1 -Extra` forwards flags to the player ([screenshots.md](screenshots.md)).
