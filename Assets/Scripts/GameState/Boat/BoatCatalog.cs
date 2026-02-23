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
        [HideInInspector] public string guid;
    }

    [SerializeField] private List<Entry> entries = new();

    private readonly Dictionary<string, GameObject> _map = new();

    private void OnEnable() => Rebuild();

    public void Rebuild()
    {
        _map.Clear();
        foreach (var e in entries)
        {
            if (e == null || e.prefab == null) continue;
            if (string.IsNullOrEmpty(e.guid)) continue;
            if (!_map.ContainsKey(e.guid))
                _map.Add(e.guid, e.prefab);
        }
    }

    public GameObject Resolve(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        if (_map.Count == 0) Rebuild();
        return _map.TryGetValue(guid, out var prefab) ? prefab : null;
    }

#if UNITY_EDITOR
    [ContextMenu("Sync GUIDs From Prefabs")]
    private void SyncGuidsFromPrefabs()
    {
        foreach (var e in entries)
        {
            if (e == null || e.prefab == null) continue;

            var id = e.prefab.GetComponent<BoatIdentity>();
            if (id == null) id = e.prefab.AddComponent<BoatIdentity>();

            e.guid = id.BoatGuid;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(e.prefab);
        }

        UnityEditor.AssetDatabase.SaveAssets();
        Rebuild();
    }
#endif
}