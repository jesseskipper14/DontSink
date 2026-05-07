using UnityEngine;

[DisallowMultipleComponent]
public sealed class WallRepairBlocker : MonoBehaviour
{
    [SerializeField] private string spanId = "";
    [SerializeField] private Transform spanRoot;
    [SerializeField] private float localBottomY;
    [SerializeField] private float localTopY;
    [SerializeField] private string blockerKind = "Door";

    public string SpanId => spanId;
    public Transform SpanRoot => spanRoot;
    public float LocalBottomY => localBottomY;
    public float LocalTopY => localTopY;
    public string BlockerKind => blockerKind;

    public void Initialize(
        string newSpanId,
        Transform root,
        float bottomY,
        float topY,
        string kind)
    {
        spanId = newSpanId;
        spanRoot = root;
        localBottomY = Mathf.Min(bottomY, topY);
        localTopY = Mathf.Max(bottomY, topY);
        blockerKind = string.IsNullOrWhiteSpace(kind) ? "Door" : kind;
    }
}