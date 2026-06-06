using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class UnderwaterResourceCatalogEntry
{
    public bool enabled = true;

    public UnderwaterResourceDefinition definition;

    [TextArea(1, 3)]
    public string notes;
}

[CreateAssetMenu(
    fileName = "UnderwaterResourceCatalog",
    menuName = "Underwater/Resource Catalog")]
public sealed class UnderwaterResourceCatalog : ScriptableObject
{
    [SerializeField] private List<UnderwaterResourceCatalogEntry> entries = new();

    private readonly List<UnderwaterResourceDefinition> enabledDefinitions = new();
    private readonly Dictionary<string, UnderwaterResourceDefinition> byStableId = new();

    private bool dirty = true;

    public IReadOnlyList<UnderwaterResourceCatalogEntry> Entries => entries;

    public IReadOnlyList<UnderwaterResourceDefinition> EnabledDefinitions
    {
        get
        {
            RebuildIfNeeded();
            return enabledDefinitions;
        }
    }

    public bool TryGetByStableId(string stableId, out UnderwaterResourceDefinition definition)
    {
        RebuildIfNeeded();

        if (string.IsNullOrWhiteSpace(stableId))
        {
            definition = null;
            return false;
        }

        return byStableId.TryGetValue(stableId, out definition);
    }

    public void GetEnabledByCategory(
        UnderwaterResourceCategory category,
        List<UnderwaterResourceDefinition> results)
    {
        if (results == null)
            return;

        results.Clear();

        RebuildIfNeeded();

        for (int i = 0; i < enabledDefinitions.Count; i++)
        {
            UnderwaterResourceDefinition definition = enabledDefinitions[i];
            if (definition != null && definition.category == category)
                results.Add(definition);
        }
    }

    public void MarkDirty()
    {
        dirty = true;
    }

    private void RebuildIfNeeded()
    {
        if (!dirty)
            return;

        dirty = false;

        enabledDefinitions.Clear();
        byStableId.Clear();

        HashSet<string> duplicateCheck = new();

        for (int i = 0; i < entries.Count; i++)
        {
            UnderwaterResourceCatalogEntry entry = entries[i];

            if (entry == null || !entry.enabled || entry.definition == null)
                continue;

            UnderwaterResourceDefinition definition = entry.definition;

            if (string.IsNullOrWhiteSpace(definition.stableId))
            {
                Debug.LogWarning(
                    $"Underwater resource definition '{definition.name}' has no stableId.",
                    definition);

                continue;
            }

            if (!duplicateCheck.Add(definition.stableId))
            {
                Debug.LogWarning(
                    $"Duplicate underwater resource stableId '{definition.stableId}' in catalog '{name}'. " +
                    $"Only the first enabled definition will be used for lookup.",
                    this);

                continue;
            }

            enabledDefinitions.Add(definition);
            byStableId.Add(definition.stableId, definition);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        dirty = true;
    }
#endif
}