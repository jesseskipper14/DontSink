Shader "DontSink/QuayConcrete2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Concrete Color", Color) = (0.52,0.54,0.52,1)
        _GrainScale ("Grain Per World Unit", Range(1,100)) = 35
        _GrainStrength ("Grain Strength", Range(0,0.3)) = 0.09
        _MottleScale ("Mottle Scale", Range(0.05,5)) = 0.45
        _MottleStrength ("Mottle Strength", Range(0,0.3)) = 0.07
        _PoreAmount ("Small Pores", Range(0,0.15)) = 0.025
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Environment/DepthLighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _GrainScale, _GrainStrength, _MottleScale, _MottleStrength, _PoreAmount;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; float4 color:COLOR; };
            float Hash(float2 p) { p=frac(p*float2(123.34,345.45));p+=dot(p,p+34.345);return frac(p.x*p.y); }
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            Varyings Vert(Attributes v)
            {
                Varyings o;float3 world=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(world);o.world=world.xy;o.uv=v.uv;o.color=v.color;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 sprite=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                float2 cell=floor(i.world*_GrainScale);
                float grain=Hash(cell);
                float brightness=1+(grain-.5)*_GrainStrength+(Noise(i.world*_MottleScale)-.5)*_MottleStrength;
                brightness-=step(1-_PoreAmount,Hash(cell+17.31))*.23;
                return half4(_Color.rgb*i.color.rgb*sprite.rgb*brightness*DontSinkDepthLight(i.world),
                    _Color.a*i.color.a*sprite.a);
            }
            ENDHLSL
        }
    }
}
