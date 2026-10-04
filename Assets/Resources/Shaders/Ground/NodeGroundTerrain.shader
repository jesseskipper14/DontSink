Shader "DontSink/NodeGroundTerrain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Terrain Colors)]
        _DryHighColor ("Dry / Sunlit Color", Color) = (0.42, 0.36, 0.24, 1)
        _DryLowColor ("Dry / Low Color", Color) = (0.30, 0.28, 0.20, 1)
        _ShoreColor ("Wet Shore Color", Color) = (0.20, 0.23, 0.18, 1)
        _UnderwaterColor ("Shallow Underwater Color", Color) = (0.22, 0.28, 0.24, 1)
        _DeepColor ("Deep Underwater Color", Color) = (0.12, 0.18, 0.19, 1)

        [Header(Waterline)]
        _WaterLevelY ("Manual Water Level Y", Float) = 0
        [Toggle] _UseGlobalWaterLevel ("Use Global Water Level", Float) = 1
        _ShoreWidth ("Wet Shore Half Width", Range(0.01, 2)) = 0.28
        _DeepFadeDepth ("Depth To Deep Color", Range(0.1, 30)) = 8
        _DryHeightRange ("Dry Height Color Range", Range(0.1, 20)) = 5

        [Header(World Pattern)]
        _PatternPixelSize ("Pattern Pixel Size", Range(0.01, 1)) = 0.08
        _LargeNoiseScale ("Large Noise Scale", Range(0.02, 4)) = 0.35
        _SmallNoiseScale ("Small Noise Scale", Range(0.1, 20)) = 3.0
        _NoiseStrength ("Noise Strength", Range(0, 0.5)) = 0.14
        [Toggle] _UseObjectPattern ("Anchor Pattern To Object", Float) = 0
        _ObjectPatternScale ("Object Pattern Reference Scale", Vector) = (1,1,0,0)

        [Header(Strata)]
        _StrataFrequency ("Strata Frequency", Range(0, 12)) = 2.0
        _StrataWarp ("Strata Warp", Range(0, 4)) = 0.75
        _StrataStrength ("Strata Strength", Range(0, 0.35)) = 0.08

        [Header(Pebble Flecks)]
        _PebbleScale ("Pebble Scale", Range(0.2, 20)) = 5
        _PebbleAmount ("Pebble Amount", Range(0, 1)) = 0.12
        _PebbleDarken ("Pebble Darken", Range(0, 0.8)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "NodeGroundTerrain"

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
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float2 positionWS : TEXCOORD1;
                float2 patternOS : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;

                float4 _DryHighColor;
                float4 _DryLowColor;
                float4 _ShoreColor;
                float4 _UnderwaterColor;
                float4 _DeepColor;

                float _WaterLevelY;
                float _UseGlobalWaterLevel;
                float _ShoreWidth;
                float _DeepFadeDepth;
                float _DryHeightRange;

                float _PatternPixelSize;
                float _LargeNoiseScale;
                float _SmallNoiseScale;
                float _NoiseStrength;
                float _UseObjectPattern;
                float4 _ObjectPatternScale;

                float _StrataFrequency;
                float _StrataWarp;
                float _StrataStrength;

                float _PebbleScale;
                float _PebbleAmount;
                float _PebbleDarken;
            CBUFFER_END

            // Updated by NodeGroundWaterlineGlobal when desired.
            float _DontSinkWaterLevelY;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i + float2(0, 0));
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                return lerp(
                    lerp(a, b, f.x),
                    lerp(c, d, f.x),
                    f.y);
            }

            float Fbm3(float2 p)
            {
                float result = 0.0;
                float amplitude = 0.57;

                result += ValueNoise(p) * amplitude;
                p = p * 2.03 + 17.17;
                amplitude *= 0.5;

                result += ValueNoise(p) * amplitude;
                p = p * 2.01 + 9.31;
                amplitude *= 0.5;

                result += ValueNoise(p) * amplitude;

                return result;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS =
                    TransformObjectToWorld(input.positionOS.xyz);

                output.positionHCS =
                    TransformWorldToHClip(positionWS);

                output.positionWS =
                    positionWS.xy;

                output.uv =
                    input.uv;

                output.patternOS = input.positionOS.xy;
                output.color =
                    input.color;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        input.uv);

                float alpha =
                    tex.a *
                    input.color.a *
                    _Color.a;

                clip(alpha - 0.001);

                float waterY =
                    lerp(
                        _WaterLevelY,
                        _DontSinkWaterLevelY,
                        step(
                            0.5,
                            _UseGlobalWaterLevel));

                // Quantize the procedural lookup slightly so the material reads
                // comfortably beside pixel-art sprites instead of becoming
                // suspiciously photorealistic dirt.
                float pixelSize =
                    max(
                        0.001,
                        _PatternPixelSize);

                float2 p =
                    floor(
                        lerp(input.positionWS, input.patternOS * _ObjectPatternScale.xy, step(0.5, _UseObjectPattern)) /
                        pixelSize) *
                    pixelSize;

                float largeNoise =
                    Fbm3(
                        p *
                        _LargeNoiseScale);

                float smallNoise =
                    ValueNoise(
                        p *
                        _SmallNoiseScale +
                        largeNoise * 3.17);

                float combinedNoise =
                    ((largeNoise - 0.5) * 0.75 +
                     (smallNoise - 0.5) * 0.25);

                float depth =
                    waterY -
                    input.positionWS.y;

                float submerged =
                    smoothstep(
                        -_ShoreWidth,
                        _ShoreWidth,
                        depth);

                float dryHeight01 =
                    saturate(
                        max(
                            input.positionWS.y - waterY,
                            0.0) /
                        max(
                            0.001,
                            _DryHeightRange));

                float3 dryColor =
                    lerp(
                        _DryLowColor.rgb,
                        _DryHighColor.rgb,
                        dryHeight01);

                float underwaterDepth01 =
                    saturate(
                        max(
                            depth,
                            0.0) /
                        max(
                            0.001,
                            _DeepFadeDepth));

                float3 underwaterColor =
                    lerp(
                        _UnderwaterColor.rgb,
                        _DeepColor.rgb,
                        underwaterDepth01);

                float3 terrainColor =
                    lerp(
                        dryColor,
                        underwaterColor,
                        submerged);

                // Dark damp band straddling the shoreline. This visually ties
                // the same terrain material together above and below the water.
                float shoreDistance =
                    abs(depth);

                float shoreMask =
                    1.0 -
                    smoothstep(
                        _ShoreWidth * 0.25,
                        _ShoreWidth * 1.35,
                        shoreDistance);

                terrainColor =
                    lerp(
                        terrainColor,
                        _ShoreColor.rgb,
                        shoreMask * 0.9);

                // Broad world-space mottling.
                terrainColor *=
                    1.0 +
                    combinedNoise *
                    _NoiseStrength;

                // Slightly warped horizontal sediment / soil bands.
                if (_StrataFrequency > 0.001)
                {
                    float strataPhase =
                        (p.y +
                         (largeNoise - 0.5) *
                         _StrataWarp) *
                        _StrataFrequency;

                    float strata =
                        0.5 +
                        0.5 *
                        sin(strataPhase);

                    strata =
                        smoothstep(
                            0.38,
                            0.62,
                            strata);

                    terrainColor *=
                        lerp(
                            1.0 - _StrataStrength,
                            1.0 + _StrataStrength,
                            strata);
                }

                // Sparse, tiny dark flecks. They are deliberately procedural
                // cells rather than smooth circles so they fit the art style.
                float pebbleNoise =
                    ValueNoise(
                        floor(
                            p *
                            _PebbleScale));

                float pebbleThreshold =
                    1.0 -
                    saturate(
                        _PebbleAmount) *
                    0.28;

                float pebble =
                    step(
                        pebbleThreshold,
                        pebbleNoise);

                terrainColor *=
                    1.0 -
                    pebble *
                    _PebbleDarken;

                terrainColor *=
                    tex.rgb *
                    input.color.rgb *
                    _Color.rgb;

                return
                    half4(
                        terrainColor,
                        alpha);
            }

            ENDHLSL
        }
    }
}
