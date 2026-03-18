using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatSeaFloorGenerator2D : MonoBehaviour, IGroundGeneratedNotifier
{
    [Header("Profile")]
    [SerializeField] private BoatSeaFloorProfile profile;

    [Header("Scene Refs")]
    [SerializeField] private BoatSceneController boatSceneController;

    [Header("Generation")]
    [Min(2)] public int pointCount = 320;
    public float landY = 0f;

    [Header("Collision / Layers")]
    public string groundLayerName = "Ground";

    [Header("Boundaries")]
    public bool createBoundaryWalls = true;
    [Min(-50f)] public float boundaryPadding = 1.5f;
    [Min(1f)] public float boundaryWallHeight = 60f;
    [Min(0.1f)] public float boundaryWallThickness = 1f;
    public string boundaryLayerName = "Ground";

    [Header("Randomization")]
    public bool randomizeSeedOnGenerate = true;
    public int seed = 12345;

    [Header("Debug")]
    public bool regenerateOnStart = true;

    private EdgeCollider2D _edge;
    private GameObject _leftWall;
    private GameObject _rightWall;

    private struct RavineStamp
    {
        public float centerX;
        public float width;
        public float depth;
    }

    private struct CliffStamp
    {
        public float startX;
        public float topLength;
        public float slopeLength;
        public float bottomLength;
        public float topY;
        public float bottomY;
    }

    public event System.Action OnGenerated;

    private void Awake()
    {
        EnsureComponents();
    }

    private void Start()
    {
        if (regenerateOnStart)
            Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (profile == null)
        {
            Debug.LogWarning("[BoatSeaFloorGenerator2D] Missing profile.");
            return;
        }

        if (boatSceneController == null)
            boatSceneController = FindAnyObjectByType<BoatSceneController>();

        EnsureComponents();

        if (randomizeSeedOnGenerate)
            seed = Random.Range(int.MinValue, int.MaxValue);

        int groundLayer = LayerMask.NameToLayer(groundLayerName);
        if (groundLayer >= 0)
            gameObject.layer = groundLayer;

        float xStart, xEnd;
        GetWorldSpan(out xStart, out xEnd);

        float totalWidth = Mathf.Max(1f, xEnd - xStart);
        float dx = totalWidth / (pointCount - 1);

        var rng = new System.Random(seed);
        float macroOffset = (float)rng.NextDouble() * 10000f;
        float microOffset = (float)rng.NextDouble() * 10000f;
        float slopeOffset = (float)rng.NextDouble() * 10000f;
        float driftOffset = (float)rng.NextDouble() * 10000f;
        float cliffOffset = (float)rng.NextDouble() * 10000f;

        RavineStamp[] ravines = BuildRavines(rng, xStart, xEnd);
        CliffStamp[] cliffs = BuildCliffs(rng, xStart, xEnd);

        float leftPlateauEnd = xStart + profile.leftPlateauLength;
        float leftSlopeEnd = leftPlateauEnd + profile.leftSlopeLength;
        float rightPlateauStart = xEnd - profile.rightPlateauLength;
        float rightSlopeStart = rightPlateauStart - profile.rightSlopeLength;

        // Safety so zones don't invert on short trips.
        leftPlateauEnd = Mathf.Min(leftPlateauEnd, xEnd);
        leftSlopeEnd = Mathf.Min(leftSlopeEnd, xEnd);
        rightPlateauStart = Mathf.Max(rightPlateauStart, xStart);
        rightSlopeStart = Mathf.Max(rightSlopeStart, xStart);

        Vector2[] pts = new Vector2[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float x = xStart + dx * i;
            float y;

            if (x <= leftPlateauEnd)
            {
                y = landY;
            }
            else if (x <= leftSlopeEnd)
            {
                float t = Mathf.InverseLerp(leftPlateauEnd, leftSlopeEnd, x);
                t = Smooth01(t);

                float targetDepth = GetSeaFloorBaseDepth(x, driftOffset);
                y = Mathf.Lerp(landY, landY - targetDepth, t);

                y += GetSlopeDeformation(x, t, slopeOffset);
            }
            else if (x < rightSlopeStart)
            {
                float depth = GetSeaFloorBaseDepth(x, driftOffset);
                y = landY - depth;

                y += FractalPerlin1D(
                    x, profile.macroScale, profile.macroOctaves,
                    profile.octavePersistence, profile.octaveLacunarity, macroOffset
                ) * profile.macroAmplitude;

                y += FractalPerlin1D(
                    x, profile.microScale, profile.microOctaves,
                    profile.octavePersistence, profile.octaveLacunarity, microOffset
                ) * profile.microAmplitude;

                y += EvaluateRavines(x, ravines);
                y = ApplyCliffs(x, y, cliffs, cliffOffset);
                y = Mathf.Max(y, landY - profile.maxAbsoluteDepth);
            }
            else if (x < rightPlateauStart)
            {
                float t = Mathf.InverseLerp(rightSlopeStart, rightPlateauStart, x);
                t = Smooth01(t);

                float targetDepth = GetSeaFloorBaseDepth(x, driftOffset);
                float seaY = landY - targetDepth;
                seaY += GetSlopeDeformation(x, 1f - t, slopeOffset);

                y = Mathf.Lerp(seaY, landY, t);
            }
            else
            {
                y = landY;
            }

            pts[i] = new Vector2(x, y);
        }

        _edge.points = pts;

        if (createBoundaryWalls)
            EnsureBoundaryWalls(xStart, xEnd);
        else
            DestroyBoundaryWallsIfAny();

        OnGenerated?.Invoke();
    }

    private float GetSeaFloorBaseDepth(float x, float driftOffset)
    {
        float drift = FractalPerlin1D(
            x, profile.depthDriftScale, 1, 1f, 2f, driftOffset
        ) * profile.depthDriftAmplitude;

        float depth = profile.baseSeaFloorDepth + drift;
        depth = Mathf.Max(depth, profile.minDockClearDepth);
        depth = Mathf.Min(depth, profile.maxAbsoluteDepth);
        return depth;
    }

    private float GetSlopeDeformation(float x, float t, float offset)
    {
        if (profile.slopeDeformationAmplitude <= 0f)
            return 0f;

        float fadeFrac = Mathf.Clamp01(profile.slopeDeformationEdgeFade);
        float fadeIn = (fadeFrac <= 0f) ? 1f : Mathf.Clamp01(t / fadeFrac);
        float fadeOut = (fadeFrac <= 0f) ? 1f : Mathf.Clamp01((1f - t) / fadeFrac);

        fadeIn = Smooth01(fadeIn);
        fadeOut = Smooth01(fadeOut);

        float seamSafe = fadeIn * fadeOut;

        float deform = FractalPerlin1D(
            x,
            profile.slopeDeformationScale,
            profile.slopeDeformationOctaves,
            profile.octavePersistence,
            profile.octaveLacunarity,
            offset
        );

        return deform * profile.slopeDeformationAmplitude * seamSafe;
    }

    private RavineStamp[] BuildRavines(System.Random rng, float xStart, float xEnd)
    {
        int count = RandomRangeInclusive(rng, profile.ravineCountMin, profile.ravineCountMax);
        if (count <= 0) return System.Array.Empty<RavineStamp>();

        float safeLeft = xStart + profile.leftPlateauLength + profile.leftSlopeLength;
        float safeRight = xEnd - profile.rightPlateauLength - profile.rightSlopeLength;

        if (safeRight <= safeLeft + 1f)
            return System.Array.Empty<RavineStamp>();

        RavineStamp[] result = new RavineStamp[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = new RavineStamp
            {
                centerX = Mathf.Lerp(safeLeft, safeRight, (float)rng.NextDouble()),
                width = Mathf.Lerp(profile.ravineWidthMin, profile.ravineWidthMax, (float)rng.NextDouble()),
                depth = Mathf.Lerp(profile.ravineDepthMin, profile.ravineDepthMax, (float)rng.NextDouble())
            };
        }

        return result;
    }

    private CliffStamp[] BuildCliffs(System.Random rng, float xStart, float xEnd)
    {
        int count = RandomRangeInclusive(rng, profile.cliffCountMin, profile.cliffCountMax);
        if (count <= 0) return System.Array.Empty<CliffStamp>();

        float safeLeft = xStart + profile.leftPlateauLength + profile.leftSlopeLength;
        float safeRight = xEnd - profile.rightPlateauLength - profile.rightSlopeLength;

        if (safeRight <= safeLeft + 1f)
            return System.Array.Empty<CliffStamp>();

        CliffStamp[] result = new CliffStamp[count];

        for (int i = 0; i < count; i++)
        {
            float topLen = Mathf.Lerp(profile.cliffTopLengthMin, profile.cliffTopLengthMax, (float)rng.NextDouble());
            float slopeLen = Mathf.Lerp(profile.cliffSlopeLengthMin, profile.cliffSlopeLengthMax, (float)rng.NextDouble());
            float bottomLen = Mathf.Lerp(profile.cliffBottomLengthMin, profile.cliffBottomLengthMax, (float)rng.NextDouble());
            float height = Mathf.Lerp(profile.cliffHeightMin, profile.cliffHeightMax, (float)rng.NextDouble());

            float totalLen = topLen + slopeLen + bottomLen;
            float start = Mathf.Lerp(safeLeft, Mathf.Max(safeLeft, safeRight - totalLen), (float)rng.NextDouble());

            float topY = landY - GetSeaFloorBaseDepth(start, 0f);
            float bottomY = topY - height;

            result[i] = new CliffStamp
            {
                startX = start,
                topLength = topLen,
                slopeLength = slopeLen,
                bottomLength = bottomLen,
                topY = topY,
                bottomY = bottomY
            };
        }

        return result;
    }

    private float EvaluateRavines(float x, RavineStamp[] ravines)
    {
        float sum = 0f;

        for (int i = 0; i < ravines.Length; i++)
        {
            float dx = Mathf.Abs(x - ravines[i].centerX);
            float halfWidth = ravines[i].width * 0.5f;
            if (dx > halfWidth) continue;

            float t = dx / halfWidth; // 0 center, 1 edge
            float shape;

            if (profile.ravinesUseFlatFloor)
            {
                float flat = Mathf.Clamp01(profile.ravineFloorFlatness);

                // inner flat region, outer falloff region
                if (t <= flat)
                {
                    shape = 1f;
                }
                else
                {
                    float falloffT = Mathf.InverseLerp(flat, 1f, t);
                    falloffT = 1f - Smooth01(falloffT);
                    shape = Mathf.Pow(falloffT, profile.ravineEdgeSoftness);
                }
            }
            else
            {
                float centerT = 1f - t;
                shape = Mathf.Pow(Smooth01(centerT), profile.ravineEdgeSoftness);
            }

            sum -= ravines[i].depth * shape;
        }

        return sum;
    }

    private float ApplyCliffs(float x, float currentY, CliffStamp[] cliffs, float noiseOffset)
    {
        float y = currentY;

        for (int i = 0; i < cliffs.Length; i++)
        {
            float topStart = cliffs[i].startX;
            float topEnd = topStart + cliffs[i].topLength;
            float slopeEnd = topEnd + cliffs[i].slopeLength;
            float bottomEnd = slopeEnd + cliffs[i].bottomLength;

            if (x < topStart || x > bottomEnd)
                continue;

            float noise = FractalPerlin1D(
                x,
                profile.cliffNoiseScale,
                2,
                profile.octavePersistence,
                profile.octaveLacunarity,
                noiseOffset
            ) * profile.cliffNoiseAmplitude;

            if (x <= topEnd)
            {
                y = Mathf.Min(y, cliffs[i].topY + noise);
            }
            else if (x <= slopeEnd)
            {
                float t = Mathf.InverseLerp(topEnd, slopeEnd, x);
                t = Smooth01(t);
                float slopeY = Mathf.Lerp(cliffs[i].topY, cliffs[i].bottomY, t);
                y = Mathf.Min(y, slopeY);
            }
            else
            {
                y = Mathf.Min(y, cliffs[i].bottomY + noise);
            }
        }

        return y;
    }

    private void GetWorldSpan(out float xStart, out float xEnd)
    {
        if (boatSceneController != null)
        {
            // assumes sourceDockX is the left/behind dock position,
            // and total travel length is baseTravelDistance * distanceScale
            float sourceDockX = GetPrivateFloat(boatSceneController, "sourceDockX", -20f);
            float baseTravelDistance = GetPrivateFloat(boatSceneController, "baseTravelDistance", 200f);
            float distanceScale = GetPrivateFloat(boatSceneController, "distanceScale", 1f);

            xStart = sourceDockX;
            xEnd = sourceDockX + (baseTravelDistance * distanceScale);
            return;
        }

        xStart = -20f;
        xEnd = 180f;
    }

    private static float GetPrivateFloat(Object obj, string fieldName, float fallback)
    {
        if (obj == null) return fallback;

        var type = obj.GetType();
        var field = type.GetField(fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);

        if (field != null && field.FieldType == typeof(float))
            return (float)field.GetValue(obj);

        return fallback;
    }

    private void EnsureComponents()
    {
        if (_edge == null) _edge = GetComponent<EdgeCollider2D>();
        if (_edge == null) _edge = gameObject.AddComponent<EdgeCollider2D>();
    }

    private void EnsureBoundaryWalls(float xStart, float xEnd)
    {
        int layer = LayerMask.NameToLayer(boundaryLayerName);

        _leftWall = EnsureWall(_leftWall, "BoundaryWall_Left", layer);
        _rightWall = EnsureWall(_rightWall, "BoundaryWall_Right", layer);

        PositionWall(_leftWall, xStart - boundaryPadding);
        PositionWall(_rightWall, xEnd + boundaryPadding);
    }

    private GameObject EnsureWall(GameObject existing, string name, int layer)
    {
        if (existing == null)
        {
            existing = new GameObject(name);
            existing.transform.SetParent(transform, false);

            var box = existing.AddComponent<BoxCollider2D>();
            box.size = new Vector2(boundaryWallThickness, boundaryWallHeight);
            box.offset = new Vector2(0f, boundaryWallHeight * 0.5f);
        }

        if (layer >= 0) existing.layer = layer;
        return existing;
    }

    private void PositionWall(GameObject wall, float x)
    {
        wall.transform.localPosition = new Vector3(x, landY - (boundaryWallHeight * 0.5f), 0f);
    }

    private void DestroyBoundaryWallsIfAny()
    {
        if (_leftWall) Destroy(_leftWall);
        if (_rightWall) Destroy(_rightWall);
        _leftWall = null;
        _rightWall = null;
    }

    private static int RandomRangeInclusive(System.Random rng, int min, int max)
    {
        if (max < min) max = min;
        return rng.Next(min, max + 1);
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float FractalPerlin1D(
        float x,
        float baseScale,
        int octaves,
        float persistence,
        float lacunarity,
        float offset)
    {
        float amp = 1f;
        float freq = 1f;
        float sum = 0f;
        float norm = 0f;

        for (int o = 0; o < octaves; o++)
        {
            float n = Mathf.PerlinNoise((x + offset) * baseScale * freq, 0.4567f);
            n = (n - 0.5f) * 2f;

            sum += n * amp;
            norm += amp;

            amp *= persistence;
            freq *= lacunarity;
        }

        return norm > 0f ? (sum / norm) : 0f;
    }
}