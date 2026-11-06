# Visibility and Depth

How far the player can see, how distant terrain is simplified and hazed, and which post-processing effects add depth. Read this before changing draw distances, fog, or the distant look of the world.

---

## Draw Distance

The camera's far clip plane is 3200 m ([WorldSetup.cs](../Assets/Editor/WorldSetup.cs) `BuildScene`), beyond the lowland's 2600 m, so neither the far side of the crater nor the lowland is clipped. Terrain and cliff meshes are always drawn; they are a few hundred thousand triangles in total and are bounded by the frustum.

[ScatterCulling.cs](../Assets/Scripts/Landscape/ScatterCulling.cs) is the level of detail for clutter, the expensive part of the world. Every `refreshSeconds` (0.25) it measures the distance from the camera to each scatter tile's bounds:

| Setting | Default | Effect |
|---|---|---|
| `dayDrawDistance` | 420 m | Tiles farther than this are disabled by day. |
| `nightDrawDistance` | 220 m | The same limit at full night. |
| `shadowDistance` | 110 m | Tiles farther than this stop casting shadows. |

Those are the scatter defaults. Each tile is registered with its own three distances; grass uses 95 m day, 60 m night and no shadows (see [grass.md](grass.md)). The draw limit moves between the day and night values with `DayNightCycle.NightFactor`.

**Scatter dissolves before its tile switches off.** `ScatterCulling.RegisterFade` publishes the current draw distance as a shader global: `_PW_ScatterDrawDistance` for scatter and `_PW_GrassDrawDistance` for grass. [PipeWorldFade.hlsl](../Assets/Shaders/PipeWorldFade.hlsl) fades scatter from 70% to 95% of that distance, measured per pixel, with a screen-space dither: the colour and depth passes both clip the dithered pixels through `PW_DitherClip`, so they agree, and the glow fades with it. **A fully visible surface is never clipped.** An earlier version clipped wherever `fade - dither - 0.001` was negative, which at full visibility still cut every pixel whose dither value was above 0.999: a sparse sprinkle of dots over every Scatter-shader surface, torches and flames included. A tile switches off when its nearest point passes the draw distance, so everything in it has already gone. **The glow made the pop-in obvious before:** it is added after fog, so the ice crystals' tips stayed bright to the last metre. The Scatter shader's `_DistanceFade` toggle turns the dissolve off; `Flame.mat` and the torch parts' material have it off. Grass sinks into the ground instead (see [grass.md](grass.md)).

---

## Fog

[DayNightCycle.cs:152](../Assets/Scripts/World/DayNightCycle.cs) writes the shader globals `_PW_FogColor`, `_PW_FogParams` and `_PW_NightFactor` every frame. [PW_ApplyFog](../Assets/Shaders/PipeWorldLighting.hlsl) (line 78) blends a surface toward the fog colour between a start and end distance with a smoothstep, reaching full strength at the end. The terrain, scatter and grass shaders all call it after lighting.

| Setting | Day | Night |
|---|---|---|
| `dayFogStart` / `nightFogStart` | 120 m | 30 m |
| `dayFogEnd` / `nightFogEnd` | 1600 m | 420 m |
| `dayFogDarken` / `nightFogDarken` | 0.62 | 0.5 |

Start, end and darkening blend by `NightFactor`.

**The fog colour is deliberately darker than the sky horizon.** It is the `skyHorizon` gradient mixed 20% toward `skyZenith`, multiplied by the darken value. Distant terrain converges on that one dark colour, and the sky at the horizon is lighter, so the terrain reads as a silhouette against the sky and its detail is gone at range. The skybox shader itself does not apply fog.

The colour is converted with `.linear` to match the sky shader's colour after conversion.

### Light through fog

Fog flattens terrain colour but not light. Two things stay visible at range:

- Glowing vertices ([Scatter.shader](../Assets/Shaders/Scatter.shader)) are added **after** fog, so flames and crystal tips are never swallowed by it.
- The [LightGrade pass](realtime-lights.md#light-grade) tints and adds a glow around lights after fog, on whatever colour the pixel ended up as.

Terrain still hides a distant light: the grade is applied per visible pixel, so a light behind a ridge colours nothing you can see.

---

## Texture Fade

The terrain shader fades its textures to each texture's average colour between `_DetailFadeStart` (100 m) and `_DetailFadeEnd` (380 m), so far terrain is flat shaded while keeping the top/side contrast that marks walkable surfaces. See [terrain-shader.md](terrain-shader.md#texture-fade). Both distances are material properties set in [WorldSetup.cs](../Assets/Editor/WorldSetup.cs).

---

## Edge Highlight

[EdgeHighlight.shader](../Assets/Shaders/EdgeHighlight.shader) is a full-screen pass that brightens silhouettes and creases using only the depth buffer. It is a `FullScreenPassRendererFeature` on `PC_Renderer`, added once by [ConfigureEdgeHighlight](../Assets/Editor/WorldSetup.cs) (line 79), injected before post-processing so bloom and tonemapping act on the lines. Rerunning setup reuses the existing feature.

The pass samples depth at the pixel and its four neighbours, `_Thickness` pixels away. Reversed-Z depth is affine in 1/z, so it is linear in screen space across any flat face; its second difference ([EdgeHighlight.shader:55](../Assets/Shaders/EdgeHighlight.shader)) is zero on a plane and large at a silhouette or a crease. The result is divided by the nearest neighbour's depth, so the threshold does not depend on distance, then thresholded with a smoothstep.

| Property | Default | Meaning |
|---|---|---|
| `_Strength` | 0.28 | Brightness added on an edge |
| `_Threshold` | 0.05 | Relative curvature where edges begin |
| `_Thickness` | 1 | Neighbour offset in pixels |
| `_FadeStart`, `_FadeEnd` | 40, 250 m | Edges fade out with eye depth (line 61), so distant terrain shows no outlines |
| `_EdgeColor` | warm white | Mixed 35% toward the fog colour so edges follow the time of day |

Lines are added to the colour, not blended. **Anything that writes depth gets outlined, including grass and torches.**

---

## Post-Processing

The global volume's effects are described in [lighting-and-day-night.md](lighting-and-day-night.md#post-processing). Those that add depth:

| Effect | Where | Setting |
|---|---|---|
| Ambient occlusion | `PC_Renderer` SSAO feature, configured by [ConfigureSsao](../Assets/Editor/WorldSetup.cs) (line 60) | Depth source, after opaques, intensity 0.9, radius 0.6, direct lighting strength 0.15, interleaved gradient noise, 12 samples, high normal quality, bilateral blur, full resolution |
| Depth of field | `WorldVolumeProfile`, Gaussian | Blur begins at 400 m, full at 1500 m, max radius 1 |
| Split toning | `WorldVolumeProfile` | Faint cool shadows, warm highlights |

**SSAO uses interleaved gradient noise, not blue noise.** URP's blue noise mode switches to a different noise texture at a random offset every frame, which is meant to be resolved by temporal anti-aliasing. Without it, the occlusion visibly crawls near objects. Interleaved gradient noise is fixed per pixel, and the higher sample count and bilateral blur hide its pattern. It costs about 2 ms a frame more than the old Medium settings.

**SSAO reads the depth buffer, not depth and normals, because the terrain and scatter shaders write depth but no normals.** A `DepthNormals` source would leave those objects out of the normal texture and the effect would misbehave.

**SSAO runs after opaques.** In the default mode each lit shader multiplies its own output by the occlusion texture, and the custom shaders do not; after-opaque mode blends it over the finished image instead.

The camera has `requiresDepthTexture` enabled, which SSAO and depth of field need.
