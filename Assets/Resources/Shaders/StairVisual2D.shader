Shader "Custom/StairVisual2D"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.82, 0.76, 0.58, 1)
        _LineColor("Line Color", Color) = (0.22, 0.18, 0.12, 1)
        _ShadowColor("Shadow Color", Color) = (0.65, 0.60, 0.45, 1)

        _StepCount("Step Count", Float) = 8
        _LineThickness("Line Thickness", Range(0.001, 0.08)) = 0.02
        _AltShadeStrength("Alt Shade Strength", Range(0, 1)) = 0.08
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 0.10

        _AscendRight("Ascend Right", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _LineColor;
                half4 _ShadowColor;
                float _StepCount;
                float _LineThickness;
                float _AltShadeStrength;
                float _ShadowStrength;
                float _AscendRight;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float LineMask(float coord, float count, float thickness)
            {
                float f = frac(coord * count);
                float d = min(f, 1.0 - f);
                return 1.0 - smoothstep(0.0, thickness, d);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float steps = max(1.0, _StepCount);

                // Flip U depending on stair direction so pattern mirrors properly.
                float u = IN.uv.x;
                float v = IN.uv.y;

                // Horizontal tread-ish lines.
                float treadLines = LineMask(v, steps, _LineThickness);

                // Vertical riser-ish lines.
                // Not mathematically perfect, but it reads like steps.
                float riserLines = LineMask(u, steps, _LineThickness * 0.85);

                // Make risers feel more "step-like" by emphasizing them below the current step band.
                float currentBandTop = ceil(u * steps) / steps;
                riserLines *= step(v, currentBandTop + _LineThickness);

                float lineMask = saturate(max(treadLines, riserLines * 0.85));

                // Alternating step band shading.
                float bandIndex = floor(v * steps);
                float alt = fmod(bandIndex, 2.0);
                float altShade = lerp(1.0, 1.0 - _AltShadeStrength, alt);

                // Mild overall shadow gradient.
                float shadowT = saturate(v * 0.65 + u * 0.20);
                half3 baseRgb = _BaseColor.rgb * altShade;
                baseRgb = lerp(baseRgb, _ShadowColor.rgb, shadowT * _ShadowStrength);

                half3 finalRgb = lerp(baseRgb, _LineColor.rgb, lineMask);

                return half4(finalRgb, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}