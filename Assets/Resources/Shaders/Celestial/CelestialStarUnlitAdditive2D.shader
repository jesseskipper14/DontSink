Shader "Custom/CelestialStarUnlitAdditive2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowStrength ("Glow Strength", Range(0,4)) = 1.5
        _TwinkleStrength ("Twinkle Strength", Range(0,0.75)) = 0.15
        _TwinkleSpeed ("Twinkle Speed", Range(0,8)) = 1.5
        _TwinklePhase ("Twinkle Phase", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _GlowStrength;
            float _TwinkleStrength;
            float _TwinkleSpeed;
            float _TwinklePhase;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float wave = sin(_Time.y * _TwinkleSpeed + _TwinklePhase);
                float twinkle = max(0.05, 1.0 + wave * _TwinkleStrength);

                fixed alpha = tex.a * i.color.a;
                fixed3 rgb = tex.rgb * i.color.rgb * (_GlowStrength * twinkle);

                // Deliberately unlit. These are emissive-looking celestial sources,
                // so Global Light2D must not dim them along with the world foreground.
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
