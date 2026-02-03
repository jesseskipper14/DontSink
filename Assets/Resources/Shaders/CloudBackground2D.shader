Shader "Custom/2DBackgroundClouds"
{
    Properties
    {
        _CloudColor ("Cloud Color", Color) = (1,1,1,1)
        _SkyColor ("Sky Color", Color) = (0.52, 0.75, 0.95, 1)

        _Scale ("Cloud Scale", Float) = 1.5
        _Speed ("Cloud Speed", Float) = 0.02
        _Coverage ("Cloud Coverage", Range(0,1)) = 0.45
        _Softness ("Edge Softness", Range(0.01,0.2)) = 0.08

        _VeryLowSpeed ("Very Low Clouds Speed", Float) = 0.0001
        _LowSpeed ("Low Clouds Speed", Float) = 0.01
        _LowMidSpeed ("Low-Mid Clouds Speed", Float) = 0.015
        _MidSpeed ("Mid Clouds Speed", Float) = 0.02
        _MidHighSpeed ("Mid-High Clouds Speed", Float) = 0.028
        _HighSpeed ("High Clouds Speed", Float) = 0.035

        _VeryLowScale ("Very Low Clouds Scale", Float) = 0.9
        _LowScale ("Low Clouds Scale", Float) = 0.9
        _LowMidScale ("Low-Mid Clouds Scale", Float) = 1.1  
        _MidScale ("Mid Clouds Scale", Float) = 1.4
        _MidHighScale ("Mid-High Clouds Scale", Float) = 1.8
        _HighScale ("High Clouds Scale", Float) = 2.1

        _UndersideDarkness ("Underside Darkness", Range(0,1)) = 0.3
        _Brightness ("Brightness", Range(0.1,2)) = 1.0
        _TintColor ("Sunrise/Sunset Tint", Color) = (1, 0.5, 0.3, 1)
        _TintStrength ("Tint Strength", Range(0,1)) = 0       
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

            float4 _CloudColor;
            float4 _SkyColor;
            float _Scale;
            float _Speed;
            float _Coverage;
            float _Softness;
            float _LowSpeed;
            float _MidSpeed;
            float _HighSpeed;

            float _LowScale;
            float _MidScale;
            float _HighScale;
            float _LowMidSpeed;
            float _LowMidScale;
            float _MidHighSpeed;
            float _MidHighScale;
            float _VeryLowSpeed;
            float _VeryLowScale;

            float _UndersideDarkness;
            float _Brightness;
            float4 _Tint;
            float4 _TintColor;
            float _TintStrength;


            // Hash
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(hash(i), hash(i + float2(1,0)), u.x),
                    lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), u.x),
                    u.y
                );
            }

            // Fractal Brownian Motion (THIS is the key)
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * noise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }

                return value;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                uv.x *= 1.2f; // CHANGE THIS FOR CLOUD STRETCH - HIGHER FOR FATTER, LOWER TO LONGER

                float cloudAlpha = 0.0;

                // -------- VERY LOW (Horizon) CLOUDS --------
                {
                    float2 cuv = uv;
                    cuv.x += _Time.y * _VeryLowSpeed;

                    float base  = fbm(cuv * _Scale * _VeryLowScale);
                    float detail = fbm(cuv * _Scale * _VeryLowScale * 3.0);

                    float cloud = base - detail * 0.2;
                    cloud = smoothstep(_Coverage, _Coverage + _Softness, cloud);
                    cloud *= smoothstep(-0.01, 0.02, uv.y) * (1.0 - smoothstep(-0.01, 0.4, uv.y));

                    cloudAlpha += cloud * 0.4;
                }

                // -------- LOW (far) CLOUDS --------
                {
                    float2 cuv = uv;
                    cuv.x += _Time.y * _LowSpeed;

                    float base  = fbm(cuv * _Scale * _LowScale);
                    float detail = fbm(cuv * _Scale * _LowScale * 3.0);

                    float cloud = base - detail * 0.1;
                    cloud = smoothstep(_Coverage, _Coverage + _Softness, cloud);
                    cloud *= smoothstep(0.02, 0.11, uv.y) * (1.0 - smoothstep(0.11, 0.3, uv.y));

                    cloudAlpha += cloud * 0.5;
                }

                // -------- LOW-MID CLOUDS --------
                {
                    float2 cuv = uv;
                    cuv.x += _Time.y * _LowMidSpeed;

                    float base  = fbm(cuv * _Scale * _LowMidScale);
                    float detail = fbm(cuv * _Scale * _LowMidScale * 3.0);

                    float cloud = base - detail * 0.1;
                    cloud = smoothstep(_Coverage, _Coverage + _Softness, cloud);

                    // Limit to low-mid band
                    cloud *= smoothstep(0.09, 0.21, uv.y) * (1.0 - smoothstep(0.21, 0.5, uv.y));

                    cloudAlpha += cloud * 0.65;
                }

                // -------- MID CLOUDS --------
                {
                    float2 cuv = uv;
                    cuv.x += _Time.y * _MidSpeed;

                    float base  = fbm(cuv * _Scale * _MidScale);
                    float detail = fbm(cuv * _Scale * _MidScale * 3.0);

                    float cloud = base - detail * 0.2;
                    cloud = smoothstep(_Coverage, _Coverage + _Softness, cloud);
                    cloud *= smoothstep(0.20, 0.4, uv.y);

                    cloudAlpha += cloud * 0.75;
                }

                // -------- MID-HIGH CLOUDS --------
                {
                    float2 cuv = uv;
                    cuv.x += _Time.y * _MidHighSpeed;

                    float base  = fbm(cuv * _Scale * _MidHighScale);
                    float detail = fbm(cuv * _Scale * _MidHighScale * 3.0);

                    float cloud = base - detail * 0.3;
                    cloud = smoothstep(_Coverage - 0.02, _Coverage + _Softness, cloud);

                    // Limit to mid-high band
                    cloud *= smoothstep(0.39, 0.55, uv.y) * (1.0 - smoothstep(0.55, 0.8, uv.y));

                    cloudAlpha += cloud * 0.8;
                }

                // -------- HIGH (near) CLOUDS --------
                {
                    float2 cuv = uv;
                    cuv.x += _Time.y * _HighSpeed;

                    float base  = fbm(cuv * _Scale * _HighScale);
                    float detail = fbm(cuv * _Scale * _HighScale * 3.0);

                    float cloud = base - detail * 0.4;
                    cloud = smoothstep(_Coverage - 0.05, _Coverage + _Softness, cloud);
                    cloud *= smoothstep(0.55, 0.9, uv.y);

                    cloudAlpha += cloud;
                }

                // --- UNDERSIDE DARKNESS ---

                // Compute a noise-based vertical gradient for subtle undersides
                // Vertical gradient (0 at bottom, 1 at top)
                float cloudHeight = saturate((uv.y - 0.4) * 3.0);

                // Invert and apply a smoothstep for a smooth but noticeable increase toward bottom
                float verticalStrength = smoothstep(0.7, 0.5, cloudHeight); 
                // This means near y=0.4+0.7/3 = ~0.63 is start fading, fully 1 at bottom (0.4)

                // Sample noise texture for texture modulation
                float undersideNoise = fbm(uv * _Scale * 5.0 + float2(0, -0.5));

                // Calculate final mask: noise always visible, scaled by vertical strength and cloudAlpha
                float undersideMask = undersideNoise * verticalStrength * cloudAlpha;

                // Darkening factor interpolates from no darkening (1.0) to darkened based on mask
                float darkFactor = lerp(1.0, 1.0 - _UndersideDarkness, undersideMask);

                // Final brightness with darkening applied
                float finalBrightness = _Brightness * darkFactor;


                cloudAlpha = saturate(cloudAlpha);
                cloudAlpha = smoothstep(0.3, 1.0, cloudAlpha);

                // Apply overall brightness control
                finalBrightness *= _Brightness;

                // --- COLOR TINT ---

                // Mix cloud color toward tint based on cloudAlpha
                float3 baseColor = lerp(_SkyColor.rgb, _CloudColor.rgb, cloudAlpha);
                float horizonFactor = saturate(1.0 - uv.y * 1.4);
                float tintAmount = _TintStrength * cloudAlpha * horizonFactor;
                float3 tintedColor = lerp(baseColor, _TintColor.rgb, tintAmount);
    
                float3 finalColor = tintedColor * finalBrightness;

                return float4(finalColor, cloudAlpha);
            }
            ENDHLSL
        }
    }
}
