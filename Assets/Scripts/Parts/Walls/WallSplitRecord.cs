using UnityEngine;

[DisallowMultipleComponent]
public sealed class WallSplitRecord : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string spanId = "";

    [Header("Original Wall Span (Parent-Local Space)")]
    [SerializeField] private float originalLocalBottomY;
    [SerializeField] private float originalLocalTopY;
    [SerializeField] private float originalLocalCenterX;

    [Header("Fragment Info")]
    [SerializeField] private bool isSplitFragment = false;
    [SerializeField] private int splitDepth = 0;

    [Header("Debug / Optional")]
    [SerializeField] private string sourceKind = "Wall";
    [SerializeField] private Transform spanRoot;

    public string SpanId => spanId;
    public float OriginalLocalBottomY => originalLocalBottomY;
    public float OriginalLocalTopY => originalLocalTopY;
    public float OriginalLocalCenterX => originalLocalCenterX;
    public bool IsSplitFragment => isSplitFragment;
    public int SplitDepth => splitDepth;
    public string SourceKind => sourceKind;
    public Transform SpanRoot => spanRoot != null ? spanRoot : transform.parent;

    public void InitializeNewRootRecord(
        string newSpanId,
        Transform root,
        float localBottomY,
        float localTopY,
        float localCenterX,
        string kind)
    {
        spanId = newSpanId;
        spanRoot = root;
        originalLocalBottomY = Mathf.Min(localBottomY, localTopY);
        originalLocalTopY = Mathf.Max(localBottomY, localTopY);
        originalLocalCenterX = localCenterX;
        isSplitFragment = false;
        splitDepth = 0;
        sourceKind = string.IsNullOrWhiteSpace(kind) ? "Wall" : kind;
    }

    public void InitializeFromExistingRecord(
        WallSplitRecord source,
        bool markAsSplitFragment,
        int newSplitDepth)
    {
        if (source == null)
            return;

        spanId = source.spanId;
        spanRoot = source.spanRoot;
        originalLocalBottomY = source.originalLocalBottomY;
        originalLocalTopY = source.originalLocalTopY;
        originalLocalCenterX = source.originalLocalCenterX;
        isSplitFragment = markAsSplitFragment;
        splitDepth = Mathf.Max(0, newSplitDepth);
        sourceKind = source.sourceKind;
    }

    public bool TryGetRoot(out Transform root)
    {
        root = SpanRoot;
        return root != null;
    }
}