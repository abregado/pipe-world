#ifndef PIPEWORLD_SCATTER_PROPS_INCLUDED
#define PIPEWORLD_SCATTER_PROPS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4 _Tint;
    half4 _GlowTint;
    float _EmissionStrength;
    float _DistanceFade;
CBUFFER_END

#endif
