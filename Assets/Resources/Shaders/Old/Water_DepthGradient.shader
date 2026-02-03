Shader "Custom/Water_Depth_Gradient_Animated"
{
    Properties
    {
        _SurfaceColor ("Surface Color", Color) = (0.2, 0.6, 0.9, 1)
        _DeepColor    ("Deep Color",    Color) = (0.0, 0.1, 0.4, 1)
        _Depth        ("Gradient Depth", Float) = 10.0

        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 0.5
        _ScrollSpeed1 ("Scroll Speed 1", Vector) = (0.05, 0.02, 0, 0)
        _ScrollSpeed2 ("Scroll Speed 2", Vector) = (-0.03, 0.01, 0, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float  localY : TEXCOORD1;
            };

            sampler2D _NormalMap;
            float4 _NormalMap_ST;

            float4 _SurfaceColor;
            float4 _DeepColor;
            float  _Depth;

            float  _NormalStrength;
            float4 _ScrollSpeed1;
            float4 _ScrollSpeed2;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.vertex.xz, _NormalMap);
                o.localY = v.vertex.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Depth gradient
                float depth01 = saturate(-i.localY / _Depth);
                fixed4 baseColor = lerp(_SurfaceColor, _DeepColor, depth01);

                // Animated normals
                float2 uv1 = i.uv + _ScrollSpeed1.xy * _Time.y;
                float2 uv2 = i.uv + _ScrollSpeed2.xy * _Time.y;

                float3 n1 = UnpackNormal(tex2D(_NormalMap, uv1));
                float3 n2 = UnpackNormal(tex2D(_NormalMap, uv2));

                float3 normal = normalize(lerp(n1, n2, 0.5));
                normal.xy *= _NormalStrength;

                // Fake lighting (top-down light)
                float light = saturate(dot(normal, float3(0, 0, 1)));
                light = lerp(0.7, 1.2, light);

                baseColor.rgb *= light;

                return baseColor;
            }
            ENDCG
        }
    }
}
