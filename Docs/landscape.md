# Landscape

How the demo world is generated: a heightfield terrain mesh for slow undulation, large block meshes for every steep face, flat building areas, plateaus, an open edge with a lowland beyond, four biomes, and non-colliding scatter. Read this before changing the world's shape, adding a biome, or adding placed geometry.

---

## Overview

[DemoWorld.cs](../Assets/Scripts/Landscape/DemoWorld.cs) builds everything in `Awake` from a seed, with no prefabs or hand-placed objects. All geometry is generated at runtime from code.

| Stage | Class | Output |
|---|---|---|
| Shape | [WorldShape.cs](../Assets/Scripts/Landscape/WorldShape.cs) | Height, wall, flat-area and biome queries |
| Terrain mesh | [TerrainMeshBuilder.cs](../Assets/Scripts/Landscape/TerrainMeshBuilder.cs) | One mesh, one submesh per biome, mesh collider, reaching 2600 m on the open side |
| Cliffs and rocks | [CliffBuilder.cs](../Assets/Scripts/Landscape/CliffBuilder.cs) | One mesh per biome, mesh collider |
| Scatter | [ScatterBuilder.cs](../Assets/Scripts/Landscape/ScatterBuilder.cs) | Meshes per biome per 96 m tile, no collider |
| Shapes | [ShapeTemplates.cs](../Assets/Scripts/Landscape/ShapeTemplates.cs) | Procedural low-poly templates |
| Grass | [GrassBuilder.cs](../Assets/Scripts/Landscape/GrassBuilder.cs) | Tundra and grassland grass tiles, no collider |
| Lights | [TorchBuilder.cs](../Assets/Scripts/Landscape/TorchBuilder.cs) | Torches, campfires, beacons and crystal monoliths, each with a point light |

**Terrain, cliffs and scatter all use flat shading.** [MeshBuilder.cs](../Assets/Scripts/Landscape/MeshBuilder.cs) gives every triangle its own three vertices and face normal, which is what produces the faceted low-poly look and what the [terrain shader](terrain-shader.md) reads to decide walkability.

---

## Shape

The player starts at the centre of a crater. Six concentric rings (`Rings`) form walls; the ground steps up at each ring, and each step is higher than the last.

| Ring | Radius | Step | Gap centre | Gap half-width |
|---|---|---|---|---|
| 0 | 50 | 5 | 20° | 17° |
| 1 | 110 | 6 | 150° | 15° |
| 2 | 175 | 7 | 280° | 14° |
| 3 | 245 | 8 | 40° | 13° |
| 4 | 320 | 9 | 170° | 12° |
| 5 | 400 | 45 | none | none |

Values live in [WorldShape.cs:107-110](../Assets/Scripts/Landscape/WorldShape.cs). Ring 5 is a 45 m perimeter wall with no gap, but only on half the world; see [The open edge](#the-open-edge).

[TerraceHeight](../Assets/Scripts/Landscape/WorldShape.cs) (line 244) adds each ring's step through a smoothstep across the ring radius. Where `GapMask` is 0 the transition is `WallTransition` (2.5 m), a near-vertical wall. Inside a ring's gap the transition widens to `RampTransition` (24 m), a ramp of at most about 21°. The staggered gap angles make the route outward a spiral.

[RingRadius](../Assets/Scripts/Landscape/WorldShape.cs) (line 172) wobbles each ring by up to 5 m with Perlin noise so the walls are arcs, not circles. **Every system that follows a wall must call `RingRadius` for the same angle, because terrain height, undulation suppression and block placement all share that radius.**

### Height

[Height](../Assets/Scripts/Landscape/WorldShape.cs) (line 299) is the terrace height plus two octaves of undulation (about ±1.6 m at 70 m wavelength, ±0.5 m at 26 m). The undulation is multiplied by:

- `1 - FlatMask`, so flat areas stay at exactly the terrace height.
- A smoothstep of `WallDistance` from 5 to 16 m, so ground within 5 m of a wall is exactly flat and meets the wall blocks' flat tops without a step.

### The open edge

**The outer wall stands on only half the world.** Across a 180° arc centred on 45° (`OpenCenterDeg`, `OpenHalfDeg`), `OpenMask` removes ring 5's step and its wall blocks, and the ground falls away instead. The cave at 225° is on the walled half.

| Piece | Value |
|---|---|
| Drop start (`DropStart`) | 40 m past ring 4 (360 m) on the open side; 35 m outside the wall elsewhere |
| Drop length (`DropLength`) | 150 m, a smoothstep, so about 53° at its steepest |
| Lowland ([Lowland](../Assets/Scripts/Landscape/WorldShape.cs), line 319) | About -100 m, with hills and ridges that only rise from 550 m out, so the foot of the drop stays low |

The walkable crater ends at `PlayRadius`: the wall, or the top of the drop. `InPlayArea` is what scatter, grass, beacons, pads and plateaus check instead of the outer radius. The player can still walk down the drop; the terrain has a collider all the way out.

Standing at the top of the drop shows the lowland's ridges against the sky. The debug menu's **Open edge** respawn puts the player there, facing out.

### Plateaus

[PlacePlateaus](../Assets/Scripts/Landscape/WorldShape.cs) (line 371) adds one plateau on each of the first two terraces and two on each terrace after that. Each is a raised disc 70 to 100% of the terrace's room in radius (about 15 to 25 m), 6 to 14 m high. Its height is added in `TerraceHeight` exactly like a ring step, and it works like a ring wall turned inside out:

- A 2.5 m transition at the rim, covered by a ring of wall blocks ([PlacePlateau](../Assets/Scripts/Landscape/CliffBuilder.cs), line 103).
- One ramp gap 14 m wide, where the transition widens to 3.6 times the height (about 22° at its steepest). The ramp runs along the terrace, where there is the most room.
- The rim counts as a wall in `WallDistance`, so the ground beside it is flat and scatter keeps clear of it.

A plateau may come within 12 m of a ring wall and block the terrace there. The terrace is a ring, so the other way round stays open. Plateaus are kept out of the arc in front of either ring's ramp gap. Plateau tops get scatter (`IsClear` is called with `avoidPlateaus` false) and a campfire, or a monolith on ice. Plateaus are logged as `[DemoWorld] plateau at (...)` with their top height and ramp angle.

### Flat areas

`Pads` are circles inside which `Height` returns the bare terrace height. The start floor is a pad of radius 28 m with a 22 m blend. [PlaceTerraceFeatures](../Assets/Scripts/Landscape/WorldShape.cs) (line 410) adds three to four random pads per terrace (radius 6 m up to the terrace's limit, at most 22 m) and one or two mesas, all kept 17 m clear of walls and 10 m clear of each other. Pads are also the keep-out zones for scatter, through `IsClear`.

---

## Terrain Mesh

[TerrainMeshBuilder.cs](../Assets/Scripts/Landscape/TerrainMeshBuilder.cs) samples `Height` on a grid and emits two triangles per cell with alternating diagonals. Each triangle's submesh is the biome at its centroid, so biome borders follow triangle edges.

Inside `HalfSize` (430 m) the grid is regular (`terrainCellSize`, default 4 m). Beyond it each row and column is 8% wider than the last, up to 64 m, out to `FarExtent` (2600 m). Rows and columns are shared, so the fine and coarse parts meet without cracks. Far cells are only built on the open side: where the cell centre's projection on the open direction is more than -150 m. The whole mesh is about 160,000 triangles.

The grid is too coarse to follow a 2.5 m wall transition, so the terrain mesh forms a rough slope at every wall. The cliff blocks below cover it.

---

## Cliffs

[CliffBuilder.cs](../Assets/Scripts/Landscape/CliffBuilder.cs) places every steep surface as overlapping flat-topped prisms from `ShapeLibrary.FlatBlock`, so the walls are made of large meshes, not terrain.

| Placement | Behaviour |
|---|---|
| Wall blocks ([PlaceWall](../Assets/Scripts/Landscape/CliffBuilder.cs):42) | Stepped along each ring every 5 to 7 m, skipped where `GapMask` (or, on the outer ring, `OpenMask`) is 0.5 or more. A block whose surroundings fall away reaches down to the lowest ground within 6 m. Each block is 8 to 14 m wide and 8 to 12 m deep, yawed up to ±22° off the tangent, and spans from 3 m (`Embed`) below the lower terrace to 0.03 m (`TopLift`) above the upper one. |
| Ledges ([PlaceLedges](../Assets/Scripts/Landscape/CliffBuilder.cs):190) | Tilted rocks embedded in the wall face at random heights. Taller walls get more. |
| Foot boulders ([PlaceFootBoulder](../Assets/Scripts/Landscape/CliffBuilder.cs):204) | Leaning rocks at the base of about half the wall blocks. |
| Mesas ([PlaceMesa](../Assets/Scripts/Landscape/CliffBuilder.cs):214) | Two stacked flat-topped tiers, each 1 m high, jumpable from the ground. |
| Outcrops ([PlaceOutcrops](../Assets/Scripts/Landscape/CliffBuilder.cs):235) | Five rock clusters per terrace, with companions and a stacked rock, away from pads and walls. |
| Plateau rims ([PlacePlateau](../Assets/Scripts/Landscape/CliffBuilder.cs):103) | Wall blocks every 4.5 to 6.5 m round the rim, skipped across the ramp, with a boulder at the foot of about a third. |
| Drop rocks ([PlaceDropRocks](../Assets/Scripts/Landscape/CliffBuilder.cs):258) | 90 outcrops down the drop and across the first 650 m of lowland, up to three times normal size, so the view from the rim has something to judge distance by. |

**Block tops sit 0.03 m above the terrain's terrace height.** A coplanar top would z-fight with the terrain mesh beyond the wall transition.

**Overlapping blocks of different biomes do not z-fight: each biome has a priority.** Neighbouring blocks overlap and their flat tops would be coplanar, so where two biomes meet along a wall, two textures fought over the same pixels. [AddBlock](../Assets/Scripts/Landscape/CliffBuilder.cs) raises each flat-topped block's top by `TopPriority` times 0.02 m (Rock 3, Ice 2, Grass 1, Tundra 0), so the higher-priority biome always shows. Blocks of the same biome still share a height, but they look identical, so it doesn't matter.

A block's biome is the biome at its centre. Blocks and terrain share the same materials, so a block's flat top shows exactly the same texture as the ground next to it.

---

## Biomes

[WorldShape.BiomeAt](../Assets/Scripts/Landscape/WorldShape.cs) (line 337) returns `Tundra` within about 60 m of the start, and `Grass` 40 m past the top of the drop (the whole lowland). Elsewhere the angle around the origin, warped by noise, picks one of four 90° sectors: Rock, Ice, Grass, Tundra. Ice and Grass share the open half, so the view over the lowland starts from grassland. The [Biome enum](../Assets/Scripts/Landscape/Biome.cs) order is the submesh order everywhere (`terrainMaterials[(int)biome]`).

| Biome | Ground | Scatter |
|---|---|---|
| Tundra | Snow over olive frozen earth | Grass-blade tufts and flattened snow mounds |
| Rock | Gravel | Piles of overlapping, stacked rocks |
| Ice | Pale ice with cracks | Clusters of tilted crystals with glowing tips |
| Grass | Green meadow with drier patches over earthy sides | Tall green tufts, some with flowers or a low bush, plus dense grass |

---

## Scatter

[ScatterBuilder.cs](../Assets/Scripts/Landscape/ScatterBuilder.cs) visits a jittered grid (`CellSize` 5.5 m) and places at most one clump per cell. A cell is skipped when it is inside a pad, within 9 m of a wall or plateau rim, on a surface with normal Y below 0.8, or outside the play area (`InPlayArea`, 14 m margin). Per-biome density is in `Density` (line 78).

Each clump is many template meshes scaled, rotated and overlapped into one shape: `AddTundraClump` (line 97), `AddMeadowClump` (line 127), `AddRockPile` (line 167), `AddCrystalCluster` (line 198). Clumps align to the ground normal.

**Scatter has no collider.** The player walks through it. Geometry is merged into one mesh per biome per 96 m tile (`TileSize`) so frustum culling works. [ScatterCulling](../Assets/Scripts/Landscape/ScatterCulling.cs) switches tiles off by distance, and the shader dissolves scatter before its tile goes; see [visibility-and-depth.md](visibility-and-depth.md#draw-distance).

Vertex colour carries per-clump colour in RGB and a glow mask in alpha (the [Scatter shader](terrain-shader.md#scatter-shader) uses it).

---

## Grass and Torches

Dense tundra and grassland grass is generated separately from scatter; see [grass.md](grass.md). Torches and campfires stand around the start area, beacons and crystal monoliths across the terraces, and a campfire or monolith on each plateau, each with a point light; see [realtime-lights.md](realtime-lights.md). Both are built by `DemoWorld.Awake` after scatter.

---

## Shape Templates

[ShapeTemplates.cs](../Assets/Scripts/Landscape/ShapeTemplates.cs) generates every template from a seed. All span local Y 0 to 1 and about ±0.5 in X and Z, so a matrix scale gives the final size.

| Template | Used for |
|---|---|
| `Prism`, flat top | Wall blocks and mesas. Top cap is exactly planar. |
| `Prism`, tapered top | Rocks, ledges, boulders, outcrops. |
| `Prism`, steep taper | Snow mounds. |
| `Crystal` | Ice clusters. Tip triangles carry glow. |
| `Blade` | Tundra tufts. Upper triangles are lighter. |

Triangle winding is corrected automatically: `Finish` flips any triangle that faces toward the template's centre, so no template needs hand-authored winding.

---

## Regenerating

Terrain, cliffs and scatter are rebuilt on every play. The textures and materials they use are generated by `WorldSetup`; see [build-pipeline.md](build-pipeline.md). Mesa, plateau, campfire and monolith positions are logged to `Player.log` (for example `[DemoWorld] mesa at (...)`) so a screenshot can be aimed at them (see [screenshots.md](screenshots.md)).
