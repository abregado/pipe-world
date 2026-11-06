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

// Value noise from a hash, so no noise texture is needed.
float PW_Hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float PW_ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = PW_Hash(i);
    float b = PW_Hash(i + float2(1, 0));
    float c = PW_Hash(i + float2(0, 1));
    float d = PW_Hash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Samples a tiling texture without a visible repeat: low-frequency noise picks one of eight offsets of the texture,
// and neighbouring offsets are blended where the noise moves between them, weighted toward the darker sample so the
// seam follows the texture's own shapes. Two samples, with gradients taken from the unshifted UV so mips stay correct.
half3 PW_SampleNoTile(TEXTURE2D_PARAM(tex, samp), float2 uv, float seed)
{
    float k = PW_ValueNoise(uv * 0.6 + seed);
    float l = k * 8.0;
    float ia = floor(l);
    float f = frac(l);
    float2 offsetA = sin(float2(3.0, 7.0) * (ia + seed));
    float2 offsetB = sin(float2(3.0, 7.0) * (ia + 1.0 + seed));
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    half3 a = SAMPLE_TEXTURE2D_GRAD(tex, samp, uv + offsetA, dx, dy).rgb;
    half3 b = SAMPLE_TEXTURE2D_GRAD(tex, samp, uv + offsetB, dx, dy).rgb;
    return lerp(a, b, smoothstep(0.2, 0.8, f - 0.1 * dot(a - b, half3(1, 1, 1))));
}

#endif
