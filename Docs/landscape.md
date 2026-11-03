# Landscape

How the demo world is generated: a heightfield terrain mesh for slow undulation, large block meshes for every steep face, flat building areas, three biomes, and non-colliding scatter. Read this before changing the world's shape, adding a biome, or adding placed geometry.

---

## Overview

[DemoWorld.cs](../Assets/Scripts/Landscape/DemoWorld.cs) builds everything in `Awake` from a seed, with no prefabs or hand-placed objects. All geometry is generated at runtime from code.

| Stage | Class | Output |
|---|---|---|
| Shape | [WorldShape.cs](../Assets/Scripts/Landscape/WorldShape.cs) | Height, wall, flat-area and biome queries |
| Terrain mesh | [TerrainMeshBuilder.cs](../Assets/Scripts/Landscape/TerrainMeshBuilder.cs) | One mesh, one submesh per biome, mesh collider |
| Cliffs and rocks | [CliffBuilder.cs](../Assets/Scripts/Landscape/CliffBuilder.cs) | One mesh per biome, mesh collider |
| Scatter | [ScatterBuilder.cs](../Assets/Scripts/Landscape/ScatterBuilder.cs) | Meshes per biome per 96 m tile, no collider |
| Shapes | [ShapeTemplates.cs](../Assets/Scripts/Landscape/ShapeTemplates.cs) | Procedural low-poly templates |
| Grass | [GrassBuilder.cs](../Assets/Scripts/Landscape/GrassBuilder.cs) | Tundra grass tiles, no collider |
| Torches | [TorchBuilder.cs](../Assets/Scripts/Landscape/TorchBuilder.cs) | Ten torches with point lights |

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

Values live in [WorldShape.cs:52-55](../Assets/Scripts/Landscape/WorldShape.cs). Ring 5 is a 45 m perimeter wall with no gap; the mesh ends at `HalfSize` (430 m).

[TerraceHeight](../Assets/Scripts/Landscape/WorldShape.cs) (line 115) adds each ring's step through a smoothstep across the ring radius. Where `GapMask` is 0 the transition is `WallTransition` (2.5 m), a near-vertical wall. Inside a ring's gap the transition widens to `RampTransition` (24 m), a ramp of at most about 21°. The staggered gap angles make the route outward a spiral.

[RingRadius](../Assets/Scripts/Landscape/WorldShape.cs) (line 101) wobbles each ring by up to 5 m with Perlin noise so the walls are arcs, not circles. **Every system that follows a wall must call `RingRadius` for the same angle, because terrain height, undulation suppression and block placement all share that radius.**

### Height

[Height](../Assets/Scripts/Landscape/WorldShape.cs) (line 157) is the terrace height plus two octaves of undulation (about ±1.6 m at 70 m wavelength, ±0.5 m at 26 m). The undulation is multiplied by:

- `1 - FlatMask`, so flat areas stay at exactly the terrace height.
- A smoothstep of `WallDistance` from 5 to 16 m, so ground within 5 m of a wall is exactly flat and meets the wall blocks' flat tops without a step.

### Flat areas

`Pads` are circles inside which `Height` returns the bare terrace height. The start floor is a pad of radius 28 m with a 22 m blend. [PlaceTerraceFeatures](../Assets/Scripts/Landscape/WorldShape.cs) (line 202) adds three to four random pads per terrace (radius 6 m up to the terrace's limit, at most 22 m) and one or two mesas, all kept 17 m clear of walls and 10 m clear of each other. Pads are also the keep-out zones for scatter, through `IsClear`.

---

## Terrain Mesh

[TerrainMeshBuilder.cs](../Assets/Scripts/Landscape/TerrainMeshBuilder.cs) samples `Height` on a regular grid (`terrainCellSize`, default 4 m) and emits two triangles per cell with alternating diagonals. Each triangle's submesh is the biome at its centroid, so biome borders follow triangle edges.

The grid is too coarse to follow a 2.5 m wall transition, so the terrain mesh forms a rough slope at every wall. The cliff blocks below cover it.

---

## Cliffs

[CliffBuilder.cs](../Assets/Scripts/Landscape/CliffBuilder.cs) places every steep surface as overlapping flat-topped prisms from `ShapeLibrary.FlatBlock`, so the walls are made of large meshes, not terrain.

| Placement | Behaviour |
|---|---|
| Wall blocks ([PlaceWall](../Assets/Scripts/Landscape/CliffBuilder.cs):37) | Stepped along each ring every 5 to 7 m, skipped where `GapMask` is 0.5 or more. Each block is 8 to 14 m wide and 8 to 12 m deep, yawed up to ±22° off the tangent, and spans from 3 m (`Embed`) below the lower terrace to 0.03 m (`TopLift`) above the upper one. |
| Ledges ([PlaceLedges](../Assets/Scripts/Landscape/CliffBuilder.cs):71) | Tilted rocks embedded in the wall face at random heights. Taller walls get more. |
| Foot boulders ([PlaceFootBoulder](../Assets/Scripts/Landscape/CliffBuilder.cs):85) | Leaning rocks at the base of about half the wall blocks. |
| Mesas ([PlaceMesa](../Assets/Scripts/Landscape/CliffBuilder.cs):95) | Two stacked flat-topped tiers, each 1 m high, jumpable from the ground. |
| Outcrops ([PlaceOutcrops](../Assets/Scripts/Landscape/CliffBuilder.cs):116) | Five rock clusters per terrace, with companions and a stacked rock, away from pads and walls. |

**Block tops sit 0.03 m above the terrain's terrace height.** A coplanar top would z-fight with the terrain mesh beyond the wall transition.

A block's biome is the biome at its centre. Blocks and terrain share the same materials, so a block's flat top shows exactly the same texture as the ground next to it.

---

## Biomes

[WorldShape.BiomeAt](../Assets/Scripts/Landscape/WorldShape.cs) (line 178) returns `Tundra` within about 60 m of the start. Beyond that, the angle around the origin, warped by noise, picks one of three sectors: Rock, Ice, Tundra. The [Biome enum](../Assets/Scripts/Landscape/Biome.cs) order is the submesh order everywhere (`terrainMaterials[(int)biome]`).

| Biome | Ground | Scatter |
|---|---|---|
| Tundra | Snow over olive frozen earth | Grass-blade tufts and flattened snow mounds |
| Rock | Gravel | Piles of overlapping, stacked rocks |
| Ice | Pale ice with cracks | Clusters of tilted crystals with glowing tips |

---

## Scatter

[ScatterBuilder.cs](../Assets/Scripts/Landscape/ScatterBuilder.cs) visits a jittered grid (`CellSize` 5.5 m) and places at most one clump per cell. A cell is skipped when it is inside a pad, within 9 m of a wall, on a surface with normal Y below 0.8, or outside the perimeter. Per-biome density is in `Density` (line 69).

Each clump is many template meshes scaled, rotated and overlapped into one shape: `AddTundraClump` (line 87), `AddRockPile` (line 116), `AddCrystalCluster` (line 147). Clumps align to the ground normal.

**Scatter has no collider.** The player walks through it. Geometry is merged into one mesh per biome per 96 m tile (`TileSize`) so frustum culling works. [ScatterCulling](../Assets/Scripts/Landscape/ScatterCulling.cs) switches tiles off by distance; see [visibility-and-depth.md](visibility-and-depth.md#draw-distance).

Vertex colour carries per-clump colour in RGB and a glow mask in alpha (the [Scatter shader](terrain-shader.md#scatter-shader) uses it).

---

## Grass and Torches

Dense tundra grass is generated separately from scatter; see [grass.md](grass.md). Ten torches with point lights stand around the start area; see [realtime-lights.md](realtime-lights.md). Both are built by `DemoWorld.Awake` after scatter.

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

Terrain, cliffs and scatter are rebuilt on every play. The textures and materials they use are generated by `WorldSetup`; see [build-pipeline.md](build-pipeline.md). Mesa positions are logged to `Player.log` as `[DemoWorld] mesa at (...)` so a screenshot can be aimed at them (see [screenshots.md](screenshots.md)).
