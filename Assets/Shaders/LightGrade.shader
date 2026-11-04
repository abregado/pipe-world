Shader "PipeWorld/LightGrade"
{
    Properties
    {
        _NightLut ("Night LUT", 2D) = "white" {}
        _WarmLut ("Warm LUT", 2D) = "white" {}
        _LightGradeStrength ("Light Grade Strength", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "LightGrade"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define MAX_GRADE_LIGHTS 32

            // Set by LightGradeSystem: position + radius, tint + strength, and (warm LUT amount, glow, unused, unused).
            float4 _PW_GradeLightPos[MAX_GRADE_LIGHTS];
            float4 _PW_GradeLightTint[MAX_GRADE_LIGHTS];
            float4 _PW_GradeLightParams[MAX_GRADE_LIGHTS];
            float _PW_GradeLightCount;
            float _PW_NightFactor;

            TEXTURE2D(_NightLut);
            TEXTURE2D(_WarmLut);
            float _LightGradeStrength;

            #define LUT_SIZE 32.0

            // 2D strip LUT (LUT_SIZE slices of LUT_SIZE x LUT_SIZE laid side by side), looked up in gamma space.
            half3 ApplyLut(TEXTURE2D_PARAM(lut, samp), half3 linearColor)
            {
                half3 c = saturate(LinearToSRGB(linearColor));
                float blue = c.b * (LUT_SIZE - 1.0);
                float slice0 = floor(blue);
                float slice1 = min(slice0 + 1.0, LUT_SIZE - 1.0);
                float t = blue - slice0;
                float v = (c.g * (LUT_SIZE - 1.0) + 0.5) / LUT_SIZE;
                float u0 = (slice0 * LUT_SIZE + c.r * (LUT_SIZE - 1.0) + 0.5) / (LUT_SIZE * LUT_SIZE);
                float u1 = (slice1 * LUT_SIZE + c.r * (LUT_SIZE - 1.0) + 0.5) / (LUT_SIZE * LUT_SIZE);
                half3 a = SAMPLE_TEXTURE2D_LOD(lut, samp, float2(u0, v), 0).rgb;
                half3 b = SAMPLE_TEXTURE2D_LOD(lut, samp, float2(u1, v), 0).rgb;
                return SRGBToLinear(lerp(a, b, t));
            }

            // Screen-space shadow: march from the pixel toward the light and test each sample against the depth buffer.
            // A sample counts as blocked when the visible surface is slightly in front of it, which means the ray has passed
            // behind geometry. Only occluders that are on screen can block, and thin ones can slip between samples.
            #define SHADOW_STEPS 12

            float LightVisibility(float3 world, float3 lightPos, float2 pixel)
            {
                float jitter = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))) * 0.5;
                float3 start = world + normalize(lightPos - world) * 0.2;
                float blocked = 0.0;

                [loop] for (int s = 0; s < SHADOW_STEPS; s++)
                {
                    float3 p = lerp(start, lightPos, ((float)s + jitter) / SHADOW_STEPS * 0.96);
                    float2 suv = ComputeNormalizedDeviceCoordinates(p, UNITY_MATRIX_VP);
                    if (suv.x < 0.0 || suv.x > 1.0 || suv.y < 0.0 || suv.y > 1.0)
                        continue;

                    float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float sampleEye = -mul(UNITY_MATRIX_V, float4(p, 1.0)).z;
                    float behind = sampleEye - sceneEye;
                    float bias = 0.1 + 0.004 * sampleEye;
                    float thickness = 1.2 + 0.02 * sampleEye;

                    // Soft on both ends so shadow edges fade across a few samples instead of stippling.
                    float hit = smoothstep(bias, bias * 3.0, behind) * (1.0 - smoothstep(thickness * 0.6, thickness, behind));
                    blocked = max(blocked, hit);
                }
                return 1.0 - blocked;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                float depth = SampleSceneDepth(uv);
#if UNITY_REVERSED_Z
                bool isSky = depth <= 0.0;
#else
                bool isSky = depth >= 1.0;
#endif

                float night = _PW_NightFactor;
                half3 graded = lerp(color, ApplyLut(_NightLut, sampler_LinearClamp, color), night);

                if (!isSky && _PW_GradeLightCount > 0.5)
                {
                    float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                    float mask = 0.0;
                    float3 tint = 0.0;
                    float warm = 0.0;
                    float glow = 0.0;
                    int count = (int)_PW_GradeLightCount;
                    [loop] for (int i = 0; i < count; i++)
                    {
                        float4 pos = _PW_GradeLightPos[i];
                        float falloff = saturate(1.0 - distance(world, pos.xyz) / max(pos.w, 0.001));
                        falloff = falloff * falloff * (3.0 - 2.0 * falloff);
                        if (falloff > 0.01 && _PW_GradeLightParams[i].z > 0.5)
                            falloff *= LightVisibility(world, pos.xyz, input.positionCS.xy);
                        float weight = falloff * _PW_GradeLightTint[i].a;
                        mask += weight;
                        tint += _PW_GradeLightTint[i].rgb * weight;
                        warm += _PW_GradeLightParams[i].x * weight;
                        glow += _PW_GradeLightParams[i].y * weight;
                    }

                    float influence = saturate(mask) * saturate(_LightGradeStrength);
                    if (influence > 0.0001)
                    {
                        float inv = 1.0 / max(mask, 1e-4);
                        half3 lightTint = tint * inv;
                        float warmAmount = saturate(warm * inv);

                        // Lit pixels replace the night grade with a warm-LUT or tinted version of the un-graded colour.
                        half3 warmed = lerp(color, ApplyLut(_WarmLut, sampler_LinearClamp, color), warmAmount);
                        half3 lit = warmed * lerp(half3(1, 1, 1), lightTint * 1.6, 0.6);

                        // Daytime light grading is faint; at night it takes over.
                        float strength = influence * lerp(0.2, 0.8, night);
                        graded = lerp(graded, lit, strength);

                        // Added on top so lit areas stay visible when distance fog has flattened them to the far colour.
                        graded += lightTint * (glow * inv) * (influence * _LightGradeStrength) * lerp(0.35, 1.0, night);
                    }
                }

                return half4(graded, 1);
            }
            ENDHLSL
        }
    }
}
