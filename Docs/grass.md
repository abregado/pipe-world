# Grass

The tundra and grassland grass: how blades are generated and placed, how they sway, and how they are culled. Read this before changing grass density, colour or movement.

---

## Generation

[GrassBuilder.cs](../Assets/Scripts/Landscape/GrassBuilder.cs) covers a square of half-width `Range` (230 m) around the origin on a jittered grid (`CellSize` 1 m) and places at most one clump per cell. A cell is skipped when:

- The biome is not Tundra or Grass, or it is Tundra more than 170 m from the origin.
- Perlin noise at an 11 m scale is below `PatchThreshold` (0.42) on tundra, or 0.25 on grassland, so tundra grass grows in patches and grassland is covered almost everywhere.
- It is outside the play area.
- It is within 7 m of a wall.
- The ground normal Y is below 0.85.

Grass is placed on flat building areas as well; it is cosmetic and has no collider.

A clump is 6 to 10 blades within 0.35 m of its centre, 0.32 to 0.62 m tall, with random lean and yaw. Each clump picks one colour from five (olive, khaki, green, frosty) on tundra, or four greens on grassland, and varies it by ±10%. Grass is about 1.2 million triangles in all; only tiles within the draw distance are drawn. Blades are the [GrassBlade template](../Assets/Scripts/Landscape/ShapeTemplates.cs): a three-triangle tapered card whose upper triangle is lighter.

Geometry is merged into one mesh per 48 m tile (`TileSize`). The mesh builder is created with `trackWeights`, which writes a per-vertex sway weight to UV0.x: 0 at the base, 0.35 at mid-height, 1 at the tip.

---

## Shader

[Grass.shader](../Assets/Shaders/Grass.shader) is two-sided and lit through [PW_Shade](../Assets/Shaders/PipeWorldLighting.hlsl) with fog applied last.

| Behaviour | How |
|---|---|
| Wind | In the vertex stage, XZ is offset by two sine waves of position and time, scaled by the sway weight and a slow gust term. `_WindStrength` is the tip offset in metres and `_WindSpeed` the rate. |
| Distance shrink | Between `_FadeStart` (45 m) and `_FadeEnd` (85 m), vertices sink by up to 0.55 m times their weight, so blades flatten into the ground before the tile is culled. Both shrink to fit inside the current draw distance (`_PW_GrassDrawDistance`): the end is at most 90% of it and the start at most 55% of the end, so at night (60 m) the blades are flat by 54 m. |
| Lighting | The normal is flipped on back faces, then blended 65% toward up, so the flat blades read as soft tufts instead of lit cards. |

The grass material has no shadow-caster pass, and its tiles are registered with shadows off. Grass receives sun and torch light but casts none.

---

## Culling

[ScatterCulling](../Assets/Scripts/Landscape/ScatterCulling.cs) switches grass tiles off beyond 95 m by day and 60 m at full night (see [visibility-and-depth.md](visibility-and-depth.md#draw-distance)). The shader shrink always finishes before the draw distance, so a tile disappears when its blades are already flat. Before the shrink followed the draw distance, night tiles switched off at 60 m with blades still standing.

---

## Notes

The depth buffer includes grass, so the [edge highlight](visibility-and-depth.md#edge-highlight) outlines blades. Lower the highlight strength if that reads as too busy.
