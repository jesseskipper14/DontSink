using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cargo/Cargo Catalog", fileName = "CargoCatalog")]
public sealed class CargoCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public GameObject prefab;
        [HideInInspector] public string typeGuid;
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
            if (string.IsNullOrEmpty(e.typeGuid)) continue;
            if (!_map.ContainsKey(e.typeGuid))
                _map.Add(e.typeGuid, e.prefab);
        }
    }

    public GameObject Resolve(string typeGuid)
    {
        if (string.IsNullOrEmpty(typeGuid)) return null;
        if (_map.Count == 0) Rebuild();
        return _map.TryGetValue(typeGuid, out var prefab) ? prefab : null;
    }

#if UNITY_EDITOR
    [ContextMenu("Sync Type GUIDs From Prefabs")]
    private void SyncTypeGuidsFromPrefabs()
    {
        foreach (var e in entries)
        {
            if (e == null || e.prefab == null) continue;

            var t = e.prefab.GetComponent<CargoTypeIdentity>();
            if (t == null) t = e.prefab.AddComponent<CargoTypeIdentity>();

            e.typeGuid = t.TypeGuid;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(e.prefab);
        }

        UnityEditor.AssetDatabase.SaveAssets();
        Rebuild();
    }
#endif
}