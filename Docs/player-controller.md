# Player Controller

The first-person player: how it moves, looks, and is wired into the `World` scene.

---

## Scene Layout

[WorldSetup.cs:136](../Assets/Editor/WorldSetup.cs) (`BuildScene`) creates the player as plain scene objects, not a prefab:

```
Player          CharacterController (height 1.8, radius 0.4, step offset 0.4) + PlayerController + PlayerLantern
└─ Camera Pivot   local y 1.6; receives pitch
   ├─ Main Camera   Camera, AudioListener, post-processing enabled
   └─ Lantern Hinge   created at runtime by PlayerLantern
```

`Player` receives yaw; `Camera Pivot` receives pitch. The player spawns at the centre of the crater at `(0, 0.05, 0)` facing +Z, with `slopeLimit` set to [Walkable.SlopeLimitDegrees](../Assets/Scripts/Landscape/Biome.cs) (see [terrain-shader.md](terrain-shader.md#walkable-cue)).

---

## Input

[PlayerController.cs:33](../Assets/Scripts/Player/PlayerController.cs) (`Awake`) defines the input actions in code, using the Input System. No `.inputactions` asset is involved.

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Look | Mouse delta | Right stick |
| Jump | Space | South button |
| Sprint | Left Shift | Left stick press |
| Crouch (hold) | Left Ctrl or C | East button |

The controller does not handle Escape or clicks: menus own the cursor through `SetMenuState`, a reference-counted counter. While the count is above zero the cursor is unlocked, and look, movement, sprint, jump and crouch input are ignored while gravity still applies. The cursor locks on `OnEnable` only when no menu is open, and unlocks on `OnDisable`. The main menu, the escape menu and the [debug menu](debug-menu.md#cursor-and-input) each add one to the count while open; see [ui-and-menus.md](ui-and-menus.md#menus).

---

## Movement

[PlayerController.cs:128](../Assets/Scripts/Player/PlayerController.cs) (`UpdateMovement`) works in two parts.

**Planar velocity** moves toward the input direction times the current speed at a fixed acceleration, higher on the ground than in the air. Diagonal input is clamped to length 1.

**Vertical velocity** integrates gravity. While grounded it is held at -2 rather than 0, because `CharacterController.isGrounded` is only true after a `Move` that pressed down into the ground.

A jump is allowed while the time since the last grounded frame is within `coyoteTime` and the player is not crouching, and sets the vertical velocity to `sqrt(jumpHeight * -2 * gravity)`.

Speed is `crouchSpeed` while crouching, otherwise `sprintSpeed` when sprint is held, otherwise `walkSpeed`. **Crouching overrides sprint.**

| Field | Default |
|---|---|
| `walkSpeed` | 5 |
| `sprintSpeed` | 9 |
| `crouchSpeed` | 2.5 |
| `groundAcceleration` | 40 |
| `airAcceleration` | 10 |
| `jumpHeight` | 1.4 |
| `gravity` | -25 |
| `coyoteTime` | 0.1 |

---

## Crouch

[PlayerController.cs:150](../Assets/Scripts/Player/PlayerController.cs) (`UpdateCrouch`) crouches while the crouch input is held. `IsCrouching` is true from the first held frame until the input is released **and** there is room to stand.

[HasStandingHeadroom](../Assets/Scripts/Player/PlayerController.cs) (line 170) overlaps a capsule over the space between the crouched height and the standing height, ignoring the player's own collider and triggers. If anything else overlaps, the player stays crouched under it. The test covers only the space above the crouched capsule, so ground under the feet or on a slope never counts as a ceiling.

[ApplyHeight](../Assets/Scripts/Player/PlayerController.cs) (line 161) moves the capsule height toward the target at `heightChangeSpeed` metres per second, keeps the capsule centred at half its height so the feet stay on the ground, and keeps the camera pivot `eyeOffsetFromTop` below the top of the capsule.

| Field | Default |
|---|---|
| `standingHeight` | 1.8 |
| `crouchHeight` | 1.0 |
| `heightChangeSpeed` | 6 |
| `eyeOffsetFromTop` | 0.2 |

`Teleport` resets the player to standing.

`IsGrounded` and `IsCrouching` feed the player pose that [netcode.md](netcode.md) sends, and `IsWalking` (planar speed above 0.5 m/s) gates tooltip dwell. The `Player` also carries a `PlayerRaycaster` for [interaction-modes.md](interaction-modes.md#the-raycast).

---

## Look

[PlayerController.cs:111](../Assets/Scripts/Player/PlayerController.cs) (`UpdateLook`) adds mouse delta times `mouseDegreesPerPixel` (0.1) to yaw and pitch, and the stick value times `stickDegreesPerSecond` (180) times `Time.deltaTime`. Pitch is clamped to `maxPitch` (89). **Mouse delta is a per-frame value, so it is not multiplied by `Time.deltaTime`; the stick is a rate, so it is.**

---

## Teleport

[PlayerController.cs:85](../Assets/Scripts/Player/PlayerController.cs) (`Teleport`) moves the player, zeroes velocity and sets yaw and pitch. The `CharacterController` is disabled for the move because it overrides direct `transform.position` writes while enabled. [screenshots.md](screenshots.md) uses it to place the camera.

---

## Verification

Walking, sprinting, strafing, jumping, crouch speed, crouch overriding sprint, and crouch blocking jumps are checked in the built player by injecting key presses with [shot.ps1](../shot.ps1) (see [screenshots.md](screenshots.md)). Mouse and gamepad look are not covered by that harness.

---

## Lantern

[PlayerLantern.cs](../Assets/Scripts/Player/PlayerLantern.cs) builds a small lantern at startup and hangs it at the lower right of the view, as if held in the right hand. It is generated from the world's shape templates, like the torches: an iron frame from flat blocks and a glowing crystal inside, in `DemoWorld.PropMaterial` and the flame material.

**The lantern swings as a damped pendulum.** Its hinge sits at `handOffset` (0.36 right, 0.16 down, 0.72 forward) in the camera pivot's space, so it follows the view, but each `LateUpdate` its rotation is reset to the player's yaw plus the swing, so it always hangs under gravity, whatever the pitch. Two swing angles, forward-back and side to side, are driven by the hinge's own acceleration (starting, stopping and turning all swing it) and by a stride term at `strideRate` scaled by walking speed. They settle with `damping`. `length` (0.3 m) sets the rate, about one swing a second. A jump of more than 2 m in one frame, such as a teleport, resets the history instead of kicking the swing.

The point light sits inside the lantern, so its shadows move with the swing. It is warm, intensity 2.2, range 9 m, soft shadows, and dims to 15% by day like the torches. No part of the lantern casts shadows. It also carries a weak `GradeLight` (strength 0.7, radius 10.8 m), so the night grade warms a little around the player.
