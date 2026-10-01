using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnderwaterAmbientBubbleField : MonoBehaviour
{
    private static readonly int SceneBrightnessId =
        Shader.PropertyToID(
            "_SceneBrightness");

    [Header("Local Viewer")]
    [Tooltip(
        "Optional. Usually leave blank and the local Main Camera is used. " +
        "For multiplayer/local-camera setups, assign the camera this client owns.")]
    [SerializeField] private Camera targetCamera;

    [Header("Water")]
    [Tooltip(
        "Optional. Auto-resolves ServiceRoot/WaveManager, then scene WaveManager.")]
    [SerializeField] private WaveManager waveManager;

    [Tooltip(
        "Fallback surface Y used only if no WaveManager can be resolved.")]
    [SerializeField] private float fallbackWaterSurfaceY = 0f;

    [Tooltip(
        "Bubbles disappear this far below the sampled live wave surface.")]
    [Min(0f)]
    [SerializeField] private float surfaceKillPadding = 0.02f;

    [Header("Ambient Density")]
    [Tooltip("Average ambient bubble births per second around this local camera.")]
    [Min(0f)]
    [SerializeField] private float bubblesPerSecond = 0.85f;

    [Tooltip(
        "Chance that one birth event creates a tiny cluster instead of one bubble.")]
    [Range(0f, 1f)]
    [SerializeField] private float clusterChance = 0.14f;

    [Min(2)]
    [SerializeField] private int clusterMin = 2;

    [Min(2)]
    [SerializeField] private int clusterMax = 4;

    [Header("Spawn Region")]
    [Tooltip(
        "Extra world distance below the camera used as an off-screen spawn reservoir.")]
    [Min(0f)]
    [SerializeField] private float belowCameraMargin = 1.5f;

    [Tooltip(
        "When enabled, all new bubbles are born below the bottom edge of the camera.")]
    [SerializeField] private bool spawnBelowScreenOnly = true;

    [Tooltip(
        "Extra gap below the visible camera edge before bubbles are allowed to spawn.")]
    [Min(0f)]
    [SerializeField] private float belowScreenSpawnBuffer = 0.15f;

    [Tooltip(
        "When Spawn Below Screen Only is disabled, controls how much of the visible " +
        "underwater height is eligible for new births. Lower values favor the bottom.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float spawnBandFraction = 0.55f;

    [Tooltip(
        "Do not spawn closer than this to the water surface.")]
    [Min(0f)]
    [SerializeField] private float surfaceSpawnClearance = 0.25f;

    [Tooltip(
        "Extra horizontal spawn margin outside the camera so bubbles may drift in.")]
    [Min(0f)]
    [SerializeField] private float horizontalMargin = 1f;

    [Header("Bubble Motion")]
    [SerializeField] private Vector2 sizeRange = new Vector2(0.035f, 0.11f);

    [SerializeField] private Vector2 lifetimeRange = new Vector2(4.5f, 10f);

    [SerializeField] private Vector2 riseSpeedRange = new Vector2(0.45f, 1.15f);

    [SerializeField] private Vector2 lateralSpeedRange = new Vector2(-0.08f, 0.08f);

    [Header("Wobble")]
    [Min(0f)]
    [SerializeField] private float noiseStrength = 0.12f;

    [Min(0.01f)]
    [SerializeField] private float noiseFrequency = 0.55f;

    [SerializeField] private float noiseScrollSpeed = 0.22f;

    [Header("Appearance")]
    [Tooltip(
        "Assign the existing material using DontSink/UnderwaterBubbleRing2D.")]
    [SerializeField] private Material bubbleMaterial;

    [SerializeField]
    private Color bubbleColor =
        new Color(0.86f, 0.97f, 1f, 0.58f);

    [Tooltip(
        "Optional explicit sorting layer. Leave blank to use Default.")]
    [SerializeField] private string sortingLayerName = "";

    [SerializeField] private int sortingOrder = 5;

    [Header("Limits")]
    [Min(8)]
    [SerializeField] private int maxParticles = 128;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;

    private sealed class Bubble
    {
        public GameObject gameObject;
        public Transform transform;
        public SpriteRenderer renderer;

        public bool active;
        public float remainingLifetime;
        public float lateralSpeed;
        public float riseSpeed;
        public float wobblePhase;
        public float wobbleRate;
        public float wobbleStrength;
    }

    private readonly List<Bubble> _pool =
        new List<Bubble>();

    private Sprite _runtimeSprite;
    private Texture2D _runtimeTexture;
    private MaterialPropertyBlock _brightnessBlock;
    private IBrightnessService _brightness;
    private float _sceneBrightness = 1f;
    private float _nextBirthTime;
    private bool _reconfigureRequested;

    private void OnEnable()
    {
        ResolveRefs();
        ResolveBrightnessService();
        EnsurePool();
        ApplyAppearanceToPool();
        _reconfigureRequested = false;
        ScheduleNextBirth();
    }

    private void OnDisable()
    {
        UnbindBrightnessService();
        ClearAmbientBubbles();
    }

    private void OnDestroy()
    {
        UnbindBrightnessService();

        if (_runtimeSprite != null)
            Destroy(_runtimeSprite);

        if (_runtimeTexture != null)
            Destroy(_runtimeTexture);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // OnValidate must remain data-only. Creating GameObjects, parenting
        // Transforms, adding SpriteRenderers, changing active state, etc. here
        // causes Unity's "SendMessage cannot be called during Awake,
        // CheckConsistency, or OnValidate" warning flood.
        clusterMin =
            Mathf.Max(
                2,
                clusterMin);

        clusterMax =
            Mathf.Max(
                clusterMin,
                clusterMax);

        sizeRange.x =
            Mathf.Max(
                0.001f,
                sizeRange.x);

        sizeRange.y =
            Mathf.Max(
                sizeRange.x,
                sizeRange.y);

        lifetimeRange.x =
            Mathf.Max(
                0.05f,
                lifetimeRange.x);

        lifetimeRange.y =
            Mathf.Max(
                lifetimeRange.x,
                lifetimeRange.y);

        riseSpeedRange.x =
            Mathf.Max(
                0f,
                riseSpeedRange.x);

        riseSpeedRange.y =
            Mathf.Max(
                riseSpeedRange.x,
                riseSpeedRange.y);

        maxParticles =
            Mathf.Max(
                8,
                maxParticles);

        // If Inspector values change during Play Mode, defer all hierarchy /
        // component / material work until the next normal runtime frame.
        _reconfigureRequested =
            true;
    }
#endif

    private void Update()
    {
        if (_reconfigureRequested)
        {
            _reconfigureRequested =
                false;

            ResolveRefs();
            ResolveBrightnessService();
            EnsurePool();
            ApplyAppearanceToPool();
        }

        ResolveRefs();
        ResolveBrightnessService();

        UpdateActiveBubbles(
            Time.deltaTime);

        if (bubblesPerSecond <= 0f)
            return;

        int safety = 0;

        while (Time.time >= _nextBirthTime &&
               safety++ < 4)
        {
            TryEmitBirthEvent();
            ScheduleNextBirth();
        }
    }

    [ContextMenu("Emit Debug Bubble")]
    public void EmitDebugBubble()
    {
        ResolveRefs();

        if (!TryGetSpawnRegion(
                out float left,
                out float right,
                out float bottom,
                out float top))
        {
            return;
        }

        EmitOne(
            new Vector2(
                (left + right) * 0.5f,
                (bottom + top) * 0.5f));
    }

    [ContextMenu("Reconfigure Bubble Field")]
    public void Reconfigure()
    {
        ResolveRefs();
        ResolveBrightnessService();
        EnsurePool();
        ApplyAppearanceToPool();
    }

    [ContextMenu("Clear Ambient Bubbles")]
    public void ClearAmbientBubbles()
    {
        for (int i = 0;
             i < _pool.Count;
             i++)
        {
            DeactivateBubble(
                _pool[i]);
        }
    }

    private void ResolveRefs()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (ServiceRoot.Instance != null &&
            ServiceRoot.Instance.WaveManager != null)
        {
            waveManager =
                ServiceRoot.Instance.WaveManager;

            return;
        }

        if (waveManager == null)
        {
            waveManager =
                FindFirstObjectByType<WaveManager>();
        }
    }

    private void EnsurePool()
    {
        EnsureRuntimeSprite();

        int desired =
            Mathf.Max(
                8,
                maxParticles);

        while (_pool.Count < desired)
        {
            _pool.Add(
                CreateBubble(
                    _pool.Count));
        }

        for (int i = desired;
             i < _pool.Count;
             i++)
        {
            DeactivateBubble(
                _pool[i]);
        }
    }

    private void EnsureRuntimeSprite()
    {
        if (_runtimeSprite != null)
            return;

        _runtimeTexture =
            new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true)
            {
                name = "Runtime Ambient Bubble Quad",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

        Color32 white =
            new Color32(
                255,
                255,
                255,
                255);

        _runtimeTexture.SetPixels32(
            new[]
            {
                white,
                white,
                white,
                white
            });

        _runtimeTexture.Apply(
            updateMipmaps: false,
            makeNoLongerReadable: true);

        _runtimeSprite =
            Sprite.Create(
                _runtimeTexture,
                new Rect(
                    0f,
                    0f,
                    2f,
                    2f),
                new Vector2(
                    0.5f,
                    0.5f),
                pixelsPerUnit: 2f);

        _runtimeSprite.name =
            "Runtime Ambient Bubble Sprite";

        _runtimeSprite.hideFlags =
            HideFlags.HideAndDontSave;
    }

    private Bubble CreateBubble(
        int index)
    {
        GameObject go =
            new GameObject(
                $"AmbientBubble_{index:000}");

        go.transform.SetParent(
            transform,
            worldPositionStays: false);

        SpriteRenderer renderer =
            go.AddComponent<SpriteRenderer>();

        renderer.sprite =
            _runtimeSprite;

        Bubble bubble =
            new Bubble
            {
                gameObject = go,
                transform = go.transform,
                renderer = renderer,
                active = false
            };

        ApplyAppearance(
            bubble);

        go.SetActive(
            false);

        return bubble;
    }

    private void ApplyAppearanceToPool()
    {
        for (int i = 0;
             i < _pool.Count;
             i++)
        {
            ApplyAppearance(
                _pool[i]);
        }
    }

    private void ApplyAppearance(
        Bubble bubble)
    {
        if (bubble == null ||
            bubble.renderer == null)
        {
            return;
        }

        bubble.renderer.sharedMaterial =
            bubbleMaterial;

        bubble.renderer.color =
            bubbleColor;

        if (!string.IsNullOrWhiteSpace(
                sortingLayerName))
        {
            bubble.renderer.sortingLayerName =
                sortingLayerName;
        }

        bubble.renderer.sortingOrder =
            sortingOrder;

        ApplyBrightness(
            bubble.renderer);
    }

    private void UpdateActiveBubbles(
        float deltaTime)
    {
        if (!IsFinite(deltaTime) ||
            deltaTime < 0f)
        {
            return;
        }

        for (int i = 0;
             i < _pool.Count;
             i++)
        {
            Bubble bubble =
                _pool[i];

            if (!bubble.active)
                continue;

            if (!IsFinite(
                    bubble.transform.position) ||
                !IsFinite(
                    bubble.remainingLifetime) ||
                !IsFinite(
                    bubble.lateralSpeed) ||
                !IsFinite(
                    bubble.riseSpeed))
            {
                DeactivateBubble(
                    bubble);

                continue;
            }

            bubble.remainingLifetime -=
                deltaTime;

            if (bubble.remainingLifetime <= 0f)
            {
                DeactivateBubble(
                    bubble);

                continue;
            }

            float wobbleTime =
                Time.time *
                bubble.wobbleRate +
                bubble.wobblePhase +
                noiseScrollSpeed;

            float wobbleVelocityX =
                Mathf.Sin(
                    wobbleTime *
                    Mathf.PI *
                    2f) *
                bubble.wobbleStrength;

            float wobbleVelocityY =
                Mathf.Cos(
                    wobbleTime *
                    Mathf.PI *
                    1.37f) *
                bubble.wobbleStrength *
                0.20f;

            Vector3 position =
                bubble.transform.position;

            position.x +=
                (bubble.lateralSpeed +
                 wobbleVelocityX) *
                deltaTime;

            position.y +=
                (bubble.riseSpeed +
                 wobbleVelocityY) *
                deltaTime;

            if (!IsFinite(position))
            {
                DeactivateBubble(
                    bubble);

                continue;
            }

            float surfaceY =
                SampleSurfaceY(
                    position.x);

            if (!IsFinite(surfaceY) ||
                position.y >=
                surfaceY -
                surfaceKillPadding)
            {
                DeactivateBubble(
                    bubble);

                continue;
            }

            bubble.transform.position =
                position;
        }
    }

    private void TryEmitBirthEvent()
    {
        if (!TryGetSpawnRegion(
                out float left,
                out float right,
                out float bottom,
                out float top))
        {
            return;
        }

        int count =
            Random.value < clusterChance
                ? Random.Range(
                    clusterMin,
                    clusterMax + 1)
                : 1;

        float clusterCenterX =
            Random.Range(
                left,
                right);

        float clusterCenterY =
            Random.Range(
                bottom,
                top);

        for (int i = 0;
             i < count;
             i++)
        {
            Vector2 position =
                new Vector2(
                    Mathf.Clamp(
                        clusterCenterX +
                        Random.Range(
                            -0.18f,
                            0.18f),
                        left,
                        right),
                    Mathf.Clamp(
                        clusterCenterY +
                        Random.Range(
                            -0.10f,
                            0.10f),
                        bottom,
                        top));

            EmitOne(
                position);
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[UnderwaterAmbientBubbleField:{name}] " +
                $"Birth event count={count} " +
                $"region=({left:0.0},{bottom:0.0})-({right:0.0},{top:0.0}) " +
                $"renderer=SpritePool",
                this);
        }
    }

    private void EmitOne(
        Vector2 worldPosition)
    {
        if (!IsFinite(worldPosition))
            return;

        Bubble bubble =
            FindInactiveBubble();

        if (bubble == null)
            return;

        float size =
            Random.Range(
                sizeRange.x,
                sizeRange.y);

        float lifetime =
            Random.Range(
                lifetimeRange.x,
                lifetimeRange.y);

        float lateralSpeed =
            Random.Range(
                lateralSpeedRange.x,
                lateralSpeedRange.y);

        float riseSpeed =
            Random.Range(
                riseSpeedRange.x,
                riseSpeedRange.y);

        if (!IsFinite(size) ||
            !IsFinite(lifetime) ||
            !IsFinite(lateralSpeed) ||
            !IsFinite(riseSpeed))
        {
            return;
        }

        bubble.remainingLifetime =
            lifetime;

        bubble.lateralSpeed =
            lateralSpeed;

        bubble.riseSpeed =
            riseSpeed;

        bubble.wobblePhase =
            Random.Range(
                0f,
                Mathf.PI *
                2f);

        bubble.wobbleRate =
            Mathf.Max(
                0.01f,
                noiseFrequency *
                Random.Range(
                    0.82f,
                    1.18f));

        bubble.wobbleStrength =
            Mathf.Max(
                0f,
                noiseStrength) *
            Random.Range(
                0.75f,
                1.25f);

        bubble.transform.position =
            new Vector3(
                worldPosition.x,
                worldPosition.y,
                0f);

        bubble.transform.rotation =
            Quaternion.identity;

        bubble.transform.localScale =
            Vector3.one *
            size;

        bubble.active =
            true;

        bubble.gameObject.SetActive(
            true);
    }

    private Bubble FindInactiveBubble()
    {
        int limit =
            Mathf.Min(
                _pool.Count,
                Mathf.Max(
                    8,
                    maxParticles));

        for (int i = 0;
             i < limit;
             i++)
        {
            if (!_pool[i].active)
                return _pool[i];
        }

        return null;
    }

    private void DeactivateBubble(
        Bubble bubble)
    {
        if (bubble == null)
            return;

        bubble.active =
            false;

        bubble.remainingLifetime =
            0f;

        if (bubble.gameObject != null)
        {
            bubble.gameObject.SetActive(
                false);
        }
    }

    private bool TryGetSpawnRegion(
        out float left,
        out float right,
        out float bottom,
        out float top)
    {
        left = 0f;
        right = 0f;
        bottom = 0f;
        top = 0f;

        if (targetCamera == null ||
            !targetCamera.orthographic)
        {
            return false;
        }

        float halfHeight =
            targetCamera.orthographicSize;

        float halfWidth =
            halfHeight *
            targetCamera.aspect;

        Vector3 cameraPosition =
            targetCamera.transform.position;

        if (!IsFinite(halfHeight) ||
            !IsFinite(halfWidth) ||
            !IsFinite(cameraPosition))
        {
            return false;
        }

        float cameraBottom =
            cameraPosition.y -
            halfHeight;

        float cameraTop =
            cameraPosition.y +
            halfHeight;

        float sampledSurfaceY =
            SampleSurfaceY(
                cameraPosition.x);

        if (!IsFinite(sampledSurfaceY))
            return false;

        float underwaterTop =
            Mathf.Min(
                cameraTop,
                sampledSurfaceY -
                surfaceSpawnClearance);

        float spawnBottom =
            cameraBottom -
            belowCameraMargin;

        float spawnTop;

        if (spawnBelowScreenOnly)
        {
            spawnTop =
                Mathf.Min(
                    cameraBottom -
                    belowScreenSpawnBuffer,
                    underwaterTop);
        }
        else
        {
            spawnTop =
                Mathf.Lerp(
                    spawnBottom,
                    underwaterTop,
                    spawnBandFraction);
        }

        if (spawnTop <=
            spawnBottom + 0.01f)
        {
            return false;
        }

        left =
            cameraPosition.x -
            halfWidth -
            horizontalMargin;

        right =
            cameraPosition.x +
            halfWidth +
            horizontalMargin;

        bottom =
            spawnBottom;

        top =
            spawnTop;

        return
            IsFinite(left) &&
            IsFinite(right) &&
            IsFinite(bottom) &&
            IsFinite(top);
    }

    private float SampleSurfaceY(
        float worldX)
    {
        if (waveManager != null)
        {
            return
                waveManager.SampleSurfaceY(
                    worldX);
        }

        return fallbackWaterSurfaceY;
    }

    private void ResolveBrightnessService()
    {
        IBrightnessService candidate =
            ServiceRoot.Instance != null
                ? ServiceRoot.Instance.Brightness
                : null;

        if (ReferenceEquals(
                candidate,
                _brightness))
        {
            return;
        }

        UnbindBrightnessService();

        _brightness =
            candidate;

        if (_brightness != null)
        {
            _brightness.OnBrightnessChanged +=
                OnBrightnessChanged;

            OnBrightnessChanged(
                _brightness.Brightness01);
        }
        else
        {
            OnBrightnessChanged(
                1f);
        }
    }

    private void UnbindBrightnessService()
    {
        if (_brightness != null)
        {
            _brightness.OnBrightnessChanged -=
                OnBrightnessChanged;
        }

        _brightness =
            null;
    }

    private void OnBrightnessChanged(
        float brightness01)
    {
        _sceneBrightness =
            Mathf.Clamp01(
                brightness01);

        for (int i = 0;
             i < _pool.Count;
             i++)
        {
            ApplyBrightness(
                _pool[i].renderer);
        }
    }

    private void ApplyBrightness(
        SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        if (_brightnessBlock == null)
        {
            _brightnessBlock =
                new MaterialPropertyBlock();
        }

        renderer.GetPropertyBlock(
            _brightnessBlock);

        _brightnessBlock.SetFloat(
            SceneBrightnessId,
            _sceneBrightness);

        renderer.SetPropertyBlock(
            _brightnessBlock);
    }

    private void ScheduleNextBirth()
    {
        if (bubblesPerSecond <= 0f)
        {
            _nextBirthTime =
                float.PositiveInfinity;

            return;
        }

        float u =
            Mathf.Clamp(
                Random.value,
                0.0001f,
                0.9999f);

        float delay =
            -Mathf.Log(
                1f - u) /
            bubblesPerSecond;

        _nextBirthTime =
            Time.time +
            delay;
    }

    private static bool IsFinite(
        float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    private static bool IsFinite(
        Vector2 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y);
    }

    private static bool IsFinite(
        Vector3 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y) &&
            IsFinite(value.z);
    }
}
