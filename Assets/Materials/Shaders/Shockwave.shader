Shader "FF/Effects/Shockwave2D"
{
    Properties
    {
        _DistortionTex ("Distortion Map", 2D) = "gray" {}
        _Center ("Wave Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _TimeValue ("Wave Time", Float) = 0
        _Radius ("Wave Radius", Float) = 0.2
        _Thickness ("Wave Thickness", Float) = 0.1
        _Strength ("Distortion Strength", Float) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            TEXTURE2D(_DistortionTex);
            SAMPLER(sampler_DistortionTex);

            float4 _Center;
            float _TimeValue;
            float _Radius;
            float _Thickness;
            float _Strength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                // Distance from shockwave center
                float d = distance(uv, _Center.xy);

                // Wave mask (thin ring)
                float ring = smoothstep(_Radius, _Radius - _Thickness, d);

                // Distortion strength based on wave
                float distortPower = ring * _Strength;

                // Sample distortion map
                float2 distortion = (SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, uv).rg - 0.5) * 2.0;

                // Apply distortion scaled by ring strength
                float2 finalUV = uv + distortion * distortPower;

                // Sample the 2D renderer’s camera texture
                float4 col = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, finalUV);

                // Keep transparent background so the quad isn't visible
                col.a = ring;

                return col;
            }
            ENDHLSL
        }
    }
}
