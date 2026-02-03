Shader "Custom/CloudGenerator2D"
{
    Properties
    {
        _CloudColor ("Cloud Color", Color) = (1,1,1,0.8)
        _CloudDensity ("Density", Range(0,1)) = 0.5
        _CloudScale ("Scale", Range(0.1,5)) = 1.5
        _CloudSpeed ("Speed", Range(0,1)) = 0.02
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _CloudColor;
            float _CloudDensity;
            float _CloudScale;
            float _CloudSpeed;

            float _Time1; // Unity built-in time

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

            // Simple 2D pseudo-random hash
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898,78.233))) * 43758.5453);
            }

            // Value noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash(i);
                float b = hash(i + float2(1,0));
                float c = hash(i + float2(0,1));
                float d = hash(i + float2(1,1));

                float2 u = f*f*(3.0-2.0*f);

                return lerp(a, b, u.x) + (c - a)*u.y*(1.0-u.x) + (d - b)*u.x*u.y;
            }

            // Fractal noise
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * noise(p * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv * _CloudScale;

                // Animate clouds horizontally
                uv.x += _Time1 * _CloudSpeed;

                float n = fbm(uv);

                // Apply density
                float alpha = smoothstep(_CloudDensity, 1.0, n);

                return float4(_CloudColor.rgb, _CloudColor.a * alpha);
            }
            ENDCG
        }
    }
}
