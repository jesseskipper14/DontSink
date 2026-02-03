Shader "Custom/Wind2D"
{
    Properties
    {
        _WindStrength ("Wind Strength", Range(0,1)) = 0
        _WindDir ("Wind Direction", Vector) = (1,0,0,0)

        _LineDensity ("Line Density", Float) = 20
        _LineThickness ("Line Thickness", Float) = 0.02
        _LineLength ("Line Length", Float) = 0.4
        _LineSpeed ("Line Speed", Float) = 0.5

        _LineColor ("Line Color", Color) = (1,1,1,1)
        _Opacity ("Opacity", Float) = 0.5
        _TimeOffset ("Time Offset", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float _WindStrength;
            float2 _WindDir;

            float _LineDensity;
            float _LineThickness;
            float _LineLength;
            float _LineSpeed;
            float _TimeOffset;

            fixed4 _LineColor;
            float _Opacity;

            // Cheap hash for per-line randomness
            float hash(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 dir = normalize(_WindDir + 1e-4);
                float2 perp = float2(-dir.y, dir.x);

                // Project UVs into wind space
                float u = dot(i.uv, dir);
                float v = dot(i.uv, perp);

                // Per-line index & randomness
                float lineIndex = floor(v * _LineDensity);
                float rand = hash(lineIndex);

                // Animate forward motion
                float speed = lerp(0.3, 1.2, rand);
                float t = u + (_Time.y - _TimeOffset) * _LineSpeed * speed;

                // Segment along line
                float segment = frac(t);

                // Base line body
                float body =
                    smoothstep(0.0, _LineLength, segment) *
                    smoothstep(1.0, 1.0 - _LineLength, segment);

                // 👇 Twirl: sideways displacement along the line
                float curlAmp = lerp(0.0, 0.5, rand);
                float curlFreq = lerp(1.0, 3.0, rand);
                float phase = rand * 6.2831;

                float curl =
                    sin(segment * curlFreq * 6.2831 + phase + _Time.y) * curlAmp;

                // Project perpendicular coordinate + curl
                float localV = frac(v * _LineDensity + curl) - 0.5;

                // Line thickness
                float thickness = smoothstep(_LineThickness, 0.0, abs(localV));

                float lineMask = body * thickness;

                // -----------------------------
                // Per-line independent gust
                // -----------------------------

                float freq = lerp(0.2, 0.5, rand);        // slightly different frequency per line
                float gustPhase = rand * 6.2831;         // random phase
                float gust = (sin(_Time.y * freq * 6.2831 + gustPhase + lineIndex) * 0.5 + 0.5);
                gust = smoothstep(0.05, 0.6, gust);       // longer near-zero period

                // Final alpha with global wind strength
                float alpha = lineMask * gust * _WindStrength * _Opacity;

                fixed4 col = _LineColor;
                col.a *= alpha;

                return col;
            }
            ENDCG
        }
    }
}
