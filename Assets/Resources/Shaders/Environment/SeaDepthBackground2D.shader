Shader "Custom/SeaDepthBackground2D"
{
    Properties
    {
        [Header(World Waterline)]
        _SeaLevelY ("Mean Sea Level Y", Float) = 0

        [Header(Depth Colors)]
        _SurfaceWater ("Surface Water", Color) = (0.27, 0.69, 0.86, 1)
        _ShallowWater ("Shallow Water", Color) = (0.09, 0.42, 0.62, 1)
        _MidWater ("Mid Water", Color) = (0.035, 0.20, 0.34, 1)
        _DeepWater ("Deep Water", Color) = (0.012, 0.055, 0.11, 1)

        [Header(World Depth Markers)]
        _ShallowDepth ("End Shallow Depth", Float) = 12
        _MidDepth ("End Mid Depth", Float) = 45
        _DeepDepth ("Deep Color Depth", Float) = 120
        _DepthPower ("Depth Transition Shape", Range(0.25, 4.0)) = 1.15

        [Header(Time Of Day)]
        _Brightness ("Brightness", Range(0,1)) = 1
        _NightWaterTint ("Night Water Tint", Color) = (0.008, 0.025, 0.06, 1)

        [Header(Distant Water Variation)]
        _VariationStrength ("Variation Strength", Range(0,0.08)) = 0.018
        _VariationWorldScale ("Variation World Scale", Range(0.02, 2.0)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "Queue"="Background+10"
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

            float _SeaLevelY;

            float4 _SurfaceWater;
            float4 _ShallowWater;
            float4 _MidWater;
            float4 _DeepWater;

            float _ShallowDepth;
            float _MidDepth;
            float _DeepDepth;
            float _DepthPower;

            float _Brightness;
            float4 _NightWaterTint;

            float _VariationStrength;
            float _VariationWorldScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = world.xy;
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
                // Mean-waterline separation is now a world-space fact, not a UV band.
                // The foreground WaveField can continue owning the actual wavy surface.
                float depth = _SeaLevelY - i.worldPos.y;
                clip(depth + 0.001);

                float shallowDepth = max(_ShallowDepth, 0.001);
                float midDepth = max(_MidDepth, shallowDepth + 0.001);
                float deepDepth = max(_DeepDepth, midDepth + 0.001);

                float3 color;

                if (depth < shallowDepth)
                {
                    float t = saturate(depth / shallowDepth);
                    t = pow(t, _DepthPower);
                    color = lerp(_SurfaceWater.rgb, _ShallowWater.rgb, t);
                }
                else if (depth < midDepth)
                {
                    float t = saturate((depth - shallowDepth) / (midDepth - shallowDepth));
                    t = pow(t, _DepthPower);
                    color = lerp(_ShallowWater.rgb, _MidWater.rgb, t);
                }
                else
                {
                    float t = saturate((depth - midDepth) / (deepDepth - midDepth));
                    t = pow(t, _DepthPower);
                    color = lerp(_MidWater.rgb, _DeepWater.rgb, t);
                }

                // Match the spirit of the old underwater treatment: night tends toward
                // a deliberate deep navy instead of merely multiplying to absolute black.
                color = lerp(_NightWaterTint.rgb, color, saturate(_Brightness));

                // World-space variation remains attached to the water rather than the camera.
                float2 variationCell = floor(i.worldPos * max(_VariationWorldScale, 0.0001));
                float noise = (Hash21(variationCell) - 0.5) * _VariationStrength;
                color *= 1.0 + noise;

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
