using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Event Outcome")]
public class EventOutcome : ScriptableObject
{
    public string outcomeId;
    public string displayName;

    [Tooltip("Buffs applied when this outcome is injected.")]
    public List<OutcomeBuffEntry> buffs = new();
}

[System.Serializable]
public struct OutcomeBuffEntry
{
    public NodeBuff buff;
    [Min(0.1f)] public float durationHours;

    [Tooltip("If the buff stacks, how many stacks to apply.")]
    [Min(1)] public int stacks;
}
