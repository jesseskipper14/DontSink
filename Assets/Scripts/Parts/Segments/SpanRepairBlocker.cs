using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpanRepairBlocker : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string spanId = "";

    [Header("Blocker Span (Parent-Local Space)")]
    [SerializeField] private float localStartX;
    [SerializeField] private float localEndX;

    [Header("Debug / Optional")]
    [SerializeField] private string blockerKind = "Opening";
    [SerializeField] private Transform spanRoot;

    public string SpanId => spanId;
    public float LocalStartX => localStartX;
    public float LocalEndX => localEndX;
    public string BlockerKind => blockerKind;
    public Transform SpanRoot => spanRoot != null ? spanRoot : transform.parent;

    public void Initialize(
        string ownerSpanId,
        Transform root,
        float startLocalX,
        float endLocalX,
        string kind)
    {
        spanId = ownerSpanId;
        spanRoot = root;
        localStartX = Mathf.Min(startLocalX, endLocalX);
        localEndX = Mathf.Max(startLocalX, endLocalX);
        blockerKind = string.IsNullOrWhiteSpace(kind) ? "Opening" : kind;
    }

    public Vector2 GetWorldSpan()
    {
        Transform root = SpanRoot;
        if (root == null)
            root = transform.parent;

        if (root == null)
            return new Vector2(localStartX, localEndX);

        Vector3 left = root.TransformPoint(new Vector3(localStartX, 0f, 0f));
        Vector3 right = root.TransformPoint(new Vector3(localEndX, 0f, 0f));
        return new Vector2(left.x, right.x);
    }

#if UNITY_EDITOR
    [ContextMenu("Log Blocker World Span")]
    private void EditorLogWorldSpan()
    {
        Vector2 span = GetWorldSpan();
        Debug.Log(
            $"[SpanRepairBlocker:{name}] spanId={spanId} worldSpan=[{span.x:F3}, {span.y:F3}] kind={blockerKind}",
            this);
    }
#endif
}