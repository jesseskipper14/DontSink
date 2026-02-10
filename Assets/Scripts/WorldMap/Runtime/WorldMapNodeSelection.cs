using UnityEngine;

public class WorldMapNodeSelection : MonoBehaviour
{
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;

    [Header("Debug")]
    [SerializeField] private int selectedNodeId = -1;
    public int SelectedNodeId => selectedNodeId;

    public string SelectedStableId { get; private set; }

    private void Reset()
    {
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
    }

    public void Bind(WorldMapGraphGenerator gen)
    {
        generator = gen;
        if (runtimeBinder == null) runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
    }

    public void Select(int nodeId)
    {
        selectedNodeId = nodeId;

        if (generator?.graph == null) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;
        if (!runtimeBinder.Registry.TryGetByIndex(nodeId, out var rt)) return;

        var state = rt.State;
        SelectedStableId = rt.StableId;
        if (state == null) return;

        float dock = state.GetStat(NodeStatId.DockRating).value;
        float trade = state.GetStat(NodeStatId.TradeRating).value;

        Debug.Log($"Selected node #{nodeId} ({rt.DisplayName}) | Dock {dock:0.00} Trade {trade:0.00}");
    }

    private void OnDrawGizmos()
    {
        if (generator?.graph == null) return;
        if (selectedNodeId < 0 || selectedNodeId >= generator.graph.nodes.Count) return;

        var n = generator.graph.nodes[selectedNodeId];
        Gizmos.color = Color.yellow;

        Vector3 wp = generator.transform.TransformPoint(new Vector3(n.position.x, n.position.y, 0f));
        Gizmos.DrawWireSphere(wp, 0.6f);
    }

    [SerializeField] private EventOutcome debugOutcome;

    [ContextMenu("DEBUG / Inject Outcome Into Selected Node (Runtime)")]
    private void DebugInjectSelected()
    {
        if (debugOutcome == null)
        {
            Debug.LogWarning("No debugOutcome assigned.");
            return;
        }

        InjectOutcomeToSelected(debugOutcome);
    }

    public void InjectOutcomeToSelected(EventOutcome outcome)
    {
        if (outcome == null) return;
        if (selectedNodeId < 0) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;
        if (!runtimeBinder.Registry.TryGetByIndex(selectedNodeId, out var rt)) return;

        var state = rt.State;
        if (state == null) return;

        // Apply buffs to RUNTIME state (authoritative).
        // Mirrors old MapNode.ApplyOutcome but writes to state.ActiveBuffsMutable.
        var list = state.ActiveBuffsMutable;
        if (list == null)
        {
            Debug.LogError("Selected node has no ActiveBuffsMutable list (state not initialized correctly).");
            return;
        }

        for (int i = 0; i < outcome.buffs.Count; i++)
        {
            var e = outcome.buffs[i];
            if (e.buff == null) continue;

            list.Add(new TimedBuffInstance(e.buff, e.durationHours, e.stacks));
        }

        Debug.Log($"Injected outcome {outcome.displayName} into node #{selectedNodeId} ({rt.DisplayName}) [runtime]");
    }
}
