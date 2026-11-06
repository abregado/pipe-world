# Interaction Modes

How the player highlights and uses props. Two modes exist: `FingerInteraction`, which uses what is under the crosshair, and `SuperTool`, the default mode, which hands the crosshair to the modes it controls. For what a prop does when used, see [props-and-inventory.md](props-and-inventory.md).

---

## The Raycast

[PlayerRaycaster.cs](../Assets/Scripts/Piper/Interaction/PlayerRaycaster.cs) is a component on the `Player`. Once per tick it casts a 100 m ray from the camera pivot along its forward vector, ignoring trigger colliders and the player's own colliders, and keeps every hit sorted nearest first in `raycastHits` and `raycastHitsCount`. Terrain colliders are hits like any other, so terrain in front of a prop hides it.

Modes and tooltips read `PiperMain.p.game.raycaster` and look only at the first hit, so **the first collider under the crosshair decides what is interactable.**

---

## Mode Selection

[InteractionModeSelector.cs](../Assets/Scripts/Piper/Interaction/InteractionModeSelector.cs) holds `allModes` (`SuperTool`, then `FingerInteraction`) and activates index 0 the first tick it runs. Each tick it resets the crosshair style and calls the active mode's `ModeUpdate`.

`PiperGame` calls `doUpdate` only while no menu is open and the main menu is closed; otherwise it calls `Suspend`, which deactivates the mode and clears its prompts, highlight and crosshair progress. The next `doUpdate` reactivates it.

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

## Tooltips

[TooltipManagerElement.cs](../Assets/Scripts/Piper/UI/TooltipManagerElement.cs) finds a `TooltipProvider` on the first hit within the mode's tooltip distance. It builds the tooltip after the crosshair has rested on the same target for 0.4 s with the player not walking, and rebuilds it when `MarkTooltipDirty` has been called and 20 ticks have passed. `PropBehaviour` is a provider; `PropTooltip` shows the prototype's name, type and description.
