using UnityEngine;

public class WorldMapNodeSelection : MonoBehaviour
{
    [SerializeField] private WorldMapGraphGenerator generator;

    [Header("Debug")]
    [SerializeField] private int selectedNodeId = -1;

    public int SelectedNodeId => selectedNodeId;

    public void Bind(WorldMapGraphGenerator gen)
    {
        generator = gen;
    }

    public void Select(int nodeId)
    {
        selectedNodeId = nodeId;

        if (generator?.graph == null) return;
        var node = generator.graph.nodes[nodeId];

        Debug.Log($"Selected node #{nodeId} ({node.displayName}) | Dock {node.dock.rating:0.00} Trade {node.tradeHub.rating:0.00}");
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
    [ContextMenu("DEBUG / Inject Outcome Into Selected Node")]
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
        if (generator?.graph == null) return;
        if (selectedNodeId < 0 || selectedNodeId >= generator.graph.nodes.Count) return;

        generator.graph.nodes[selectedNodeId].ApplyOutcome(outcome);
        Debug.Log($"Injected outcome {outcome.displayName} into node #{selectedNodeId}");
    }
}
