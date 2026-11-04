#ifndef PIPEWORLD_LIGHTING_INCLUDED
#define PIPEWORLD_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Set globally by DayNightCycle. xyz = linear fog colour, w = strength. Params: x = start distance, y = 1 / (end - start).
float4 _PW_FogColor;
float4 _PW_FogParams;
// When above 0.5, PW_Shade returns only the additional-light contribution: a mask of where torches reach and where shadows block them.
float _PW_DebugLights;

half3 PW_AdditionalDiffuse(Light light, float3 normalWS)
{
    return light.color * (light.distanceAttenuation * light.shadowAttenuation * saturate(dot(normalWS, light.direction)));
}

half3 PW_Shade(half3 albedo, float3 normalWS, float3 positionWS, float4 positionCS)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    half3 ambient = SampleSH(normalWS);
    half3 diffuse = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation);
    half3 additional = 0;

#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    inputData.shadowCoord = shadowCoord;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    half4 shadowMask = half4(1, 1, 1, 1);
    AmbientOcclusionFactor aoFactor;
    aoFactor.indirectAmbientOcclusion = 1;
    aoFactor.directAmbientOcclusion = 1;
    uint pixelLightCount = GetAdditionalLightsCount();

#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light dirLight = GetAdditionalLight(dirIndex, inputData, shadowMask, aoFactor);
        additional += PW_AdditionalDiffuse(dirLight, normalWS);
    }
#endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        additional += PW_AdditionalDiffuse(light, normalWS);
    LIGHT_LOOP_END
#endif

    if (_PW_DebugLights > 0.5)
        return additional;
    return albedo * (ambient + diffuse + additional);
}

half3 PW_ApplyFog(half3 color, float3 positionWS)
{
    float dist = distance(positionWS, _WorldSpaceCameraPos);
    float f = saturate((dist - _PW_FogParams.x) * _PW_FogParams.y);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(color, _PW_FogColor.rgb, f * _PW_FogColor.w);
}

#endif
