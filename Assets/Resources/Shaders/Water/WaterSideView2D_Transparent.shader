Shader "Custom/WaterSideView2D_Transparent"
{
    Properties
    {
        [Header(Existing Depth Color)]
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

        [Header(Surface Foam)]
        _FoamBandStart ("Foam Band Start", Range(0,1)) = 0.00
        _FoamBandEnd   ("Foam Band End",   Range(0,1)) = 0.06
        _FoamEdgeSoftness ("Foam Edge Softness", Range(0.001, 0.25)) = 0.035
        _FoamPatchScale ("Foam Patch Scale", Range(0.05, 8)) = 0.85
        _FoamPatchSpeed ("Foam Patch Speed", Range(0, 2)) = 0.16
        _FoamCoverage ("Foam Coverage", Range(0, 1)) = 0.48
        _FoamSlopeInfluence ("Foam Slope Influence", Range(0, 1)) = 0.55
        _FoamSlopeReference ("Foam Slope Reference", Range(0.01, 2)) = 0.35

        [Header(Surface Sparkles)]
        _SparkleColor ("Sparkle Color", Color) = (0.92, 0.99, 1.0, 1)
        _SparkleDepth ("Sparkle Depth", Range(0.02, 2)) = 0.30
        _SparkleCellSize ("Sparkle Cell Size", Range(0.10, 4)) = 0.90
        _SparkleDensity ("Sparkle Density", Range(0, 1)) = 0.34
        _SparkleSize ("Sparkle Horizontal Size", Range(0.01, 0.30)) = 0.085
        _SparkleVerticalSize ("Sparkle Vertical Size", Range(0.005, 0.20)) = 0.035
        _SparkleTwinkleSpeed ("Sparkle Twinkle Speed", Range(0, 12)) = 3.0

        [Header(Existing Depth Wobble)]
        _NoiseScale1  ("Noise Scale 1", Float) = 0.18
        _NoiseScale2  ("Noise Scale 2", Float) = 0.42
        _NoiseSpeed1  ("Noise Speed 1", Float) = 0.18
        _NoiseSpeed2  ("Noise Speed 2", Float) = -0.11
        _Distortion   ("Depth Wobble", Range(0, 0.2)) = 0.03

        [Header(Water Body Motion)]
        _BodyFlowScale ("Body Flow Scale", Range(0.01, 2)) = 0.11
        _BodyFlowSpeed ("Body Flow Speed", Range(0, 1)) = 0.045
        _BodyFlowStrength ("Body Flow Strength", Range(0, 0.3)) = 0.055

        [Header(Caustics)]
        [Toggle] _EnableCaustics ("Enable Caustics", Float) = 1
        _CausticColor ("Caustic Color", Color) = (0.58, 0.92, 0.94, 1)
        _CausticScale ("Caustic Scale", Range(0.2, 12)) = 2.15
        _CausticSpeed ("Caustic Speed", Range(0, 3)) = 0.32
        _CausticStrength ("Caustic Strength", Range(0, 1)) = 0.16
        _CausticSharpness ("Caustic Sharpness", Range(1, 12)) = 5.5
        _CausticFadeDepth ("Caustic Fade Depth", Range(0.5, 40)) = 8

        [Header(View Dependent Sun Rays)]
        [Toggle] _EnableRays ("Enable Rays", Float) = 1
        _RayColor ("Ray Color", Color) = (0.66, 0.92, 0.92, 1)
        _RayStrength ("Ray Strength", Range(0, 1)) = 0.22
        _RaySpacing ("Surface Ray Spacing", Range(0.10, 6)) = 1.15
        _RayMinWidth ("Minimum Ray Width", Range(0.02, 0.45)) = 0.055
        _RayMaxWidth ("Maximum Ray Width", Range(0.03, 0.48)) = 0.16
        _RayEdgeSoftness ("Ray Edge Softness", Range(0.005, 0.20)) = 0.045
        _RayDanceAmount ("Ray Dance Amount", Range(0, 0.45)) = 0.16
        _RayDanceSpeed ("Ray Dance Speed", Range(0, 4)) = 0.65
        _RaySpawnRate ("Ray Birth Rate", Range(0, 4)) = 0.65
        _RayBrightnessVariance ("Ray Brightness Variance", Range(0, 1)) = 0.70
        _RayCoreHalfAngleDeg ("Ray Core Half Angle", Range(0.25, 40)) = 8
        _RayCorePower ("Ray Core Focus Power", Range(0.25, 8)) = 2.4
        _RayDepthLossPerMeter ("Ray Strength Loss Per Depth", Range(0, 2)) = 0.12
        _RayFadeDepth ("Ray Fade Depth", Range(0.5, 60)) = 18
        _RayConeHalfAngleDeg ("View Cone Half Angle", Range(2, 70)) = 28
        _RayConeSoftnessDeg ("View Cone Edge Softness", Range(0.1, 25)) = 9
        _RayConePower ("View Cone Falloff Power", Range(0.25, 6)) = 1.25
        _RayBelowViewerFade ("Beyond Viewer Fade Distance", Range(0.25, 30)) = 6
        _RayBeyondViewerStrength ("Far Beyond Viewer Strength", Range(0, 1)) = 0.28

        [HideInInspector] _RayAlignmentStart ("Legacy Ray Alignment Start", Float) = 0.86
        [HideInInspector] _RayAlignmentPower ("Legacy Ray Alignment Power", Float) = 1.6

        [Header(Suspended Specks)]
        [Toggle] _EnableSpecks ("Enable Specks", Float) = 1
        _SpeckColor ("Speck Color", Color) = (0.68, 0.88, 0.86, 1)
        _SpeckCellSize ("Speck Cell Size", Range(0.05, 2)) = 0.34
        _SpeckAmount ("Speck Amount", Range(0, 1)) = 0.14
        _SpeckBrightness ("Speck Brightness", Range(0, 1)) = 0.15
        _SpeckDriftX ("Speck Drift X", Range(-1, 1)) = 0.025
        _SpeckDriftY ("Speck Drift Y", Range(-1, 1)) = 0.012

        // Serialized legacy properties from older material revisions.
        // Kept hidden so existing .mat data remains harmless and recognizable.
        [HideInInspector] _SurfaceY ("Legacy Surface Y", Float) = 0
        [HideInInspector] _SurfaceBand ("Legacy Surface Band", Float) = 0.25
        [HideInInspector] _SparkleBand ("Legacy Sparkle Band", Float) = 0.001
        [HideInInspector] _DepthColorDistance ("Legacy Depth Color Distance", Float) = 50
        [HideInInspector] _DepthPower ("Legacy Depth Power", Float) = 2.5
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
            #include "../Environment/DepthLighting.hlsl"

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
                half _FoamEdgeSoftness;
                half _FoamPatchScale;
                half _FoamPatchSpeed;
                half _FoamCoverage;
                half _FoamSlopeInfluence;
                half _FoamSlopeReference;

                half4 _SparkleColor;
                half _SparkleDepth;
                half _SparkleCellSize;
                half _SparkleDensity;
                half _SparkleSize;
                half _SparkleVerticalSize;
                half _SparkleTwinkleSpeed;

                half _NoiseScale1;
                half _NoiseScale2;
                half _NoiseSpeed1;
                half _NoiseSpeed2;
                half _Distortion;

                half _BodyFlowScale;
                half _BodyFlowSpeed;
                half _BodyFlowStrength;

                half _EnableCaustics;
                half4 _CausticColor;
                half _CausticScale;
                half _CausticSpeed;
                half _CausticStrength;
                half _CausticSharpness;
                half _CausticFadeDepth;

                half _EnableRays;
                half4 _RayColor;
                half _RayStrength;
                half _RaySpacing;
                half _RayMinWidth;
                half _RayMaxWidth;
                half _RayEdgeSoftness;
                half _RayDanceAmount;
                half _RayDanceSpeed;
                half _RaySpawnRate;
                half _RayBrightnessVariance;
                half _RayCoreHalfAngleDeg;
                half _RayCorePower;
                half _RayDepthLossPerMeter;
                half _RayFadeDepth;
                half _RayConeHalfAngleDeg;
                half _RayConeSoftnessDeg;
                half _RayConePower;
                half _RayBelowViewerFade;
                half _RayBeyondViewerStrength;

                half _RayAlignmentStart;
                half _RayAlignmentPower;

                half _EnableSpecks;
                half4 _SpeckColor;
                half _SpeckCellSize;
                half _SpeckAmount;
                half _SpeckBrightness;
                half _SpeckDriftX;
                half _SpeckDriftY;

                half _SurfaceY;
                half _SurfaceBand;
                half _SparkleBand;
                half _DepthColorDistance;
                half _DepthPower;
            CBUFFER_END

            // Per-renderer local presentation data.
            // These are intentionally NOT gameplay state. WaterViewEffectsController
            // supplies them through a MaterialPropertyBlock for the local viewer.
            float4 _DontSinkViewerPositionWS;
            float4 _DontSinkSunPositionWS;
            float _DontSinkSunVisibility;
            float _DontSinkSparkleVisibility;

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

                return lerp(
                    lerp(a, b, u.x),
                    lerp(c, d, u.x),
                    u.y);
            }

            float Fbm3(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.57;

                value += Noise(p) * amplitude;
                p = p * 2.03 + 11.71;
                amplitude *= 0.5;

                value += Noise(p) * amplitude;
                p = p * 2.01 + 7.37;
                amplitude *= 0.5;

                value += Noise(p) * amplitude;

                return value;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs pos =
                    GetVertexPositionInputs(
                        IN.positionOS.xyz);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv = IN.uv;
                OUT.uv2 = IN.uv2;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float2 worldXY = IN.positionWS.xy;

                // uv2.x: local sampled surface Y for this mesh column.
                // uv2.y: local wave slope magnitude, supplied by WaterMeshRenderer.
                float surfaceY = IN.uv2.x;
                float surfaceSlope = max(0.0, IN.uv2.y);

                float worldDepthFromSurface =
                    max(
                        0.0,
                        surfaceY -
                        IN.positionWS.y);

                // -------------------------------------------------------------
                // EXISTING DEPTH MODEL
                // Intentionally preserved.
                // -------------------------------------------------------------
                float n1 =
                    Noise(
                        worldXY * _NoiseScale1 +
                        float2(
                            t * _NoiseSpeed1,
                            t * 0.07));

                float n2 =
                    Noise(
                        worldXY * _NoiseScale2 +
                        float2(
                            t * _NoiseSpeed2,
                            -t * 0.05));

                float combined =
                    lerp(
                        n1,
                        n2,
                        0.5);

                float depthWobble =
                    (combined - 0.5) *
                    _Distortion;

                float d =
                    max(
                        0.0,
                        worldDepthFromSurface +
                        depthWobble);

                float shallowY =
                    max(
                        0.0,
                        _ShallowDepthY);

                float midY =
                    max(
                        shallowY + 0.001,
                        _MidDepthY);

                float deepY =
                    max(
                        midY + 0.001,
                        _DeepDepthY);

                float soft =
                    max(
                        0.001,
                        _DepthBlendSoftness);

                float shallowToMid =
                    smoothstep(
                        shallowY - soft,
                        midY + soft,
                        d);

                float midToDeep =
                    smoothstep(
                        midY - soft,
                        deepY + soft,
                        d);

                half3 colorSM =
                    lerp(
                        _ShallowColor.rgb,
                        _MidColor.rgb,
                        shallowToMid);

                half3 colorMD =
                    lerp(
                        _MidColor.rgb,
                        _DeepColor.rgb,
                        midToDeep);

                half midSelector =
                    step(
                        midY,
                        d);

                half3 baseColor =
                    lerp(
                        colorSM,
                        colorMD,
                        midSelector);

                half alphaSM =
                    lerp(
                        _ShallowColor.a,
                        _MidColor.a,
                        shallowToMid);

                half alphaMD =
                    lerp(
                        _MidColor.a,
                        _DeepColor.a,
                        midToDeep);

                half alpha =
                    lerp(
                        alphaSM,
                        alphaMD,
                        midSelector);

                half3 color =
                    baseColor;

                // -------------------------------------------------------------
                // Broad slow body motion.
                // -------------------------------------------------------------
                float flow =
                    Fbm3(
                        worldXY * _BodyFlowScale +
                        float2(
                            t * _BodyFlowSpeed,
                            -t * _BodyFlowSpeed * 0.41));

                color *=
                    1.0 +
                    (flow - 0.5) *
                    2.0 *
                    _BodyFlowStrength;

                // -------------------------------------------------------------
                // CAUSTICS
                // Two moving warped wave fields, strongest near the surface.
                // -------------------------------------------------------------
                if (_EnableCaustics > 0.5)
                {
                    float warp =
                        Fbm3(
                            worldXY * 0.27 +
                            float2(
                                t * 0.025,
                                -t * 0.019));

                    float2 cp =
                        worldXY *
                        _CausticScale;

                    cp +=
                        (warp - 0.5) *
                        1.7;

                    float c1 =
                        sin(
                            cp.x * 1.11 +
                            sin(
                                cp.y * 0.73 +
                                t * _CausticSpeed) *
                            1.35 +
                            t * _CausticSpeed * 1.7);

                    float c2 =
                        sin(
                            cp.y * 1.27 +
                            sin(
                                cp.x * 0.61 -
                                t * _CausticSpeed * 0.83) *
                            1.17 -
                            t * _CausticSpeed * 1.21);

                    float causticRaw =
                        1.0 -
                        abs(
                            c1 *
                            c2);

                    float caustic =
                        pow(
                            saturate(
                                causticRaw),
                            _CausticSharpness);

                    float causticDepthFade =
                        1.0 -
                        saturate(
                            worldDepthFromSurface /
                            max(
                                0.001,
                                _CausticFadeDepth));

                    caustic *=
                        causticDepthFade *
                        _CausticStrength;

                    color +=
                        _CausticColor.rgb *
                        caustic;
                }

                // -------------------------------------------------------------
                // VIEW-DEPENDENT SUN RAYS
                //
                // Each bright shaft belongs to a moving entry point on the water
                // surface and points back toward the actual scene sun position.
                // Visibility is strongest where the fragment->sun direction and
                // fragment->viewer direction line up. That makes the whole bundle
                // follow the local player's point of view without turning the
                // effect into globally parallel wallpaper.
                // -------------------------------------------------------------
                if (_EnableRays > 0.5 &&
                    _DontSinkViewerPositionWS.w > 0.5 &&
                    _DontSinkSunPositionWS.w > 0.5)
                {
                    float2 viewerPosition =
                        _DontSinkViewerPositionWS.xy;

                    float2 sunPosition =
                        _DontSinkSunPositionWS.xy;

                    float2 toSunVector =
                        sunPosition -
                        worldXY;

                    float2 toViewerVector =
                        viewerPosition -
                        worldXY;

                    float sunDistance =
                        max(
                            0.001,
                            length(
                                toSunVector));

                    float viewerDistance =
                        max(
                            0.001,
                            length(
                                toViewerVector));

                    float2 toSun =
                        toSunVector /
                        sunDistance;

                    float2 toViewer =
                        toViewerVector /
                        viewerDistance;

                    // VIEW GEOMETRY
                    //
                    // The desired visible region is a ONE-SIDED CONE:
                    //
                    //                         SUN
                    //                          *
                    //                         /|\
                    //                        / | \
                    //                 ~~~~~~/~~|~~\~~~~~~ surface
                    //                      /   |   \
                    //                     /    |    \
                    //                    /   VIEWER  \
                    //
                    // The cone axis is Sun -> local viewer.
                    //
                    // The previous abs(dot(fragment->sun, fragment->viewer))
                    // test described an infinite bidirectional LINE instead.
                    // That creates exactly the observed bug: a dead band near
                    // the viewer, then rays reappear far to either side because
                    // distant points see the Sun and viewer in almost the same
                    // direction again.
                    float2 sunToViewerVector =
                        viewerPosition -
                        sunPosition;

                    float sunToViewerDistance =
                        max(
                            0.001,
                            length(
                                sunToViewerVector));

                    float2 sunToViewerDirection =
                        sunToViewerVector /
                        sunToViewerDistance;

                    float2 sunToFragmentVector =
                        worldXY -
                        sunPosition;

                    float sunToFragmentDistance =
                        max(
                            0.001,
                            length(
                                sunToFragmentVector));

                    float2 sunToFragmentDirection =
                        sunToFragmentVector /
                        sunToFragmentDistance;

                    // 1 on the cone axis, progressively smaller as the water
                    // fragment moves laterally away from the player's sun-line.
                    // Crucially: NO abs(). This is a forward cone from the Sun,
                    // not an infinite line extending in both directions.
                    float coneDirectionDot =
                        dot(
                            sunToFragmentDirection,
                            sunToViewerDirection);

                    float coneOuterAngle =
                        radians(
                            max(
                                0.1,
                                _RayConeHalfAngleDeg));

                    float coneInnerAngle =
                        radians(
                            max(
                                0.0,
                                _RayConeHalfAngleDeg -
                                _RayConeSoftnessDeg));

                    float coneOuterCos =
                        cos(
                            coneOuterAngle);

                    float coneInnerCos =
                        cos(
                            coneInnerAngle);

                    float viewConeMask =
                        smoothstep(
                            coneOuterCos,
                            coneInnerCos,
                            coneDirectionDot);

                    viewConeMask =
                        pow(
                            saturate(
                                viewConeMask),
                            _RayConePower);

                    // CENTER EMPHASIS
                    //
                    // The broad cone defines the allowed region.
                    // Inside that, a narrower "core" makes shafts ramp up
                    // toward the exact Sun->viewer axis and fall away quickly
                    // a few degrees to either side.
                    float coreOuterAngle =
                        radians(
                            max(
                                0.01,
                                _RayCoreHalfAngleDeg));

                    float coreOuterCos =
                        cos(
                            coreOuterAngle);

                    float coreMask =
                        smoothstep(
                            coreOuterCos,
                            1.0,
                            coneDirectionDot);

                    coreMask =
                        pow(
                            saturate(
                                coreMask),
                            _RayCorePower);

                    // Keep a faint residual presence in the outer cone so the
                    // field still feels volumetric, while heavily favoring the
                    // central bundle.
                    float viewConeWeight =
                        viewConeMask *
                        lerp(
                            0.08,
                            1.0,
                            coreMask);

                    // Find where the sun->fragment line crosses THIS COLUMN'S
                    // live wave surface. This gives every ray a real surface
                    // origin and naturally creates a fan rather than parallel
                    // vertical stripes.
                    float sunVerticalDistance =
                        sunPosition.y -
                        worldXY.y;

                    float safeSunVerticalDistance =
                        abs(
                            sunVerticalDistance) >
                        0.0001
                            ? sunVerticalDistance
                            : 0.0001;

                    float surfaceT =
                        saturate(
                            (surfaceY -
                             worldXY.y) /
                            safeSunVerticalDistance);

                    float surfaceEntryX =
                        worldXY.x +
                        (sunPosition.x -
                         worldXY.x) *
                        surfaceT;

                    float spacing =
                        max(
                            0.05,
                            _RaySpacing);

                    float rayCoordinate =
                        surfaceEntryX /
                        spacing;

                    float rayCell =
                        floor(
                            rayCoordinate);

                    float rayLocal =
                        frac(
                            rayCoordinate) -
                        0.5;

                    float raySeed =
                        Hash21(
                            float2(
                                rayCell,
                                19.17));

                    float raySeed2 =
                        Hash21(
                            float2(
                                rayCell + 31.7,
                                71.3));

                    // GENERATIONAL RAYS
                    //
                    // Instead of the same stripes endlessly sliding back and
                    // forth, each surface ray cell periodically "rebirths" a
                    // new shaft with a new center, width, brightness, and motion.
                    float spawnRate =
                        max(
                            0.001,
                            _RaySpawnRate) *
                        lerp(
                            0.82,
                            1.28,
                            raySeed2);

                    float generationClock =
                        t *
                        spawnRate +
                        raySeed *
                        7.13;

                    float generationIndex =
                        floor(
                            generationClock);

                    float generationPhase =
                        frac(
                            generationClock);

                    float lifeEnvelope =
                        smoothstep(
                            0.0,
                            0.18,
                            generationPhase) *
                        (1.0 -
                         smoothstep(
                             0.72,
                             1.0,
                             generationPhase));

                    float generationSeedA =
                        Hash21(
                            float2(
                                rayCell * 1.37 +
                                generationIndex * 11.7,
                                101.3));

                    float generationSeedB =
                        Hash21(
                            float2(
                                rayCell * 0.91 +
                                generationIndex * 17.9,
                                59.1));

                    float generationSeedC =
                        Hash21(
                            float2(
                                rayCell * 1.83 +
                                generationIndex * 5.3,
                                149.8));

                    float dancePhase =
                        t *
                        _RayDanceSpeed *
                        lerp(
                            0.72,
                            1.32,
                            raySeed2) +
                        generationSeedA *
                        TWO_PI;

                    float danceOffset =
                        sin(
                            dancePhase) *
                        _RayDanceAmount;

                    danceOffset +=
                        sin(
                            dancePhase *
                            0.43 +
                            generationSeedB *
                            11.0) *
                        _RayDanceAmount *
                        0.35;

                    float randomCenter =
                        (generationSeedA -
                         0.5) *
                        0.24;

                    float rayCenter =
                        randomCenter +
                        danceOffset;

                    float rayWidth =
                        lerp(
                            _RayMinWidth,
                            _RayMaxWidth,
                            generationSeedB);

                    float rayDistance =
                        abs(
                            rayLocal -
                            rayCenter);

                    // Soft gaussian-like profile: no hard stripe edge, just a
                    // brighter core that fades smoothly outward.
                    float softExtent =
                        max(
                            0.001,
                            rayWidth +
                            _RayEdgeSoftness *
                            1.75);

                    float normalizedDistance =
                        rayDistance /
                        softExtent;

                    float beam =
                        exp(
                            -normalizedDistance *
                            normalizedDistance *
                            2.6);

                    // Add a wide faint halo so shafts feel volumetric instead
                    // of cut with scissors.
                    float halo =
                        exp(
                            -normalizedDistance *
                            normalizedDistance *
                            0.55) *
                        0.35;

                    beam =
                        saturate(
                            beam +
                            halo);

                    float brightnessFloor =
                        lerp(
                            1.0,
                            0.35,
                            saturate(
                                _RayBrightnessVariance));

                    float randomIntensity =
                        lerp(
                            brightnessFloor,
                            1.25,
                            generationSeedC);

                    // Life envelope gives visible "birth" and "death" of shafts.
                    randomIntensity *=
                        lerp(
                            0.15,
                            1.0,
                            lifeEnvelope);

                    // Additional breathing so live shafts shimmer instead of
                    // holding a flat constant brightness.
                    float breath =
                        0.66 +
                        0.34 *
                        sin(
                            t *
                            _RayDanceSpeed *
                            lerp(
                                0.45,
                                1.05,
                                generationSeedA) +
                            generationSeedB *
                            TWO_PI);

                    breath =
                        saturate(
                            breath);

                    // DEPTH ATTENUATION
                    //
                    // Rays should be strongest near the surface, lose strength
                    // continuously with depth, and then be decisively killed at
                    // the user-chosen fade depth.
                    float depthLoss =
                        exp(
                            -worldDepthFromSurface *
                            max(
                                0.0,
                                _RayDepthLossPerMeter));

                    float fadeDepth =
                        max(
                            0.001,
                            _RayFadeDepth);

                    float terminalFadeStart =
                        max(
                            0.0,
                            fadeDepth *
                            0.82);

                    float fadeKill =
                        1.0 -
                        smoothstep(
                            terminalFadeStart,
                            fadeDepth,
                            worldDepthFromSurface);

                    float depthFade =
                        depthLoss *
                        fadeKill;

                    // DEPTH RELATIVE TO THE VIEWER
                    //
                    // Position along the SAME Sun -> Viewer axis that defines
                    // the cone. Between surface and viewer is full strength.
                    // Past the viewer remains visible, but fades toward a lower
                    // residual strength instead of vanishing or becoming the
                    // preferred region.
                    float fragmentAlongRay =
                        dot(
                            worldXY -
                            sunPosition,
                            sunToViewerDirection);

                    float pastViewerDistance =
                        max(
                            0.0,
                            fragmentAlongRay -
                            sunToViewerDistance);

                    float pastViewer01 =
                        smoothstep(
                            0.0,
                            max(
                                0.001,
                                _RayBelowViewerFade),
                            pastViewerDistance);

                    float viewSegmentWeight =
                        lerp(
                            1.0,
                            _RayBeyondViewerStrength,
                            pastViewer01);

                    float ray =
                        beam *
                        randomIntensity *
                        breath *
                        viewConeWeight *
                        depthFade *
                        viewSegmentWeight *
                        _RayStrength *
                        saturate(
                            _DontSinkSunVisibility);

                    color +=
                        _RayColor.rgb *
                        ray;
                }

                // -------------------------------------------------------------
                // SUSPENDED SPECKS
                // Atmosphere only. Recognizable bubbles should be particles.
                // -------------------------------------------------------------
                if (_EnableSpecks > 0.5)
                {
                    float cellSize =
                        max(
                            0.02,
                            _SpeckCellSize);

                    float2 p =
                        worldXY /
                        cellSize +
                        float2(
                            t * _SpeckDriftX,
                            t * _SpeckDriftY);

                    float2 cell =
                        floor(p);

                    float2 local =
                        frac(p) -
                        0.5;

                    float exists =
                        step(
                            1.0 -
                            saturate(
                                _SpeckAmount) *
                            0.34,
                            Hash21(cell));

                    float2 offset =
                        float2(
                            Hash21(cell + 17.3),
                            Hash21(cell + 41.7)) -
                        0.5;

                    float dist =
                        length(
                            local -
                            offset * 0.68);

                    float speck =
                        exists *
                        (1.0 -
                         smoothstep(
                             0.012,
                             0.050,
                             dist));

                    color +=
                        _SpeckColor.rgb *
                        speck *
                        _SpeckBrightness;
                }

                // -------------------------------------------------------------
                // FOAM
                // Follows the actual dynamic wave surface from uv2.x.
                // uv2.y makes active/sloped water foamier than calm flat water.
                // -------------------------------------------------------------
                float foamStart =
                    min(
                        _FoamBandStart,
                        _FoamBandEnd);

                float foamEnd =
                    max(
                        _FoamBandStart,
                        _FoamBandEnd);

                float foamSoft =
                    max(
                        0.001,
                        _FoamEdgeSoftness);

                float foamEnter =
                    smoothstep(
                        foamStart - foamSoft,
                        foamStart + foamSoft,
                        worldDepthFromSurface);

                float foamExit =
                    1.0 -
                    smoothstep(
                        foamEnd - foamSoft,
                        foamEnd + foamSoft,
                        worldDepthFromSurface);

                float foamBand =
                    saturate(
                        foamEnter *
                        foamExit);

                float foamNoise =
                    Fbm3(
                        float2(
                            worldXY.x *
                            _FoamPatchScale +
                            t * _FoamPatchSpeed,
                            surfaceY *
                            _FoamPatchScale * 0.37 -
                            t * _FoamPatchSpeed * 0.31));

                float coverageThreshold =
                    lerp(
                        0.82,
                        0.24,
                        saturate(
                            _FoamCoverage));

                float foamPatch =
                    smoothstep(
                        coverageThreshold - 0.10,
                        coverageThreshold + 0.10,
                        foamNoise);

                float slope01 =
                    saturate(
                        surfaceSlope /
                        max(
                            0.001,
                            _FoamSlopeReference));

                float slopeWeight =
                    lerp(
                        1.0,
                        0.28 + 0.72 * slope01,
                        _FoamSlopeInfluence);

                float foamMask =
                    foamBand *
                    foamPatch *
                    slopeWeight *
                    _FoamIntensity;

                color =
                    lerp(
                        color,
                        _FoamColor.rgb,
                        saturate(
                            foamMask *
                            _FoamColor.a));

                alpha =
                    saturate(
                        alpha +
                        foamMask *
                        _FoamColor.a *
                        0.14);

                // -------------------------------------------------------------
                // SURFACE SPARKLES
                //
                // These are intentionally 1D along the live water surface, then
                // drawn as tiny cross-shaped glints just under that surface.
                // That makes them readable at game scale instead of disappearing
                // into the suspended-speck field.
                // -------------------------------------------------------------
                float sparkleCellSize =
                    max(
                        0.05,
                        _SparkleCellSize);

                float sparkleCoordinate =
                    worldXY.x /
                    sparkleCellSize;

                float sparkleCell =
                    floor(
                        sparkleCoordinate);

                float sparkleLocalX =
                    frac(
                        sparkleCoordinate) -
                    0.5;

                float sparkleSeed =
                    Hash21(
                        float2(
                            sparkleCell,
                            83.17));

                float sparkleExists =
                    step(
                        1.0 -
                        saturate(
                            _SparkleDensity),
                        sparkleSeed);

                float sparkleOffset =
                    (Hash21(
                        float2(
                            sparkleCell + 17.2,
                            4.3)) -
                     0.5) *
                    0.64;

                float horizontalDistance =
                    abs(
                        sparkleLocalX -
                        sparkleOffset);

                float sparkleDepthCenter =
                    lerp(
                        0.025,
                        max(
                            0.03,
                            _SparkleDepth * 0.42),
                        Hash21(
                            float2(
                                sparkleCell + 6.1,
                                42.9)));

                float verticalDistance =
                    abs(
                        worldDepthFromSurface -
                        sparkleDepthCenter);

                float horizontalArm =
                    (1.0 -
                     smoothstep(
                         _SparkleSize,
                         _SparkleSize * 2.0,
                         horizontalDistance)) *
                    (1.0 -
                     smoothstep(
                         _SparkleVerticalSize * 0.35,
                         _SparkleVerticalSize,
                         verticalDistance));

                float verticalArm =
                    (1.0 -
                     smoothstep(
                         _SparkleSize * 0.18,
                         _SparkleSize * 0.52,
                         horizontalDistance)) *
                    (1.0 -
                     smoothstep(
                         _SparkleVerticalSize,
                         _SparkleVerticalSize * 2.7,
                         verticalDistance));

                float sparkleShape =
                    max(
                        horizontalArm,
                        verticalArm);

                float twinklePhase =
                    sparkleSeed *
                    47.0 +
                    t *
                    _SparkleTwinkleSpeed;

                float twinkle =
                    0.5 +
                    0.5 *
                    sin(
                        twinklePhase);

                twinkle =
                    smoothstep(
                        0.28,
                        0.92,
                        twinkle);

                // A small nonzero floor means a selected sparkle is normally
                // visible, then flares brighter instead of existing for one
                // mathematically unfortunate frame every few seconds.
                twinkle =
                    lerp(
                        0.18,
                        1.0,
                        twinkle);

                float sparkleDepthMask =
                    1.0 -
                    smoothstep(
                        0.0,
                        max(
                            0.02,
                            _SparkleDepth),
                        worldDepthFromSurface);

                // Sparkles have their own perceptual celestial response.
                // Rays and sparkles still share the same active Sun/Moon source,
                // but moon glints can be lifted without making moon shafts absurdly bright.
                float celestialSparkleStrength =
                    saturate(
                        _DontSinkSparkleVisibility);

                float sparkles =
                    sparkleExists *
                    sparkleShape *
                    twinkle *
                    sparkleDepthMask *
                    _SparkleIntensity *
                    celestialSparkleStrength;

                color +=
                    _SparkleColor.rgb *
                    sparkles;

                color *= _DontSinkDepthLightingEnabled > .5
                    ? DontSinkDepthLight(IN.positionWS.xy) : float3(_Brightness, _Brightness, _Brightness);

                return
                    half4(
                        color,
                        alpha);
            }

            ENDHLSL
        }
    }
}
