Shader "Custom/Moon2D"
{
    Properties
    {
        _MoonColor ("Moon Color", Color) = (1,1,1,1)
        _Brightness ("Brightness", Range(0,1)) = 1
        _Phase ("Phase (0=New, 0.25=Waxing, 0.5=Full, 0.75=Waning, 1=New)", Range(0,1)) = 0
        _SmoothEdge ("Phase Edge Smoothness", Range(0.01,0.2)) = 0.05
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

            float4 _MoonColor;
            float _Brightness;
            float _Phase;
            float _SmoothEdge;

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
                o.uv = v.uv * 2.0 - 1.0; // 0..1 -> -1..1
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float dist = length(uv);

                if (dist > 1.0) discard; // outside moon

                // Compute terminator position (-1..1)
                float phase = _Phase;
                float termX;

                if (phase <= 0.5)
                {
                    // Waxing: dark recedes right to left
                    termX = -1.0 + 4.0 * phase; // 0 -> 0.5 maps -1 -> 1
                }
                else
                {
                    // Waning: dark comes back right to left
                    termX = 3.0 - 4.0 * phase; // 0.5 -> 1 maps 1 -> -1
                }

                // Circle chord for crescent effect
                float maxX = sqrt(saturate(1.0 - uv.y * uv.y));
                float diff = maxX - (uv.x - termX);

                float lit = smoothstep(0.0, _SmoothEdge, diff);

                fixed4 col;
                col.rgb = _MoonColor.rgb * lit * _Brightness;
                col.a = 1.0;

                return col;
            }

            ENDCG
        }
    }
}
