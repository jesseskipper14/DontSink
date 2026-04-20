using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResizableSegment2D : MonoBehaviour
{
    public enum ResizeAxis
    {
        Horizontal,
        Vertical,
        Both
    }

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BoxCollider2D boxCollider;

    [Header("Size")]
    [Min(0.01f)]
    [SerializeField] private float width = 1f;

    [Min(0.01f)]
    [SerializeField] private float height = 1f;

    [Header("Behavior")]
    [SerializeField] private ResizeAxis resizeAxis = ResizeAxis.Horizontal;
    [SerializeField] private bool resizeSprite = true;
    [SerializeField] private bool resizeCollider = true;
    [SerializeField] private bool forceTiledSprite = true;

    [Header("Editor Resize Snapping")]
    [SerializeField] private bool snapResizeInEditor = true;

    [Min(0.01f)]
    [SerializeField] private float resizeSnapIncrement = 1f;

    public bool SnapResizeInEditor => snapResizeInEditor;
    public float ResizeSnapIncrement => resizeSnapIncrement;

    public float SnapSize(float rawSize)
    {
        rawSize = Mathf.Max(0.01f, rawSize);

        if (!snapResizeInEditor)
            return rawSize;

        float inc = Mathf.Max(0.01f, resizeSnapIncrement);
        return Mathf.Max(inc, Mathf.Round(rawSize / inc) * inc);
    }

    public float Width => width;
    public float Height => height;
    public ResizeAxis Axis => resizeAxis;

    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public BoxCollider2D BoxCollider => boxCollider;

    private void Reset()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider2D>();

        SyncSizeFromCollider();
    }

    private void Awake()
    {
        ApplySize(width, height);
    }

    public void ApplyWidth(float newWidth)
    {
        ApplySize(newWidth, height);
    }

    public void ApplyHeight(float newHeight)
    {
        ApplySize(width, newHeight);
    }

    public void ApplySize(float newWidth, float newHeight)
    {
        width = Mathf.Max(0.01f, newWidth);
        height = Mathf.Max(0.01f, newHeight);

        if (resizeCollider && boxCollider != null)
        {
            Vector2 size = boxCollider.size;

            if (resizeAxis == ResizeAxis.Horizontal || resizeAxis == ResizeAxis.Both)
                size.x = width;

            if (resizeAxis == ResizeAxis.Vertical || resizeAxis == ResizeAxis.Both)
                size.y = height;

            boxCollider.size = size;
        }

        if (resizeSprite && spriteRenderer != null)
        {
            if (forceTiledSprite && spriteRenderer.drawMode == SpriteDrawMode.Simple)
                spriteRenderer.drawMode = SpriteDrawMode.Tiled;

            if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
            {
                Vector2 size = spriteRenderer.size;

                if (resizeAxis == ResizeAxis.Horizontal || resizeAxis == ResizeAxis.Both)
                    size.x = width;

                if (resizeAxis == ResizeAxis.Vertical || resizeAxis == ResizeAxis.Both)
                    size.y = height;

                spriteRenderer.size = size;
                spriteRenderer.transform.localScale = Vector3.one;
            }
            else if (spriteRenderer.sprite != null)
            {
                Vector2 baseSize = spriteRenderer.sprite.bounds.size;
                Vector3 scale = spriteRenderer.transform.localScale;

                if ((resizeAxis == ResizeAxis.Horizontal || resizeAxis == ResizeAxis.Both) && baseSize.x > 0.0001f)
                    scale.x = width / baseSize.x;

                if ((resizeAxis == ResizeAxis.Vertical || resizeAxis == ResizeAxis.Both) && baseSize.y > 0.0001f)
                    scale.y = height / baseSize.y;

                spriteRenderer.transform.localScale = scale;
            }
        }
    }

    public void SyncSizeFromCollider()
    {
        if (boxCollider == null)
            return;

        width = Mathf.Max(0.01f, Mathf.Abs(boxCollider.size.x));
        height = Mathf.Max(0.01f, Mathf.Abs(boxCollider.size.y));
    }

    public void NormalizeScalesAndApply()
    {
        transform.localScale = Vector3.one;

        if (spriteRenderer != null)
            spriteRenderer.transform.localScale = Vector3.one;

        ApplySize(width, height);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        width = Mathf.Max(0.01f, width);
        height = Mathf.Max(0.01f, height);

        if (!Application.isPlaying)
            ApplySize(width, height);
    }

    [ContextMenu("Sync Size From Collider")]
    private void EditorSyncSizeFromCollider()
    {
        SyncSizeFromCollider();
        ApplySize(width, height);
    }

    [ContextMenu("Normalize Scales And Apply")]
    private void EditorNormalizeScalesAndApply()
    {
        NormalizeScalesAndApply();
    }
#endif
}