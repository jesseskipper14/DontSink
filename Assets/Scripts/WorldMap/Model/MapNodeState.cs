using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MapNodeState
{
    [SerializeField] private string _nodeId;

    [SerializeField] private Dictionary<NodeStatId, SimStat> _stats;
    [SerializeField] private Dictionary<FactionId, float> _factionInfluence; // 0..1 (or 0..100)
    [SerializeField] private List<string> _flags; // “famine”, “pirate_pressure”, “cult_tithe”

    [SerializeField] private List<TimedBuffInstance> _activeBuffs = new();
    public IReadOnlyList<TimedBuffInstance> ActiveBuffs => _activeBuffs;
    public List<TimedBuffInstance> ActiveBuffsMutable => _activeBuffs; // internal escape hatch for sim


    [Header("Population")]
    public float population = 100f;           // absolute count (or abstract units)
    public float minPopulation = 10f;          // settlement never fully dies (for now)
    public float maxPopulation = 500f;         // soft cap, can grow later

    public string NodeId => _nodeId;

    public IReadOnlyDictionary<NodeStatId, SimStat> Stats => _stats;
    public IReadOnlyDictionary<FactionId, float> FactionInfluence => _factionInfluence;
    public IReadOnlyList<string> Flags => _flags;

    public MapNodeState(string nodeId)
    {
        _nodeId = nodeId;

        _stats = new Dictionary<NodeStatId, SimStat>
        {
            { NodeStatId.DockRating,  new SimStat(initial: 1.0f, eq: 1.2f, restore: 0.15f) },
            { NodeStatId.TradeRating, new SimStat(initial: 1.0f, eq: 1.2f, restore: 0.15f) },
            { NodeStatId.Prosperity,  new SimStat(initial: 1.0f, eq: 1.2f, restore: 0.15f) },
            { NodeStatId.Stability,   new SimStat(initial: 1.0f, eq: 1.2f, restore: 0.15f) },
            { NodeStatId.Security,    new SimStat(initial: 1.0f, eq: 1.2f, restore: 0.15f) },
            { NodeStatId.FoodBalance, new SimStat(initial: 0.0f, eq: 0.0f, restore: 0.15f, -4f, 4f) }
        };

        _factionInfluence = new Dictionary<FactionId, float>();
        _flags = new List<string>();
    }

    public SimStat GetStat(NodeStatId id) => _stats[id];

    public void ForceSetStat(NodeStatId id, float value)
    {
        var s = _stats[id];

        // FoodBalance is a pressure stat and must allow negatives.
        // Most other stats are 0..4 ratings.
        if (id == NodeStatId.FoodBalance)
            s.value = Mathf.Clamp(value, -4f, 4f);
        else
            s.value = Mathf.Clamp(value, 0f, 4f);

        s.velocity = 0f;
        _stats[id] = s;
    }

    public bool TryGetStat(NodeStatId id, out SimStat stat)
    {
        if (_stats != null && _stats.TryGetValue(id, out stat)) return true;
        stat = default;
        return false;
    }

    public void SetStatPreserveVelocity(NodeStatId id, SimStat stat)
    {
        // Trust SimStat.Tick to clamp to its own bounds.
        _stats[id] = stat;
    }

    public void SetFactionInfluence(FactionId faction, float value01)
    {
        _factionInfluence[faction] = Mathf.Clamp01(value01);
    }

    public void AddFlag(string flag)
    {
        if (!_flags.Contains(flag)) _flags.Add(flag);
    }

    public void RemoveFlag(string flag) => _flags.Remove(flag);
}
