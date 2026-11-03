#ifndef PIPEWORLD_GRASS_PROPS_INCLUDED
#define PIPEWORLD_GRASS_PROPS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4 _Tint;
    float _WindStrength;
    float _WindSpeed;
    float _FadeStart;
    float _FadeEnd;
CBUFFER_END

#endif
