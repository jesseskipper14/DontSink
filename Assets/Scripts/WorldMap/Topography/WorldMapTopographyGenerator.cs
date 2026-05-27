using System.Collections.Generic;
using UnityEngine;

public static class WorldMapTopographyGenerator
{
    private struct ChainFeature
    {
        public Vector2 p0;
        public Vector2 p1;
        public Vector2 p2;
        public float ridgeWidth;
        public float ridgeStrength;
        public float shelfWidth;
        public float shelfStrength;
        public int sampleSteps;
        public List<PeakFeature> peaks;
    }

    private struct PeakFeature
    {
        public Vector2 center;

        public float radiusX;
        public float radiusY;

        public float cos;
        public float sin;

        public float strength;

        public float edgeWarpStrength;
        public float edgeWarpFrequency;
        public float edgeWarpPhaseA;
        public float edgeWarpPhaseB;

        public float interiorRoughness;
        public float interiorNoiseScale;
        public Vector2 noiseOffset;
    }

    private struct BasinFeature
    {
        public Vector2 center;
        public float radiusX;
        public float radiusY;
        public float cos;
        public float sin;
        public float strength;
    }

    private struct TrenchFeature
    {
        public Vector2 p0;
        public Vector2 p1;
        public Vector2 p2;
        public float width;
        public float strength;
        public int sampleSteps;
    }

    private sealed class FeatureSet
    {
        public readonly List<ChainFeature> chains = new();
        public readonly List<BasinFeature> basins = new();
        public readonly List<TrenchFeature> trenches = new();
    }

    public static WorldMapTopographyField Generate(int worldSeed, WorldMapTopographySettings settings)
    {
        if (settings == null)
        {
            Debug.LogError("[WorldMapTopographyGenerator] Missing settings.");
            return null;
        }

        int res = Mathf.Max(8, settings.heightResolution);
        float[] raw = new float[res * res];
        float[] normalized = new float[res * res];

        Rect bounds = new Rect(
            -settings.worldSize.x * 0.5f,
            -settings.worldSize.y * 0.5f,
            settings.worldSize.x,
            settings.worldSize.y
        );

        int combinedSeed = unchecked(worldSeed ^ settings.seedSalt);
        var rng = new System.Random(combinedSeed);

        Vector2 offsetA = RandomOffset(rng);
        Vector2 offsetB = RandomOffset(rng);
        Vector2 offsetC = RandomOffset(rng);

        FeatureSet features = settings.useStructuredOceanFloor
            ? BuildFeatureSet(rng, bounds, settings)
            : null;

        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;

        for (int y = 0; y < res; y++)
        {
            float v = res <= 1 ? 0f : y / (float)(res - 1);

            for (int x = 0; x < res; x++)
            {
                float u = res <= 1 ? 0f : x / (float)(res - 1);

                Vector2 world = new Vector2(
                    Mathf.Lerp(bounds.xMin, bounds.xMax, u),
                    Mathf.Lerp(bounds.yMin, bounds.yMax, v)
                );

                float h = EvaluateBaseNoise(world, offsetA, offsetB, offsetC, settings);

                if (features != null)
                {
                    float raisedFeatureFade = settings.fadeRaisedFeaturesNearEdge
                        ? EdgeRaisedFeatureFade(u, v, settings)
                        : 1f;

                    h += EvaluateStructuredFeatures(world, features, raisedFeatureFade);
                }

                if (settings.useRadialFalloff)
                {
                    Vector2 centered = new Vector2((u * 2f) - 1f, (v * 2f) - 1f);
                    float d = Mathf.Clamp01(centered.magnitude / 1.41421356f);

                    float falloff = Mathf.Pow(d, settings.radialFalloffPower);
                    h -= falloff * settings.radialFalloffStrength;
                }

                if (settings.useOceanBorderFalloff)
                {
                    h -= EvaluateOceanBorderFalloff(u, v, settings);
                }

                h = ApplyContrastBias(h, settings.contrast, settings.bias);

                int idx = y * res + x;
                raw[idx] = h;

                if (h < min) min = h;
                if (h > max) max = h;
            }
        }

        float range = Mathf.Max(0.0001f, max - min);

        for (int i = 0; i < raw.Length; i++)
            normalized[i] = Mathf.Clamp01((raw[i] - min) / range);

        return new WorldMapTopographyField(
            worldSeed,
            res,
            res,
            bounds,
            normalized,
            min,
            max
        );
    }

    private static float EvaluateBaseNoise(
        Vector2 world,
        Vector2 offsetA,
        Vector2 offsetB,
        Vector2 offsetC,
        WorldMapTopographySettings settings)
    {
        float baseLayer = FractalNoise(
            world,
            offsetA,
            settings.baseNoiseScale,
            settings.octaves,
            settings.persistence,
            settings.lacunarity
        );

        float broadLayer = FractalNoise(
            world,
            offsetB,
            settings.baseNoiseScale * 0.28f,
            Mathf.Max(1, settings.octaves - 2),
            settings.persistence,
            settings.lacunarity
        );

        float detailLayer = FractalNoise(
            world,
            offsetC,
            settings.baseNoiseScale * 2.6f,
            Mathf.Max(1, settings.octaves - 2),
            settings.persistence * 0.75f,
            settings.lacunarity
        );

        float h = Mathf.Lerp(baseLayer, broadLayer, 0.48f);
        h = Mathf.Lerp(h, detailLayer, 0.13f);

        // Center the raw noise around 0-ish before structured features.
        return h - 0.5f;
    }

    private static FeatureSet BuildFeatureSet(
        System.Random rng,
        Rect bounds,
        WorldMapTopographySettings settings)
    {
        var features = new FeatureSet();

        for (int i = 0; i < settings.basinCount; i++)
            features.basins.Add(CreateBasin(rng, bounds, settings));

        List<Vector2> chainCenters = new();

        for (int i = 0; i < settings.islandChainCount; i++)
        {
            ChainFeature chain = CreateChain(rng, bounds, settings, chainCenters);
            features.chains.Add(chain);
            chainCenters.Add(GetBezierMidpoint(chain.p0, chain.p1, chain.p2));
        }

        for (int i = 0; i < settings.trenchCount; i++)
            features.trenches.Add(CreateTrench(rng, bounds, settings));

        return features;
    }

    private static ChainFeature CreateChain(
        System.Random rng,
        Rect bounds,
        WorldMapTopographySettings settings,
        List<Vector2> existingCenters)
    {
        Vector2 center = ChooseChainCenter(rng, bounds, settings, existingCenters);

        float angle = RandRange(rng, 0f, Mathf.PI * 2f);
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 normal = new Vector2(-dir.y, dir.x);

        float length = RandRange(rng, settings.chainLengthMin, settings.chainLengthMax);
        float curve = RandRange(rng, -settings.chainCurveStrength, settings.chainCurveStrength);

        Vector2 p0 = center - dir * (length * 0.5f);
        Vector2 p2 = center + dir * (length * 0.5f);
        Vector2 p1 = center + normal * curve;

        int peakCount = rng.Next(
            settings.volcanicPeaksPerChainMin,
            settings.volcanicPeaksPerChainMax + 1
        );

        var peaks = new List<PeakFeature>(peakCount);

        for (int i = 0; i < peakCount; i++)
        {
            float baseT = (i + 1f) / (peakCount + 1f);
            float t = Mathf.Clamp01(
                baseT + RandRange(rng, -settings.volcanicPeakTangentJitter01, settings.volcanicPeakTangentJitter01)
            );

            Vector2 path = QuadraticBezier(p0, p1, p2, t);
            Vector2 localNormal = BezierNormal(p0, p1, p2, t);

            Vector2 peakCenter =
                path +
                localNormal * RandRange(rng, -settings.volcanicPeakLateralJitter, settings.volcanicPeakLateralJitter);

            float baseRadius = RandRange(
                rng,
                settings.volcanicPeakRadiusMin,
                settings.volcanicPeakRadiusMax
            );

            float stretch = RandRange(rng, 1f, settings.volcanicPeakStretchMax);
            bool stretchX = rng.NextDouble() < 0.5;

            float radiusX = stretchX ? baseRadius * stretch : baseRadius;
            float radiusY = stretchX ? baseRadius : baseRadius * stretch;

            float rotation = RandRange(rng, 0f, Mathf.PI * 2f);

            peaks.Add(new PeakFeature
            {
                center = peakCenter,

                radiusX = Mathf.Max(0.1f, radiusX),
                radiusY = Mathf.Max(0.1f, radiusY),

                cos = Mathf.Cos(rotation),
                sin = Mathf.Sin(rotation),

                strength = RandRange(
                    rng,
                    settings.volcanicPeakStrengthMin,
                    settings.volcanicPeakStrengthMax
                ),

                edgeWarpStrength = settings.volcanicPeakEdgeWarpStrength,
                edgeWarpFrequency = settings.volcanicPeakEdgeWarpFrequency,
                edgeWarpPhaseA = RandRange(rng, 0f, Mathf.PI * 2f),
                edgeWarpPhaseB = RandRange(rng, 0f, Mathf.PI * 2f),

                interiorRoughness = settings.volcanicPeakInteriorRoughness,
                interiorNoiseScale = settings.volcanicPeakInteriorNoiseScale,
                noiseOffset = RandomOffset(rng)
            });
        }

        return new ChainFeature
        {
            p0 = p0,
            p1 = p1,
            p2 = p2,
            ridgeWidth = settings.chainRidgeWidth,
            ridgeStrength = settings.chainRidgeStrength,
            shelfWidth = settings.chainShelfWidth,
            shelfStrength = settings.chainShelfStrength,
            sampleSteps = Mathf.Max(4, settings.chainSampleSteps),
            peaks = peaks
        };
    }

    private static Vector2 ChooseChainCenter(
    System.Random rng,
    Rect bounds,
    WorldMapTopographySettings settings,
    List<Vector2> existingCenters)
    {
        float inset = Mathf.Max(0.01f, settings.featureEdgeInset01);

        if (!settings.enforceChainCenterSpacing || existingCenters == null || existingCenters.Count == 0)
            return RandomPointInBounds(rng, bounds, inset);

        Vector2 best = RandomPointInBounds(rng, bounds, inset);
        float bestScore = -1f;

        int attempts = Mathf.Max(1, settings.chainPlacementAttempts);
        float minDist = Mathf.Max(0f, settings.chainCenterMinDistance);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 candidate = RandomPointInBounds(rng, bounds, inset);

            float nearest = float.PositiveInfinity;
            for (int j = 0; j < existingCenters.Count; j++)
            {
                float d = Vector2.Distance(candidate, existingCenters[j]);
                if (d < nearest)
                    nearest = d;
            }

            // Prefer candidates that satisfy min distance, but keep a best fallback
            // so generation never fails completely.
            float score = nearest;

            if (nearest >= minDist)
                return candidate;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static Vector2 GetBezierMidpoint(Vector2 p0, Vector2 p1, Vector2 p2)
    {
        return QuadraticBezier(p0, p1, p2, 0.5f);
    }

    private static BasinFeature CreateBasin(
        System.Random rng,
        Rect bounds,
        WorldMapTopographySettings settings)
    {
        Vector2 center = RandomPointInBounds(rng, bounds, 0.04f);

        float r = RandRange(rng, settings.basinRadiusMin, settings.basinRadiusMax);
        float stretch = RandRange(rng, 1f, settings.basinStretchMax);

        bool stretchX = rng.NextDouble() < 0.5;

        float rx = stretchX ? r * stretch : r;
        float ry = stretchX ? r : r * stretch;

        float angle = RandRange(rng, 0f, Mathf.PI * 2f);

        return new BasinFeature
        {
            center = center,
            radiusX = Mathf.Max(0.1f, rx),
            radiusY = Mathf.Max(0.1f, ry),
            cos = Mathf.Cos(angle),
            sin = Mathf.Sin(angle),
            strength = RandRange(rng, settings.basinStrengthMin, settings.basinStrengthMax)
        };
    }

    private static TrenchFeature CreateTrench(
        System.Random rng,
        Rect bounds,
        WorldMapTopographySettings settings)
    {
        Vector2 center = RandomPointInBounds(rng, bounds, 0.06f);

        float angle = RandRange(rng, 0f, Mathf.PI * 2f);
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 normal = new Vector2(-dir.y, dir.x);

        float length = RandRange(rng, settings.trenchLengthMin, settings.trenchLengthMax);
        float curve = RandRange(rng, -settings.trenchCurveStrength, settings.trenchCurveStrength);

        Vector2 p0 = center - dir * (length * 0.5f);
        Vector2 p2 = center + dir * (length * 0.5f);
        Vector2 p1 = center + normal * curve;

        return new TrenchFeature
        {
            p0 = p0,
            p1 = p1,
            p2 = p2,
            width = settings.trenchWidth,
            strength = RandRange(rng, settings.trenchStrengthMin, settings.trenchStrengthMax),
            sampleSteps = Mathf.Max(4, settings.trenchSampleSteps)
        };
    }

    private static float EvaluateStructuredFeatures(
        Vector2 world,
        FeatureSet features,
        float raisedFeatureFade)
    {
        float h = 0f;

        for (int i = 0; i < features.basins.Count; i++)
            h -= EvaluateBasin(world, features.basins[i]);

        float raised = 0f;

        for (int i = 0; i < features.chains.Count; i++)
            raised += EvaluateChain(world, features.chains[i]);

        h += raised * Mathf.Clamp01(raisedFeatureFade);

        for (int i = 0; i < features.trenches.Count; i++)
            h -= EvaluateTrench(world, features.trenches[i]);

        return h;
    }

    private static float EvaluateChain(Vector2 world, ChainFeature chain)
    {
        float distance = DistanceToBezierApprox(
            world,
            chain.p0,
            chain.p1,
            chain.p2,
            chain.sampleSteps
        );

        float shelf = SmoothInfluenceByDistance(distance, chain.shelfWidth) * chain.shelfStrength;
        float ridge = SmoothInfluenceByDistance(distance, chain.ridgeWidth) * chain.ridgeStrength;

        float h = shelf + ridge;

        if (chain.peaks != null)
        {
            for (int i = 0; i < chain.peaks.Count; i++)
                h += EvaluatePeak(world, chain.peaks[i]);
        }

        return h;
    }

    private static float EdgeRaisedFeatureFade(
    float u,
    float v,
    WorldMapTopographySettings settings)
    {
        float margin = Mathf.Max(0.0001f, settings.raisedFeatureEdgeFadeMargin01);

        float edgeDist = DistanceToUvEdge(u, v);

        // 0 near edge, 1 after margin.
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeDist / margin));
    }

    private static float EvaluateOceanBorderFalloff(
        float u,
        float v,
        WorldMapTopographySettings settings)
    {
        float margin = Mathf.Max(0.0001f, settings.oceanBorderMargin01);

        float edgeDist = DistanceToUvEdge(u, v);

        if (edgeDist >= margin)
            return 0f;

        float t = 1f - Mathf.Clamp01(edgeDist / margin);
        t = Mathf.Pow(t, Mathf.Max(0.01f, settings.oceanBorderPower));

        return t * Mathf.Max(0f, settings.oceanBorderStrength);
    }

    private static float DistanceToUvEdge(float u, float v)
    {
        return Mathf.Min(
            Mathf.Min(u, 1f - u),
            Mathf.Min(v, 1f - v)
        );
    }

    private static float EvaluatePeak(Vector2 world, PeakFeature peak)
    {
        Vector2 d = world - peak.center;

        // Rotate world point into peak-local space.
        float lx = d.x * peak.cos + d.y * peak.sin;
        float ly = -d.x * peak.sin + d.y * peak.cos;

        float nx = lx / Mathf.Max(0.0001f, peak.radiusX);
        float ny = ly / Mathf.Max(0.0001f, peak.radiusY);

        float angle = Mathf.Atan2(ny, nx);

        // Cheap angular edge warping. This breaks the "perfect circle" contour problem.
        float warpA = Mathf.Sin(angle * peak.edgeWarpFrequency + peak.edgeWarpPhaseA);
        float warpB = Mathf.Sin(angle * (peak.edgeWarpFrequency * 1.73f) + peak.edgeWarpPhaseB) * 0.5f;

        float edgeWarp = 1f + (warpA + warpB) * peak.edgeWarpStrength;
        edgeWarp = Mathf.Max(0.25f, edgeWarp);

        float dist01 = Mathf.Sqrt(nx * nx + ny * ny) / edgeWarp;

        if (dist01 >= 1f)
            return 0f;

        // Smoother volcanic mound, still steep enough to create high terrain.
        float influence = 1f - Mathf.SmoothStep(0f, 1f, dist01);

        // Interior roughness makes the highland/mountain color zones less circular too.
        if (peak.interiorRoughness > 0f)
        {
            float noise = Mathf.PerlinNoise(
                (world.x + peak.noiseOffset.x) * peak.interiorNoiseScale,
                (world.y + peak.noiseOffset.y) * peak.interiorNoiseScale
            );

            float rough = (noise - 0.5f) * 2f;
            influence *= 1f + rough * peak.interiorRoughness;
            influence = Mathf.Clamp01(influence);
        }

        return influence * peak.strength;
    }

    private static float EvaluateBasin(Vector2 world, BasinFeature basin)
    {
        Vector2 d = world - basin.center;

        // Rotate into basin local space.
        float lx = d.x * basin.cos + d.y * basin.sin;
        float ly = -d.x * basin.sin + d.y * basin.cos;

        float nx = lx / Mathf.Max(0.0001f, basin.radiusX);
        float ny = ly / Mathf.Max(0.0001f, basin.radiusY);

        float dist01 = Mathf.Sqrt(nx * nx + ny * ny);

        if (dist01 >= 1f)
            return 0f;

        float influence = 1f - Mathf.SmoothStep(0f, 1f, dist01);
        return influence * basin.strength;
    }

    private static float EvaluateTrench(Vector2 world, TrenchFeature trench)
    {
        float distance = DistanceToBezierApprox(
            world,
            trench.p0,
            trench.p1,
            trench.p2,
            trench.sampleSteps
        );

        float influence = SmoothInfluenceByDistance(distance, trench.width);
        return influence * trench.strength;
    }

    private static float FractalNoise(
        Vector2 world,
        Vector2 offset,
        float baseScale,
        int octaves,
        float persistence,
        float lacunarity)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float sum = 0f;
        float ampSum = 0f;

        int count = Mathf.Max(1, octaves);

        for (int i = 0; i < count; i++)
        {
            float nx = (world.x + offset.x) * baseScale * frequency;
            float ny = (world.y + offset.y) * baseScale * frequency;

            float n = Mathf.PerlinNoise(nx, ny);

            sum += n * amplitude;
            ampSum += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        if (ampSum <= 0.0001f)
            return 0f;

        return sum / ampSum;
    }

    private static float ApplyContrastBias(float h, float contrast, float bias)
    {
        h = (h - 0.5f) * Mathf.Max(0.01f, contrast) + 0.5f;
        h += bias;
        return h;
    }

    private static float SmoothInfluenceByDistance(float distance, float radius)
    {
        if (radius <= 0.0001f)
            return 0f;

        float t = Mathf.Clamp01(distance / radius);
        return 1f - Mathf.SmoothStep(0f, 1f, t);
    }

    private static float DistanceToBezierApprox(
        Vector2 point,
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        int steps)
    {
        steps = Mathf.Max(4, steps);

        Vector2 prev = p0;
        float best = float.PositiveInfinity;

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 cur = QuadraticBezier(p0, p1, p2, t);

            float d = DistanceToSegment(point, prev, cur);
            if (d < best)
                best = d;

            prev = cur;
        }

        return Mathf.Sqrt(best);
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denom = Vector2.Dot(ab, ab);

        if (denom <= 0.000001f)
            return Vector2.SqrMagnitude(p - a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
        Vector2 c = a + ab * t;

        return Vector2.SqrMagnitude(p - c);
    }

    private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    private static Vector2 BezierNormal(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        Vector2 tangent =
            2f * (1f - t) * (p1 - p0) +
            2f * t * (p2 - p1);

        if (tangent.sqrMagnitude <= 0.000001f)
            return Vector2.up;

        tangent.Normalize();
        return new Vector2(-tangent.y, tangent.x);
    }

    private static Vector2 RandomPointInBounds(System.Random rng, Rect bounds, float inset01)
    {
        inset01 = Mathf.Clamp01(inset01);

        float insetX = bounds.width * inset01;
        float insetY = bounds.height * inset01;

        return new Vector2(
            RandRange(rng, bounds.xMin + insetX, bounds.xMax - insetX),
            RandRange(rng, bounds.yMin + insetY, bounds.yMax - insetY)
        );
    }

    private static Vector2 RandomOffset(System.Random rng)
    {
        return new Vector2(
            rng.Next(-100000, 100000),
            rng.Next(-100000, 100000)
        );
    }

    private static float RandRange(System.Random rng, float min, float max)
    {
        if (max < min)
            (min, max) = (max, min);

        return Mathf.Lerp(min, max, (float)rng.NextDouble());
    }
}