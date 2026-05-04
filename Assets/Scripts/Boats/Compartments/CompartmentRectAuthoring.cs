using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Authoring helper for rectangular compartments.
/// Keeps visual/collider/simulation geometry synchronized.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CompartmentRectAuthoring : MonoBehaviour
{
    [Header("Rect Size (whole cells)")]
    [Min(1)] public int width = 4;
    [Min(1)] public int height = 2;

    [Header("Grid")]
    [Min(0.1f)] public float cellSize = 0.5f;

    [Header("Center Offset (in cells)")]
    public Vector2Int centerOffsetCells = Vector2Int.zero;

    [Header("Refs")]
    [SerializeField] private Compartment compartment;
    [SerializeField] private BoxCollider2D boxCollider;
    [SerializeField] private ResizableSegment2D resizableSegment;

    [Header("Auto components")]
    public bool ensureBoxCollider2D = true;
    public bool colliderIsTrigger = true;

    [Header("Resizable Segment Sync")]
    [Tooltip("If true, scene resize handles on ResizableSegment2D can update this authoring size.")]
    [SerializeField] private bool allowResizableSegmentToDriveSize = true;

    [Tooltip("Preserves current water fill percentage when compartment dimensions change.")]
    [SerializeField] private bool preserveWaterFractionOnResize = true;

    private int _lastW;
    private int _lastH;
    private float _lastCell;
    private Vector2Int _lastOffset;
    private float _lastSegmentWidth;
    private float _lastSegmentHeight;

    private bool _applying;

#if UNITY_EDITOR
    private bool _editorApplyQueued;
#endif

    public Vector2 WorldUnitSize => new Vector2(width * cellSize, height * cellSize);
    public Vector2 LocalOffset => new Vector2(centerOffsetCells.x * cellSize, centerOffsetCells.y * cellSize);

    private void Reset()
    {
        ResolveRefs();
        PullSizeFromResizableIfAvailable();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            QueueEditorApply();
        else
            Apply();
#else
        Apply();
#endif
    }

    private void OnEnable()
    {
        ResolveRefs();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            QueueEditorApply();
        else
            Apply();
#else
        Apply();
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.1f, cellSize);

        ResolveRefs();

        if (!Application.isPlaying)
            QueueEditorApply();
        else
            Apply();
    }

    private void QueueEditorApply()
    {
        if (_editorApplyQueued)
            return;

        _editorApplyQueued = true;

        EditorApplication.delayCall += () =>
        {
            _editorApplyQueued = false;

            if (this == null)
                return;

            Apply();

            EditorUtility.SetDirty(this);

            if (gameObject != null)
                EditorUtility.SetDirty(gameObject);
        };
    }
#endif

    private void Update()
    {
        if (_applying)
            return;

        ResolveRefs();

        if (allowResizableSegmentToDriveSize && resizableSegment != null)
        {
            bool segmentChanged =
                !Mathf.Approximately(_lastSegmentWidth, resizableSegment.Width) ||
                !Mathf.Approximately(_lastSegmentHeight, resizableSegment.Height);

            if (segmentChanged)
            {
                PullSizeFromResizableIfAvailable();
                Apply();
                return;
            }
        }

        bool authoringChanged =
            _lastW != width ||
            _lastH != height ||
            !Mathf.Approximately(_lastCell, cellSize) ||
            _lastOffset != centerOffsetCells;

        if (authoringChanged)
            Apply();
    }

    private void ResolveRefs()
    {
        if (compartment == null)
            compartment = GetComponent<Compartment>();

        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider2D>();

        if (resizableSegment == null)
            resizableSegment = GetComponent<ResizableSegment2D>();
    }

    private void PullSizeFromResizableIfAvailable()
    {
        if (resizableSegment == null)
            return;

        float safeCell = Mathf.Max(0.1f, cellSize);

        width = Mathf.Max(1, Mathf.RoundToInt(resizableSegment.Width / safeCell));
        height = Mathf.Max(1, Mathf.RoundToInt(resizableSegment.Height / safeCell));
    }

    public void Apply()
    {
        if (_applying)
            return;

        _applying = true;

        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.1f, cellSize);

        ResolveRefs();

        Vector2 size = WorldUnitSize;
        Vector2 offset = LocalOffset;

        if (ensureBoxCollider2D)
        {
            if (boxCollider == null)
                boxCollider = GetComponent<BoxCollider2D>();

            if (boxCollider == null)
                boxCollider = gameObject.AddComponent<BoxCollider2D>();

            boxCollider.isTrigger = colliderIsTrigger;
            boxCollider.size = size;
            boxCollider.offset = offset;
        }

        if (resizableSegment != null)
        {
            resizableSegment.ApplySize(size.x, size.y);
        }

        if (compartment != null)
        {
            compartment.SetLocalRect(size, offset, preserveWaterFractionOnResize);
        }

        _lastW = width;
        _lastH = height;
        _lastCell = cellSize;
        _lastOffset = centerOffsetCells;

        if (resizableSegment != null)
        {
            _lastSegmentWidth = resizableSegment.Width;
            _lastSegmentHeight = resizableSegment.Height;
        }
        else
        {
            _lastSegmentWidth = size.x;
            _lastSegmentHeight = size.y;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (compartment != null)
                EditorUtility.SetDirty(compartment);

            if (boxCollider != null)
                EditorUtility.SetDirty(boxCollider);

            if (resizableSegment != null)
                EditorUtility.SetDirty(resizableSegment);

            EditorUtility.SetDirty(this);
        }
#endif

        _applying = false;
    }

#if UNITY_EDITOR
    [ContextMenu("Apply Rect To Compartment")]
    private void EditorApply()
    {
        Apply();
    }

    [ContextMenu("Pull Size From Resizable Segment")]
    private void EditorPullSizeFromResizable()
    {
        ResolveRefs();
        PullSizeFromResizableIfAvailable();
        Apply();
    }

    private void OnDrawGizmos()
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