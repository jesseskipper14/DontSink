Shader "Custom/SkyAtmosphere2D"
{
    Properties
    {
        [Header(Sky Gradient)]
        _HorizonSky ("Horizon Sky", Color) = (0.62, 0.82, 0.95, 1)
        _MidSky ("Mid Sky", Color) = (0.43, 0.68, 0.88, 1)
        _TopSky ("Top Sky", Color) = (0.12, 0.26, 0.48, 1)
        _MidPoint ("Gradient Mid Point", Range(0.1, 0.9)) = 0.48
        _GradientPower ("Gradient Shape", Range(0.25, 4.0)) = 1.0

        [Header(Time Of Day)]
        _Brightness ("Brightness", Range(0,1)) = 1
        _BrightnessExponent ("Night Darkness Response", Range(0.5,3.0)) = 1.65
        _NightFloor ("Minimum Color Floor", Range(0,0.25)) = 0.008

        [Header(Horizon Atmosphere)]
        _HorizonTint ("Horizon Tint", Color) = (1, 0.55, 0.28, 1)
        _SunriseFactor ("Sunrise Sunset Factor", Range(0,1)) = 0
        _HorizonTintFalloff ("Horizon Tint Falloff", Range(0.5, 8.0)) = 3.5

        [Header(Subtle Variation)]
        _VariationStrength ("Fine Grain Strength", Range(0,0.04)) = 0.006
    }

    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Opaque"
            "IgnoreProjector"="True"
        }

        Pass
        {
            ZWrite Off
            Cull Off
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _HorizonSky;
            float4 _MidSky;
            float4 _TopSky;
            float _MidPoint;
            float _GradientPower;

            float _Brightness;
            float _BrightnessExponent;
            float _NightFloor;

            float4 _HorizonTint;
            float _SunriseFactor;
            float _HorizonTintFalloff;

            float _VariationStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Screen-space height is intentional here. The sky gradient now means
                // "bottom-to-top of the player's view", not "some arbitrary slice of
                // a gigantic 300 x 1000 sprite".
                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 0.00001);
                float y = saturate(screenUV.y);

                float midpoint = clamp(_MidPoint, 0.001, 0.999);
                float3 color;

                if (y < midpoint)
                {
                    float t = saturate(y / midpoint);
                    t = pow(t, _GradientPower);
                    color = lerp(_HorizonSky.rgb, _MidSky.rgb, t);
                }
                else
                {
                    float t = saturate((y - midpoint) / (1.0 - midpoint));
                    t = pow(t, _GradientPower);
                    color = lerp(_MidSky.rgb, _TopSky.rgb, t);
                }

                // If/when we choose to feed SunriseFactor from the existing sunrise/
                // sunset service, the tint is naturally strongest near the horizon.
                // Today the existing overlay can continue doing that job with this at 0.
                float horizonMask = pow(saturate(1.0 - y), _HorizonTintFalloff);
                color = lerp(color, _HorizonTint.rgb,
                    saturate(_SunriseFactor) * horizonMask);

                // Maintain the existing SkyVisualManager contract.
                // Non-linear response lets night become genuinely dark without changing
                // the authoritative global brightness value used by other systems.
                float brightness = pow(saturate(_Brightness), max(_BrightnessExponent, 0.001));
                brightness = max(brightness, _NightFloor);
                color *= brightness;

                // Fine per-pixel dither. The previous implementation quantized the screen
                // into visible cells, which was the source of the blocky sky appearance.
                float2 pixel = floor(screenUV * _ScreenParams.xy);
                float noise = (Hash21(pixel) - 0.5) * _VariationStrength;
                color *= 1.0 + noise;

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
