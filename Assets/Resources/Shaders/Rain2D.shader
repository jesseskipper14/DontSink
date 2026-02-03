Shader "Custom/Rain2D"
{
    Properties
    {
        _MainTex("Background Texture", 2D) = "white" {}
        _RaindropCount("Raindrop Count", Float) = 200
        _RaindropSize("Raindrop Size (X=width, Y=height)", Vector) = (0.01,0.05,0,0)
        _RainSpeed("Rain Speed", Float) = 0.2
        _Wind("Wind Vector", Vector) = (0.1, 0, 0, 0)
        _Fog("Fog Density", Float) = 0.0
        _SplashHeight("Splash Height", Float) = 0.1
        _TimeOffset("Time Offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

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

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _RaindropCount;
            float2 _RaindropSize;
            float _RainSpeed;
            float2 _Wind;
            float _Fog;
            float _SplashHeight;
            float _TimeOffset;

            // Simple hash function for pseudo-randomness
            float hash(float2 p)
            {
                return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 bg = tex2D(_MainTex, uv);

                // ---------- Rain ----------
                float dropValue = 0.0;

            // Loop over raindrops
                for (int idx = 0; idx < (int)_RaindropCount; idx++)
                {
                    // Base position from hash
                    float2 dropCenter = float2(hash(float2(idx,0)), hash(float2(0,idx)));

                    // Total movement vector
                    float2 move = float2(_Wind.x, _Wind.y + _RainSpeed);

                    // Compute current drop position using _TimeOffset
                    float localTime = _Time.y - _TimeOffset;
                    float2 baseFall = float2(0, _RainSpeed * localTime);
float2 windDrift = float2(_Wind.x * localTime, 0);

float2 currentDropPos = dropCenter + baseFall + windDrift;
  
                    currentDropPos = frac(currentDropPos); // wrap around 0..1

                    float2 dir = normalize(move);
                    float2 perp = float2(-dir.y, dir.x);

                    float2 diff = uv - currentDropPos; // <-- use currentDropPos, not dropCenter

                    // Project diff into velocity space
                    float2 rotatedDiff = float2(
                        dot(diff, perp),  // width axis
                        dot(diff, dir)    // length axis
                    );

                    // Ellipse distance
                    float ellipseDist = sqrt(
                        (rotatedDiff.x / _RaindropSize.x) * (rotatedDiff.x / _RaindropSize.x) +
                        (rotatedDiff.y / _RaindropSize.y) * (rotatedDiff.y / _RaindropSize.y)
                    );

                    // Drop mask
                    float drop = smoothstep(1.0, 0.0, ellipseDist);
                    dropValue = max(dropValue, drop);
                }



                // ---------- Splash ----------
                float splash = 0.0;
                if (uv.y < _SplashHeight)
                {
                    splash = smoothstep(0.0, _SplashHeight, uv.y) * dropValue;
                }

                // ---------- Fog ----------
                bg.rgb = lerp(bg.rgb, float3(0.7,0.7,0.7), _Fog);

                // ---------- Output ----------
                fixed4 finalColor;
                finalColor.rgb = bg.rgb + dropValue * 0.5; // brighten rain slightly
                finalColor.a = dropValue + splash; // alpha for blending

                return finalColor;
            }
            ENDCG
        }
    }
}
