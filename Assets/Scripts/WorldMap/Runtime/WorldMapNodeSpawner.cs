using System.Collections.Generic;
using UnityEngine;

public class WorldMapNodeSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapNodeSelection selection;

    [Header("Spawn")]
    [SerializeField] private Transform container;
    [SerializeField, Min(0.05f)] private float nodeRadiusWorld = 0.25f;
    [SerializeField] private bool spawnOnStart = true;

    private readonly List<GameObject> _spawned = new();

    private void Reset()
    {
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        selection = FindAnyObjectByType<WorldMapNodeSelection>();
        container = transform;
    }

    private void OnEnable()
    {
        if (generator != null)
            generator.OnGraphGenerated += Respawn;
    }

    private void OnDisable()
    {
        if (generator != null)
            generator.OnGraphGenerated -= Respawn;
    }

    private void Start()
    {
        if (!spawnOnStart) return;
        Respawn();
    }

    [ContextMenu("Respawn Node Views")]
    public void Respawn()
    {
        Clear();

        if (generator == null || generator.graph == null) return;

        if (container == null) container = transform;

        // Ensure selection knows where to find nodes
        if (selection != null) selection.Bind(generator);

        for (int i = 0; i < generator.graph.nodes.Count; i++)
        {
            var n = generator.graph.nodes[i];

            var go = new GameObject($"NodeView_{i}");
            go.transform.SetParent(container, worldPositionStays: true);
            go.transform.position = ToWorld(n.position);
            go.layer = LayerMask.NameToLayer("MapNodes");

            // Clickable collider
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = nodeRadiusWorld;

            // Optional: make it visible in Game view (cheap sprite dot)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeDotSprite();
            sr.sortingOrder = 1000;
            sr.transform.localScale = Vector3.one * (nodeRadiusWorld * 2f);

            var view = go.AddComponent<MapNodeView>();
            view.Initialize(generator, selection, i);

            var viz = go.AddComponent<NodeBuffVisualizer>();
            viz.Initialize(generator, view);
            viz.Refresh(); // immediate

            _spawned.Add(go);
        }
    }

    private void Clear()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i]);
        }
        _spawned.Clear();
    }

    private Vector3 ToWorld(Vector2 p)
        => generator != null ? generator.transform.TransformPoint(new Vector3(p.x, p.y, 0f)) : new Vector3(p.x, p.y, 0f);

    // --- Tiny runtime sprite (white dot). No assets required.
    private static Sprite _dotSprite;
    private static Sprite MakeDotSprite()
    {
        if (_dotSprite != null) return _dotSprite;

        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var c = new Color32(255, 255, 255, 255);
        var clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float dx = (x - 7.5f) / 7.5f;
                float dy = (y - 7.5f) / 7.5f;
                float d = dx * dx + dy * dy;
                tex.SetPixel(x, y, d <= 1f ? c : clear);
            }

        tex.Apply();
        _dotSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
        return _dotSprite;
    }
}
