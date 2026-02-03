Shader "Unlit/SunriseSunset2D"
{
    Properties
    {
        _SunriseColor("Sunrise Color", Color) = (1.0, 0.5, 0.2, 1)
        _SunsetColor("Sunset Color", Color) = (1.0, 0.2, 0.1, 1)
        _GradientPower("Gradient Power", Range(0.1, 5.0)) = 2.0
        _Intensity("Intensity", Range(0.0, 2.0)) = 1.0
        _Brightness("Brightness", Range(0.0, 2.0)) = 1.0
        _Alpha("Overall Alpha", Range(0.0,1.0)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _SunriseColor;
            float4 _SunsetColor;
            float _GradientPower;
            float _Intensity;
            float _Brightness;
            float _Alpha;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Vertical gradient factor (0 = horizon, 1 = top)
                float v = saturate(uv.y);

                // Gradient from horizon to sky
                float3 color = lerp(_SunriseColor.rgb, _SunsetColor.rgb, pow(v, _GradientPower));

                // Apply intensity and brightness
                color *= _Intensity;
                color *= _Brightness;

                // Fade alpha toward top
                float alpha = (1.0 - v) * _Alpha;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}
