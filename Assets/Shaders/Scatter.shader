Shader "PipeWorld/Scatter"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _GlowTint ("Glow Tint", Color) = (0.6, 0.95, 1, 1)
        _EmissionStrength ("Glow Strength (vertex alpha mask)", Range(0, 3)) = 0.6
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

            #include "ScatterProps.hlsl"
            #include "PipeWorldLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                half3 albedo = SRGBToLinear(input.color.rgb) * _Tint.rgb;
                half4 shaded = PW_ShadeMasked(albedo, n, input.positionWS, input.positionCS);
                half3 lit = PW_ApplyFog(shaded.rgb, input.positionWS);
                // Glow is added after fog so it stays visible at any distance.
                half3 glow = albedo * _GlowTint.rgb * (input.color.a * _EmissionStrength);
                // Glowing surfaces (flames, crystals) count as lit, so the grade does not dim them.
                return half4(lit + glow, max(shaded.a, input.color.a));
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

            #include "ScatterProps.hlsl"
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

            #include "ScatterProps.hlsl"
            #include "PipeWorldPasses.hlsl"
            ENDHLSL
        }
    }
}
