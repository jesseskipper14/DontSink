Shader "Custom/WaterSideView2D_Transparent"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.18, 0.78, 0.88, 0.45)
        _MidColor     ("Mid Color",     Color) = (0.03, 0.22, 0.40, 0.70)
        _DeepColor    ("Deep Color",    Color) = (0.01, 0.04, 0.10, 0.92)
        _FoamColor    ("Foam Color",    Color) = (0.88, 0.98, 1.00, 0.95)

        _Brightness       ("Brightness", Float) = 1
        _SparkleIntensity ("Sparkle Intensity", Range(0, 2)) = 0.35
        _FoamIntensity    ("Foam Intensity", Range(0, 1)) = 1

        _ShallowDepthY ("Shallow Depth Y", Float) = 2
        _MidDepthY     ("Mid Depth Y",     Float) = 10
        _DeepDepthY    ("Deep Depth Y",    Float) = 30
        _DepthBlendSoftness ("Depth Blend Softness", Float) = 1.5

        _FoamBandStart ("Foam Band Start", Range(0,1)) = 0.00
        _FoamBandEnd   ("Foam Band End",   Range(0,1)) = 0.06

        _NoiseScale1  ("Noise Scale 1", Float) = 0.18
        _NoiseScale2  ("Noise Scale 2", Float) = 0.42
        _NoiseSpeed1  ("Noise Speed 1", Float) = 0.18
        _NoiseSpeed2  ("Noise Speed 2", Float) = -0.11
        _Distortion   ("Distortion", Range(0, 0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _MidColor;
                half4 _DeepColor;
                half4 _FoamColor;

                half _Brightness;
                half _SparkleIntensity;
                half _FoamIntensity;

                half _ShallowDepthY;
                half _MidDepthY;
                half _DeepDepthY;
                half _DepthBlendSoftness;

                half _FoamBandStart;
                half _FoamBandEnd;

                half _NoiseScale1;
                half _NoiseScale2;
                half _NoiseSpeed1;
                half _NoiseSpeed2;
                half _Distortion;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv = IN.uv;
                OUT.uv2 = IN.uv2;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float2 worldXZ = IN.positionWS.xy;

                float n1 = Noise(worldXZ * _NoiseScale1 + float2(t * _NoiseSpeed1,  t * 0.07));
                float n2 = Noise(worldXZ * _NoiseScale2 + float2(t * _NoiseSpeed2, -t * 0.05));
                float combined = lerp(n1, n2, 0.5);

                //float localDepth01 = 1.0 - IN.uv.y;

                // uv2.x carries the sampled surface Y for this mesh column
                float surfaceY = IN.uv2.x;
                float worldDepthFromSurface = max(0.0, surfaceY - IN.positionWS.y);

                // subtle wobble in world-depth units
                float depthWobble = (combined - 0.5) * _Distortion;
                float d = max(0.0, worldDepthFromSurface + depthWobble);

                // enforce sane ordering
                float shallowY = max(0.0, _ShallowDepthY);
                float midY = max(shallowY + 0.001, _MidDepthY);
                float deepY = max(midY + 0.001, _DeepDepthY);
                float soft = max(0.001, _DepthBlendSoftness);

                float shallowToMid = smoothstep(shallowY - soft, midY + soft, d);
                float midToDeep = smoothstep(midY - soft, deepY + soft, d);

                half3 colorSM = lerp(_ShallowColor.rgb, _MidColor.rgb, shallowToMid);
                half3 colorMD = lerp(_MidColor.rgb, _DeepColor.rgb, midToDeep);

                half midSelector = step(midY, d);
                half3 baseColor = lerp(colorSM, colorMD, midSelector);

                half alphaSM = lerp(_ShallowColor.a, _MidColor.a, shallowToMid);
                half alphaMD = lerp(_MidColor.a, _DeepColor.a, midToDeep);
                half alpha = lerp(alphaSM, alphaMD, midSelector);

                // fixed world-space foam/sparkle band under local surface
                float foamBand = step(_FoamBandStart, worldDepthFromSurface) * (1.0 - step(_FoamBandEnd, worldDepthFromSurface));
                float foamMask = foamBand * _FoamIntensity;

                float sparkleBand = step(_FoamBandStart, worldDepthFromSurface) * (1.0 - step(_FoamBandEnd, worldDepthFromSurface));
                float sparkleNoise = Noise(worldXZ * 1.25 + float2(t * 0.35, -t * 0.18));
                float sparkles = pow(saturate(sparkleNoise), 18.0) * sparkleBand * _SparkleIntensity;

                half3 color = baseColor;
                color = lerp(color, _FoamColor.rgb, saturate(foamMask));
                color += sparkles.xxx;
                color *= _Brightness;

                alpha = saturate(alpha + foamMask * 0.2);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}