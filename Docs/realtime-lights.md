# Real-Time Lights

How point lights work in the visual set-up: the torches and beacons, the colour grade they apply to their surroundings, and what the custom shaders and the URP asset need for additional lights to take effect. Read this before adding any light besides the sun.

---

## Torches and Beacons

[TorchBuilder.cs](../Assets/Scripts/Landscape/TorchBuilder.cs) (`BuildAll`, line 76) builds every torch from one `Spec` (size, colours, light settings), called from `DemoWorld`.

| Kind | Count | Placement | Size | Light | Shadows within |
|---|---|---|---|---|---|
| Torch | 2 | (4.5, 6) and (-5, 8) | 1x | Warm, intensity 9, range 12 m | 90 m |
| Torch | 8 | Evenly spaced angles with jitter, radius 12 to 25 m | 1x | Same | 90 m |
| Beacon | 11 | One on the start floor at (-14, 35), then two on opposite sides of each of the five terraces | 5x (9.5 m post) | Intensity 70, range 60 m | 220 m |
| Cave torch | 1 per cave | Centred, 1.8 m in front of the cave's back wall | 1x | Same as a torch | 90 m |

Every light casts soft shadows (strength 0.95) while the camera is within its shadow distance; [TorchFlicker](../Assets/Scripts/Landscape/TorchBuilder.cs) switches them off beyond it. Torches and beacons each have two large `Shadow Rock`s beside them, 2.4 to 3.8 m away (scaled by the light's size, so 12 to 19 m for a beacon). They use the biome's terrain material and convex mesh colliders, and give the light something to throw a shadow from ([AddShadowRocks](../Assets/Scripts/Landscape/TorchBuilder.cs), line 208). A cave torch has no rocks; the cave walls cast its shadows.

**Ranges are short so torch pools do not overlap.** With the old 24 m range, the ten start torches and the start beacon lit the whole start floor about evenly, so each torch's rock shadow was filled in by the others. With 12 m, only a torch's own light reaches most of its shadow.

**Torches dim to 20% by day** (`TorchFlicker.dayIntensity`, faded in by `DayNightCycle.NightFactor`). At full strength they lit the start floor more than the low morning sun does and filled in its shadows.

Beacon positions are random per terrace, kept 12 m from flat areas and 25 m from walls ([BuildBeacons](../Assets/Scripts/Landscape/TorchBuilder.cs), line 107). **A beacon takes its colour from the biome it stands in** ([Beacon](../Assets/Scripts/Landscape/TorchBuilder.cs), line 44):

| Biome | Light and flame | Grade |
|---|---|---|
| Tundra | Warm orange | Warm LUT plus tint |
| Rock | Red-orange | Warm LUT plus tint |
| Ice | Cyan | Tint only |

A torch is a root object with a capsule collider and these children: `Body` (the wooden post), `Brazier` (an iron flat block at the top of the post), `Flame` (a crystal template with every triangle's glow alpha at 255), `Light` (the point light plus its `GradeLight`) and the shadow rocks. **The brazier casts no shadows.** It sits just below the light, so as a caster it would throw a dark disc several metres wide around its own torch. The thin post sits directly under the light and casts only a small spot. [TorchFlicker](../Assets/Scripts/Landscape/TorchBuilder.cs) varies intensity and range with two Perlin noise rates and scales the flame mesh each frame. Its `shadowType` (soft by default) is the shadow mode used within the shadow distance.

The flame uses its own material, `Flame.mat`: the Scatter shader with a white glow tint and an emission strength of 3, so the flame glows in its own vertex colour and bloom picks it up. Everything else uses the normal scatter material.

Point-light shadows render six faces each into the additional-light shadow atlas (4096x4096). With every light near the start in range, about 11 lights request 66 faces, and URP scales each face down to fit (by 4, logged once as `Reduced additional punctual light shadows resolution`). Fewer shadowed lights in range, or a larger atlas, gives sharper torch shadows. Hard shadows look almost the same as soft at this resolution.

---

## Light Grade

[LightGrade.shader](../Assets/Shaders/LightGrade.shader) is a full-screen pass, a `FullScreenPassRendererFeature` named `LightGrade` on `PC_Renderer`, that runs **after** post-processing. It replaces the volume's Color Lookup and does two jobs per pixel:

1. **Global night grade.** The pixel is blended toward its night-LUT value by `_PW_NightFactor`.
2. **Local light grade.** The pixel's world position is rebuilt from the depth buffer. Each nearby `GradeLight` contributes a smoothstep falloff over its radius. The pixel blends from the night grade toward a lit version of its ungraded colour: a warm-LUT lookup (by the lights' `warmLut` amount) multiplied by the lights' tint. A small additive glow of the tint colour is added on top.

Sky pixels are never light-graded. Lit strength is 20% by day and 80% at full night.

**The grade is computed on visible pixels, so terrain hides a light's effect behind it.** From far away a lit area still shows as a coloured patch, because the added glow survives the dark [distance fog](visibility-and-depth.md#fog).

**The local grade follows the real point-light shadows.** The lighting shaders write how much shadowed point light reached each pixel into the colour buffer's alpha ([PW_LightMask](../Assets/Shaders/PipeWorldLighting.hlsl): luminance of the additional light times 4, saturated). The grade multiplies its influence by that alpha, so a pixel in a torch's shadow keeps the plum night grade. Without it, a shadowed pixel still got the warm grade from the lights' falloff and lost most of its contrast. `LightGradeSystem.shadowedGrade` (`_PW_GradeUseLightMask`) turns the mask off.

Three settings carry the alpha to the grade pass:

| Setting | Why |
|---|---|
| `PC_RPAsset` HDR precision **64 bits** | The default 32-bit B10G11R11 buffer has no alpha channel |
| `PC_RPAsset` **Alpha Processing** (`m_AllowPostProcessAlphaOutput`) on | The grade runs after post-processing, and without this the post pass writes alpha 1 |
| `EdgeHighlight.shader` passes the source alpha through | It runs between the opaques and the grade |

A screen-space march toward each light used to approximate the shadows here. It missed most of each rock's shadow, because a ray toward the light only grazes the rock's top, and it shadowed the ground around the flame and brazier.

The LUTs are 32x32x32 strips sampled in gamma space (the pass converts linear to sRGB before the lookup and back after). [LightGrade.shader:63](../Assets/Shaders/LightGrade.shader) is the fragment function.

| Piece | Role |
|---|---|
| [GradeLight.cs](../Assets/Scripts/World/GradeLight.cs) | Component on a light: `tint`, `strength`, `warmLut` (0 tint only, 1 warm LUT), `glow`, and a radius that is the light's range times `radiusScale` |
| [LightGradeSystem.cs](../Assets/Scripts/World/LightGradeSystem.cs) | Each `LateUpdate`, uploads the 32 `GradeLight`s nearest the camera as shader arrays; `globalStrength` scales all of them |
| `NightLUT.png`, `WarmLUT.png` | Generated by `NightGrade` and `WarmGrade` in [WorldSetup.cs](../Assets/Editor/WorldSetup.cs) (lines 171 and 183) |

Torches grade within 1.25 times their range (15 m) with glow 0.07. Beacons grade within 0.9 times their range (54 m) with glow 0.14; a larger radius made the whole crater floor orange.

The debug menu's **Light grade** slider edits `globalStrength` ([debug-menu.md](debug-menu.md)).

### Ordering against the debug menu

The Dear ImGui render feature draws at `AfterRenderingPostProcessing + 10`, after the grade pass, so the debug window is never graded.

---

## What Additional Lights Need

Three things must be true for a light other than the sun to affect a custom-shader surface:

| Requirement | Where |
|---|---|
| The URP asset's additional lights rendering mode is **Per Pixel**, with a non-zero per-object limit (8) | [WorldSetup.cs](../Assets/Editor/WorldSetup.cs) `ConfigureUrpAsset` |
| The shader's forward pass compiles the additional-light keywords (`_ADDITIONAL_LIGHTS`, `_ADDITIONAL_LIGHT_SHADOWS`, `_CLUSTER_LIGHT_LOOP`) | Each shader's `ForwardLit` pass |
| The fragment code loops the lights | [PW_ShadeMasked](../Assets/Shaders/PipeWorldLighting.hlsl) (line 27) |

**The serialized `m_AdditionalLightsRenderingMode` is `LightRenderingMode`, where Per Pixel is 1 and Per Vertex is 2.** Under Forward+ a Per Vertex setting still lights every surface per pixel through the cluster loop, so nothing looks wrong, but URP turns additional-light shadows off for the whole frame. Every light then gets shadow slice -1 and an empty 4x4 shadow map. `.\shot.ps1 -Probe` logs the per-light shadow parameters ([screenshots.md](screenshots.md)); a `w` of -1 on every light means no point light has a shadow map.

The renderer is Forward+, so `PW_Shade` loops the directional lights, then the lights in the pixel's cluster, using URP's `LIGHT_LOOP_BEGIN` macros. **`PW_Shade` takes the fragment's `positionCS`: the cluster lookup needs the normalised screen position.**

Each light contributes `color * distanceAttenuation * shadowAttenuation * saturate(dot(normal, direction))`, added to the diffuse term alongside the sun. Ambient is unchanged, so lights add warmth rather than replacing the flat ambient. `PW_ShadeMasked` also returns the light mask for the grade in alpha.

All three shaders that light surfaces (terrain, scatter, grass) call `PW_Shade`.

---

## Adding a Light

Create a `Light` of type Point in code, as `TorchBuilder` does. No shader or asset change is needed: any point or spot light within range of a surface lights it. Add a `GradeLight` on the same object, with `source` pointing at the light, if its surroundings should also be colour-graded.
