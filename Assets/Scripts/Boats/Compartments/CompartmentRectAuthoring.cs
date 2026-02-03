using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Authoring helper for rectangular compartments.
/// Keeps dimensions integer, axis-aligned, and updates attached components.
/// Intended to be saved inside Boat prefabs (persistent authoring).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CompartmentRectAuthoring : MonoBehaviour
{
    [Header("Rect Size (whole numbers)")]
    [Min(1)] public int width = 4;
    [Min(1)] public int height = 2;

    [Header("Grid")]
    [Min(0.1f)] public float cellSize = 1f;

    [Header("Center Offset (in cells)")]
    public Vector2Int centerOffsetCells = Vector2Int.zero;

    [Header("Auto components")]
    public bool ensureBoxCollider2D = true;
    public bool colliderIsTrigger = true;

    // Cache
    private int _lastW, _lastH;
    private float _lastCell;
    private Vector2Int _lastOffset;

    void OnEnable() => Apply();
    void OnValidate() => Apply();
    void Update()
    {
        // ExecuteAlways can get spammy. Only reapply if values changed.
        if (_lastW != width || _lastH != height || !Mathf.Approximately(_lastCell, cellSize) || _lastOffset != centerOffsetCells)
            Apply();
    }

    private void Apply()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.1f, cellSize);

        _lastW = width;
        _lastH = height;
        _lastCell = cellSize;
        _lastOffset = centerOffsetCells;

        // Rect in world units
        Vector2 size = new Vector2(width * cellSize, height * cellSize);
        Vector2 offset = new Vector2(centerOffsetCells.x * cellSize, centerOffsetCells.y * cellSize);

        // Collider for selection/debug
        if (ensureBoxCollider2D)
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = colliderIsTrigger;
            box.size = size;
            box.offset = offset;
        }

        // If you have a Compartment component, keep it in sync (best-effort).
        // I don't know your exact API, so this is intentionally conservative:
        var comp = GetComponent<Compartment>();
        if (comp != null)
        {
            // Common patterns:
            // - comp.Width/Height fields
            // - comp.Bounds/Rect
            // - polygon vertices
            //
            // If your Compartment has no such public fields, we’ll add a tiny method on Compartment
            // like SetRectWorld(size, offset) and call it here.

            // Example if Compartment has public width/height in cells:
            // comp.Width = width;
            // comp.Height = height;

            // Leave as-is for now until we wire to your real Compartment API.
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 center = new Vector3(centerOffsetCells.x * cellSize, centerOffsetCells.y * cellSize, 0f);
        Vector3 size = new Vector3(width * cellSize, height * cellSize, 0f);

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.25f);
        Gizmos.DrawCube(center, size);

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
