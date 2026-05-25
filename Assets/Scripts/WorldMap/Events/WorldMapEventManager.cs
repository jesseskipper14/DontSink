using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldMapEventManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private TimeOfDayManager timeOfDay;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;

    [Header("Catalog")]
    [SerializeField] private WorldMapEffectCatalog effectCatalog;
    public WorldMapEffectCatalog EffectCatalog => effectCatalog;

    [Header("Ticking")]
    [Min(0.05f)] public float tickSeconds = 0.25f;
    [Min(0f)] public float simSpeed = 1f;

    [Header("Debug")]
    public bool logResolutions = true;

    [NonSerialized] public readonly List<WorldMapEventInstance> active = new();

    private float _accum;

    private void Reset()
    {
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
    }

    private void Update()
    {
        if (generator == null || generator.graph == null) return;
        if (timeOfDay == null) return;

        _accum += Time.deltaTime * simSpeed;
        while (_accum >= tickSeconds)
        {
            _accum -= tickSeconds;

            float dtHours = (24f / Mathf.Max(0.0001f, timeOfDay.DayLength)) * tickSeconds;
            Tick(dtHours);
        }
    }

    public void AddEvent(WorldMapEventDefinition def, int sourceNodeId, int seed)
    {
        var inst = def.CreateInstance(sourceNodeId, seed);
        active.Add(inst);
    }

    public bool TryAddEventById(string eventId, int sourceNodeId, int seed)
    {
        if (effectCatalog == null)
        {
            Debug.LogWarning("[WorldMapEventManager] Cannot add event by id: missing WorldMapEffectCatalog.", this);
            return false;
        }

        if (!effectCatalog.TryGetEvent(eventId, out var def) || def == null)
        {
            Debug.LogWarning($"[WorldMapEventManager] Cannot add event: unknown eventId '{eventId}'.", this);
            return false;
        }

        AddEvent(def, sourceNodeId, seed);
        return true;
    }

    public bool TryApplyOutcomeToNode(int nodeIndex, EventOutcome outcome)
    {
        if (outcome == null)
            return false;

        if (runtimeBinder == null || !runtimeBinder.IsBuilt)
        {
            Debug.LogWarning("[WorldMapEventManager] Cannot apply outcome: runtime binder missing or not built.", this);
            return false;
        }

        if (!runtimeBinder.Registry.TryGetByIndex(nodeIndex, out var rt) || rt == null)
        {
            Debug.LogWarning($"[WorldMapEventManager] Cannot apply outcome: no runtime node #{nodeIndex}.", this);
            return false;
        }

        rt.ApplyOutcome(outcome);
        return true;
    }

    public bool TryApplyOutcomeByIdToNode(string outcomeId, int nodeIndex)
    {
        if (effectCatalog == null)
        {
            Debug.LogWarning("[WorldMapEventManager] Cannot apply outcome by id: missing WorldMapEffectCatalog.", this);
            return false;
        }

        if (!effectCatalog.TryGetOutcome(outcomeId, out var outcome) || outcome == null)
        {
            Debug.LogWarning($"[WorldMapEventManager] Cannot apply outcome: unknown outcomeId '{outcomeId}'.", this);
            return false;
        }

        return TryApplyOutcomeToNode(nodeIndex, outcome);
    }

    public bool TryInjectBuffToNode(int nodeIndex, NodeBuff buff, float durationHours, int stacks)
    {
        if (buff == null)
            return false;

        if (runtimeBinder == null || !runtimeBinder.IsBuilt)
        {
            Debug.LogWarning("[WorldMapEventManager] Cannot inject buff: runtime binder missing or not built.", this);
            return false;
        }

        if (!runtimeBinder.Registry.TryGetByIndex(nodeIndex, out var rt) || rt == null || rt.State == null)
        {
            Debug.LogWarning($"[WorldMapEventManager] Cannot inject buff: no runtime node #{nodeIndex}.", this);
            return false;
        }

        float duration = Mathf.Max(0.1f, durationHours);
        int stackCount = Mathf.Max(1, stacks);

        rt.State.ActiveBuffsMutable.Add(new TimedBuffInstance(buff, duration, stackCount));
        return true;
    }

    public bool TryInjectBuffByIdToNode(string buffId, int nodeIndex, float durationHours, int stacks)
    {
        if (effectCatalog == null)
        {
            Debug.LogWarning("[WorldMapEventManager] Cannot inject buff by id: missing WorldMapEffectCatalog.", this);
            return false;
        }

        if (!effectCatalog.TryGetBuff(buffId, out var buff) || buff == null)
        {
            Debug.LogWarning($"[WorldMapEventManager] Cannot inject buff: unknown buffId '{buffId}'.", this);
            return false;
        }

        return TryInjectBuffToNode(nodeIndex, buff, durationHours, stacks);
    }

    private void Tick(float dtHours)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var ev = active[i];
            if (ev.isResolved) { active.RemoveAt(i); continue; }

            ev.Tick(dtHours);

            if (ev.def is IWorldMapEventResolver resolver)
            {
                if (resolver.TryResolve(ref ev, generator, out var resolved))
                {
                    ev.isResolved = true;
                    active[i] = ev;
                    ApplyResolved(resolved);
                    active.RemoveAt(i);
                    continue;
                }
            }

            active[i] = ev;
        }
    }

    public bool TryCompleteSelectedEvent(int index)
    {
        if (index < 0 || index >= active.Count) return false;

        var ev = active[index];
        if (ev.def is not IWorldMapEventResolver resolver) return false;

        if (resolver.TryPlayerComplete(ref ev, generator, out var resolved))
        {
            ev.isResolved = true;
            ApplyResolved(resolved);
            active[index] = ev;
            active.RemoveAt(index);
            return true;
        }

        active[index] = ev;
        return false;
    }

    private void ApplyResolved(WorldMapEventResolved resolved)
    {
        if (resolved.outcome == null) return;
        if (runtimeBinder == null)
        {
            Debug.LogError("EventManager: Missing WorldMapRuntimeBinder.");
            return;
        }

        int id = resolved.sourceNodeId;

        if (!runtimeBinder.Registry.TryGetByIndex(id, out var rt))
        {
            Debug.LogError($"EventManager: No runtime node for index {id}.");
            return;
        }

        rt.ApplyOutcome(resolved.outcome);

        if (logResolutions)
            Debug.Log($"[EventResolved] {resolved.eventName} -> {resolved.outcome.displayName} on node #{id}");
    }

    public int CountEventsAtNode(int nodeId)
    {
        int count = 0;
        for (int i = 0; i < active.Count; i++)
            if (active[i].sourceNodeId == nodeId && !active[i].isResolved)
                count++;
        return count;
    }

    public void GetEventsAtNode(int nodeId, System.Collections.Generic.List<WorldMapEventInstance> results)
    {
        results.Clear();
        for (int i = 0; i < active.Count; i++)
        {
            var e = active[i];
            if (e.sourceNodeId == nodeId && !e.isResolved)
                results.Add(e);
        }
    }

    [SerializeField] private StormEventDefinition debugStormDef;

    [ContextMenu("DEBUG / Spawn Storm At Selected Node")]
    private void DebugSpawnStormAtSelected()
    {
        var sel = FindAnyObjectByType<WorldMapNodeSelection>();
        if (sel == null) { Debug.LogWarning("No WorldMapNodeSelection found."); return; }

        int id = sel.SelectedNodeId;
        if (id < 0) { Debug.LogWarning("No node selected."); return; }
        if (debugStormDef == null) { Debug.LogWarning("No debugStormDef assigned."); return; }

        AddEvent(debugStormDef, id, seed: UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        Debug.Log($"Spawned storm at node #{id}");
    }

}
