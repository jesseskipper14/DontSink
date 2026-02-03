Shader "Unlit/SeaForeground2D"
{
    Properties
    {
        _DeepColor ("Deep Sea Color", Color) = (0.0, 0.25, 0.45, 1)
        _HorizonColor ("Horizon Color", Color) = (0.45, 0.75, 0.95, 1)
        _GradientPower ("Gradient Power", Range(0.3, 3.0)) = 1.2
        _WaveScale ("Wave Scale", Float) = 5.0
        _WaveStrength ("Wave Strength", Float) = 0.18
        _WaveSpeed ("Wave Speed", Float) = 0.25
        _BGWaveStrength ("Background Wave Strength", Range(0, 0.5)) = 0.08
        _BGWaveScale ("Background Wave Scale", Float) = 2.0
        _SparkleIntensity ("Sparkle Intensity", Float) = 1.2
        _FoamIntensity ("Foam Intensity", Float) = 0.7
        _SparkleDensity ("Sparkle Density", Float) = 35.0
        _SparkleSpeed ("Sparkle Speed", Float) = 2.0
        _SparkleFalloffSharpness ("Sparkle Falloff Sharpness", Range(0.5, 4.0)) = 2.5
        _Brightness ("Brightness", Range(0.0, 2.0)) = 1.0
        _Tint ("Color Tint", Color) = (1,1,1,1)
        _BGWaveNoiseScale ("Background Wave Noise Scale", Float) = 12.0
        _BGWaveNoiseStrength ("Background Wave Noise Strength", Float) = 0.06
        _BGWaveNoiseSpeed ("Background Wave Noise Speed", Float) = 0.1
        _BGWaveTint ("Background Wave Tint", Color) = (0.0,0.35,0.6,1)
    }

    SubShader
    {
        Pass
        {
            Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            Stencil
            {
                Ref 2
                Comp NotEqual
                Pass Keep
            }

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

            float4 _DeepColor;
            float4 _HorizonColor;
            float _GradientPower;
            float _WaveScale;
            float _WaveStrength;
            float _WaveSpeed;
            float _SparkleIntensity;
            float _FoamIntensity;
            float _SparkleDensity;
            float _SparkleSpeed;
            float _SparkleFalloffSharpness;
            float _Brightness;
            float4 _Tint;
            float _BGWaveStrength;
            float _BGWaveScale;
            float _BGWaveNoiseScale;
            float _BGWaveNoiseStrength;
            float _BGWaveNoiseSpeed;
            float4 _BGWaveTint;

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f*f*(3.0-2.0*f);
                return lerp(lerp(hash(i), hash(i+float2(1,0)), u.x),
                            lerp(hash(i+float2(0,1)), hash(i+float2(1,1)), u.x), u.y);
            }

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
                float v = saturate(1.0 - uv.y);

                // ---- Gradient ----
                float grad = pow(v, _GradientPower);
                float3 color = lerp(_HorizonColor.rgb, _DeepColor.rgb, grad);

                float time = _Time.y * _WaveSpeed;

                // ---- Cartoony waves ----
                float2 waveUV1 = uv * float2(_WaveScale, _WaveScale * 0.8);
                waveUV1.x += time;

                float2 waveUV2 = uv * float2(_WaveScale * 1.8, _WaveScale);
                waveUV2.x -= time * 1.3;

                float waves = noise(waveUV1)*0.6 + noise(waveUV2)*0.4;
                waves = sin(waves * 6.2831);
                waves *= lerp(0.5, 1.0, v);
                color += waves * _WaveStrength;

                // ---- Background waves (more horizontal) ----
                float bgWaveScale = _BGWaveScale;
                float bgWaveStrength = _BGWaveStrength;

                // Stretch X more, compress Y more
                float2 bgWaveUV = uv * float2(bgWaveScale * 2.0, bgWaveScale * 0.2);

                // Optional: shrink waves as we move up (fade effect)
                float distanceFactor = pow(v, 2.0); 
                bgWaveUV *= lerp(1.0, 0.3, distanceFactor); 
                bgWaveUV.y *= lerp(1.0, 0.1, distanceFactor); // smaller vertical influence

                // Animate slowly over time
                bgWaveUV.x += time * 0.15;
                bgWaveUV.y += time * 0.05;

                // Sample noise and apply sine for smooth horizontal waves
                float bgWave = sin(noise(bgWaveUV) * 6.2831);
                color += bgWave * bgWaveStrength;

                // ---- Background subtle noise waves ----
                float2 bgUV = uv * float2(_BGWaveNoiseScale * 0.1, _BGWaveNoiseScale * 5); // stretch X, compress Y
                bgUV.x += _Time.y * _BGWaveNoiseSpeed;
                bgUV.y += _Time.y * _BGWaveNoiseSpeed * 0.1;

                float bgNoise = noise(bgUV);
                bgNoise = sin(bgNoise * 6.2831) * 0.5 + 0.5;

                // Fade waves toward top
                float fade = pow(1.0 - v, 2.0);
                color = lerp(color, _BGWaveTint.rgb, bgNoise * _BGWaveNoiseStrength * fade);

                // ---- Horizontal sparkles ----
                float sparkleSpeed = lerp(0.6, 1.6, v);
                float lifetimeTime = _Time.y * 0.35;

                float2 sparkleUV = uv * float2(_SparkleDensity * 0.02, _SparkleDensity * 4); 
                float sparklePattern = noise(sparkleUV + 0.5) * noise(sparkleUV);
                float sparkleLife = noise(sparkleUV + lifetimeTime);
                float sparkles = smoothstep(0.45, 0.65, sparklePattern * sparkleLife);

                float cutoff = 0.33;
                float topThird = saturate((cutoff - v)/cutoff);
                float slowFalloff = lerp(1.0, v, 0.3);
                float fastFalloff = pow(1.0 - topThird, _SparkleFalloffSharpness);
                float sparkleFalloff = slowFalloff * fastFalloff;

                sparkles *= sparkleFalloff;
                color += float3(1,1,0.95) * sparkles * _SparkleIntensity;

                // ---- Horizontal foam ----
                float foamSpeed = lerp(0.6, 1.6, v);
                float foamLifetime = _Time.y * 0.35;

                float2 foamUV = uv * float2(_SparkleDensity * 0.1, _SparkleDensity * 4); 
                float foamPattern = noise(foamUV + 0.5) * noise(foamUV);
                float foamLife = noise(foamUV + foamLifetime);
                float foam = smoothstep(0.45, 0.65, foamPattern * foamLife);

                float foamCutoff = 0.33;
                float foamTop = saturate((foamCutoff - v)/foamCutoff);
                float foamSlowFalloff = lerp(1.0, v, 0.3);
                float foamFastFalloff = pow(1.0 - foamTop, _SparkleFalloffSharpness);
                float foamFalloff = foamSlowFalloff * foamFastFalloff;

                foam *= foamFalloff;
                color += float3(0.85, 0.9, 1.0) * foam * _FoamIntensity;

                // ---- Apply tint and brightness ----
                color *= _Tint.rgb;
                color *= _Brightness;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
