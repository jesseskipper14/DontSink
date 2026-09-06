using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TetherLineCatalog", menuName = "Game/Tether/Tether Line Catalog")]
public sealed class TetherLineCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private ItemDefinition itemDefinition;
        [SerializeField, Min(0.1f)] private float lengthMeters = 50f;
        [SerializeField, Min(0f)] private float workingLoadNewtons = 5000f;
        [SerializeField, Min(0f)] private float breakingLoadNewtons = 7500f;

        public ItemDefinition ItemDefinition => itemDefinition;
        public float LengthMeters => Mathf.Max(0.1f, lengthMeters);
        public float WorkingLoadNewtons => Mathf.Max(0f, workingLoadNewtons);
        public float BreakingLoadNewtons => Mathf.Max(WorkingLoadNewtons, breakingLoadNewtons);
    }

    [SerializeField] private List<Entry> entries = new();

    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGet(ItemDefinition itemDefinition, out Entry entry)
    {
        entry = null;

        if (itemDefinition == null || entries == null)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry candidate = entries[i];
            if (candidate == null || candidate.ItemDefinition == null)
                continue;

            if (ReferenceEquals(candidate.ItemDefinition, itemDefinition))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (entries == null)
            entries = new List<Entry>();
    }
#endif
}
