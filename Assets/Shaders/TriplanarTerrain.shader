Shader "PipeWorld/TriplanarTerrain"
{
    Properties
    {
        _TopTex ("Top Texture (walkable)", 2D) = "white" {}
        _SideTex ("Side Texture (cliff)", 2D) = "gray" {}
        _TopTint ("Top Tint", Color) = (1, 1, 1, 1)
        _SideTint ("Side Tint", Color) = (1, 1, 1, 1)
        _TopScale ("Top Tile Size (m)", Float) = 8
        _SideScale ("Side Tile Size (m)", Float) = 8
        _WalkableCos ("Walkable Min Normal Y", Range(0, 1)) = 0.64
        _EdgeSoftness ("Walkable Edge Softness", Range(0.001, 0.2)) = 0.03
        _BlockedDarken ("Blocked Surface Brightness", Range(0, 1)) = 0.9
        _AccentColor ("Walkable Accent", Color) = (1, 1, 1, 1)
        _AccentStrength ("Walkable Accent Strength", Range(0, 1)) = 0.12
        _DetailFadeStart ("Texture Fade Start (m)", Float) = 150
        _DetailFadeEnd ("Texture Fade End (m)", Float) = 500
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "TerrainProps.hlsl"
            #include "PipeWorldLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 p = input.positionWS;

                float walkable = smoothstep(_WalkableCos - _EdgeSoftness, _WalkableCos + _EdgeSoftness, n.y);

                half3 top = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, p.xz / _TopScale).rgb;
                // A much larger copy of the same texture, relative to its average colour, breaks up visible tiling.
                half3 macro = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, p.xz / (_TopScale * 7.3) + 0.37).rgb;
                half3 mean = SAMPLE_TEXTURE2D_LOD(_TopTex, sampler_TopTex, float2(0.5, 0.5), 12).rgb;
                // A rotated, differently scaled second layer is blended in by the large-scale value so the repeat is never aligned.
                float2 rotated = float2(p.x * 0.8 - p.z * 0.6, p.x * 0.6 + p.z * 0.8) / (_TopScale * 1.9);
                half3 topB = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, rotated).rgb;
                top = lerp(top, topB, saturate(0.5 + (dot(macro, 0.3333) - dot(mean, 0.3333)) * 4.0));
                top *= clamp(1.0 + (dot(macro, 0.3333) - dot(mean, 0.3333)) * 1.2, 0.75, 1.25);
                half3 detail = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, p.xz / (_TopScale * 0.13)).rgb;
                top *= clamp(1.0 + (dot(detail, 0.3333) - dot(mean, 0.3333)) * 0.9, 0.88, 1.12);
                top *= _TopTint.rgb;
                top = lerp(top, top * _AccentColor.rgb, _AccentStrength);

                float3 w = pow(abs(n), 4.0);
                w /= (w.x + w.y + w.z + 1e-4);
                half3 sideX = SAMPLE_TEXTURE2D(_SideTex, sampler_SideTex, p.zy / _SideScale).rgb;
                half3 sideY = SAMPLE_TEXTURE2D(_SideTex, sampler_SideTex, p.xz / _SideScale).rgb;
                half3 sideZ = SAMPLE_TEXTURE2D(_SideTex, sampler_SideTex, p.xy / _SideScale).rgb;
                half3 side = (sideX * w.x + sideY * w.y + sideZ * w.z);
                half3 sideMacro = SAMPLE_TEXTURE2D(_SideTex, sampler_SideTex, p.xz / (_SideScale * 6.1) + 0.21).rgb;
                half3 sideMean = SAMPLE_TEXTURE2D_LOD(_SideTex, sampler_SideTex, float2(0.5, 0.5), 12).rgb;
                side *= clamp(1.0 + (dot(sideMacro, 0.3333) - dot(sideMean, 0.3333)) * 1.5, 0.75, 1.25);
                side *= _SideTint.rgb * _BlockedDarken;

                half3 albedo = lerp(side, top, walkable);

                // Far away, texture detail fades to each texture's average colour so distant terrain reads as flat shaded.
                half3 topFlat = mean * _TopTint.rgb;
                half3 sideFlat = sideMean * _SideTint.rgb * _BlockedDarken;
                half3 flatAlbedo = lerp(sideFlat, topFlat, walkable);
                float detailFade = smoothstep(_DetailFadeStart, _DetailFadeEnd, distance(p, _WorldSpaceCameraPos));
                albedo = lerp(albedo, flatAlbedo, detailFade);

                half4 lit = PW_ShadeMasked(albedo, n, p, input.positionCS);
                return half4(PW_ApplyFog(lit.rgb, p), lit.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PWShadowVert
            #pragma fragment PWShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "TerrainProps.hlsl"
            #include "PipeWorldPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PWDepthVert
            #pragma fragment PWDepthFrag

            #include "TerrainProps.hlsl"
            #include "PipeWorldPasses.hlsl"
            ENDHLSL
        }
    }
}
