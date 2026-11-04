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
            float _PW_DebugLights;
            // 1 when each torch's grade is masked by the light the pixel actually received (alpha of the colour buffer).
            float _PW_GradeUseLightMask;

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

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half3 color = source.rgb;
                // The lighting shaders write how much shadowed point light each pixel received; see PW_LightMask.
                half received = _PW_GradeUseLightMask > 0.5 ? source.a : 1.0;

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

                    // Debug view 3: the light mask the grade is using, as grey.
                    if (_PW_DebugLights > 2.5)
                        return half4(received.xxx, 1);

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
                        float weight = falloff * _PW_GradeLightTint[i].a;
                        mask += weight;
                        tint += _PW_GradeLightTint[i].rgb * weight;
                        warm += _PW_GradeLightParams[i].x * weight;
                        glow += _PW_GradeLightParams[i].y * weight;
                    }

                    // Shadowed pixels keep the night grade: the lights' falloff is multiplied by the light that reached them.
                    float influence = saturate(mask) * received * saturate(_LightGradeStrength);
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
