using UnityEngine;

[DisallowMultipleComponent]
public sealed class NodeGroundGenerator2D : MonoBehaviour
{
    [Header("Generation")]
    [Min(2)] public int pointCount = 220;
    [Min(1f)] public float worldWidth = 90f;

    [Tooltip("Y position of flat land section (left side).")]
    public float landY = 0f;

    [Tooltip("How long the left-side island stays flat before sloping down.")]
    [Min(0f)] public float islandLength = 35f;

    [Tooltip("Horizontal distance over which the ground slopes down into underwater.")]
    [Min(0.1f)] public float slopeLength = 15f;

    [Tooltip("Total vertical drop over the slope section.")]
    [Min(0f)] public float slopeDrop = 10f;

    [Header("Slope Shape")]
    [Tooltip("How much vertical deformation to add on the slope section.")]
    [Min(0f)] public float slopeDeformationAmplitude = 0.6f;

    [Tooltip("Noise scale for slope deformation.")]
    [Min(0.01f)] public float slopeDeformationScale = 0.10f;

    [Tooltip("Detail layers for slope deformation.")]
    [Range(1, 6)] public int slopeDeformationOctaves = 2;

    [Tooltip("How softly slope deformation fades in/out within the slope (0 = hard).")]
    [Min(0f)] public float slopeDeformationEdgeFade = 0.15f;

    [Header("Underwater Shape")]
    [Tooltip("How much vertical deformation to add underwater (0 = smooth beach, high = reefy).")]
    [Min(0f)] public float deformationAmplitude = 2.2f;

    [Tooltip("Noise scale (bigger = smoother, smaller = busier).")]
    [Min(0.01f)] public float deformationScale = 0.12f;

    [Tooltip("Adds smaller detail noise layers underwater.")]
    [Range(1, 6)] public int deformationOctaves = 3;

    [Tooltip("Each octave amplitude multiplier (smaller = less spiky detail).")]
    [Range(0.1f, 0.9f)] public float octavePersistence = 0.5f;

    [Tooltip("Each octave frequency multiplier.")]
    [Range(1.2f, 3.5f)] public float octaveLacunarity = 2.0f;

    [Tooltip("How quickly deformation ramps in after the slope begins (prevents nasty seam).")]
    [Min(0.01f)] public float deformationRampDistance = 6f;

    [Header("Optional Land Variation")]
    [Tooltip("If you want the 'flat' land to have slight wobble. 0 = perfectly flat.")]
    [Min(0f)] public float landWobbleAmplitude = 0f;

    [Min(0.01f)] public float landWobbleScale = 0.06f;

    [Header("Collision / Layers")]
    [Tooltip("Layer name to assign to generated ground object.")]
    public string groundLayerName = "Ground";

    [Header("Boundaries")]
    public bool createBoundaryWalls = true;

    [Tooltip("How far beyond left/right ends to place the invisible walls.")]
    [Min(-50f)] public float boundaryPadding = 1.5f;

    [Tooltip("Wall height (should exceed player jump/swim range).")]
    [Min(1f)] public float boundaryWallHeight = 40f;

    [Tooltip("Wall thickness.")]
    [Min(0.1f)] public float boundaryWallThickness = 1f;

    [Tooltip("Layer name for boundary walls (often same as Ground).")]
    public string boundaryLayerName = "Ground";

    [Header("Debug")]
    public bool regenerateOnValidate = true;

    [Header("Randomization")]
    public bool randomizeSeedOnGenerate = true;
    public float slopeRandomizationRange = 3f;

    [Tooltip("Used if randomizeSeedOnGenerate is false.")]
    public int seed = 12345;

    private EdgeCollider2D _edge;
    private GameObject _leftWall;
    private GameObject _rightWall;

    public event System.Action OnGenerated;

    private void Awake()
    {
        EnsureComponents();
    }

    private void Start()
    {
        Generate();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!regenerateOnValidate) return;
        if (!Application.isPlaying) return;

        EnsureComponents();
        Generate();
    }
#endif

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (randomizeSeedOnGenerate)
        {
            seed = Random.Range(int.MinValue, int.MaxValue);
        }

        EnsureComponents();

        // Layer
        int groundLayer = LayerMask.NameToLayer(groundLayerName);
        if (groundLayer < 0)
        {
            Debug.LogWarning($"Layer '{groundLayerName}' not found. Ground will stay on Default.");
        }
        else
        {
            gameObject.layer = groundLayer;
        }

        // Deterministic noise offsets per instance
        var rng = new System.Random(seed);
        float noiseOffset = (float)rng.NextDouble() * 10000f;
        float landOffset = (float)rng.NextDouble() * 10000f;

        Vector2[] pts = new Vector2[pointCount];

        float xStart = 0f;
        float xEnd = worldWidth;
        float dx = (xEnd - xStart) / (pointCount - 1);

        float actualSlopeLength = slopeLength + Random.Range(-slopeRandomizationRange, slopeRandomizationRange);
        float actualSlopeDrop = slopeDrop + Random.Range(-slopeRandomizationRange, slopeRandomizationRange);

        float slopeStartX = Mathf.Clamp(islandLength, xStart, xEnd);
        float slopeEndX = Mathf.Clamp(islandLength + slopeLength, xStart, xEnd);

        for (int i = 0; i < pointCount; i++)
        {
            float x = xStart + dx * i;
            float y = landY;

            // Left-side land section (mostly flat)
            if (x <= slopeStartX)
            {
                if (landWobbleAmplitude > 0f)
                {
                    float wobble = (Mathf.PerlinNoise((x * landWobbleScale) + landOffset, 0.123f) - 0.5f) * 2f;
                    y += wobble * landWobbleAmplitude;
                }
            }
            // Slope-down transition
            else if (x <= slopeEndX)
            {
                float t = Mathf.InverseLerp(slopeStartX, slopeEndX, x);
                // Smoothstep for nicer beach shapes
                t = t * t * (3f - 2f * t);
                y = landY - (slopeDrop * t);

                // Slope deformation (ramps in/out so seams stay clean)
                if (slopeDeformationAmplitude > 0f)
                {
                    float fadeFrac = Mathf.Clamp01(slopeDeformationEdgeFade);
                    float fadeIn = (fadeFrac <= 0f) ? 1f : Mathf.Clamp01(t / fadeFrac);
                    float fadeOut = (fadeFrac <= 0f) ? 1f : Mathf.Clamp01((1f - t) / fadeFrac);

                    // Smooth fades (avoids linear "crease" feeling)
                    fadeIn = fadeIn * fadeIn * (3f - 2f * fadeIn);
                    fadeOut = fadeOut * fadeOut * (3f - 2f * fadeOut);

                    float seamSafe = fadeIn * fadeOut;

                    float slopeDeform = FractalPerlin1D(
                        x,
                        slopeDeformationScale,
                        slopeDeformationOctaves,
                        octavePersistence,   // reuse existing persistence
                        octaveLacunarity,    // reuse existing lacunarity
                        noiseOffset + 1337.7f);

                    y += slopeDeform * slopeDeformationAmplitude * seamSafe;
                }
            }
            // Underwater continuation with deformation
            else
            {
                y = landY - slopeDrop;

                // Ramp deformation in so it doesn't pop right at slope end
                float rampT = Mathf.Clamp01((x - slopeEndX) / deformationRampDistance);

                float deform = FractalPerlin1D(x, deformationScale, deformationOctaves, octavePersistence, octaveLacunarity, noiseOffset);
                y += deform * deformationAmplitude * rampT;
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

    private void EnsureComponents()
    {
        if (_edge == null) _edge = GetComponent<EdgeCollider2D>();
        if (_edge == null) _edge = gameObject.AddComponent<EdgeCollider2D>();

        // EdgeCollider2D expects points in local space
        // We generate in local coordinates (x from 0..worldWidth), so keep transform clean-ish.
    }

    private void EnsureBoundaryWalls(float xStart, float xEnd)
    {
        int layer = LayerMask.NameToLayer(boundaryLayerName);

        _leftWall = EnsureWall(_leftWall, "BoundaryWall_Left", layer);
        _rightWall = EnsureWall(_rightWall, "BoundaryWall_Right", layer);

        float leftX = xStart - boundaryPadding;
        float rightX = xEnd + boundaryPadding;

        PositionWall(_leftWall, leftX);
        PositionWall(_rightWall, rightX);
    }

    private GameObject EnsureWall(GameObject existing, string name, int layer)
    {
        if (existing == null)
        {
            existing = new GameObject(name);
            existing.transform.SetParent(transform, false);

            var box = existing.AddComponent<BoxCollider2D>();
            box.size = new Vector2(boundaryWallThickness, boundaryWallHeight);
            box.offset = new Vector2(0f, boundaryWallHeight * 0.5f); // sit on y=0 baseline

            // no rigidbody needed: static collider works fine
        }

        if (layer >= 0) existing.layer = layer;
        return existing;
    }

    private void PositionWall(GameObject wall, float x)
    {
        wall.transform.localPosition = new Vector3(x, landY - (boundaryWallHeight * 0.5f), 0f);
        // This places the wall so it spans upward around the landY baseline.
    }

    private void DestroyBoundaryWallsIfAny()
    {
        if (_leftWall) Destroy(_leftWall);
        if (_rightWall) Destroy(_rightWall);
        _leftWall = null;
        _rightWall = null;
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
            // Convert 0..1 to -1..1
            n = (n - 0.5f) * 2f;

            sum += n * amp;
            norm += amp;

            amp *= persistence;
            freq *= lacunarity;
        }

        return (norm > 0f) ? (sum / norm) : 0f;
    }
}