using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class PlayerAirState : MonoBehaviour
{
    public enum AirState
    {
        Full,
        Normal,
        Low,
        Critical,
        None
    }

    [Header("Air Capacity (Units)")]
    [Min(0.1f)] public float baseMaxAir = 100f;

    [Min(0f)] public float airCurrent = 100f;

    public float MaxAir { get; private set; }
    public float Air01 => (MaxAir <= 0.0001f) ? 0f : Mathf.Clamp01(airCurrent / MaxAir);

    [Header("Breathing")]
    [Tooltip("Base underwater consumption rate (units/sec) when no sources supply air.")]
    [Min(0f)] public float underwaterDrainPerSecond = 8f;

    [Tooltip("Optional extra drain when panicking/struggling (exertion hooks later).")]
    [Min(0f)] public float extraDrainPerSecond = 0f;

    [Header("Surface Recovery (Non-instant)")]
    [Tooltip("Baseline recovery rate at high air (units/sec).")]
    [Min(0f)] public float surfaceRegenHighAir = 10f;

    [Tooltip("Recovery rate when nearly empty (units/sec). Keep low so near-drowning takes time.")]
    [Min(0f)] public float surfaceRegenLowAir = 2f;

    [Tooltip("Shapes regen based on current Air01. X=Air01 (0..1), Y=multiplier (0..1).")]
    public AnimationCurve surfaceRegenCurve = AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f);

    [Tooltip("Optional: delay before regen starts after surfacing (seconds).")]
    [Min(0f)] public float surfaceRegenDelay = 0f;

    private float _timeSinceSurfaced = 999f;

    [Header("State Thresholds (UI + Behaviors)")]
    [Range(0f, 1f)] public float lowThreshold = 0.35f;
    [Range(0f, 1f)] public float criticalThreshold = 0.15f;

    public AirState CurrentState { get; private set; } = AirState.Full;

    [Header("Sources")]
    [Tooltip("Auto-collect all IAirSource components on this GameObject at Awake.")]
    public bool autoCollectSources = true;

    private readonly List<IAirSource> _sources = new();
    public IReadOnlyList<IAirSource> Sources => _sources;

    // Exposed for other systems
    public bool IsUnderwater { get; set; } // set by a source (AmbientSurfaceAirSource) or another detector

    void Awake()
    {
        RebuildSources();
        RecomputeMaxAir();

        // Clamp current to max
        airCurrent = Mathf.Clamp(airCurrent, 0f, MaxAir);
    }

    public void RebuildSources()
    {
        _sources.Clear();
        if (!autoCollectSources) return;

        // Collect from this GO only (simple and explicit).
        // If you want child sources later, change to GetComponentsInChildren.
        var monos = GetComponents<MonoBehaviour>();
        foreach (var m in monos)
        {
            if (m is IAirSource src) _sources.Add(src);
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        RecomputeMaxAir();

        // Detect surface/underwater transition based on IsUnderwater
        if (!IsUnderwater) _timeSinceSurfaced += dt;
        else _timeSinceSurfaced = 0f; // underwater, reset surfaced timer (so delay re-applies next surfacing)

        // Sum flows from sources
        float flow = 0f;
        for (int i = 0; i < _sources.Count; i++)
            flow += _sources[i].GetAirFlowPerSecond(this, dt);

        // If underwater and no source is supplying enough, apply baseline drain.
        // We treat "underwater drain" as an environmental sink, separate from sources.
        if (IsUnderwater)
            flow -= (underwaterDrainPerSecond + extraDrainPerSecond);

        // If above water, apply surface regen (non-instant, shaped by Air01)
        if (!IsUnderwater && _timeSinceSurfaced >= surfaceRegenDelay)
        {
            float shaped = Mathf.Clamp01(surfaceRegenCurve != null ? surfaceRegenCurve.Evaluate(Air01) : Air01);

            // Regen rate rises with Air01 (near empty = slow, near full = fast)
            float regen = Mathf.Lerp(surfaceRegenLowAir, surfaceRegenHighAir, shaped);
            flow += regen;
        }

        airCurrent += flow * dt;
        airCurrent = Mathf.Clamp(airCurrent, 0f, MaxAir);

        CurrentState = ComputeState(Air01);
    }

    private void RecomputeMaxAir()
    {
        float max = baseMaxAir;

        for (int i = 0; i < _sources.Count; i++)
            max += Mathf.Max(0f, _sources[i].MaxAirBonus);

        MaxAir = Mathf.Max(0.1f, max);

        // If max changes downward, clamp current
        if (airCurrent > MaxAir) airCurrent = MaxAir;
    }

    private AirState ComputeState(float a01)
    {
        if (a01 <= 0.0001f) return AirState.None;
        if (a01 < criticalThreshold) return AirState.Critical;
        if (a01 < lowThreshold) return AirState.Low;
        if (a01 < 0.90f) return AirState.Normal;
        return AirState.Full;
    }
}