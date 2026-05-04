using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [SerializeField] private float resizeSnapIncrement = 0.5f;

    public event Action<ResizableSegment2D> SizeApplied;

    public bool SnapResizeInEditor => snapResizeInEditor;
    public float ResizeSnapIncrement => resizeSnapIncrement;

    public float Width => width;
    public float Height => height;
    public ResizeAxis Axis => resizeAxis;

    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public BoxCollider2D BoxCollider => boxCollider;

#if UNITY_EDITOR
    private bool _editorApplyQueued;
#endif

    public float SnapSize(float rawSize)
    {
        rawSize = Mathf.Max(0.01f, rawSize);

        if (!snapResizeInEditor)
            return rawSize;

        float inc = Mathf.Max(0.01f, resizeSnapIncrement);

        // Stabilize snapping for fractional increments like 0.5.
        // Adding a tiny epsilon reduces threshold jitter when dragging handles.
        float snapped = Mathf.Round((rawSize + 0.0001f) / inc) * inc;

        return Mathf.Max(inc, snapped);
    }

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

        SizeApplied?.Invoke(this);
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
        resizeSnapIncrement = Mathf.Max(0.01f, resizeSnapIncrement);

        if (!Application.isPlaying)
        {
            ResolveEditorRefsIfMissing();
            QueueEditorApplySize();
        }
    }

    private void ResolveEditorRefsIfMissing()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider2D>();
    }

    private void QueueEditorApplySize()
    {
        if (_editorApplyQueued)
            return;

        _editorApplyQueued = true;

        EditorApplication.delayCall += () =>
        {
            _editorApplyQueued = false;

            if (this == null)
                return;

            ResolveEditorRefsIfMissing();
            ApplySize(width, height);

            EditorUtility.SetDirty(this);

            if (gameObject != null)
                EditorUtility.SetDirty(gameObject);

            if (spriteRenderer != null)
                EditorUtility.SetDirty(spriteRenderer);

            if (boxCollider != null)
                EditorUtility.SetDirty(boxCollider);
        };
    }

    [ContextMenu("Sync Size From Collider")]
    private void EditorSyncSizeFromCollider()
    {
        SyncSizeFromCollider();
        ApplySize(width, height);

        EditorUtility.SetDirty(this);

        if (gameObject != null)
            EditorUtility.SetDirty(gameObject);
    }

    [ContextMenu("Normalize Scales And Apply")]
    private void EditorNormalizeScalesAndApply()
    {
        NormalizeScalesAndApply();

        EditorUtility.SetDirty(this);

        if (gameObject != null)
            EditorUtility.SetDirty(gameObject);
    }
#endif
}