Shader "PipeWorld/GradientSky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.4, 0.6, 0.9, 1)
        _HorizonColor ("Horizon", Color) = (0.8, 0.9, 1, 1)
        _GroundColor ("Ground", Color) = (0.7, 0.8, 0.9, 1)
        _HorizonFalloff ("Horizon Falloff", Range(0.1, 4)) = 0.6
        _SunColor ("Sun Color", Color) = (1, 0.95, 0.85, 1)
        _SunSize ("Sun Size", Range(0.001, 0.2)) = 0.04
        _SunGlow ("Sun Glow", Range(0, 1)) = 0.25
        _SunDir ("Sun Direction", Vector) = (0, 1, 0, 0)
        _Bands ("Gradient Bands", Range(2, 32)) = 10
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor;
            half _HorizonFalloff, _SunSize, _SunGlow, _Bands;
            float4 _SunDir;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sunDir = normalize(_SunDir.xyz);

                float up = saturate(d.y);
                float down = saturate(-d.y * 3.0);
                float skyT = pow(up, _HorizonFalloff);
                skyT = min(floor(skyT * _Bands), _Bands - 1.0) / (_Bands - 1.0);
                down = floor(down * 3.0 + 0.5) / 3.0;
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, skyT);
                col = lerp(col, _GroundColor.rgb, down);

                float s = saturate(dot(d, sunDir));
                float aboveHorizon = saturate(d.y * 20.0);
                float disc = smoothstep(1.0 - _SunSize * 0.05, 1.0 - _SunSize * 0.04, s) * aboveHorizon;
                float glow = floor(pow(s, 24.0) * 5.0) / 5.0 * _SunGlow;
                col += _SunColor.rgb * (glow + disc * 4.0);

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
