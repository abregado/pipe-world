#ifndef PIPEWORLD_TERRAIN_PROPS_INCLUDED
#define PIPEWORLD_TERRAIN_PROPS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_TopTex);
SAMPLER(sampler_TopTex);
TEXTURE2D(_SideTex);
SAMPLER(sampler_SideTex);

CBUFFER_START(UnityPerMaterial)
    half4 _TopTint;
    half4 _SideTint;
    half4 _AccentColor;
    float _TopScale;
    float _SideScale;
    float _WalkableCos;
    float _EdgeSoftness;
    float _BlockedDarken;
    float _AccentStrength;
    float _DetailFadeStart;
    float _DetailFadeEnd;
CBUFFER_END

#endif
