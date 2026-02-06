using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldMapEventManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private TimeOfDayManager timeOfDay;

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

        // For now: apply to source node only.
        // Next step (Step 8): radius propagation + falloff here.
        int id = resolved.sourceNodeId;
        if (id < 0 || id >= generator.graph.nodes.Count) return;

        generator.graph.nodes[id].ApplyOutcome(resolved.outcome);

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
