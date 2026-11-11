# Buildings

Player-built structures: each building is a footprint of rooms on one floor plane, a ceiling above it, a scaffold under it, and props attached to its faces. The footprint editing rules come from the `builder-proto` 2D tool (`D:\repos\builder-proto`); this doc describes how they run in Pipe World. How the modes are used is in [interaction-modes.md](interaction-modes.md#building-modes); how actions travel is in [netcode.md](netcode.md).

---

## Data

A building is four kinds of unmanaged slab entities in the `World` ([BuildingTypes.cs](../Assets/Scripts/Piper/Systems/BuildingTypes.cs)), so they are saved, snapshotted and diffed with everything else. Its props are ordinary world props ([Props](#props)).

| Entity | Fields |
|---|---|
| `Building` | position (the footprint centre, on the floor plane), yaw, ceiling `height`, `groundY` |
| `BuildingVertexData` | owner, `x`, `z` in building space |
| `BuildingEdgeData` | owner, two vertex ids, wall type (`WallStyle`: 0 solid, 1 outer, 2 open, 3 glass) |
| `BuildingAreaData` | owner, three or four edge ids, type |

Building space has X and Z on the floor plane and the origin at the building's position; the building's yaw is the only rotation. **A floor is at the building's Y and every room shares it.** The ceiling is `height` above the floor, between `BuildingConstants.MinHeight` and `MaxHeight`. `groundY` is the terrain level the building was placed on; the floor may be raised up to `MaxRaise` above it, and a [scaffold](#scaffold) fills the gap. Walls are `WallThickness` thick and extend `FoundationDepth` below the floor so they meet uneven ground.

**Entities are identified by `EntityId`, the slab header's player index and per-player counter packed into a `uint`, never by slot.** A slot is the first free one when the entity is created, which can differ between the server and a predicting client. The counter is `AllocatorContextData.nextEntityId`, shared by every entity kind except props, which use `nextPropId`. Ids made while predicting an action equal the ids the server makes for it, because the server allocates from the context stamped on that action ([netcode.md](netcode.md#allocator-context)).

`EntitySlab<T>` finds, creates and removes entities by id with a scan of the slab.

---

## The Model

[BuildingModel.cs](../Assets/Scripts/Piper/Systems/Building/BuildingModel.cs) is the building as ordinary managed lists (`BVertex`, `BEdge`, `BArea`, `BObject`). It carries the builder-proto queries and validation; [BuildingOps.cs](../Assets/Scripts/Piper/Systems/Building/BuildingOps.cs) carries the footprint edits; [BuildingProps.cs](../Assets/Scripts/Piper/Systems/Building/BuildingProps.cs) the prop rules; [BuildingGeometry.cs](../Assets/Scripts/Piper/Systems/Building/BuildingGeometry.cs) the plane math. [BuildingStore.cs](../Assets/Scripts/Piper/Systems/Building/BuildingStore.cs) moves a building between the slabs and a model: `load` collects the entities and props of a building, in id order so every peer iterates the same way, and `save` writes changes back, freeing entities the model no longer holds.

**Every edit runs on a clone and writes back only if `validate()` passes.** An edit method returns null on success or the reason it was refused, so a rejected edit never reaches the world, and the host, a predicting client and a preview all give the same answer.

| Edit | Rule |
|---|---|
| `moveEdge(edge, offset)` | Moves the edge and its collinear chain along the edge normal. Refused for triangle walls and if a quad would degenerate |
| `moveVertex` | Only the corners of triangle-only rooms move freely |
| `extrude(edge, offset)` | Only a wall with one room, outwards only; adds a quad |
| `createTri(edge, x, z)` | Only a wall with one room; adds a triangle |
| `slice(area, x, z)` | Cuts in from the outer wall nearest the point, along its inward normal, through every room it crosses until it reaches an outer wall. Each room it crosses must be four-sided and be crossed from one wall to the opposite one, and each wall it crosses is split away from its corners, so no triangle or five-sided room is ever made (two rooms side by side become four). A cut that would meet a triangular room or a corner is refused. A triangle that is sliced directly cuts from its farthest corner |
| `deleteArea` | Only a room attached by exactly one wall, never the last room |
| `setWallStyle(edge, style)` | A wall between two rooms becomes solid, glass or open (see [Wall styles](#wall-styles)); refused on an outer wall |
| `setHeights(floorY, height)` | Moves the floor (up to `MaxRaise` above `groundY`, never below) and sets the ceiling height |
| `placeProp`, `deleteObject` | See [Props](#props) |

`validate()` rejects overlapping rooms, overlapping collinear walls, walls under half a metre, a footprint beyond 40 m, a height or floor level out of range, and a building without an exterior door or a terminal. After every edit `fixObjects` re-checks every prop against its face.

---

## Props

A building prop is a `Prop` with `building` set to the owning building (0 for an outdoor prop, which the prop syncer loads like any other) and a **face**: `Prop.face`, `faceRef` and the 0-1 coordinates `faceU`, `faceV`. The prop's `position` and `eulerRotation` are derived from those each time the building is saved ([BuildingStore.cs](../Assets/Scripts/Piper/Systems/Building/BuildingStore.cs)), so the prop syncer shows building props exactly like outdoor ones and a prop follows its face when the face changes.

| Face | `faceRef` | `u`, `v` |
|---|---|---|
| `Floor` | room (area id) | the room's bounding box in building space |
| `Ceiling` | room (area id) | the room's bounding box in building space |
| `WallPositive`, `WallNegative` | wall (edge id) | distance along the wall from its first vertex, and height as a fraction of the ceiling height |

A floor or ceiling prop also has a `yaw` in degrees (`Prop.faceYaw`, `BObject.yaw`), a rotation about Y that is added to the building's yaw when the prop's rotation is derived; wall props always have 0. The placement and move actions carry it, so a prop keeps its rotation through every edit and a save.

The two wall faces are the sides of the wall: positive is the side the edge normal `(-dz, dx)` points to. A prop's +Z points out of its face, and wall props have their origin on the wall's centre line, so a prop's mesh decides which side it shows on: a painting protrudes from one face, a door frame reaches both. Because `u` and `v` are fractions, moving a wall, extruding or changing the ceiling height moves the props with it. A slice replaces a room, so floor and ceiling props are re-homed to the half that holds their position, and a split wall hands its props to the half that holds them.

[BuildingProps.cs](../Assets/Scripts/Piper/Systems/Building/BuildingProps.cs) lists the placement rules (`BuildingPropDef`):

| Prop | Goes on | Notes |
|---|---|---|
| Table, Chair | A floor | Footprint radius must be inside the room and clear of other floor props |
| Ceiling Lamp | A ceiling | Hangs below the ceiling |
| Painting | A wall side that faces a room | One-sided, shares a wall with props on the other side |
| Door | A wall between two rooms | Cuts a walkable opening, 0.9 m wide |
| Archway | A wall between two rooms | Cuts a walkable opening, 1.3 m wide |
| Exterior Door | An outer wall | Does not cut the wall; always faces outwards. 0.4 m thick against a 0.16 m wall, so it stands proud on both sides. Every exterior door has stairs on its outside ([Exterior stairs](#exterior-stairs)), so it reserves 1.5 m of wall. Using it moves the player ([Doors](#doors)) |
| Exterior Window | An outer wall, from either side | Cuts a 1.2 m wide, 1.1 m high hole in the wall, filled by a glass pane (the glass material) with a collider, so it can be seen through but not walked through, and nothing else can be placed over it; the wall stays above and below it |
| Cargo Terminal | A floor | Makes the building's inventory reachable ([Inventory](#inventory)); every new building starts with one; it can be deleted |
| Ladder | A room's ceiling | A ladder from the ceiling to the floor with a closed hatch on the roof above it, one prop with two parts. Using the ladder climbs to the roof beside the hatch; using the hatch from the roof puts the player back on the floor beside the ladder (`BuildingTravel.UseLadder`). It does not cut the ceiling |
| Exterior Ladder | The outside of an outer wall | 0.6 m wide, from the ground to a metre above the roof, rebuilt to fit when shown (`ExteriorLadderBehaviour`) with the same ground measurement as the stairs. Using it from the ground puts the player on the roof above it, and using it from the roof puts them back on the ground ([Doors](#doors)) |
| Exterior Light | The outside of an outer wall | A small glowing lamp; one-sided |
| Terminal | An outer wall | 1.4 m wide and 1.6 m tall, standing through the wall (0.5 m deep against the 0.16 m wall) so it can be used from both sides; each side carries the four [terminal buttons](#terminal). Not placeable by the player; every new building starts with one on the wall opposite its door, and an edit that leaves it without an outer wall moves it to another |

Wall props stay `PropMargin` clear of the ends of their wall and may not share a rectangle with another wall prop on the same wall (a prop that reaches both sides blocks both). A prop that no longer fits after an edit is dropped; an exterior door that no longer has an outer wall is moved to another outer wall. A new building is always a 4x4x4 m room (`BuildingConstants.StartHalfSize`, `CeilingHeight`) with an exterior door on the south wall, a terminal in the north wall and a cargo terminal inside, and an edit that would leave the building without a door or a terminal is refused.

Prop prefabs are built by `PiperSetup.BuildBuildingProps` ([PiperSetup.cs](../Assets/Editor/PiperSetup.cs)) with the wall convention above. The terminal buttons, the Exterior Door, the Ladder and the Exterior Ladder implement `IInteractable`; the rest are decoration.

### Exterior stairs

[ExteriorDoorBehaviour.cs](../Assets/Scripts/Piper/Props/Building/ExteriorDoorBehaviour.cs) builds the stairs of every exterior door on a child object named `Stairs`, which has the door's material and one box collider per step. The steps go from the floor level down to the real ground at a gentle slope: each time the view is bound to a prop it raycasts down (ignoring buildings and props) where the flight will end and rebuilds the steps to fit, falling back to the building's `groundY` when nothing is hit. The `Stairs` object has no `IInteractable`, so aiming at a step highlights nothing and the door is the only thing to click. The prefab's door part never changes.

### Terminal

[TerminalBehaviour.cs](../Assets/Scripts/Piper/Props/Building/TerminalBehaviour.cs) is a slab with four buttons on each face: +Z is the outside face, -Z the inside one. Each button is a child with its own collider and a [TerminalButton](../Assets/Scripts/Piper/Props/Building/TerminalButton.cs) carrying a `TerminalAction`; aiming at it lights it, and a left click with the `FingerInteraction` presses it. The slab itself is not interactable.

| Button | Colour | Effect |
|---|---|---|
| Edit building | Blue | `InteractionModeSelector.EnterBuildingEdit` ([interaction-modes.md](interaction-modes.md#editing-buildings)) |
| Place furniture | Green | Opens the [furniture list](ui-and-menus.md#furniture-picker) |
| Move furniture | Yellow | Enters [moving furniture](interaction-modes.md#moving-furniture) |
| Delete furniture | Red | Enters [deleting furniture](interaction-modes.md#deleting-furniture) |

---

## Inventory

Each `Building` holds an `Inventory` ([SystemTypes.cs](../Assets/Scripts/Piper/Systems/SystemTypes.cs)); the world has no inventory of its own. **Only a building with a Cargo Terminal inside is reachable** (`BuildingInventory.buildingsWithCargo`), so one building in a group can serve as the store for the whole area. Items collected, taken from a box or spent come from the nearest reachable building within `BuildingInventory.Range` of the prop or placement involved, measured on the ground plane from the building's position; ties go to the lower entity id so every peer agrees. **With none in range a collectable cannot be taken:** its prompt is greyed with "No base inventory in range." and the action does nothing. A building that loses its last Cargo Terminal keeps what it stored, but nothing can reach it until another is placed. A new building starts with one.

The HUD list in the top left shows the inventory of the nearest reachable building within `BuildingInventory.Range` (30 m) of the player, and nothing when none is in range; it restarts its list when that building changes. The debug `AdjustInventory` action names a building (0 means the first reachable one). Saves of another version do not load (`Constants.saveVersion`).

[InventoryRadiusDisplay.cs](../Assets/Scripts/Piper/Core/InventoryRadiusDisplay.cs) draws each reachable building's radius as a line on the ground (sampled every 3.75 degrees, with the ground height measured every few seconds): always for every reachable building while a building is being edited (the edited building too, in grey if it has no Cargo Terminal), and otherwise whenever the player is within 4 m of a radius.

---

## Views

[BuildingSyncer.cs](../Assets/Scripts/Piper/Core/BuildingSyncer.cs) keeps a `BuildingView` for every building. Each tick it sums a hash of every entity and building prop per building and rebuilds a view when its hash changes, so an edit arriving as an action, a snapshot or a loaded save is handled alike. The hash is a sum, so slab order does not matter.

[BuildingView.cs](../Assets/Scripts/Piper/Props/Building/BuildingView.cs) builds a shell mesh with a `MeshCollider` (two-sided floors, so the underside of a raised floor shows from outside, and wall boxes either side of each doorway with a lintel above), a ceiling mesh with collision, and the [scaffold](#scaffold). Geometry uses the landscape's `MeshBuilder` with `Resources/Materials/Building.mat` (the `PipeWorld/Scatter` shader, so the day/night lighting applies). The syncer hides the ceiling of the building being edited.

### Floors and wall styles

A floor is a slab `FloorThickness` (0.2 m) deep: its top at the floor level, its underside below, and sides closed round the edge, so it shows from every angle. Outer walls rise to the top of the roof slab, so the two are flush. The roof is a slab too (`RoofThickness`, 0.2 m): its underside is the ceiling, at `height`, and the player can stand on its top. The scaffold's top rail sits under the slab.

<a id="wall-styles"></a>
A wall between two rooms has a style, chosen with the Wall Style edit tool ([interaction-modes.md](interaction-modes.md#editing-buildings)):

| Style | Looks | Movement | Props |
|---|---|---|---|
| Solid | The wall | Blocked | Allowed |
| Glass | A thin pane drawn with `PipeWorld/Glass` (alpha blended, more opaque at grazing angles) | Blocked (it has a collider) | Allowed |
| Open | Nothing | Free (no geometry and no collider) | Refused; props already there are dropped |

Outer walls are always solid; a wall that becomes an outer wall, or stops being one, is set back by `fixObjects`. An open wall has no collider, so the prop placer's ray passes through it to whatever is behind.

### Scaffold

Under every room's floor a scaffold of posts and rails reaches from `groundY - FoundationDepth` up to the floor. A collider-only prism of the same volume blocks the player from walking into it. The player's `CharacterController` is off while [flying](player-controller.md#flight), so the editor's flight is not blocked.

---

## Doors

[BuildingTravel.cs](../Assets/Scripts/Piper/Props/Building/BuildingTravel.cs) implements the Exterior Door and the Exterior Ladder. Teleporting changes only the player's pose, which clients own, so neither needs an action.

**Using an exterior door** tests whether the player is inside the building (their footprint position is in a room, below the roof). Going out puts them on the stairs 0.65 m beyond the door, at floor level, facing away from it; the stairs always exist, so this works however far the floor is raised. Going in puts them at the nearest spot inside where a box the size of a standing player touches nothing (`FindClearSpot`: from 1.3 m out in steps to 4 m, and 0.5 m or 1 m to each side, so furniture in front of the door never traps them), falling back to the first spot, at floor level, facing away from the door.

**Using an exterior ladder** from the ground puts the player on the roof 0.8 m inside the wall at the ladder, facing the building; from the roof (within the footprint and above the ceiling) it puts them on the nearest clear ground spot in front of the ladder, found by a downward raycast that ignores buildings and props.

**A ladder on a ceiling** (`UseLadder`) climbs to the roof beside its hatch, and its hatch on the roof comes back down.

---

## Tuning

[BuildingConstants](../Assets/Scripts/Piper/Systems/BuildingTypes.cs) holds the default ceiling height and its limits, the floor raise limit, wall thickness, foundation depth, start size (a 4 m square), default ceiling height (4 m), minimum wall length, footprint limit, prop margin and the 8 m minimum spacing between buildings, which `PlaceBuilding` enforces.
