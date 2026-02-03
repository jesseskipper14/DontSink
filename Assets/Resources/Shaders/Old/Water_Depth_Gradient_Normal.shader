Shader "Custom/Water_Depth_Gradient_Normals"
{
    Properties
    {
        _SurfaceColor ("Surface Color", Color) = (0.2, 0.6, 0.9, 1)
        _DeepColor    ("Deep Color", Color)    = (0.0, 0.1, 0.4, 1)
        _Depth        ("Gradient Depth", Float) = 10

        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 0.5
        _NormalTiling ("Normal Tiling", Float) = 4
        _ScrollSpeed ("Scroll Speed", Vector) = (0.05, 0.02, 0, 0)
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

            sampler2D _NormalMap;
            float _NormalStrength;
            float _NormalTiling;
            float4 _ScrollSpeed;

            float4 _SurfaceColor;
            float4 _DeepColor;
            float _Depth;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv * _NormalTiling;
                o.localY = v.vertex.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Depth gradient
                float depth01 = saturate(-i.localY / _Depth);
                fixed3 color = lerp(_SurfaceColor.rgb, _DeepColor.rgb, depth01);

                // Animated normal map
                float2 uv = i.uv + _ScrollSpeed.xy * _Time.y;
                float3 normalMap = UnpackNormal(tex2D(_NormalMap, uv));
                normalMap.xy *= _NormalStrength;

                // Combine with mesh normal
                float3 normal = normalize(i.normalWS + normalMap);

                // Simple directional light (angled, not vertical!)
                float3 lightDir = normalize(float3(0.3, 0.8, 0.5));
                float light = saturate(dot(normal, lightDir));

                color *= lerp(0.6, 1.2, light);

                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}
