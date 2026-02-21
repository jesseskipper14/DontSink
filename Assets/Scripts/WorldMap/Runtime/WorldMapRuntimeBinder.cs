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
            archetypes.BuildPlan(generator.graph, generator.seed);
        else
            Debug.LogWarning("[Binder] ArchetypeCatalog not assigned.");

        var gs = GameState.I;
        if (gs == null)
        {
            Debug.LogError("[Binder] GameState missing. Cannot persist node state.");
            return;
        }

        if (gs.worldMap == null)
            gs.worldMap = new WorldMapSimState();

        var stateStore = gs.worldMap.byNodeStableId;

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
                go.transform.SetParent(container, true);
                rt = go.AddComponent<MapNodeRuntime>();
            }

            rt.transform.position =
                generator.transform.TransformPoint(new Vector3(n.position.x, n.position.y, 0f));

            rt.InitializeFromGraph(i, stableId, n);

            // -------------------------------
            // PERSISTENT STATE REUSE
            // -------------------------------

            if (!stateStore.TryGetValue(stableId, out var state))
            {
                state = rt.State; // use freshly created state
                stateStore[stableId] = state;

                if (archetypes != null)
                {
                    var affinity = archetypes.PickClusterAffinity(rt.ClusterId, worldSeed);
                    var nodeArch = archetypes.PickNodeArchetype(
                        rt.StableId,
                        rt.ClusterId,
                        worldSeed,
                        affinity
                    );

                    rt.SetArchetypeIdentity(
                        affinity != null ? affinity.affinityId : "(null)",
                        nodeArch != null ? nodeArch.archetypeId : "(null)"
                    );

                    if (nodeArch != null)
                        state.ApplyArchetype(nodeArch);
                }
            }
            else
            {
                // Reattach persistent state
                rt.AttachExistingState(state);
            }

            Registry.Add(i, stableId, rt);
            _spawned.Add(rt.gameObject);
        }

        IsBuilt = true;
        EnsurePlayerHasCurrentNode();
        OnRuntimeBuilt?.Invoke();
    }

    private void EnsurePlayerHasCurrentNode()
    {
        var gs = GameState.I;
        if (gs == null) return;

        var p = gs.player;
        if (p == null) return;

        if (!string.IsNullOrEmpty(p.currentNodeId))
            return;

        var g = generator?.graph;
        if (g != null && g.nodes != null)
        {
            for (int i = 0; i < g.nodes.Count; i++)
            {
                if (g.nodes[i].kind == NodeKind.StartDock)
                {
                    if (Registry.TryGetByIndex(i, out var rt) && rt != null)
                    {
                        p.currentNodeId = rt.StableId;
                        return;
                    }
                }
            }
        }

        foreach (var rt in Registry.AllRuntimes)
        {
            if (rt == null) continue;
            p.currentNodeId = rt.StableId;
            return;
        }
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