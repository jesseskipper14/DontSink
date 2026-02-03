Shader "Custom/Sun2D"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (1, 0.9, 0.6, 1)
        _OuterColor ("Outer Color", Color) = (1, 0.9, 0.6, 0)
        _Radius ("Radius", Range(0,1)) = 0.5
        _Softness ("Softness", Range(0.01, 1)) = 0.3
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

            float4 _InnerColor;
            float4 _OuterColor;
            float _Radius;
            float _Softness;

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

            fixed4 frag(v2f i) : SV_Target
            {
                // Center UV = (0.5, 0.5)
                float2 center = i.uv - 0.5;
                float dist = length(center) / _Radius;
                dist = saturate(dist);

                // Soft gradient with smoothstep
                float t = smoothstep(1.0, 1.0 - _Softness, dist);

                return lerp(_OuterColor, _InnerColor, t);
            }
            ENDCG
        }
    }
}
