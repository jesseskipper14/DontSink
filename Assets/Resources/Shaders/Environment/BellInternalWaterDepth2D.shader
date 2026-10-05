Shader "DontSink/BellInternalWaterDepth2D"
{
    Properties { _Color("Water Color", Color) = (0.1, 0.4, 0.9, 0.55) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DepthLighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 worldPosition : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 worldPosition = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.worldPosition = worldPosition.xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_Color.rgb * DontSinkDepthLight(input.worldPosition), _Color.a);
            }
            ENDHLSL
        }
    }
}
