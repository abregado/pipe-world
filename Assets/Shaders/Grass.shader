Shader "PipeWorld/Grass"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _WindStrength ("Wind Strength (m at tip)", Range(0, 0.5)) = 0.14
        _WindSpeed ("Wind Speed", Range(0, 6)) = 1.6
        _FadeStart ("Shrink Start (m)", Float) = 45
        _FadeEnd ("Shrink End (m)", Float) = 85
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "GrassProps.hlsl"
            #include "PipeWorldLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 sway : TEXCOORD0;
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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float weight = input.sway.x;

                float t = _Time.y * _WindSpeed;
                float2 wave = float2(sin(t + positionWS.x * 0.35 + positionWS.z * 0.2),
                                     cos(t * 0.8 + positionWS.z * 0.3 + positionWS.x * 0.1));
                float gust = sin(t * 0.37 + positionWS.x * 0.05) * 0.5 + 0.5;
                positionWS.xz += wave * _WindStrength * weight * (0.5 + gust);

                // Blades sink toward the ground with distance so they never pop out of existence.
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, distance(positionWS, _WorldSpaceCameraPos));
                positionWS.y -= weight * 0.55 * (1.0 - fade);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // Blades are flat, so lighting leans toward up to read as soft tufts, and the back face is flipped to match.
                float3 n = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                n = normalize(lerp(n, float3(0, 1, 0), 0.65));
                half3 albedo = SRGBToLinear(input.color.rgb) * _Tint.rgb;
                half4 lit = PW_ShadeMasked(albedo, n, input.positionWS, input.positionCS);
                return half4(PW_ApplyFog(lit.rgb, input.positionWS), lit.a);
            }
            ENDHLSL
        }
    }
}
