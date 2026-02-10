using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldMapRuntimeBinder : MonoBehaviour
{
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private Transform container;
    [SerializeField] private MapNodeRuntime runtimePrefab;

    public WorldMapRuntimeRegistry Registry { get; private set; } = new WorldMapRuntimeRegistry();

    public event Action OnRuntimeBuilt;
    public bool IsBuilt { get; private set; }

    private readonly List<GameObject> _spawned = new();

    private void OnEnable()
    {
        if (generator != null)
            generator.OnGraphGenerated += Rebuild;
    }

    private void OnDisable()
    {
        if (generator != null)
            generator.OnGraphGenerated -= Rebuild;
    }

    private void Start()
    {
        // If graph already exists (eg generated in editor), still build runtime.
        if (generator != null && generator.graph != null)
            Rebuild();
    }

    [ContextMenu("Rebuild Runtime Nodes")]
    public void Rebuild()
    {
        IsBuilt = false;

        Clear();

        if (generator == null || generator.graph == null) return;
        if (container == null) container = transform;

        Registry.Clear();

        for (int i = 0; i < generator.graph.nodes.Count; i++)
        {
            var n = generator.graph.nodes[i];
            string stableId = $"{generator.seed}:{n.id}";

            MapNodeRuntime rt;
            if (runtimePrefab != null)
            {
                rt = Instantiate(runtimePrefab, container);
                rt.name = $"NodeRuntime_{i}";
            }
            else
            {
                var go = new GameObject($"NodeRuntime_{i}");
                go.transform.SetParent(container, worldPositionStays: true);
                rt = go.AddComponent<MapNodeRuntime>();
            }

            rt.transform.position = generator.transform.TransformPoint(new Vector3(n.position.x, n.position.y, 0f));
            rt.InitializeFromGraph(i, stableId, n);

            Registry.Add(i, stableId, rt);
            _spawned.Add(rt.gameObject);
        }

        IsBuilt = true;
        OnRuntimeBuilt?.Invoke();
    }

    private void Clear()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null)
                Destroy(_spawned[i]);

        _spawned.Clear();
        Registry.Clear();
    }
}
