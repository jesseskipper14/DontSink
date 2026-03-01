using UnityEngine;

/// <summary>
/// Aggregates submersion info from any ISubmersionProvider(s) on this object (or children).
/// Provides a single place to ask: are we swimming, wading, or dry?
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSubmersionState : MonoBehaviour
{
    [Header("Thresholds")]
    [Range(0f, 1f)]
    [Tooltip("At/above this, we consider the player in swim mode (not wading).")]
    public float swimThreshold = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("Below this, treat as effectively dry for gameplay.")]
    public float dryEpsilon = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool logWhenStateChanges = false;

    public float Submersion01 { get; private set; }         // 0..1
    public bool InWater => Submersion01 > dryEpsilon;
    public bool SubmergedEnoughToSwim => Submersion01 >= swimThreshold;

    /// <summary>
    /// 0 when dry, 1 when just about to become swimming.
    /// Nice for wading slowdown curves.
    /// </summary>
    public float Wading01 => Mathf.InverseLerp(dryEpsilon, swimThreshold, Submersion01);

    private ISubmersionProvider[] _providers;

    private bool _lastInWater;
    private bool _lastSwimming;

    void Awake()
    {
        // Include inactive if you want, but usually false is fine.
        _providers = GetComponentsInChildren<MonoBehaviour>(true) as ISubmersionProvider[];
        // The cast above won't work directly because Unity returns MonoBehaviour[].
        // So do it properly:
        var monos = GetComponentsInChildren<MonoBehaviour>(true);
        int count = 0;
        for (int i = 0; i < monos.Length; i++)
            if (monos[i] is ISubmersionProvider) count++;

        _providers = new ISubmersionProvider[count];
        int w = 0;
        for (int i = 0; i < monos.Length; i++)
            if (monos[i] is ISubmersionProvider p) _providers[w++] = p;
    }

    void FixedUpdate()
    {
        float max = 0f;

        // Choose "most submerged" provider. This handles weird overlaps gracefully.
        for (int i = 0; i < _providers.Length; i++)
        {
            var p = _providers[i];
            if (p == null) continue;

            float s = Mathf.Clamp01(p.SubmergedFraction);
            if (s > max) max = s;
        }

        Submersion01 = max;

        if (logWhenStateChanges)
        {
            bool inWater = InWater;
            bool swimming = SubmergedEnoughToSwim;

            if (inWater != _lastInWater || swimming != _lastSwimming)
            {
                Debug.Log($"[PlayerSubmersionState] InWater={inWater} Swimming={swimming} Submersion={Submersion01:0.00}");
                _lastInWater = inWater;
                _lastSwimming = swimming;
            }
        }
    }
}