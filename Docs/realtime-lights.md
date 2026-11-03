# Real-Time Lights

How point lights work in the visual set-up: the torches around the start area, and what the custom shaders and the URP asset need for additional lights to take effect. Read this before adding any light besides the sun.

---

## Torches

[TorchBuilder.cs](../Assets/Scripts/Landscape/TorchBuilder.cs) (`BuildAll`, line 15) places ten torches, called from `DemoWorld`:

| Group | Count | Placement | Shadows |
|---|---|---|---|
| Beside the spawn | 2 | (4.5, 6) and (-5, 8) | Soft |
| Ring | 8 | Evenly spaced angles with jitter, radius 12 to 25 m | None |

A torch is a root object with a capsule collider and three children: `Body` (wooden post and an iron brazier from the flat-block template), `Flame` (a crystal template with every triangle's glow alpha at 255), and `Light`, a point light 2.35 m up.

| Light setting | Value |
|---|---|
| Colour | (1.0, 0.62, 0.3) |
| Intensity | 6 |
| Range | 24 m |

[TorchFlicker](../Assets/Scripts/Landscape/TorchBuilder.cs) (line 94) varies intensity and range with two Perlin noise rates and scales the flame mesh each frame. The torches use the [Scatter shader](terrain-shader.md#scatter-shader) material, so the flame glows through the vertex-alpha mask.

Point-light shadows render six faces each, so only the two torches beside the spawn cast them.

---

## What Additional Lights Need

Three things must be true for a light other than the sun to affect a custom-shader surface:

| Requirement | Where |
|---|---|
| The URP asset renders additional lights per pixel, with a non-zero per-object limit (8) | [WorldSetup.cs](../Assets/Editor/WorldSetup.cs) `ConfigureUrpAsset` |
| The shader's forward pass compiles the additional-light keywords (`_ADDITIONAL_LIGHTS`, `_ADDITIONAL_LIGHT_SHADOWS`, `_CLUSTER_LIGHT_LOOP`) | Each shader's `ForwardLit` pass |
| The fragment code loops the lights | [PW_Shade](../Assets/Shaders/PipeWorldLighting.hlsl) (line 16) |

The renderer is Forward+, so `PW_Shade` loops the directional lights, then the lights in the pixel's cluster, using URP's `LIGHT_LOOP_BEGIN` macros. **`PW_Shade` takes the fragment's `positionCS`: the cluster lookup needs the normalised screen position.**

Each light contributes `color * distanceAttenuation * shadowAttenuation * saturate(dot(normal, direction))`, added to the diffuse term alongside the sun. Ambient is unchanged, so lights add warmth rather than replacing the high flat ambient.

All three shaders that light surfaces (terrain, scatter, grass) call `PW_Shade`.

---

## Adding a Light

Create a `Light` of type Point in code, as `TorchBuilder` does, and set `shadows` only if the light needs them. No shader or asset change is needed: any point or spot light within range of a surface lights it.
