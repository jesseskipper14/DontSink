using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldMapDriftSimulator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TimeOfDayManager timeOfDay;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;

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

    private void Reset()
    {
        timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
    }

    private void Update()
    {
        if (timeOfDay == null) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;

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
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;

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
            var keys = new List<NodeStatId>(state.Stats.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var statId = keys[i];
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
}
