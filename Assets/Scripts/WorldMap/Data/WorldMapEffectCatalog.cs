using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/Effects/Effect Catalog",
    fileName = "WorldMapEffectCatalog")]
public sealed class WorldMapEffectCatalog : ScriptableObject
{
    [Header("Events")]
    [SerializeField] private List<WorldMapEventDefinition> events = new();

    [Header("Outcomes")]
    [SerializeField] private List<EventOutcome> outcomes = new();

    [Header("Buffs")]
    [SerializeField] private List<NodeBuff> buffs = new();

    [NonSerialized] private Dictionary<string, WorldMapEventDefinition> _eventsById;
    [NonSerialized] private Dictionary<string, EventOutcome> _outcomesById;
    [NonSerialized] private Dictionary<string, NodeBuff> _buffsById;
    [NonSerialized] private bool _indexBuilt;

    public IReadOnlyList<WorldMapEventDefinition> Events => events;
    public IReadOnlyList<EventOutcome> Outcomes => outcomes;
    public IReadOnlyList<NodeBuff> Buffs => buffs;

    private void OnEnable()
    {
        RebuildIndex();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Rebuild lazily in editor too so inspector edits are reflected quickly.
        RebuildIndex();
    }
#endif

    [ContextMenu("Rebuild Index")]
    public void RebuildIndex()
    {
        _eventsById = new Dictionary<string, WorldMapEventDefinition>(StringComparer.Ordinal);
        _outcomesById = new Dictionary<string, EventOutcome>(StringComparer.Ordinal);
        _buffsById = new Dictionary<string, NodeBuff>(StringComparer.Ordinal);

        IndexEvents();
        IndexOutcomes();
        IndexBuffs();

        _indexBuilt = true;
    }

    [ContextMenu("Validate Catalog")]
    public void ValidateCatalog()
    {
        RebuildIndex();

        Debug.Log(
            $"[WorldMapEffectCatalog:{name}] Validation complete. " +
            $"Events={events.Count}, Outcomes={outcomes.Count}, Buffs={buffs.Count}",
            this);
    }

    public bool TryGetEvent(string eventId, out WorldMapEventDefinition def)
    {
        EnsureIndex();
        def = null;

        if (string.IsNullOrWhiteSpace(eventId))
            return false;

        return _eventsById.TryGetValue(eventId, out def);
    }

    public bool TryGetOutcome(string outcomeId, out EventOutcome outcome)
    {
        EnsureIndex();
        outcome = null;

        if (string.IsNullOrWhiteSpace(outcomeId))
            return false;

        return _outcomesById.TryGetValue(outcomeId, out outcome);
    }

    public bool TryGetBuff(string buffId, out NodeBuff buff)
    {
        EnsureIndex();
        buff = null;

        if (string.IsNullOrWhiteSpace(buffId))
            return false;

        return _buffsById.TryGetValue(buffId, out buff);
    }

    public WorldMapEventDefinition GetEventOrNull(string eventId)
    {
        return TryGetEvent(eventId, out var def) ? def : null;
    }

    public EventOutcome GetOutcomeOrNull(string outcomeId)
    {
        return TryGetOutcome(outcomeId, out var outcome) ? outcome : null;
    }

    public NodeBuff GetBuffOrNull(string buffId)
    {
        return TryGetBuff(buffId, out var buff) ? buff : null;
    }

    public string GetEventLabel(int index)
    {
        if (events == null || index < 0 || index >= events.Count)
            return "(none)";

        var def = events[index];
        if (def == null)
            return "(null event)";

        if (!string.IsNullOrWhiteSpace(def.displayName))
            return def.displayName;

        if (!string.IsNullOrWhiteSpace(def.eventId))
            return def.eventId;

        return def.name;
    }

    public string GetOutcomeLabel(int index)
    {
        if (outcomes == null || index < 0 || index >= outcomes.Count)
            return "(none)";

        var outcome = outcomes[index];
        if (outcome == null)
            return "(null outcome)";

        if (!string.IsNullOrWhiteSpace(outcome.displayName))
            return outcome.displayName;

        if (!string.IsNullOrWhiteSpace(outcome.outcomeId))
            return outcome.outcomeId;

        return outcome.name;
    }

    public string GetBuffLabel(int index)
    {
        if (buffs == null || index < 0 || index >= buffs.Count)
            return "(none)";

        var buff = buffs[index];
        if (buff == null)
            return "(null buff)";

        if (!string.IsNullOrWhiteSpace(buff.displayName))
            return buff.displayName;

        if (!string.IsNullOrWhiteSpace(buff.buffId))
            return buff.buffId;

        return buff.name;
    }

    private void EnsureIndex()
    {
        if (_indexBuilt &&
            _eventsById != null &&
            _outcomesById != null &&
            _buffsById != null)
        {
            return;
        }

        RebuildIndex();
    }

    private void IndexEvents()
    {
        if (events == null)
            return;

        for (int i = 0; i < events.Count; i++)
        {
            var def = events[i];

            if (def == null)
            {
                Debug.LogWarning($"[WorldMapEffectCatalog:{name}] Null event at index {i}.", this);
                continue;
            }

            string id = def.eventId;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning(
                    $"[WorldMapEffectCatalog:{name}] Event '{def.name}' has empty eventId.",
                    def);
                continue;
            }

            if (_eventsById.ContainsKey(id))
            {
                Debug.LogWarning(
                    $"[WorldMapEffectCatalog:{name}] Duplicate eventId '{id}'. Keeping first, ignoring '{def.name}'.",
                    def);
                continue;
            }

            _eventsById.Add(id, def);
        }
    }

    private void IndexOutcomes()
    {
        if (outcomes == null)
            return;

        for (int i = 0; i < outcomes.Count; i++)
        {
            var outcome = outcomes[i];

            if (outcome == null)
            {
                Debug.LogWarning($"[WorldMapEffectCatalog:{name}] Null outcome at index {i}.", this);
                continue;
            }

            string id = outcome.outcomeId;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning(
                    $"[WorldMapEffectCatalog:{name}] Outcome '{outcome.name}' has empty outcomeId.",
                    outcome);
                continue;
            }

            if (_outcomesById.ContainsKey(id))
            {
                Debug.LogWarning(
                    $"[WorldMapEffectCatalog:{name}] Duplicate outcomeId '{id}'. Keeping first, ignoring '{outcome.name}'.",
                    outcome);
                continue;
            }

            _outcomesById.Add(id, outcome);
        }
    }

    private void IndexBuffs()
    {
        if (buffs == null)
            return;

        for (int i = 0; i < buffs.Count; i++)
        {
            var buff = buffs[i];

            if (buff == null)
            {
                Debug.LogWarning($"[WorldMapEffectCatalog:{name}] Null buff at index {i}.", this);
                continue;
            }

            string id = buff.buffId;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning(
                    $"[WorldMapEffectCatalog:{name}] Buff '{buff.name}' has empty buffId.",
                    buff);
                continue;
            }

            if (_buffsById.ContainsKey(id))
            {
                Debug.LogWarning(
                    $"[WorldMapEffectCatalog:{name}] Duplicate buffId '{id}'. Keeping first, ignoring '{buff.name}'.",
                    buff);
                continue;
            }

            _buffsById.Add(id, buff);
        }
    }
}