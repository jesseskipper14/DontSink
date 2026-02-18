using UnityEngine;

// LIKELY DEPRECATED

[RequireComponent(typeof(Collider2D))]
public class MapNodeView : MonoBehaviour
{
    [SerializeField] private int nodeId = -1;

    private WorldMapGraphGenerator _generator;
    private WorldMapNodeSelection _selection;

    [Header("Primary Node Visuals")]
    [SerializeField] private float primaryScaleMultiplier = 1.25f;
    [SerializeField] private Color primaryTint = new Color(1f, 0.85f, 0.55f);


    private SpriteRenderer _sr;
    private Color _baseColor = Color.white;

    public string StableId { get; private set; }

    public int NodeId => nodeId;

    public void Initialize(WorldMapGraphGenerator generator, WorldMapNodeSelection selection, int id)
    {
        _generator = generator;
        _selection = selection;
        nodeId = id;
        name = $"NodeView_{id}";

        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _baseColor = _sr.color;

        // 👇 NEW: primary node emphasis
        if (_generator != null && _generator.graph != null)
        {
            var node = _generator.graph.nodes[nodeId];
            if (node.isPrimary)
            {
                transform.localScale *= primaryScaleMultiplier;

                if (_sr != null)
                    _sr.color = primaryTint;
            }
        }
    }

    public void SetTint(Color c)
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) return;
        _sr.color = c;
    }

    public void ResetTint()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) return;
        _sr.color = _baseColor;
    }

}
