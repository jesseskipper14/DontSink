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

    [SerializeField] private ArchetypeCatalog archetypes;
    private int worldSeed => generator != null ? generator.seed : 0;

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

        if (archetypes != null)
        {
            // Precompute deterministic picks for this graph+seed so:
            // - affinity repeats can be penalized globally
            // - archetype coverage can be guaranteed globally
            archetypes.BuildPlan(generator.graph, generator.seed);
        }

        archetypes.BuildPlan(generator.graph, generator.seed);

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

            if (archetypes != null)
            {
                var affinity = archetypes.PickClusterAffinity(rt.ClusterId, worldSeed);
                var nodeArch = archetypes.PickNodeArchetype(rt.StableId, rt.ClusterId, worldSeed, affinity);

                rt.SetArchetypeIdentity(
                    affinity != null ? affinity.affinityId : "(null)",
                    nodeArch != null ? nodeArch.archetypeId : "(null)"
                );

                if (nodeArch == null)
                    Debug.LogWarning($"[Binder] Node {rt.DisplayName} stableId={rt.StableId} got NULL archetype (cluster={rt.ClusterId}).");
                else if (nodeArch.pressureBiases == null || nodeArch.pressureBiases.Count == 0)
                    Debug.LogWarning($"[Binder] Node {rt.DisplayName} archetype={nodeArch.archetypeId} has ZERO pressureBiases.");

                rt.State.ApplyArchetype(nodeArch);
            }
            else
            {
                Debug.LogWarning("[Binder] ArchetypeCatalog not assigned on WorldMapRuntimeBinder.");
            }

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
