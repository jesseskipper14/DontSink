using System.Collections.Generic;
using UnityEngine;

public static class WorldMapPOIGenerator
{
    private struct Candidate
    {
        public Vector2 position;
        public float u;
        public float v;
        public float height01;
        public float depth01;
        public WorldMapTopographyClass centerClass;
        public Metrics metrics;
    }

    private struct ScoredCandidate
    {
        public Candidate candidate;
        public float score;
    }

    private struct Metrics
    {
        public int totalSamples;

        public int deepOcean;
        public int openOcean;
        public int shelfWater;
        public int shallowWater;
        public int beach;
        public int lowland;
        public int highland;
        public int mountain;

        public float minHeight01;
        public float maxHeight01;

        public int WaterCount => deepOcean + openOcean + shelfWater + shallowWater;
        public int LandCount => beach + lowland + highland + mountain;

        public float DeepOcean01 => Ratio(deepOcean);
        public float OpenOcean01 => Ratio(openOcean);
        public float ShelfWater01 => Ratio(shelfWater);
        public float ShallowWater01 => Ratio(shallowWater);
        public float Beach01 => Ratio(beach);
        public float Lowland01 => Ratio(lowland);
        public float Highland01 => Ratio(highland);
        public float Mountain01 => Ratio(mountain);

        public float Water01 => Ratio(WaterCount);
        public float Land01 => Ratio(LandCount);
        public float ShallowShelf01 => ShelfWater01 + ShallowWater01;
        public float DeepOpen01 => DeepOcean01 + OpenOcean01;
        public float Ruggedness01 => Mathf.Clamp01((maxHeight01 - minHeight01) * 2.5f);

        public float CoastPresence01
        {
            get
            {
                if (WaterCount <= 0 || LandCount <= 0)
                    return 0f;

                return 1f - Mathf.Abs(Water01 - Land01);
            }
        }

        private float Ratio(int count)
        {
            return totalSamples <= 0 ? 0f : count / (float)totalSamples;
        }
    }

    public static WorldMapPOILayer Generate(
        int seed,
        WorldMapTopographyField field,
        WorldMapTopographySettings topographySettings,
        float effectiveSeaLevel01,
        WorldMapPOIGenerationSettings settings,
        WorldMapPOICatalog catalog)
    {
        var layer = new WorldMapPOILayer
        {
            seed = seed,
            worldBounds = field != null ? field.WorldBounds : default,
            pois = new List<WorldMapPOIInstance>()
        };

        if (field == null || !field.IsValid || topographySettings == null || settings == null || catalog == null || catalog.Count <= 0)
            return layer;

        float sea = Mathf.Clamp01(effectiveSeaLevel01 > 0f ? effectiveSeaLevel01 : topographySettings.seaLevel01);

        List<Candidate> candidates = BuildCandidates(
            seed,
            field,
            topographySettings,
            sea,
            settings
        );

        for (int i = 0; i < catalog.Count; i++)
        {
            WorldMapPOIDef def = catalog.GetAt(i);
            if (def == null || string.IsNullOrWhiteSpace(def.poiId) || def.targetCount <= 0)
                continue;

            AddPOIsForDefinition(layer, candidates, def, seed, settings);
        }

        return layer;
    }

    private static List<Candidate> BuildCandidates(
        int seed,
        WorldMapTopographyField field,
        WorldMapTopographySettings topographySettings,
        float sea,
        WorldMapPOIGenerationSettings settings)
    {
        int gw = Mathf.Max(1, settings.candidateGridWidth);
        int gh = Mathf.Max(1, settings.candidateGridHeight);
        float edgeInset = Mathf.Clamp01(settings.candidateEdgeInset01);

        var candidates = new List<Candidate>(gw * gh / 2);

        for (int y = 0; y < gh; y++)
        {
            float v = (y + 0.5f) / gh;

            for (int x = 0; x < gw; x++)
            {
                float u = (x + 0.5f) / gw;

                if (DistanceToUvEdge(u, v) < edgeInset)
                    continue;

                float h = field.Sample01UV(u, v);

                // Global rule for this phase: every generated POI center is underwater.
                if (h >= sea)
                    continue;

                WorldMapTopographyClass centerClass = ClassifyHeight(h, topographySettings, sea);

                Metrics metrics = CalculateMetrics(
                    field,
                    topographySettings,
                    sea,
                    settings,
                    u,
                    v
                );

                candidates.Add(new Candidate
                {
                    position = new Vector2(
                        Mathf.Lerp(field.WorldBounds.xMin, field.WorldBounds.xMax, u),
                        Mathf.Lerp(field.WorldBounds.yMin, field.WorldBounds.yMax, v)
                    ),
                    u = u,
                    v = v,
                    height01 = h,
                    depth01 = Mathf.Clamp01(sea - h),
                    centerClass = centerClass,
                    metrics = metrics
                });
            }
        }

        return candidates;
    }

    private static Metrics CalculateMetrics(
        WorldMapTopographyField field,
        WorldMapTopographySettings topographySettings,
        float sea,
        WorldMapPOIGenerationSettings settings,
        float centerU,
        float centerV)
    {
        var m = new Metrics
        {
            minHeight01 = 1f,
            maxHeight01 = 0f
        };

        int grid = Mathf.Max(1, settings.sampleGrid);
        if (grid % 2 == 0)
            grid++;

        float radiusU = settings.sampleRadiusWorld / Mathf.Max(0.0001f, field.WorldBounds.width);
        float radiusV = settings.sampleRadiusWorld / Mathf.Max(0.0001f, field.WorldBounds.height);

        for (int sy = 0; sy < grid; sy++)
        {
            float ty = grid <= 1 ? 0f : sy / (float)(grid - 1);
            float oy = Mathf.Lerp(-radiusV, radiusV, ty);

            for (int sx = 0; sx < grid; sx++)
            {
                float tx = grid <= 1 ? 0f : sx / (float)(grid - 1);
                float ox = Mathf.Lerp(-radiusU, radiusU, tx);

                float u = Mathf.Clamp01(centerU + ox);
                float v = Mathf.Clamp01(centerV + oy);
                float h = field.Sample01UV(u, v);

                if (h < m.minHeight01) m.minHeight01 = h;
                if (h > m.maxHeight01) m.maxHeight01 = h;

                AddClass(ref m, ClassifyHeight(h, topographySettings, sea));
                m.totalSamples++;
            }
        }

        return m;
    }

    private static void AddPOIsForDefinition(
        WorldMapPOILayer layer,
        List<Candidate> candidates,
        WorldMapPOIDef def,
        int seed,
        WorldMapPOIGenerationSettings settings)
    {
        if (def == null || def.targetCount <= 0 || candidates == null || candidates.Count == 0)
            return;

        var scored = new List<ScoredCandidate>(candidates.Count);
        int defSalt = StableHash(def.poiId);

        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate c = candidates[i];
            float score = ScoreCandidate(c, def);

            int salt = defSalt ^ i;
            score += SignedHash01(seed, Mathf.RoundToInt(c.u * 100000f), Mathf.RoundToInt(c.v * 100000f), salt) * settings.candidateNoise;

            if (score < def.minScore)
                continue;

            scored.Add(new ScoredCandidate
            {
                candidate = c,
                score = score
            });
        }

        scored.Sort((a, b) =>
        {
            int byScore = b.score.CompareTo(a.score);
            if (byScore != 0)
                return byScore;

            int byX = a.candidate.position.x.CompareTo(b.candidate.position.x);
            if (byX != 0)
                return byX;

            return a.candidate.position.y.CompareTo(b.candidate.position.y);
        });

        int added = 0;
        string token = MakeStableToken(def.poiId);

        for (int i = 0; i < scored.Count && added < def.targetCount; i++)
        {
            ScoredCandidate s = scored[i];

            if (!IsFarEnough(layer, s.candidate.position, def, settings))
                continue;

            string stableId = $"poi_{token}_{added:00}";

            layer.pois.Add(new WorldMapPOIInstance
            {
                stableId = stableId,
                poiDefId = def.poiId,
                displayName = GenerateDisplayName(def, seed, added, s.candidate.position),
                position = s.candidate.position,
                height01 = s.candidate.height01,
                depth01 = s.candidate.depth01,
                score = s.score,
                discovered = false,
                surveyed = false,
                depleted = false
            });

            added++;
        }
    }

    private static float ScoreCandidate(Candidate c, WorldMapPOIDef def)
    {
        if (def == null)
            return float.NegativeInfinity;

        if (def.mustBeUnderwater && c.depth01 <= 0f)
            return float.NegativeInfinity;

        if (c.depth01 < def.minDepth01 || c.depth01 > def.maxDepth01)
            return float.NegativeInfinity;

        Metrics m = c.metrics;

        float localLow01 = Mathf.Clamp01((m.maxHeight01 - c.height01) * 4f);
        float localHigh01 = Mathf.Clamp01((c.height01 - m.minHeight01) * 4f);

        float score = def.baseScore;

        score += m.DeepOcean01 * def.deepOceanWeight;
        score += m.OpenOcean01 * def.openOceanWeight;
        score += m.ShelfWater01 * def.shelfWaterWeight;
        score += m.ShallowWater01 * def.shallowWaterWeight;

        score += m.Beach01 * def.beachNearbyWeight;
        score += m.Lowland01 * def.lowlandNearbyWeight;
        score += m.Highland01 * def.highlandNearbyWeight;
        score += m.Mountain01 * def.mountainNearbyWeight;

        score += m.Water01 * def.waterWeight;
        score += m.Land01 * def.landNearbyWeight;
        score += m.ShallowShelf01 * def.shallowShelfWeight;
        score += m.DeepOpen01 * def.deepOpenWeight;
        score += m.CoastPresence01 * def.coastPresenceWeight;
        score += m.Ruggedness01 * def.ruggednessWeight;

        score += localLow01 * def.localLowWeight;
        score += localHigh01 * def.localHighWeight;

        score += (1f - m.Land01) * def.farFromLandWeight;
        score += (1f - m.CoastPresence01) * def.isolationWeight;

        score += Preference(
            c.depth01,
            def.preferredDepth01,
            def.depthTolerance01,
            def.depthPreferenceWeight
        );

        return score;
    }

    private static bool IsFarEnough(
        WorldMapPOILayer layer,
        Vector2 position,
        WorldMapPOIDef def,
        WorldMapPOIGenerationSettings settings)
    {
        if (layer == null || layer.pois == null || def == null || settings == null)
            return true;

        float globalSqr = settings.minGlobalSpacing * settings.minGlobalSpacing;
        float sameSqr = def.minSameTypeSpacing * def.minSameTypeSpacing;

        for (int i = 0; i < layer.pois.Count; i++)
        {
            WorldMapPOIInstance existing = layer.pois[i];
            if (existing == null)
                continue;

            float sqr = Vector2.SqrMagnitude(position - existing.position);

            if (sqr < globalSqr)
                return false;

            if (existing.poiDefId == def.poiId && sqr < sameSqr)
                return false;
        }

        return true;
    }

    private static string GenerateDisplayName(
        WorldMapPOIDef def,
        int seed,
        int index,
        Vector2 position)
    {
        if (def == null)
            return "Unknown POI";

        string[] names = def.generatedNames;

        if (names == null || names.Length == 0)
            return $"{def.displayName} #{index + 1}";

        int pick = Mathf.Abs(StableHash($"{seed}:{def.poiId}:{index}:{position.x:0.0}:{position.y:0.0}")) % names.Length;
        string picked = names[pick];

        return string.IsNullOrWhiteSpace(picked)
            ? $"{def.displayName} #{index + 1}"
            : picked;
    }

    private static float Preference(float value, float preferred, float tolerance, float weight)
    {
        if (Mathf.Abs(weight) <= 0.0001f)
            return 0f;

        tolerance = Mathf.Max(0.0001f, tolerance);

        float d = Mathf.Abs(value - preferred);
        float t = 1f - Mathf.Clamp01(d / tolerance);

        return t * weight;
    }

    private static WorldMapTopographyClass ClassifyHeight(
        float height01,
        WorldMapTopographySettings settings,
        float effectiveSeaLevel)
    {
        float h = Mathf.Clamp01(height01);
        float sea = Mathf.Clamp01(effectiveSeaLevel);

        if (h < sea)
        {
            float depth = sea - h;

            if (depth <= settings.shallowDepth01 * 0.35f)
                return WorldMapTopographyClass.ShallowWater;

            if (depth <= settings.shallowDepth01)
                return WorldMapTopographyClass.ShelfWater;

            if (depth <= settings.openOceanDepth01)
                return WorldMapTopographyClass.OpenOcean;

            return WorldMapTopographyClass.DeepOcean;
        }

        float landHeight = h - sea;

        if (landHeight <= settings.beachHeight01)
            return WorldMapTopographyClass.Beach;

        if (landHeight <= settings.lowlandHeight01)
            return WorldMapTopographyClass.Lowland;

        if (landHeight <= settings.highlandHeight01)
            return WorldMapTopographyClass.Highland;

        return WorldMapTopographyClass.Mountain;
    }

    private static void AddClass(ref Metrics m, WorldMapTopographyClass cls)
    {
        switch (cls)
        {
            case WorldMapTopographyClass.DeepOcean:
                m.deepOcean++;
                break;

            case WorldMapTopographyClass.OpenOcean:
                m.openOcean++;
                break;

            case WorldMapTopographyClass.ShelfWater:
                m.shelfWater++;
                break;

            case WorldMapTopographyClass.ShallowWater:
                m.shallowWater++;
                break;

            case WorldMapTopographyClass.Beach:
                m.beach++;
                break;

            case WorldMapTopographyClass.Lowland:
                m.lowland++;
                break;

            case WorldMapTopographyClass.Highland:
                m.highland++;
                break;

            case WorldMapTopographyClass.Mountain:
                m.mountain++;
                break;
        }
    }

    private static float DistanceToUvEdge(float u, float v)
    {
        return Mathf.Min(
            Mathf.Min(u, 1f - u),
            Mathf.Min(v, 1f - v)
        );
    }

    private static string MakeStableToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "poi";

        raw = raw.Trim().ToLowerInvariant();
        char[] chars = raw.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            bool ok =
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c == '_';

            chars[i] = ok ? c : '_';
        }

        return new string(chars);
    }

    private static float SignedHash01(int seed, int x, int y, int salt)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seed) * 16777619u;
            h = (h ^ (uint)(x * 73856093)) * 16777619u;
            h = (h ^ (uint)(y * 19349663)) * 16777619u;
            h = (h ^ (uint)(salt * 83492791)) * 16777619u;

            float v = (h & 0x00FFFFFF) / (float)0x00FFFFFF;
            return (v * 2f) - 1f;
        }
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            if (string.IsNullOrEmpty(s))
                return 0;

            int hash = 23;
            for (int i = 0; i < s.Length; i++)
                hash = hash * 31 + s[i];

            return hash;
        }
    }
}
