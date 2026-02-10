using System;
using System.Collections.Generic;
using UnityEngine;

public class MapNodeRuntime : MonoBehaviour
{
    [SerializeField] private MapNodeDefinition _definition;

    [Header("Runtime State (read-only-ish)")]
    [SerializeField] private MapNodeState _state;
    [SerializeField] private int _clusterId;
    [SerializeField] private string _stableId;
    [SerializeField] private int _nodeIndex = -1;
    [SerializeField] private string _displayName;

    public MapNodeDefinition Definition => _definition;
    public MapNodeState State => _state;
    public int ClusterId => _clusterId;
    public string StableId => _stableId;
    public int NodeIndex => _nodeIndex;
    public string DisplayName => _displayName;

    // 2a: Basic messaging
    public event Action<MapNodeRuntime, NodeStatId, float, float> OnStatChanged;

    public void Initialize(MapNodeDefinition def, int clusterId)
    {
        _definition = def;
        _clusterId = clusterId;
        _state = new MapNodeState(def.NodeId);
    }

    public void Tick(float dt, Func<MapNodeRuntime, NodeStatId, float> influenceProvider)
    {
        foreach (var key in new List<NodeStatId>(_state.Stats.Keys))
        {
            float accel = influenceProvider?.Invoke(this, key) ?? 0f;

            var stat = _state.GetStat(key);
            float old = stat.value;

            if (stat.Tick(dt, accel))
            {
                // Dictionary stores struct: write back
                _state.ForceSetStat(key, stat.value); // (forces zero velocity, not desired)

                // Better: assign back the updated struct without killing velocity:
                // (We’ll fix this properly once you paste yesterday’s code.)
                // For now keep it simple.

                OnStatChanged?.Invoke(this, key, old, stat.value);
            }
        }
    }

    public void InitializeFromGraph(int nodeIndex, string stableId, MapNode data)
    {
        _nodeIndex = nodeIndex;
        _displayName = data.displayName;

        _stableId = stableId;
        _clusterId = data.clusterId;

        _state = new MapNodeState(stableId);

        _state.population = data.population;
        _state.minPopulation = data.minPopulation;
        _state.maxPopulation = data.maxPopulation;

        for (int i = 0; i < data.stats.Count; i++)
        {
            var s = data.stats[i];
            _state.ForceSetStat(s.id, s.stat.value);
        }

        _state.ForceSetStat(NodeStatId.DockRating, data.dock.rating);
        _state.ForceSetStat(NodeStatId.TradeRating, data.tradeHub.rating);
    }

    public void ApplyOutcome(EventOutcome outcome)
    {
        if (outcome == null) return;

        var list = _state.ActiveBuffsMutable;
        for (int i = 0; i < outcome.buffs.Count; i++)
        {
            var e = outcome.buffs[i];
            if (e.buff == null) continue;
            list.Add(new TimedBuffInstance(e.buff, e.durationHours, e.stacks));
        }
    }
}
