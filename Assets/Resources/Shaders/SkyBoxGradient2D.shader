Shader "Custom/SkyBoxGradient2D"
{
    Properties
    {
        // ==== Water ====
        _DeepWater ("Deep Water", Color) = (0.04, 0.12, 0.22, 1)
        _MidWater ("Mid Water", Color) = (0.10, 0.35, 0.55, 1)
        _WaterlineBottom ("Waterline Bottom", Color) = (0.35, 0.65, 0.85, 1)
        _WaterlineTop ("Waterline Top", Color) = (0.45, 0.70, 0.90, 1)
        _WaterFalloff ("Water Darkening", Range(1,4)) = 2.5

        // ==== Sky ====
        _MidSky ("Mid Sky", Color) = (0.55, 0.80, 0.95, 1)
        _TopSky ("Top Sky", Color) = (0.29, 0.435, 0.647, 1)
        _SkyFalloff ("Sky Gradient Spread", Range(0.2,1)) = 0.6

        // ==== Day/Night ====
        _Brightness ("Brightness", Range(0,1)) = 1

        // ==== Sunrise/Sunset ====
        _HorizonTint ("Horizon Tint Color", Color) = (1,0.6,0.3,1)
        _SunriseFactor ("Sunrise Factor", Range(0,1)) = 0

        // ==== Variation ====
        _VariationStrength ("Variation Strength", Range(0,0.1)) = 0.05
        _NightTint ("Night Tint (Underwater)", Color) = (0.02,0.05,0.1,1)

        // ==== Stars ====
        _StarSeed ("Star Seed", Float) = 123.0
        _StarDensity ("Star Density", Float) = 50.0
        _StarAlpha ("Star Alpha", Range(0,1)) = 1.0
        _StarTwinkleSpeed ("Star Twinkle Speed", Float) = 1.0
        _StarTwinkleIntensity ("Star Twinkle Intensity", Float) = 0.2
        _StarColor ("Star Color", Color) = (1,1,1,1)
        _StarSize ("Star Size", Range(0.001, 0.05)) = 0.01
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Transparent" }

        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // ==== Properties ====
            float4 _DeepWater;
            float4 _MidWater;
            float4 _WaterlineBottom;
            float4 _WaterlineTop;
            float _WaterFalloff;

            float4 _MidSky;
            float4 _TopSky;
            float _SkyFalloff;

            float _Brightness;
            float4 _HorizonTint;
            float _SunriseFactor;
            float _VariationStrength;
            float4 _NightTint;

            float _StarSeed;
            float _StarDensity;
            float _StarAlpha;
            float _StarTwinkleSpeed;
            float _StarTwinkleIntensity;
            float4 _StarColor;
            float _StarSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // ---- Hash function for fixed stars ----
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float y = i.uv.y;
                fixed4 finalColor;

                // ==== Underwater ====
                if (y < 0.49)
                {
                    float t = y / 0.49;
                    t = pow(t, _WaterFalloff);

                    if (t < 0.5)
                        finalColor = lerp(_DeepWater, _MidWater, t/0.5);
                    else
                        finalColor = lerp(_MidWater, _WaterlineBottom, (t-0.5)/0.5);

                    // Night tint underwater
                    finalColor.rgb = lerp(_NightTint.rgb, finalColor.rgb, _Brightness);
                }
                // ==== Waterline ====
                else if (y < 0.506)
                {
                    float t = (y - 0.49) / 0.02;
                    finalColor = lerp(_WaterlineBottom, _WaterlineTop, t);
                    finalColor.rgb *= _Brightness;
                }
                // ==== Sky ====
                else
                {
                    // Define localized gradient range
                    float localMin = 0.506;
                    float localMax = 0.53; // the small portion you want to emphasize

                    // remap y from localMin..localMax → 0..1
                    float t = saturate((y - localMin) / (localMax - localMin));
                    t = pow(t, _SkyFalloff); // keep falloff effect

                    // Interpolate in two steps like before
                    if (t < 0.5)
                        finalColor = lerp(_WaterlineTop, _MidSky, t / 0.5);
                    else
                        finalColor = lerp(_MidSky, _TopSky, (t - 0.5) / 0.5);

                    // Apply day/night brightness
                    finalColor.rgb *= _Brightness;

                    // Sunrise/Sunset glow
                    finalColor.rgb = lerp(finalColor.rgb, _HorizonTint.rgb, _SunriseFactor);
                }

                // ==== Subtle random variation ====
                float noise = (frac(sin(dot(i.uv * 100.0, float2(12.9898,78.233))) * 43758.5453) - 0.5) * _VariationStrength;
                finalColor.rgb *= 1.0 + noise;

                // ==== Stars ====
                float starIntensity = 0.0;

                // Only generate stars in the sky
                if (i.uv.y >= 0.506)
                {
                    float2 starUV = i.uv;
                    float2 gridUV = starUV * _StarDensity;
                    float2 cell = floor(gridUV);
                    float2 cellOffset = frac(gridUV);

                    float starHash = Hash21(cell + _StarSeed);

                    // sparsity threshold
                    if(starHash > 0.85)
                    {
                        float2 starPos = float2(frac(starHash*12.345), frac(starHash*67.89));
                        float2 delta = cellOffset - starPos;

                        // Use _StarSize to control radius
                        float radius = _StarSize; // expose in properties
                        float dist = length(delta);
                        starIntensity = smoothstep(radius, 0.0, dist);

                        // Twinkle
                        starIntensity *= 1.0 + sin(_Time.y * _StarTwinkleSpeed + starHash * 6.28) * _StarTwinkleIntensity;
                    }

                    finalColor.rgb = lerp(finalColor.rgb, _StarColor.rgb, starIntensity * _StarAlpha);
                }

                finalColor.a = 1.0;
                return finalColor;
            }

            ENDCG
        }
    }
}
