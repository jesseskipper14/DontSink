using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MapNodeRuntime : MonoBehaviour
{
    // =========================
    // Inspector / identity
    // =========================

    [Header("Definition")]
    [SerializeField] private MapNodeDefinition _definition;

    [Header("Runtime Identity")]
    [SerializeField] private int _nodeIndex = -1;
    [SerializeField] private string _stableId;
    [SerializeField] private int _clusterId;
    [SerializeField] private string _displayName;

    [Header("Economy Identity (debug/telemetry)")]
    [SerializeField] private string _clusterAffinityId;
    [SerializeField] private string _nodeArchetypeId;

    [Header("Runtime State (authoritative)")]
    [SerializeField] private MapNodeState _state;

    // =========================
    // Public API
    // =========================

    public MapNodeDefinition Definition => _definition;
    public MapNodeState State => _state;

    public int NodeIndex => _nodeIndex;
    public string StableId => _stableId;
    public int ClusterId => _clusterId;
    public string DisplayName => _displayName;

    public string ClusterAffinityId => _clusterAffinityId;
    public string NodeArchetypeId => _nodeArchetypeId;

    public event Action<MapNodeRuntime, NodeStatId, float, float> OnStatChanged;

    // =========================
    // Initialization
    // =========================

    public void Initialize(MapNodeDefinition def, int clusterId)
    {
        _definition = def;
        _clusterId = clusterId;

        // NOTE: This init path uses def.NodeId. Your graph init uses stableId.
        // Keep both, but be intentional about which is used where.
        _state = new MapNodeState(def != null ? def.NodeId : "");
    }

    public void InitializeFromGraph(int nodeIndex, string stableId, MapNode data)
    {
        _nodeIndex = nodeIndex;
        _stableId = stableId ?? "";
        _clusterId = data.clusterId;
        _displayName = data.displayName;

        _state = new MapNodeState(_stableId);

        // Population
        _state.population = data.population;
        _state.minPopulation = data.minPopulation;
        _state.maxPopulation = data.maxPopulation;

        // Stats from data (if present)
        for (int i = 0; i < data.stats.Count; i++)
        {
            var s = data.stats[i];
            _state.ForceSetStat(s.id, s.stat.value);
        }

        // Ratings (override)
        _state.ForceSetStat(NodeStatId.DockRating, data.dock.rating);
        _state.ForceSetStat(NodeStatId.TradeRating, data.tradeHub.rating);
    }

    public void SetArchetypeIdentity(string affinityId, string archetypeId)
    {
        _clusterAffinityId = affinityId ?? "";
        _nodeArchetypeId = archetypeId ?? "";
    }

    // =========================
    // Simulation
    // =========================

    private static readonly List<NodeStatId> _tmpStatKeys = new List<NodeStatId>(16);

    public void Tick(float dt, Func<MapNodeRuntime, NodeStatId, float> influenceProvider)
    {
        if (_state == null || _state.Stats == null) return;

        // Avoid GC: copy keys into a static temp list (safe because we don't re-enter Tick concurrently).
        _tmpStatKeys.Clear();
        foreach (var kvp in _state.Stats)
            _tmpStatKeys.Add(kvp.Key);

        for (int i = 0; i < _tmpStatKeys.Count; i++)
        {
            var key = _tmpStatKeys[i];
            float accel = influenceProvider?.Invoke(this, key) ?? 0f;

            var stat = _state.GetStat(key);
            float old = stat.value;

            if (!stat.Tick(dt, accel))
                continue;

            // IMPORTANT: preserve velocity (ForceSetStat kills it)
            _state.SetStatPreserveVelocity(key, stat);

            OnStatChanged?.Invoke(this, key, old, stat.value);
        }
    }

    // =========================
    // Events / buffs
    // =========================

    public void ApplyOutcome(EventOutcome outcome)
    {
        if (outcome == null || _state == null) return;

        var list = _state.ActiveBuffsMutable;
        for (int i = 0; i < outcome.buffs.Count; i++)
        {
            var e = outcome.buffs[i];
            if (e.buff == null) continue;

            list.Add(new TimedBuffInstance(e.buff, e.durationHours, e.stacks));
        }
    }
}
