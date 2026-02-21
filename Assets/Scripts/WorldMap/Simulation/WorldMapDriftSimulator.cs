using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldMapDriftSimulator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    private TimeOfDayManager _timeOfDay;

    [Header("Ticking")]
    [Tooltip("How often to simulate drift (seconds).")]
    [Min(0.05f)] public float simTickSeconds = 0.25f;

    [Tooltip("Extra multiplier on simulation speed (debug).")]
    [Min(0f)] public float simSpeed = 1f;

    [Header("Buildings drift too")]
    public bool driftDockAndTrade = true;

    [Tooltip("How strongly dock/trade drift back toward their baseline (per hour).")]
    [Min(0f)] public float buildingRestoreStrength = 0.08f;

    [Tooltip("Where dock/trade drift toward if left alone.")]
    [Range(0f, 4f)] public float buildingEquilibrium = 1.2f;

    [Header("Population Response")]
    public float populationLossRate = 0.02f;
    public float populationGrowthRate = 0.01f;

    private float _accum;

    [Header("Pressure Diffusion")]
    [SerializeField] private WorldMapGraphGenerator graphGenerator;
    [SerializeField] private ResourceCatalog resourceCatalog;

    [Tooltip("Base pressure transfer rate per in-game hour.")]
    public float pressureConductance = 0.04f;

    [Tooltip("Multiplier for intra-cluster edges.")]
    public float intraClusterPressureMult = 1.5f;

    [Tooltip("Multiplier for inter-cluster edges.")]
    public float interClusterPressureMult = 0.3f;

    [Tooltip("How much dock/trade improve transfer (0–1).")]
    [Range(0f, 1f)] public float logisticsInfluence = 0.6f;

    [Tooltip("Pressure lost per edge hop.")]
    [Range(0f, 0.25f)] public float edgeLoss01 = 0.05f;

    [Tooltip("Max absolute pressure transferred per edge per tick.")]
    public float maxPressurePerEdge = 0.2f;

    [Tooltip("Soft cap throughput when node has many connections.")]
    public int targetEdgeDegree = 4;

    private PressureDiffusionSystem _diffusion;
    private bool _warnedTime;
    private static readonly List<NodeStatId> _tmpStatKeys = new List<NodeStatId>(32);

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveServices();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Persistent object survives; scene-local sim is reconstructed.
        // Still, the reference can be null due to execution order.
        ResolveServices();
    }

    private void ResolveServices()
    {
        // Prefer a direct singleton if you have it.
        // If you don’t, FindAnyObjectByType is acceptable here (it’s not per-frame).
        _timeOfDay = FindAnyObjectByType<TimeOfDayManager>();

        if (_timeOfDay == null && !_warnedTime)
        {
            _warnedTime = true;
            Debug.LogWarning("[WorldMapDriftSimulator] TimeOfDayManager not found yet; drift paused.");
        }
    }

    private void Reset()
    {
        _timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        graphGenerator = FindAnyObjectByType<WorldMapGraphGenerator>();
        resourceCatalog = FindAnyObjectByType<ResourceCatalog>();
    }

    private void Update()
    {
        if (_timeOfDay == null) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;

        _accum += Time.deltaTime * simSpeed;
        while (_accum >= simTickSeconds)
        {
            _accum -= simTickSeconds;

            // Convert real seconds to game-hours using your TimeOfDayManager’s dayLength.
            // dayLength = real seconds per in-game day (24h).
            float gameHours = (24f / Mathf.Max(0.0001f, _timeOfDay.DayLength)) * simTickSeconds;

            TickAllNodes(gameHours);
        }
    }

    private void TickAllNodes(float dtHours)
    {

        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;

        if (resourceCatalog == null)
            Debug.LogError("[WorldMapDriftSimulator] resourceCatalog is NULL (ScriptableObject must be assigned in inspector).");

        EnsureDiffusion();

        if (graphGenerator != null && graphGenerator.graph != null)
        {
            _diffusion.Tick(
                graphGenerator.graph,
                runtimeBinder.Registry,
                resourceCatalog,
                dtHours
            );
        }

        // Iterate runtime nodes (authoritative)
        foreach (var rt in runtimeBinder.Registry.AllRuntimes)
        {
            var state = rt.State;
            if (state == null) continue;

            // Configure dock/trade drift parameters (NOT a tick)
            if (driftDockAndTrade)
            {
                if (state.TryGetStat(NodeStatId.DockRating, out var dock))
                {
                    dock.equilibrium = buildingEquilibrium;
                    dock.restoreStrength = buildingRestoreStrength;
                    state.SetStatPreserveVelocity(NodeStatId.DockRating, dock);
                }

                if (state.TryGetStat(NodeStatId.TradeRating, out var trade))
                {
                    trade.equilibrium = buildingEquilibrium;
                    trade.restoreStrength = buildingRestoreStrength;
                    state.SetStatPreserveVelocity(NodeStatId.TradeRating, trade);
                }
            }

            // Drift stats with buff accel
            _tmpStatKeys.Clear();
            foreach (var kvp in state.Stats)
                _tmpStatKeys.Add(kvp.Key);

            for (int i = 0; i < _tmpStatKeys.Count; i++)
            {
                var statId = _tmpStatKeys[i];
                var stat = state.GetStat(statId);

                float old = stat.value;
                float accel = GetInfluenceAccelForStat(state, statId);

                if (stat.Tick(dtHours, accel))
                {
                    state.SetStatPreserveVelocity(statId, stat);

                    WorldMapMessageBus.Publish(
                        new WorldMapChange(
                            WorldMapChangeKind.StatChanged,
                            rt.NodeIndex,
                            rt.DisplayName,
                            statId.ToString(),
                            old,
                            stat.value
                        )
                    );
                }
            }

            // Population response based on runtime FoodBalance
            if (state.TryGetStat(NodeStatId.FoodBalance, out var foodStat))
            {
                float food = foodStat.value;
                float oldPop = state.population;

                if (food < -0.6f) state.population -= populationLossRate * dtHours;
                else if (food > 0.4f) state.population += populationGrowthRate * dtHours;

                state.population = Mathf.Clamp(state.population, state.minPopulation, state.maxPopulation);

                if (!Mathf.Approximately(oldPop, state.population))
                {
                    WorldMapMessageBus.Publish(
                        new WorldMapChange(WorldMapChangeKind.StatChanged, rt.NodeIndex, rt.DisplayName, "Population", oldPop, state.population)
                    );
                }
            }

            // Tick and cleanup buffs (runtime-owned)
            TickAndCleanupBuffs(state, dtHours);
        }
    }

    private float GetInfluenceAccelForStat(MapNodeState state, NodeStatId statId)
    {
        var buffs = state.ActiveBuffsMutable;
        if (buffs == null || buffs.Count == 0) return 0f;

        float accel = 0f;
        for (int i = 0; i < buffs.Count; i++)
        {
            var inst = buffs[i];
            if (inst.buff == null) continue;

            var target = inst.buff.target;
            if (target.kind != NodeValueTargetKind.Stat) continue;
            if (target.statId != statId) continue;

            accel += inst.GetAccelThisTick();
        }

        return accel;
    }

    private void TickAndCleanupBuffs(MapNodeState state, float dtHours)
    {
        var buffs = state.ActiveBuffsMutable;
        if (buffs == null || buffs.Count == 0) return;

        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            var inst = buffs[i];
            inst.Tick(dtHours);

            if (inst.IsExpired) buffs.RemoveAt(i);
            else buffs[i] = inst; // struct writeback
        }
    }

    private void EnsureDiffusion()
    {
        if (_diffusion != null) return;

        _diffusion = new PressureDiffusionSystem(
            new PressureDiffusionSystem.Settings
            {
                baseConductancePerHour = pressureConductance,
                intraClusterMultiplier = intraClusterPressureMult,
                interClusterMultiplier = interClusterPressureMult,
                dockTradeInfluence01 = logisticsInfluence,
                edgeLoss01 = edgeLoss01,
                maxAbsFlowPerEdge = maxPressurePerEdge,
                targetDegree = targetEdgeDegree
            }
        );
    }

}
