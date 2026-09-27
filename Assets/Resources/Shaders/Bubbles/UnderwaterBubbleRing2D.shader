Shader "DontSink/UnderwaterBubbleRing2D"
{
    Properties
    {
        _RingColor ("Ring Color", Color) = (0.92, 0.99, 1.0, 1)
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.06
        _RingRadius ("Ring Radius", Range(0.2, 0.49)) = 0.38
        _RingThickness ("Ring Thickness", Range(0.01, 0.25)) = 0.075
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.12)) = 0.025
        _HighlightStrength ("Highlight Strength", Range(0, 2)) = 0.55
        [HideInInspector] _SceneBrightness ("Scene Brightness", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BubbleRing"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _RingColor;
                half _FillAlpha;
                half _RingRadius;
                half _RingThickness;
                half _EdgeSoftness;
                half _HighlightStrength;
                half _SceneBrightness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz);

                output.color =
                    input.color;

                output.uv =
                    input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered =
                    input.uv -
                    0.5;

                float distanceFromCenter =
                    length(
                        centered);

                float radius =
                    max(
                        0.01,
                        _RingRadius);

                float thickness =
                    max(
                        0.001,
                        _RingThickness);

                float softness =
                    max(
                        0.0005,
                        _EdgeSoftness);

                float ringDistance =
                    abs(
                        distanceFromCenter -
                        radius);

                float ring =
                    1.0 -
                    smoothstep(
                        thickness,
                        thickness +
                        softness,
                        ringDistance);

                float fill =
                    1.0 -
                    smoothstep(
                        radius - thickness,
                        radius + softness,
                        distanceFromCenter);

                // Tiny upper-left highlight so even very small bubbles read as
                // bubbles rather than anonymous circles.
                float highlightDistance =
                    length(
                        centered -
                        float2(
                            -0.16,
                            0.16));

                float highlight =
                    1.0 -
                    smoothstep(
                        0.035,
                        0.10,
                        highlightDistance);

                float alpha =
                    saturate(
                        ring +
                        fill *
                        _FillAlpha +
                        highlight *
                        _HighlightStrength);

                alpha *=
                    input.color.a *
                    _RingColor.a;

                clip(
                    alpha -
                    0.002);

                float3 color =
                    _RingColor.rgb *
                    input.color.rgb;

                color +=
                    highlight *
                    _HighlightStrength *
                    0.35;

                // This procedural shader is intentionally unlit, so URP Light2D
                // sorting-layer inclusion alone cannot affect it. The bubble
                // controller supplies the same global Brightness01 used to drive
                // the scene's Global Light 2D.
                color *=
                    saturate(
                        _SceneBrightness);

                return
                    half4(
                        color,
                        alpha);
            }

            ENDHLSL
        }
    }
}
