using UnityEngine;

[DisallowMultipleComponent]
public sealed class SplitSpanRecord : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string spanId = "";

    [Header("Original Span (Parent-Local Space)")]
    [SerializeField] private float originalLocalStartX;
    [SerializeField] private float originalLocalEndX;
    [SerializeField] private float originalLocalCenterY;

    [Header("Fragment Info")]
    [SerializeField] private bool isSplitFragment = false;
    [SerializeField] private int splitDepth = 0;

    [Header("Debug / Optional")]
    [SerializeField] private string sourceKind = "Floor";
    [SerializeField] private Transform spanRoot;

    public string SpanId => spanId;
    public float OriginalLocalStartX => originalLocalStartX;
    public float OriginalLocalEndX => originalLocalEndX;
    public float OriginalLocalCenterY => originalLocalCenterY;
    public bool IsSplitFragment => isSplitFragment;
    public int SplitDepth => splitDepth;
    public string SourceKind => sourceKind;
    public Transform SpanRoot => spanRoot != null ? spanRoot : transform.parent;

    public void InitializeNewRootRecord(
        string newSpanId,
        Transform root,
        float localStartX,
        float localEndX,
        float localCenterY,
        string kind)
    {
        spanId = newSpanId;
        spanRoot = root;
        originalLocalStartX = Mathf.Min(localStartX, localEndX);
        originalLocalEndX = Mathf.Max(localStartX, localEndX);
        originalLocalCenterY = localCenterY;
        isSplitFragment = false;
        splitDepth = 0;
        sourceKind = string.IsNullOrWhiteSpace(kind) ? "Floor" : kind;
    }

    public void InitializeFromExistingRecord(
        SplitSpanRecord source,
        bool markAsSplitFragment,
        int newSplitDepth)
    {
        if (source == null)
            return;

        spanId = source.spanId;
        spanRoot = source.spanRoot;
        originalLocalStartX = source.originalLocalStartX;
        originalLocalEndX = source.originalLocalEndX;
        originalLocalCenterY = source.originalLocalCenterY;
        isSplitFragment = markAsSplitFragment;
        splitDepth = Mathf.Max(0, newSplitDepth);
        sourceKind = source.sourceKind;
    }

    public void MarkAsSplitFragment(int newSplitDepth)
    {
        isSplitFragment = true;
        splitDepth = Mathf.Max(0, newSplitDepth);
    }

    public Vector2 GetOriginalWorldSpan()
    {
        Transform root = SpanRoot;
        if (root == null)
            root = transform.parent;

        if (root == null)
            return new Vector2(originalLocalStartX, originalLocalEndX);

        Vector3 left = root.TransformPoint(new Vector3(originalLocalStartX, 0f, 0f));
        Vector3 right = root.TransformPoint(new Vector3(originalLocalEndX, 0f, 0f));
        return new Vector2(left.x, right.x);
    }

    public bool TryGetRoot(out Transform root)
    {
        root = SpanRoot;
        return root != null;
    }

#if UNITY_EDITOR
    [ContextMenu("Log Original World Span")]
    private void EditorLogOriginalWorldSpan()
    {
        Vector2 span = GetOriginalWorldSpan();
        Debug.Log(
            $"[SplitSpanRecord:{name}] spanId={spanId} worldSpan=[{span.x:F3}, {span.y:F3}] splitDepth={splitDepth}",
            this);
    }
#endif
}