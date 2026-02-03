Shader "Custom/Water_SideView_Opaque"
{
    Properties
    {
        _SurfaceColor("Surface Color", Color) = (0.2,0.6,0.9,1)
        _DeepColor("Deep Color", Color) = (0.0,0.1,0.4,1)
        _Depth("Gradient Depth", Float) = 5
        _VolumeTilt("Back Y Offset", Float) = 0.5
        _Brightness("Brightness", Range(0.0, 2.0)) = 1.0

        _NormalMap1("Primary Normal", 2D) = "bump" {}
        _Normal1Strength("Primary Strength", Range(0,2)) = 0.5
        _Normal1Tiling("Primary Tiling", Float) = 6
        _Scroll1("Primary Scroll", Vector) = (0.03,0.02,0,0)

        _NormalMap2("Caustics Normal", 2D) = "bump" {}
        _Normal2Strength("Caustics Strength", Range(0,2)) = 0.2
        _Normal2Tiling("Caustics Tiling", Float) = 1
        _Scroll2("Caustics Scroll", Vector) = (0.005,0.003,0,0)
    }

    SubShader
    {
        Pass
        {
            Tags { "Queue"="Geometry" "RenderType"="Opaque" }
            ZWrite On
            Cull Off

            Stencil
            {
                Ref 2
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float localY : TEXCOORD1;
                float volumeY : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
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
            float _VolumeTilt;
            float _Brightness;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float tilt = _VolumeTilt * IN.positionOS.z;
                float3 pos = IN.positionOS.xyz;
                pos.y += tilt;

                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.uv = IN.uv;
                OUT.localY = IN.positionOS.y;
                OUT.volumeY = pos.y;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float depth01 = saturate(-IN.volumeY / _Depth);
                float3 color = lerp(_SurfaceColor.rgb, _DeepColor.rgb, depth01);

                float2 uv1 = IN.uv * _Normal1Tiling + _Scroll1.xy * _Time.y;
                float3 n1 = UnpackNormal(tex2D(_NormalMap1, uv1));
                n1.xy *= _Normal1Strength;

                float2 uv2 = IN.uv * _Normal2Tiling + _Scroll2.xy * _Time.y;
                float3 n2 = UnpackNormal(tex2D(_NormalMap2, uv2));
                n2.xy *= _Normal2Strength;

                float3 combinedNormal = normalize(n1 + n2);
                float3 normal = normalize(IN.normalWS + combinedNormal);

                float3 lightDir = normalize(float3(0.3,0.8,0.5));
                float light = saturate(dot(normal, lightDir));
                color *= lerp(0.6, 1.2, light);

                color *= _Brightness; // scale with sun/moon intensity
                return half4(color, 1.0);
            }
            ENDHLSL
        } // end Pass
    } // end SubShader
} // end Shader
