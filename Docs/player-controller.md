# Player Controller

The first-person player: how it moves, looks, and is wired into the `World` scene.

---

## Scene Layout

[WorldSetup.cs:136](../Assets/Editor/WorldSetup.cs) (`BuildScene`) creates the player as plain scene objects, not a prefab:

```
Player          CharacterController (height 1.8, radius 0.4, step offset 0.4) + PlayerController
└─ Camera Pivot   local y 1.6; receives pitch
   └─ Main Camera   Camera, AudioListener, post-processing enabled
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

Escape releases the cursor; a left click locks it again ([PlayerController.cs:97](../Assets/Scripts/Player/PlayerController.cs)). Look input is ignored while the cursor is unlocked. The cursor locks on `OnEnable` and unlocks on `OnDisable`.

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

---

## Look

[PlayerController.cs:111](../Assets/Scripts/Player/PlayerController.cs) (`UpdateLook`) adds mouse delta times `mouseDegreesPerPixel` (0.1) to yaw and pitch, and the stick value times `stickDegreesPerSecond` (180) times `Time.deltaTime`. Pitch is clamped to `maxPitch` (89). **Mouse delta is a per-frame value, so it is not multiplied by `Time.deltaTime`; the stick is a rate, so it is.**

---

## Teleport

[PlayerController.cs:85](../Assets/Scripts/Player/PlayerController.cs) (`Teleport`) moves the player, zeroes velocity and sets yaw and pitch. The `CharacterController` is disabled for the move because it overrides direct `transform.position` writes while enabled. [screenshots.md](screenshots.md) uses it to place the camera.

---

## Verification

Walking, sprinting, strafing, jumping, crouch speed, crouch overriding sprint, and crouch blocking jumps are checked in the built player by injecting key presses with [shot.ps1](../shot.ps1) (see [screenshots.md](screenshots.md)). Mouse and gamepad look are not covered by that harness.
