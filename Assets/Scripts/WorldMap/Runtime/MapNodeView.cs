using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MapNodeView : MonoBehaviour
{
    [SerializeField] private int nodeId = -1;

    private WorldMapGraphGenerator _generator;
    private WorldMapNodeSelection _selection;

    private SpriteRenderer _sr;
    private Color _baseColor = Color.white;

    public int NodeId => nodeId;

    public void Initialize(WorldMapGraphGenerator generator, WorldMapNodeSelection selection, int id)
    {
        _generator = generator;
        _selection = selection;
        nodeId = id;
        name = $"NodeView_{id}";

        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _baseColor = _sr.color;
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
