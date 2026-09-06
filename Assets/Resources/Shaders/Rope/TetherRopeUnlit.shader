Shader "DontSink/TetherRopeUnlit"
{
    Properties
    {
        _BaseColor ("Rope Color", Color) = (0.58, 0.43, 0.25, 1)
        _DarkColor ("Twist Shadow", Color) = (0.28, 0.18, 0.09, 1)
        _Repeat ("Twist Repeats", Float) = 18
        _Twist ("Diagonal Twist", Float) = 1.5
        _BandSharpness ("Band Sharpness", Range(0.5, 8)) = 2.5
        _EdgeDarkening ("Edge Darkening", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "TetherRopeUnlit"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DarkColor;
                float _Repeat;
                float _Twist;
                float _BandSharpness;
                float _EdgeDarkening;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float phase =
                    (input.uv.x * _Repeat) +
                    (input.uv.y * _Twist);

                float wave =
                    0.5 + 0.5 *
                    sin(phase * TWO_PI);

                float band =
                    pow(saturate(wave), _BandSharpness);

                half4 ropeColor =
                    lerp(_DarkColor, _BaseColor, band);

                // Slightly darken the top/bottom edges so a thin LineRenderer
                // reads as round rope rather than a flat ribbon.
                float edge =
                    abs(input.uv.y * 2.0 - 1.0);

                ropeColor.rgb *=
                    lerp(
                        1.0,
                        1.0 - _EdgeDarkening,
                        edge);

                return ropeColor;
            }

            ENDHLSL
        }
    }
}
