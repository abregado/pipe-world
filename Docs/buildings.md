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
| `slice(area, x, z)` | Splits a room between two of its unshared walls, from the wall nearest the point along its inward normal; a triangle slices from its farthest corner |
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

The two wall faces are the sides of the wall: positive is the side the edge normal `(-dz, dx)` points to. A prop's +Z points out of its face, and wall props have their origin on the wall's centre line, so a prop's mesh decides which side it shows on: a painting protrudes from one face, a door frame reaches both. Because `u` and `v` are fractions, moving a wall, extruding or changing the ceiling height moves the props with it. A slice replaces a room, so floor and ceiling props are re-homed to the half that holds their position, and a split wall hands its props to the half that holds them.

[BuildingProps.cs](../Assets/Scripts/Piper/Systems/Building/BuildingProps.cs) lists the placement rules (`BuildingPropDef`):

| Prop | Goes on | Notes |
|---|---|---|
| Table, Chair | A floor | Footprint radius must be inside the room and clear of other floor props |
| Ceiling Lamp | A ceiling | Hangs below the ceiling |
| Painting | A wall side that faces a room | One-sided, shares a wall with props on the other side |
| Door | A wall between two rooms | Cuts a walkable opening, 0.9 m wide |
| Archway | A wall between two rooms | Cuts a walkable opening, 1.3 m wide |
| Exterior Door | An outer wall | Does not cut the wall; always faces outwards. 0.4 m thick against a 0.16 m wall, so it stands proud on both sides. Using it moves the player ([Doors](#doors)) |
| Exterior Window | An outer wall, from either side | Cuts a 1.2 m wide, 1.1 m high hole in the wall, filled by a glass pane (the glass material) with a collider, so it can be seen through but not walked through, and nothing else can be placed over it; the wall stays above and below it |
| Exterior Stairs | The outside of an outer wall | 1.5 m wide. Steps from the floor level down to the ground at a gentle slope, rebuilt to fit the drop of the building they belong to (`ExteriorStairsBehaviour`); every step has a collider. They may share wall space with other props, so they go under an exterior door; only other stairs block them |
| Exterior Light | The outside of an outer wall | A small glowing lamp; one-sided |
| Terminal | A floor | Not placeable by the player; starts inside every new building |

Wall props stay `PropMargin` clear of the ends of their wall and may not share a rectangle with another wall prop on the same wall (a prop that reaches both sides blocks both). A prop that no longer fits after an edit is dropped; an exterior door that no longer has an outer wall is moved to another outer wall. A new building is a 3x3 room with an exterior door on the south wall and a terminal inside, and an edit that would leave the building without either is refused.

Prop prefabs are built by `PiperSetup.BuildBuildingProps` ([PiperSetup.cs](../Assets/Editor/PiperSetup.cs)) with the wall convention above. The Terminal and Exterior Door behaviours implement `IInteractable`; the rest are decoration.

---

## Views

[BuildingSyncer.cs](../Assets/Scripts/Piper/Core/BuildingSyncer.cs) keeps a `BuildingView` for every building. Each tick it sums a hash of every entity and building prop per building and rebuilds a view when its hash changes, so an edit arriving as an action, a snapshot or a loaded save is handled alike. The hash is a sum, so slab order does not matter.

[BuildingView.cs](../Assets/Scripts/Piper/Props/Building/BuildingView.cs) builds a shell mesh with a `MeshCollider` (two-sided floors, so the underside of a raised floor shows from outside, and wall boxes either side of each doorway with a lintel above), a ceiling mesh with collision, and the [scaffold](#scaffold). Geometry uses the landscape's `MeshBuilder` with `Resources/Materials/Building.mat` (the `PipeWorld/Scatter` shader, so the day/night lighting applies). The syncer hides the ceiling of the building being edited.

### Floors and wall styles

A floor is a slab `FloorThickness` (0.2 m) deep: its top at the floor level, its underside below, and sides closed round the edge, so it shows from every angle. The scaffold's top rail sits under the slab.

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

[BuildingTravel.cs](../Assets/Scripts/Piper/Props/Building/BuildingTravel.cs) implements the Exterior Door. Using one tests whether the player is inside the building (their footprint position is in a room) and teleports them 1.5 m through the door: out onto the ground, found by a downward raycast that ignores buildings and props, or in at floor level. The player faces away from the door. Teleporting changes only the player's pose, which clients own, so it needs no action.

---

## Tuning

[BuildingConstants](../Assets/Scripts/Piper/Systems/BuildingTypes.cs) holds the default ceiling height and its limits, the floor raise limit, wall thickness, foundation depth, start size, minimum wall length, footprint limit, prop margin and the 8 m minimum spacing between buildings, which `PlaceBuilding` enforces.
