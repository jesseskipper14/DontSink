Shader "Custom/Water_Depth_Gradient_Dual_Normals"
{
    Properties
    {
        _SurfaceColor ("Surface Color", Color) = (0.2, 0.6, 0.9, 0.3)
        _DeepColor    ("Deep Color", Color) = (0.0, 0.1, 0.4, 0.7)
        _Depth        ("Gradient Depth", Float) = 10
        _WorldOffset  ("World Offset", Vector) = (0,0,0,0)

        _NormalMap1 ("Primary Normal", 2D) = "bump" {}
        _Normal1Strength ("Primary Strength", Range(0,2)) = 0.5
        _Normal1Tiling ("Primary Tiling", Float) = 6
        _Scroll1 ("Primary Scroll", Vector) = (0.03, 0.02, 0, 0)

        _NormalMap2 ("Caustics Normal", 2D) = "bump" {}
        _Normal2Strength ("Caustics Strength", Range(0,2)) = 0.2
        _Normal2Tiling ("Caustics Tiling", Float) = 1
        _Scroll2 ("Caustics Scroll", Vector) = (0.005, 0.003, 0, 0)
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
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float localY : TEXCOORD2;
            };

            sampler2D _NormalMap1;
            sampler2D _NormalMap2;

            float _Normal1Strength;
            float _Normal2Strength;
            float _Normal1Tiling;
            float _Normal2Tiling;
            float4 _Scroll1;
            float4 _Scroll2;

            float4 _SurfaceColor;
            float4 _DeepColor;
            float _Depth;

            float4 _WorldOffset; // XY offset for boat/world movement

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalWS = UnityObjectToWorldNormal(v.normal);

                // Apply world offset to UVs
                o.uv = v.uv + _WorldOffset.xy;

                o.localY = v.vertex.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Depth gradient
                float depth01 = saturate(-i.localY / _Depth);
                fixed3 color = lerp(_SurfaceColor.rgb, _DeepColor.rgb, depth01);

                // Primary normal (ripples)
                float2 uv1 = i.uv * _Normal1Tiling + _Scroll1.xy * _Time.y;
                float3 n1 = UnpackNormal(tex2D(_NormalMap1, uv1));
                n1.xy *= _Normal1Strength;

                // Secondary normal (caustics)
                float2 uv2 = i.uv * _Normal2Tiling + _Scroll2.xy * _Time.y;
                float3 n2 = UnpackNormal(tex2D(_NormalMap2, uv2));
                n2.xy *= _Normal2Strength;

                // Combine normals
                float3 combinedNormal = normalize(n1 + n2);

                // Combine with mesh normal
                float3 normal = normalize(i.normalWS + combinedNormal);

                // Simple angled light
                float3 lightDir = normalize(float3(0.3, 0.8, 0.5));
                float light = saturate(dot(normal, lightDir));

                color *= lerp(0.6, 1.2, light);

                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}
