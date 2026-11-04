#ifndef PIPEWORLD_LIGHTING_INCLUDED
#define PIPEWORLD_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Set globally by DayNightCycle. xyz = linear fog colour, w = strength. Params: x = start distance, y = 1 / (end - start).
float4 _PW_FogColor;
float4 _PW_FogParams;
// Debug view for PW_Shade. 1: only the additional-light contribution, a mask of where torches reach and where shadows block them.
// 2: the sun's shadow attenuation (red) and the shadow-map coordinate (green, blue). 3: LightGrade shows the light mask.
float _PW_DebugLights;

half3 PW_AdditionalDiffuse(Light light, float3 normalWS)
{
    return light.color * (light.distanceAttenuation * light.shadowAttenuation * saturate(dot(normalWS, light.direction)));
}

// How much shadowed point light reached the pixel, 0..1. Written to the colour buffer's alpha, where the LightGrade pass
// reads it as the mask for each torch's warm grade, so the grade follows the real shadows.
half PW_LightMask(half3 additional)
{
    return saturate(dot(additional, half3(0.3, 0.59, 0.11)) * 4.0);
}

// rgb: lit colour. a: PW_LightMask.
half4 PW_ShadeMasked(half3 albedo, float3 normalWS, float3 positionWS, float4 positionCS)
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

    half mask = PW_LightMask(additional);
    if (_PW_DebugLights > 2.5)
        return half4(albedo * (ambient + diffuse + additional), mask);
    if (_PW_DebugLights > 1.5)
        return half4(mainLight.shadowAttenuation, frac(shadowCoord.xy * 8.0), mask);
    if (_PW_DebugLights > 0.5)
        return half4(additional, mask);
    return half4(albedo * (ambient + diffuse + additional), mask);
}

half3 PW_Shade(half3 albedo, float3 normalWS, float3 positionWS, float4 positionCS)
{
    return PW_ShadeMasked(albedo, normalWS, positionWS, positionCS).rgb;
}

half3 PW_ApplyFog(half3 color, float3 positionWS)
{
    float dist = distance(positionWS, _WorldSpaceCameraPos);
    float f = saturate((dist - _PW_FogParams.x) * _PW_FogParams.y);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(color, _PW_FogColor.rgb, f * _PW_FogColor.w);
}

#endif
