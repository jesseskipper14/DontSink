using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Boats/Boat Catalog", fileName = "BoatCatalog")]
public sealed class BoatCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public GameObject prefab;

        [Tooltip("Auto-synced from the prefab's BoatIdentity.BoatGuid.")]
        [SerializeField] public string guid;
    }

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    [Header("Entries")]
    [SerializeField] private List<Entry> entries = new();

    private readonly Dictionary<string, GameObject> _map = new();

    private void OnEnable()
    {
        Rebuild();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SyncGuidsFromPrefabsInternal(markDirty: false, saveAssets: false, log: false);
        Rebuild();
    }
#endif

    public void Rebuild()
    {
        _map.Clear();

        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            if (e == null || e.prefab == null)
                continue;

            if (string.IsNullOrWhiteSpace(e.guid))
                continue;

            if (_map.ContainsKey(e.guid))
            {
                LogWarning(
                    $"Duplicate boat guid '{e.guid}' at entry {i}. " +
                    $"Existing prefab='{_map[e.guid].name}', duplicate prefab='{e.prefab.name}'. Keeping first.");
                continue;
            }

            _map.Add(e.guid, e.prefab);
        }
    }

    public GameObject Resolve(string guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return null;

        if (_map.Count == 0)
            Rebuild();

        if (_map.TryGetValue(guid, out GameObject prefab) && prefab != null)
            return prefab;

        LogWarning($"Resolve failed for boat guid='{guid}'.");
        LogCatalogContents();

        return null;
    }

    [ContextMenu("Log Catalog Contents")]
    private void LogCatalogContents()
    {
        if (!verboseLogging)
            return;

        if (entries == null)
        {
            Debug.LogWarning($"[BoatCatalog:{name}] entries=NULL", this);
            return;
        }

        Debug.Log($"[BoatCatalog:{name}] Catalog contents | entries={entries.Count} | mapCount={_map.Count}", this);

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            Debug.Log(
                $"[BoatCatalog:{name}] Entry {i}: " +
                $"prefab='{(e?.prefab != null ? e.prefab.name : "NULL")}', " +
                $"guid='{(e != null ? e.guid : "NULL_ENTRY")}'",
                this);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Sync GUIDs From Prefabs")]
    private void SyncGuidsFromPrefabsContextMenu()
    {
        SyncGuidsFromPrefabsInternal(markDirty: true, saveAssets: true, log: true);
        Rebuild();
    }

    private void SyncGuidsFromPrefabsInternal(bool markDirty, bool saveAssets, bool log)
    {
        if (entries == null)
            return;

        bool changed = false;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            if (e == null || e.prefab == null)
                continue;

            BoatIdentity id = e.prefab.GetComponent<BoatIdentity>();

            if (id == null)
            {
                id = e.prefab.AddComponent<BoatIdentity>();
                changed = true;

                if (log)
                    Debug.LogWarning($"[BoatCatalog:{name}] Added missing BoatIdentity to prefab '{e.prefab.name}'.", e.prefab);

                if (markDirty)
                    UnityEditor.EditorUtility.SetDirty(e.prefab);
            }

            string newGuid = id.BoatGuid;

            if (string.IsNullOrWhiteSpace(newGuid))
            {
                if (log)
                    Debug.LogWarning($"[BoatCatalog:{name}] Prefab '{e.prefab.name}' has empty BoatIdentity.BoatGuid.", e.prefab);

                continue;
            }

            if (e.guid != newGuid)
            {
                string oldGuid = e.guid;
                e.guid = newGuid;
                changed = true;

                if (log)
                {
                    Debug.Log(
                        $"[BoatCatalog:{name}] Synced entry {i}: prefab='{e.prefab.name}' " +
                        $"oldGuid='{oldGuid}' newGuid='{newGuid}'",
                        this);
                }
            }
        }

        if (changed && markDirty)
            UnityEditor.EditorUtility.SetDirty(this);

        if (changed && saveAssets)
            UnityEditor.AssetDatabase.SaveAssets();
    }
#endif

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[BoatCatalog:{name}] {msg}", this);
    }
}