using UnityEngine;

[DisallowMultipleComponent]
public sealed class FloorSegmentAuthoring : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoxCollider2D floorCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private ResizableSegment2D resizableSegment;

    [Header("Dimensions")]
    [Min(0.01f)]
    [SerializeField] private float width = 1f;

    [Min(0.01f)]
    [SerializeField] private float height = 1f;

    public BoxCollider2D FloorCollider => floorCollider;
    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public ResizableSegment2D ResizableSegment => resizableSegment;

    public float Width => resizableSegment != null ? resizableSegment.Width : width;
    public float Height => resizableSegment != null ? resizableSegment.Height : height;

    public float LocalLeftX => -Width * 0.5f;
    public float LocalRightX => Width * 0.5f;
    public float LocalBottomY => -Height * 0.5f;
    public float LocalTopY => Height * 0.5f;

    public Vector2 WorldLeftPoint => transform.TransformPoint(new Vector2(LocalLeftX, 0f));
    public Vector2 WorldRightPoint => transform.TransformPoint(new Vector2(LocalRightX, 0f));

    public void ApplyWidth(float newWidth)
    {
        newWidth = Mathf.Max(0.01f, newWidth);
        width = newWidth;

        if (resizableSegment != null)
        {
            resizableSegment.ApplyWidth(newWidth);
            return;
        }

        if (floorCollider != null)
        {
            Vector2 size = floorCollider.size;
            size.x = newWidth;
            floorCollider.size = size;
        }

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
            {
                Vector2 size = spriteRenderer.size;
                size.x = newWidth;
                spriteRenderer.size = size;
                spriteRenderer.transform.localScale = Vector3.one;
            }
            else
            {
                float baseSpriteWidth = spriteRenderer.sprite.bounds.size.x;
                if (baseSpriteWidth > 0.0001f)
                {
                    Vector3 scale = spriteRenderer.transform.localScale;
                    scale.x = newWidth / baseSpriteWidth;
                    spriteRenderer.transform.localScale = scale;
                }
            }
        }
    }

    public void SyncFromResizable()
    {
        if (resizableSegment == null)
            return;

        width = resizableSegment.Width;
        height = resizableSegment.Height;
    }

    public float WorldLeftX
    {
        get
        {
            if (floorCollider != null)
                return floorCollider.bounds.min.x;

            return transform.position.x - Width * 0.5f;
        }
    }

    public float WorldRightX
    {
        get
        {
            if (floorCollider != null)
                return floorCollider.bounds.max.x;

            return transform.position.x + Width * 0.5f;
        }
    }

    public float WorldCenterX
    {
        get
        {
            if (floorCollider != null)
                return floorCollider.bounds.center.x;

            return transform.position.x;
        }
    }

    public float WorldCenterY
    {
        get
        {
            if (floorCollider != null)
                return floorCollider.bounds.center.y;

            return transform.position.y;
        }
    }

    /// <summary>
    /// Moves the root transform so the collider's world center X lands on the requested value.
    /// This preserves collider offsets instead of pretending transform.position is the collider center.
    /// </summary>
    public void SetWorldCenterXPreservingColliderOffset(float targetCenterX)
    {
        float currentCenterX = WorldCenterX;
        float deltaX = targetCenterX - currentCenterX;

        Vector3 p = transform.position;
        p.x += deltaX;
        transform.position = p;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (floorCollider == null)
            floorCollider = GetComponent<BoxCollider2D>();

        if (floorCollider == null)
            floorCollider = GetComponentInChildren<BoxCollider2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (resizableSegment == null)
            resizableSegment = GetComponent<ResizableSegment2D>();

        SyncFromResizable();
    }

    private void OnValidate()
    {
        width = Mathf.Max(0.01f, width);
        height = Mathf.Max(0.01f, height);

        if (resizableSegment == null)
            resizableSegment = GetComponent<ResizableSegment2D>();

        if (resizableSegment != null)
            SyncFromResizable();
    }

    [ContextMenu("Sync From Resizable Segment")]
    private void EditorSyncFromResizable()
    {
        SyncFromResizable();
    }

    [ContextMenu("Copy Width/Height From Collider")]
    private void EditorCopyWidthHeightFromCollider()
    {
        if (floorCollider == null)
            floorCollider = GetComponent<BoxCollider2D>();

        if (floorCollider == null)
        {
            Debug.LogWarning("[FloorSegmentAuthoring] No BoxCollider2D found.", this);
            return;
        }

        width = Mathf.Max(0.01f, Mathf.Abs(floorCollider.size.x));
        height = Mathf.Max(0.01f, Mathf.Abs(floorCollider.size.y));

        if (resizableSegment != null)
            resizableSegment.ApplySize(width, height);

        Debug.Log($"[FloorSegmentAuthoring] Copied dimensions from collider. width={width:F3}, height={height:F3}", this);
    }

    [ContextMenu("Repair Span")]
    private void EditorRepairSpan()
    {
        if (!SpanRepairUtility.RepairFromSelectedFloor(this))
            Debug.LogWarning("[FloorSegmentAuthoring] Repair Span failed or found nothing to repair.", this);
    }
#endif
}