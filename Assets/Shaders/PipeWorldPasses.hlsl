#ifndef PIPEWORLD_PASSES_INCLUDED
#define PIPEWORLD_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct PWAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct PWVaryings
{
    float4 positionCS : SV_POSITION;
};

PWVaryings PWShadowVert(PWAttributes input)
{
    PWVaryings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    output.positionCS = positionCS;
    return output;
}

half4 PWShadowFrag(PWVaryings input) : SV_Target
{
    return 0;
}

PWVaryings PWDepthVert(PWAttributes input)
{
    PWVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

half4 PWDepthFrag(PWVaryings input) : SV_Target
{
    return 0;
}

#endif
