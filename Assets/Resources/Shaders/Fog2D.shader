Shader "Custom/Fog2D"
{
    Properties
    {
        _MainTex("Background Texture", 2D) = "white" {}
        _FogColor("Fog Color", Color) = (0.8,0.8,0.8,1)
        _FogAlpha("Fog Alpha", Range(0,1)) = 0.3
        _Brightness("Fog Brightness", Range(0,2)) = 1.0
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _NoiseScale("Noise Scale", Float) = 3.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
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
            sampler2D _NoiseTex;
            float4 _MainTex_ST;

            fixed4 _FogColor;
            float _FogAlpha;
            float _Brightness;
            float _NoiseScale;

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

                // ---------- Noise for variation ----------
                float noise = tex2D(_NoiseTex, uv * _NoiseScale + _Time.y * 0.05).r;

                // Slight variation around base alpha
                float alphaVariation = _FogAlpha * (0.7 + 0.3 * noise);

                // ---------- Apply brightness ----------
                fixed3 fogCol = _FogColor.rgb * _Brightness;

                // ---------- Output ----------
                fixed4 finalColor;
                finalColor.rgb = lerp(bg.rgb, fogCol, alphaVariation);
                finalColor.a = alphaVariation;

                return finalColor;
            }

            ENDCG
        }
    }
}
