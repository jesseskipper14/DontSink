//using UnityEngine;

//// DEPRECATED CLASS

//public class WorldMapPlayer : MonoBehaviour
//{
//    public WorldMapPlayerState State { get; private set; }

//    [SerializeField] private WorldMapGraphGenerator generator;
//    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;

//    private void Reset()
//    {
//        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
//        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
//    }

//    private void OnEnable()
//    {
//        if (runtimeBinder != null)
//            runtimeBinder.OnRuntimeBuilt += InitializeFromWorld;
//    }

//    private void OnDisable()
//    {
//        if (runtimeBinder != null)
//            runtimeBinder.OnRuntimeBuilt -= InitializeFromWorld;
//    }

//    private void Start()
//    {
//        State ??= new WorldMapPlayerState();

//        // If runtime already built, initialize now.
//        if (runtimeBinder != null && runtimeBinder.IsBuilt)
//            InitializeFromWorld();
//    }

//    private void InitializeFromWorld()
//    {
//        State ??= new WorldMapPlayerState();

//        if (generator == null || generator.graph == null)
//        {
//            Debug.LogError("WorldMapPlayer: generator/graph missing.");
//            return;
//        }

//        int startIndex = -1;
//        for (int i = 0; i < generator.graph.nodes.Count; i++)
//        {
//            if (generator.graph.nodes[i].kind == NodeKind.StartDock)
//            {
//                startIndex = i;
//                break;
//            }
//        }

//        if (startIndex < 0)
//        {
//            Debug.LogError("WorldMapPlayer: no StartDock node found.");
//            return;
//        }

//        if (!runtimeBinder.Registry.TryGetByIndex(startIndex, out var startRuntime))
//        {
//            Debug.LogError("WorldMapPlayer: start runtime not found even after build.");
//            return;
//        }

//        State.currentNodeId = startRuntime.StableId;
//        transform.position = startRuntime.transform.position;
//    }

//}
