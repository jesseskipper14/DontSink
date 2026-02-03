Shader "Environment/ProceduralCloud2D"
{
    Properties
    {
        _CloudType ("Cloud Type", Float) = 1
        _DetailScale ("Detail Scale", Float) = 1.0
        _Darkness ("Darkness", Range(0,1)) = 0
        _SunriseTintStrength ("Sunrise Tint Strength", Range(0,1)) = 0
        _VerticalShadow ("Vertical Shadow", Range(0,1)) = 0.5
        _SunriseTint ("Sunrise Tint Color", Color) = (1,0.6,0.3,1)
        _EdgeSoftness ("Edge Softness", Range(0,1)) = 0.3
        _OuterFade ("Outer Edge Fade", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _CloudType;
            float _DetailScale;
            float _Darkness;
            float _SunriseTintStrength;
            float _VerticalShadow;
            float4 _SunriseTint;
            float _EdgeSoftness;
            float _OuterFade;

            // -------------------------------------------------
            // Simple 2D value noise
            // -------------------------------------------------
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash(i);
                float b = hash(i + float2(1,0));
                float c = hash(i + float2(0,1));
                float d = hash(i + float2(1,1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) +
                       (c - a) * u.y * (1.0 - u.x) +
                       (d - b) * u.x * u.y;
            }

            // -------------------------------------------------
            // Cloud Shape Generator
            // -------------------------------------------------
            float cloudShape(float2 uv)
            {
                float n = 0;
                float scale = _DetailScale;

                if (_CloudType < 0.5) // Cirrus
                    n = noise(uv * scale * float2(3,1));
                else if (_CloudType < 1.5) // Cumulus
                    n = noise(uv * scale) * 0.6 +
                        noise(uv * scale * 2.1) * 0.4;
                else if (_CloudType < 2.5) // Nimbus
                    n = noise(uv * scale * 1.2) * 0.5 +
                        noise(uv * scale * 3.5) * 0.5;
                else // Storm
                    n = noise(uv * scale * 0.8) * 0.4 +
                        noise(uv * scale * 2.5) * 0.6;

                return n;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 cuv = uv - 0.5;

                float shape = cloudShape(cuv + 0.5);

                // --------------------------
                // Soft edge based on cloud shape
                // --------------------------
                float edgeThreshold = 0.65; // original hard threshold
                float edgeSoft = _EdgeSoftness * _OuterFade;
                float alpha = smoothstep(edgeThreshold - edgeSoft, edgeThreshold, shape);

                // --------------------------
                // Vertical shadow
                // --------------------------
                float vertical = lerp(1.0 - _VerticalShadow, 1.0, uv.y);

                // --------------------------
                // Base cloud color
                // --------------------------
                float3 cloudColor = float3(1,1,1);

                // Apply darkness (storminess)
                cloudColor = lerp(cloudColor, float3(0.15,0.15,0.15), _Darkness);

                // Apply sunrise/sunset tint
                cloudColor = lerp(cloudColor, _SunriseTint.rgb, _SunriseTintStrength);

                cloudColor *= vertical;

                return float4(cloudColor, alpha);
            }
            ENDCG
        }
    }
}
