# Interaction Modes

How the player highlights and uses props and places and edits buildings. `FingerInteraction` uses what is under the crosshair and `SuperTool`, the default mode, hands the crosshair to the modes it controls. `BuildingPlacerInteraction` is entered from the hotbar; `ObjectPlacerInteraction`, `PropMoveInteraction`, `PropDeleteInteraction` and `BuildingEditInteraction` are entered from a building's terminal. Together they are the [building modes](#building-modes). For what a prop does when used, see [props-and-inventory.md](props-and-inventory.md).

---

## The Raycast

[PlayerRaycaster.cs](../Assets/Scripts/Piper/Interaction/PlayerRaycaster.cs) is a component on the `Player`. Once per tick it casts a 100 m ray from the camera pivot along its forward vector, ignoring trigger colliders and the player's own colliders, and keeps every hit sorted nearest first in `raycastHits` and `raycastHitsCount`. Terrain colliders are hits like any other, so terrain in front of a prop hides it.

Modes and tooltips read `PiperMain.p.game.raycaster` and look only at the first hit, so **the first collider under the crosshair decides what is interactable.**

---

## Mode Selection

[InteractionModeSelector.cs](../Assets/Scripts/Piper/Interaction/InteractionModeSelector.cs) holds `allModes` (`SuperTool`, `FingerInteraction`, `BuildingPlacerInteraction`, `ObjectPlacerInteraction`, `BuildingEditInteraction`, `PropMoveInteraction`, `PropDeleteInteraction`) and activates index 0 the first tick it runs. **B** enters the building placer from any mode except the edit mode, but only outdoors; the same key, or right click in the placer, returns to the `SuperTool`. Right click in the furniture modes also returns to it. The terminal's buttons call `EnterBuildingEdit`, `OpenFurniturePicker`, `EnterPlaceFurniture`, `EnterMoveFurniture` and `EnterDeleteFurniture`. Each tick it resets the crosshair style and calls the active mode's `ModeUpdate`.

`PiperGame` calls `doUpdate` only while no menu is open and the main menu is closed; otherwise it calls `Suspend`, which deactivates the mode and clears its prompts, highlight and crosshair progress. The next `doUpdate` reactivates it. **`Suspend` leaves the edit mode alone,** because deactivating it moves the player back to the terminal; a menu opened while editing only pauses the mode's updates.

---

## InteractionMode

[InteractionMode.cs](../Assets/Scripts/Piper/Interaction/InteractionMode.cs) is the base class.

| Member | Role |
|---|---|
| `interactDistance` | How far the mode reaches, 5 m by default |
| `ActivateMode`, `DeactivateMode` | Set up and clear the HUD mode label, prompts and tooltip distance |
| `ModeUpdate` | Per-tick work; returns true to ask the selector to switch to the mode that returned it |
| `UpdateHighlight` | Re-evaluates the highlighted object |
| `ObjectOfInterestUnderCursor` | Whether this mode has something to act on right now |
| `TryOverrideTooltips`, `RequestedTooltipTags` | Lets a mode supply its own tooltips or choose which kind the tooltip manager builds |

`ChangeHighlight` turns highlighting off on the old target and on on the new one through `IGetHighlighted`, which `PropBehaviour` implements.

---

## FingerInteraction

[FingerInteraction.cs](../Assets/Scripts/Piper/Interaction/FingerInteraction.cs) highlights the first hit when it is within `interactDistance` and its collider carries an `IInteractable` that is highlightable. It then drives three kinds of use:

| Input | Condition | Behaviour |
|---|---|---|
| Left click | `interactionTime()` is 0 | Calls `Interact` on the press |
| Left hold | `interactionTime()` above 0 | Fills the crosshair ring over that many seconds, then calls `Interact`; releasing or losing the target cancels |
| Right hold | `secondaryInteractionTime()` above 0 | Same, calling `InteractSecondary` |

The left prompt shows `GetPrimaryVerb`, greyed with its error text when the verb is disabled. Input comes from `InputFixed`, so a press is seen once per tick ([piper-systems.md](piper-systems.md#the-fixed-tick)).

### IInteractable

| Member | Role |
|---|---|
| `GetPrimaryVerb` | Prompt text, whether it is enabled, and an error message |
| `Interact` | Dispatches the [input action](adding-an-input-action.md); it must not change the world directly |
| `IsInteractable` | Whether `Interact` currently does anything |
| `IsHighlightable` | Defaults to true for `FingerInteraction` only |
| `interactionTime`, `secondaryInteractionTime` | Hold durations in seconds; 0 means none |
| `GetCrosshairStyle` | Crosshair sprite while highlighted |

[IInteractablePassthrough.cs](../Assets/Scripts/Piper/Interaction/IInteractablePassthrough.cs) forwards every call to a target object, for colliders on a prop's children.

---

## SuperTool

[SuperTool.cs](../Assets/Scripts/Piper/Interaction/SuperTool.cs) lists `controlledModes` (`FingerInteraction`). Each tick it asks each controlled mode whether it has an object of interest under the cursor, remembers the last one that did in `lastHighlightMode`, and runs that mode's `ModeUpdate`. If a mode's `ModeUpdate` returns true the selector switches to it. With nothing of interest it clears the prompts and the previous mode's highlight.

It forwards tooltip overrides and `RequestedTooltipTags` to `lastHighlightMode`, so an object never shows two modes' tooltips; with no mode claiming the cursor it requests `Normal`, so basic prop info still shows.

---

## Building Modes

[buildings.md](buildings.md) describes the data these modes change. All three aim with the crosshair ray, send their changes as [input actions](adding-an-input-action.md), and show what the action would do before sending it by running it on a copy of the building ([buildings.md](buildings.md#the-model)). `BuildingGizmos` draws previews as pooled line renderers and spheres with `PipeWorld/Gizmo`, an unlit shader that ignores depth so edit overlays show through walls and terrain.

### Placing Buildings

[BuildingPlacerInteraction.cs](../Assets/Scripts/Piper/Interaction/BuildingPlacerInteraction.cs) places a building in two clicks while a 4x4x4 m wireframe shows the result. A new building always has that size; the hologram also draws the door, an amber frame on the side that will have it with an arrow pointing out, so the player can turn the hologram until the door faces the way they want. **The mouse wheel turns the hologram smoothly throughout (5 degrees per notch, not aligned to anything); holding Shift makes each scroll a whole 15 degree step, measured from world X.** Shift also snaps the floor height of click 2 to whole units of world Y.

| Click | Sets | Aiming |
|---|---|---|
| 1 | The origin on the ground | The first hit within 30 m, valid on level ground (surface normal Y at least 0.7), not on a building, and 8 m from other buildings |
| 2 | The floor height above the ground, up to `BuildingConstants.MaxRaise` | The crosshair's closest approach to the vertical line through the origin, in 0.25 m steps; the raised volume is outlined as the scaffold |

The second click sends `PlaceBuilding` with the position, yaw, ceiling height and ground level, and returns to the `SuperTool`. Right click goes back a step, and cancels from the first.

### Placing Furniture

[ObjectPlacerInteraction.cs](../Assets/Scripts/Piper/Interaction/ObjectPlacerInteraction.cs) places one prop on the faces of buildings ([buildings.md](buildings.md#props)). **The prop is chosen before the mode starts**: the terminal's Place Furniture button opens the [furniture list](ui-and-menus.md#furniture-picker), and choosing an item calls `Begin` with its `BuildingPropDef` and enters the mode. The list depends on where the player stands: outdoors only props that go on an outer wall (exterior door, window, ladder, light), indoors everything except the outdoor-only ones (`BuildingProps.placeableFor`). **The mouse wheel turns the hologram of a floor or ceiling prop in 15 degree steps and right click leaves the mode;** wall props do not turn. The placer looks through props to the first building surface within 8 m and finds the face from its normal: a floor, a ceiling (the hit must be on the underside) or the nearest wall and the side the hit is on. It converts the hit to the face's 0-1 coordinates, snapped to 0.1 m, and runs `placeProp` on a copy of the building with the chosen rotation: the hologram is blue when the placement is valid and red otherwise, a line from its middle shows which way a floor or ceiling prop faces, and the left prompt shows the reason. Left click sends `PlaceBuildingProp` with the rotation.

### Moving Furniture

[PropMoveInteraction.cs](../Assets/Scripts/Piper/Interaction/PropMoveInteraction.cs) moves a prop in two steps. The first click selects the prop under the crosshair and outlines it; nothing moves until a second click chooses where it goes, and **right click with a prop selected goes back to choosing a prop** (right click with none leaves the mode). A building prop keeps to its own building and is checked with the same rules as placing it (`BuildingModel.moveProp`), then sent as `MoveBuildingProp`; an outdoor prop goes to the ground or floor under the crosshair as `MoveProp`. **While a building prop is being placed the mouse wheel turns its hologram in 15 degree steps, starting from its current rotation, and the move sends the rotation;** wall props and outdoor props do not turn. The hologram is blue when the spot is valid and red when it is not, and the left prompt gives the reason.

### Deleting Furniture

[PropDeleteInteraction.cs](../Assets/Scripts/Piper/Interaction/PropDeleteInteraction.cs) deletes the prop under the crosshair with a click: a building prop through `DeleteBuildingObject`, an outdoor prop through `DeleteProp`. The prop is outlined red when it can go and grey when it cannot; a building prop is tried on a copy of its building first, so the left prompt gives the reason (the terminal and the last exterior door cannot be deleted). Right click leaves the mode.

### Editing Buildings

The terminal's Edit Building button ([buildings.md](buildings.md#terminal)) enters the editor through `InteractionModeSelector.EnterBuildingEdit`, from either side of the terminal. [BuildingEditInteraction.cs](../Assets/Scripts/Piper/Interaction/BuildingEditInteraction.cs) records where the player stood and switches the player to [free flight](player-controller.md#flight). A stand-in `OtherPlayer` avatar is left where the player stood, and follows the terminal if an edit moves it (a wall pulled, the floor raised), as does the place the player returns to; and over 1.4 seconds the camera backs away from it (7 m behind and 3.5 m above, looking at its feet) while the player has no control; the building's ceiling is hidden. When the move ends the player has the mouse back, flying from where the camera stopped. **Right click (with no drag in progress) or Tab leaves the mode:** the eyes close (the Piperspace eye-close effect, drawn by [EyeCloseElement.cs](../Assets/Scripts/Piper/UI/EyeCloseElement.cs) over the whole HUD), the avatar is removed and the player is put back at the recorded position and view, and the eyes open again. The number keys or the mouse wheel choose the tool:

| Key | Tool | Behaviour |
|---|---|---|
| 1 | Manipulate | Drag an outer or inner wall marker along its normal (the collinear chain moves together), or drag a corner of a triangular room freely |
| 2 | Extrude | Drag an outer wall marker outwards to add a room |
| 3 | Create Triangle | Drag from an outer wall marker to a point to add a triangle |
| 4 | Slice | Aim into a room to preview the cut, which may run through several four-sided rooms (a marker on every wall it splits); left click commits |
| 5 | Delete Room | Aim into a room; it outlines red when it can be deleted; left click deletes |
| 6 | Floor and Ceiling Height | Drag the floor marker to raise or lower the whole building above its ground, or the ceiling marker to change the ceiling height; sends `SetBuildingHeights` |
| 7 | Wall Style | Aim at the marker on a wall between two rooms (markers show open walls too, since they have no geometry); click cycles solid, glass, open; sends `SetBuildingWallStyle` |

Targets are on the building's floor plane. A marker is hit when the ray passes near it, with a radius that grows with distance. Drags along a wall normal use the point of the normal line closest to the ray, so the pull does not depend on the aiming angle; free drags intersect the ray with the floor plane. Height drags use the closest approach to a vertical line, in 0.25 m steps. Right click cancels a drag; releasing applies it. Preview lines are green when the edit is valid and red when it is not, and the left prompt shows the reason an edit was refused.

---

## Mode Toolbar

[ModeToolbarElement.cs](../Assets/Scripts/Piper/UI/ModeToolbarElement.cs) is a row of icons above the mode label ([GameHud.cs](../Assets/Scripts/Piper/UI/GameHud.cs)): the hotbar. **Outdoors it has two tools, Interact and New building; inside a room it has only Interact.** The selector rechecks every 10 frames whether the player stands in a room of a building (`BuildingTravel.IsLocalPlayerInsideAny`; the roof is outdoors), refreshes the hotbar when that changes, and leaves the building placer if the player walks in. While editing a building the toolbar shows the seven edit tools instead. The furniture modes, entered from a terminal, light no slot. `InteractionModeSelector.RefreshToolbar` fills it whenever the mode or the tool changes. Each slot shows the number key that selects it in its top right corner: 1 and 2 choose the hotbar slots (in any mode but the editor), 1 to 7 the editor's tools. In the default mode the wheel steps to the next or previous slot; the other modes use the wheel themselves. In the edit mode the wheel steps through the tools. A wheel that sends many small deltas per notch still gives one step per notch ([ScrollStepper.cs](../Assets/Scripts/Piper/Core/ScrollStepper.cs)). Shift is the only modifier key the game uses. The icons are PNGs in `Assets/Resources/Icons/` from game-icons.net ([CREDITS.txt](../Assets/Resources/Icons/CREDITS.txt)).

---

## Tooltips

[TooltipManagerElement.cs](../Assets/Scripts/Piper/UI/TooltipManagerElement.cs) finds a `TooltipProvider` on the first hit within the mode's tooltip distance. It builds the tooltip after the crosshair has rested on the same target for 0.4 s with the player not walking, and rebuilds it when `MarkTooltipDirty` has been called and 20 ticks have passed. `PropBehaviour` is a provider; `PropTooltip` shows the prototype's name, type and description.
