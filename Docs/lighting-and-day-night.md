# Lighting and Day/Night Cycle

How the world is lit, post-processed and recoloured over the day. Read this before changing the sun, ambient, skybox or night grade.

---

## Design

The only light that is always present is a soft-shadow directional `Sun`; the torches add point lights near the start (see [realtime-lights.md](realtime-lights.md)). Everything else is lit by high, flat ambient colour, so unlit sides never go dark. At night the sun is switched off and a colour grade recolours the scene toward plum and amber. **The night palette is deliberately not blue.**

| Piece | Where |
|---|---|
| URP asset settings | [WorldSetup.cs:35](../Assets/Editor/WorldSetup.cs) (`ConfigureUrpAsset`), applied to `Assets/Settings/PC_RPAsset.asset` |
| Post-processing profile | `Assets/Settings/WorldVolumeProfile.asset`, built in `WorldSetup.BuildVolumeProfile` |
| Night LUT | `Assets/Art/LUTs/NightLUT.png`, generated in `WorldSetup.BuildNightLut` |
| Sky shader | [GradientSky.shader](../Assets/Shaders/GradientSky.shader) |
| Runtime driver | [DayNightCycle.cs](../Assets/Scripts/World/DayNightCycle.cs) |

---

## URP Asset

`PC_RPAsset` is configured for one main shadow-casting light: HDR on, 4x MSAA, soft shadows, 4 cascades, a 4096 main-light shadow map, 220 m shadow distance. Additional lights render per pixel with a limit of 8 per object, and additional-light shadows use the asset's 2048 atlas.

---

## Post-Processing

`WorldVolumeProfile` is a global volume on the `Global Volume` object. Its overrides:

| Effect | Setting |
|---|---|
| Tonemapping | Neutral |
| Bloom | threshold 1.0, intensity 0.25, scatter 0.6 |
| Color Adjustments | contrast +8, saturation +6 |
| Vignette | intensity 0.18, smoothness 0.5 |
| Split Toning | shadows (0.78, 0.78, 0.88), highlights (1.0, 0.95, 0.87) |
| Depth of Field | Gaussian, start 400 m, end 1500 m, max radius 1 |
| Color Lookup | `NightLUT`, contribution driven by the night factor |

Screen-space ambient occlusion comes from the renderer, not the volume; see [visibility-and-depth.md](visibility-and-depth.md#post-processing). Distance fog is applied by the terrain and scatter shaders from values `DayNightCycle` sets; see [visibility-and-depth.md](visibility-and-depth.md#fog).

The camera has post-processing enabled through `UniversalAdditionalCameraData`.

### Night LUT

`NightLUT.png` is a 32x32x32 strip (1024x32). Each colour is desaturated by half, tinted from plum (shadows) to amber (highlights) by its luminance, then darkened. The texture is imported with sRGB off, no mipmaps, clamp wrap and no compression; **a LUT imported with sRGB on or compression enabled shifts every colour in the grade.**

---

## Time of Day

[DayNightCycle.cs:100](../Assets/Scripts/World/DayNightCycle.cs) (`Apply`) runs every frame and in edit mode (`[ExecuteAlways]`).

`timeOfDay` runs 0 to 1: 0 is midnight, 0.25 sunrise, 0.5 noon, 0.75 sunset. `dayLengthSeconds` (default 600) sets how fast it advances; `paused` freezes it.

From `timeOfDay`, `Apply` derives the sun elevation (-1 at midnight, +1 at noon), then:

| Output | Derived from |
|---|---|
| Sun rotation | `timeOfDay * 360 - 90` pitch, `sunHeading` yaw |
| Sun colour and intensity | `sunColor` gradient and a fast ramp after elevation passes 0 |
| Sun enabled | elevation above 0.01 |
| Ambient colour | `ambientColor` gradient, flat ambient mode |
| Sky | `skyZenith`, `skyHorizon` gradients; ground colour is 90% of horizon |
| Night factor | `InverseLerp(nightStart, nightFull, elevation)`, written to the Color Lookup contribution |

**The sun is disabled below the horizon.** A directional light below the horizon points upward and would cast shadows from underneath the ground.

The gradients are sampled by elevation remapped to 0..1 (0 midnight, 0.5 horizon, 1 noon), so stops near 0.5 control sunrise and sunset colours.

`DayNightCycle` instantiates its own copy of the sky material at runtime, so edits to the skybox never modify the asset.

---

## Sky Shader

[GradientSky.shader](../Assets/Shaders/GradientSky.shader) blends horizon to zenith above the horizon and horizon to ground below it. The blend is quantised into `_Bands` (10) equal steps, the ground blend into three, and the sun's glow into five rings, so the sky has the same hard-edged, stepped look as the faceted ground. A sun disc and glow are drawn toward `_SunDir`, which `DayNightCycle` sets from the sun transform. The disc is HDR, so bloom picks it up, and is masked to the sky above the horizon.

---

## Checking Changes

Capture the four times of day with [screenshots.md](screenshots.md): `-Time 0.32` (morning), `0.5` (noon), `0.74` (dusk), `0.0` (midnight).
