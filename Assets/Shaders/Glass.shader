Shader "PipeWorld/Glass"
{
    // See-through glass for windows and glass walls: an alpha-blended tint that turns more opaque at grazing angles, so a pane reads
    // as glass from the side and clear head-on. Not lit, so it looks the same by day and by night.
    Properties
    {
        _BaseColor ("Tint", Color) = (0.62, 0.82, 0.92, 0.22)
        _EdgeAlpha ("Edge alpha", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "Glass"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _EdgeAlpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetCameraPositionWS() - positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half facing = abs(dot(normalize(input.normalWS), normalize(input.viewWS)));
                half fresnel = pow(1.0 - saturate(facing), 3.0);
                half alpha = lerp(_BaseColor.a, _EdgeAlpha, fresnel);
                half3 color = lerp(_BaseColor.rgb, half3(1, 1, 1), fresnel * 0.5);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
