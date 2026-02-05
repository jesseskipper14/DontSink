using System;
using System.Collections.Generic;
using UnityEngine;

public class MapNodeRuntime : MonoBehaviour
{
    [SerializeField] private MapNodeDefinition _definition;

    [Header("Runtime State (read-only-ish)")]
    [SerializeField] private MapNodeState _state;

    public MapNodeDefinition Definition => _definition;
    public MapNodeState State => _state;

    // 2a: Basic messaging
    public event Action<MapNodeRuntime, NodeStatId, float, float> OnStatChanged;

    public void Initialize(MapNodeDefinition def)
    {
        _definition = def;
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
}
