using System;
using UnityEngine;

public class WorldMapDriftSimulator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private TimeOfDayManager timeOfDay;

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

    // 2a-ready messaging (optional)
    public event Action<MapNode, NodeStatId, float, float> OnNodeStatChanged;
    public event Action<MapNode, string, float, float> OnNodeBuildingChanged; // "Dock"/"Trade"

    private float _accum;

    private void Reset()
    {
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
    }

    private void Update()
    {
        if (generator == null || generator.graph == null) return;
        if (timeOfDay == null) return;

        _accum += Time.deltaTime * simSpeed;
        while (_accum >= simTickSeconds)
        {
            _accum -= simTickSeconds;

            // Convert real seconds to game-hours using your TimeOfDayManager’s dayLength.
            // dayLength = real seconds per in-game day (24h).
            float gameHours = (24f / Mathf.Max(0.0001f, timeOfDay.DayLength)) * simTickSeconds;

            TickAllNodes(gameHours);
        }
    }

    private void TickAllNodes(float dtHours)
    {
        var g = generator.graph;

        for (int i = 0; i < g.nodes.Count; i++)
        {
            var node = g.nodes[i];

            // Drift core stats (with buff influence)
            for (int s = 0; s < node.stats.Count; s++)
            {
                var ns = node.stats[s];
                float old = ns.stat.value;

                float accel = GetInfluenceAccelForStat(node, ns.id);

                if (ns.stat.Tick(dtHours, influenceAccel: accel))
                {
                    node.stats[s] = ns;
                    OnNodeStatChanged?.Invoke(node, ns.id, old, ns.stat.value);

                    WorldMapMessageBus.Publish(
                        new WorldMapChange(WorldMapChangeKind.StatChanged, node, ns.id.ToString(), old, ns.stat.value)
                    );
                }
            }

            if (driftDockAndTrade)
            {
                float dockAccel = GetInfluenceAccelForBuilding(node, NodeValueTargetKind.DockRating);
                float tradeAccel = GetInfluenceAccelForBuilding(node, NodeValueTargetKind.TradeRating);

                DriftBuilding(ref node.dock, "Dock", node, dtHours, dockAccel);
                DriftBuilding(ref node.tradeHub, "Trade", node, dtHours, tradeAccel);
            }
            TickAndCleanupBuffs(node, dtHours);
        }
    }

    private void DriftBuilding(ref BuildingRating building, string label, MapNode node, float dtHours, float influenceAccel)
    {
        float old = building.rating;

        float toEq = (buildingEquilibrium - building.rating);
        float eqAccel = toEq * buildingRestoreStrength;

        // influenceAccel is also "per hour" acceleration
        building.rating += (eqAccel + influenceAccel) * dtHours;
        building.rating = Mathf.Clamp(building.rating, 0f, 4f);

        if (!Mathf.Approximately(old, building.rating))
        {
            OnNodeBuildingChanged?.Invoke(node, label, old, building.rating);

            WorldMapMessageBus.Publish(
                new WorldMapChange(WorldMapChangeKind.BuildingChanged, node, label, old, building.rating)
            );
        }
    }

    private float GetInfluenceAccelForStat(MapNode node, NodeStatId statId)
    {
        if (node.activeBuffs == null || node.activeBuffs.Count == 0) return 0f;

        float accel = 0f;

        for (int i = 0; i < node.activeBuffs.Count; i++)
        {
            var inst = node.activeBuffs[i];
            if (inst.buff == null) continue;

            var target = inst.buff.target;
            if (target.kind != NodeValueTargetKind.Stat) continue;
            if (target.statId != statId) continue;

            accel += inst.GetAccelThisTick();
        }

        return accel;
    }

    private float GetInfluenceAccelForBuilding(MapNode node, NodeValueTargetKind buildingKind)
    {
        if (node.activeBuffs == null || node.activeBuffs.Count == 0) return 0f;

        float accel = 0f;

        for (int i = 0; i < node.activeBuffs.Count; i++)
        {
            var inst = node.activeBuffs[i];
            if (inst.buff == null) continue;

            var target = inst.buff.target;
            if (target.kind != buildingKind) continue;

            accel += inst.GetAccelThisTick();
        }

        return accel;
    }

    private void TickAndCleanupBuffs(MapNode node, float dtHours)
    {
        if (node.activeBuffs == null || node.activeBuffs.Count == 0) return;

        for (int i = node.activeBuffs.Count - 1; i >= 0; i--)
        {
            var inst = node.activeBuffs[i];
            inst.Tick(dtHours);
            if (inst.IsExpired)
            {
                node.activeBuffs.RemoveAt(i);
            }
            else
            {
                // TimedBuffInstance is a struct, so write back
                node.activeBuffs[i] = inst;
            }
        }
    }

}
