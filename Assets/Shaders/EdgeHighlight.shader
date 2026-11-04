Shader "PipeWorld/EdgeHighlight"
{
    Properties
    {
        _EdgeColor ("Edge Colour", Color) = (1, 0.96, 0.88, 1)
        _Strength ("Strength", Range(0, 2)) = 0.5
        _Threshold ("Curvature Threshold", Range(0.001, 1)) = 0.05
        _Thickness ("Thickness (pixels)", Range(0.5, 3)) = 1
        _FadeStart ("Fade Start (m)", Float) = 120
        _FadeEnd ("Fade End (m)", Float) = 500
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
            Name "EdgeHighlight"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _PW_FogColor;

            half4 _EdgeColor;
            float _Strength;
            float _Threshold;
            float _Thickness;
            float _FadeStart;
            float _FadeEnd;

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                // Alpha carries the light mask from the lighting shaders through to the LightGrade pass.
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half3 color = source.rgb;

                float2 texel = _Thickness / _ScreenParams.xy;
                float d0 = SampleSceneDepth(uv);
                float dl = SampleSceneDepth(uv - float2(texel.x, 0));
                float dr = SampleSceneDepth(uv + float2(texel.x, 0));
                float du = SampleSceneDepth(uv + float2(0, texel.y));
                float dd = SampleSceneDepth(uv - float2(0, texel.y));

                // Reversed-Z depth is affine in 1/z, so it is linear in screen space across any flat face:
                // its second difference is zero on a plane and large at silhouettes and creases.
                float curvature = max(abs(dl + dr - 2.0 * d0), abs(du + dd - 2.0 * d0));
                float nearest = max(max(d0, dl), max(max(dr, du), dd));
                float relative = curvature / max(nearest, 1e-5);
                float edge = smoothstep(_Threshold, _Threshold * 2.0, relative);

                float eyeDepth = LinearEyeDepth(nearest, _ZBufferParams);
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, eyeDepth);

                half3 tint = lerp(_EdgeColor.rgb, _PW_FogColor.rgb * 1.6, 0.35);
                return half4(color + tint * (edge * fade * _Strength), source.a);
            }
            ENDHLSL
        }
    }
}
