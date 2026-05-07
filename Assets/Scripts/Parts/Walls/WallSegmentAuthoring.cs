using UnityEngine;

[DisallowMultipleComponent]
public sealed class WallSegmentAuthoring : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoxCollider2D wallCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private ResizableSegment2D resizableSegment;

    [Header("Dimensions")]
    [Min(0.01f)]
    [SerializeField] private float width = 1f;

    [Min(0.01f)]
    [SerializeField] private float height = 1f;

    public BoxCollider2D WallCollider => wallCollider;
    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public ResizableSegment2D ResizableSegment => resizableSegment;

    public float Width => resizableSegment != null ? resizableSegment.Width : width;
    public float Height => resizableSegment != null ? resizableSegment.Height : height;

    public float WorldBottomY
    {
        get
        {
            if (wallCollider != null)
                return wallCollider.bounds.min.y;

            return transform.position.y - Height * 0.5f;
        }
    }

    public float WorldTopY
    {
        get
        {
            if (wallCollider != null)
                return wallCollider.bounds.max.y;

            return transform.position.y + Height * 0.5f;
        }
    }

    public float WorldCenterY
    {
        get
        {
            if (wallCollider != null)
                return wallCollider.bounds.center.y;

            return transform.position.y;
        }
    }

    public float WorldCenterX
    {
        get
        {
            if (wallCollider != null)
                return wallCollider.bounds.center.x;

            return transform.position.x;
        }
    }

    public void ResolveRefs()
    {
        if (wallCollider == null)
            wallCollider = GetComponent<BoxCollider2D>();

        if (wallCollider == null)
            wallCollider = GetComponentInChildren<BoxCollider2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (resizableSegment == null)
            resizableSegment = GetComponent<ResizableSegment2D>();
    }

    public void ApplyHeight(float newHeight)
    {
        newHeight = Mathf.Max(0.01f, newHeight);
        height = newHeight;

        if (resizableSegment != null)
        {
            resizableSegment.ApplyHeight(newHeight);
            SyncFromResizable();
            return;
        }

        if (wallCollider != null)
        {
            Vector2 size = wallCollider.size;
            size.y = newHeight;
            wallCollider.size = size;
        }

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
            {
                Vector2 size = spriteRenderer.size;
                size.y = newHeight;
                spriteRenderer.size = size;
                spriteRenderer.transform.localScale = Vector3.one;
            }
            else
            {
                float baseSpriteHeight = spriteRenderer.sprite.bounds.size.y;
                if (baseSpriteHeight > 0.0001f)
                {
                    Vector3 scale = spriteRenderer.transform.localScale;
                    scale.y = newHeight / baseSpriteHeight;
                    spriteRenderer.transform.localScale = scale;
                }
            }
        }
    }

    public void SetWorldCenterYPreservingColliderOffset(float targetCenterY)
    {
        float currentCenterY = WorldCenterY;
        float deltaY = targetCenterY - currentCenterY;

        Vector3 p = transform.position;
        p.y += deltaY;
        transform.position = p;
    }

    public void SyncFromResizable()
    {
        if (resizableSegment == null)
            return;

        width = resizableSegment.Width;
        height = resizableSegment.Height;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveRefs();
        SyncFromResizable();
    }

    private void OnValidate()
    {
        width = Mathf.Max(0.01f, width);
        height = Mathf.Max(0.01f, height);

        ResolveRefs();

        if (resizableSegment != null)
            SyncFromResizable();
    }

    [ContextMenu("Sync From Resizable Segment")]
    private void EditorSyncFromResizable()
    {
        ResolveRefs();
        SyncFromResizable();
    }

    [ContextMenu("Copy Width/Height From Collider")]
    private void EditorCopyWidthHeightFromCollider()
    {
        ResolveRefs();

        if (wallCollider == null)
        {
            Debug.LogWarning("[WallSegmentAuthoring] No BoxCollider2D found.", this);
            return;
        }

        width = Mathf.Max(0.01f, Mathf.Abs(wallCollider.size.x));
        height = Mathf.Max(0.01f, Mathf.Abs(wallCollider.size.y));

        if (resizableSegment != null)
            resizableSegment.ApplySize(width, height);
    }

    [ContextMenu("Repair Wall Span")]
    private void EditorRepairWallSpan()
    {
        if (!WallRepairUtility.RepairFromSelectedWall(this))
            Debug.LogWarning("[WallSegmentAuthoring] Repair Wall Span failed or found nothing to repair.", this);
    }
#endif
}