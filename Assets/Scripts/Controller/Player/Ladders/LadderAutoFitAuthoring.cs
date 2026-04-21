using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LadderAutoFitAuthoring : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ResizableSegment2D resizable;
    [SerializeField] private BoxCollider2D ladderCollider;

    [Header("Marker Transforms")]
    [SerializeField] private Transform top;
    [SerializeField] private Transform middle;
    [SerializeField] private Transform bottom;

    [Header("Optional Exit Points")]
    [SerializeField] private Transform topExitPoint;
    [SerializeField] private Transform bottomExitPoint;

    [Header("Offsets")]
    [SerializeField] private float topInset = 0.05f;
    [SerializeField] private float bottomInset = 0.05f;

    [Tooltip("How far above the ladder top the top exit point should sit.")]
    [SerializeField] private Vector2 topExitLocalOffset = new Vector2(0f, 0.35f);

    [Tooltip("How far below the ladder bottom the bottom exit point should sit.")]
    [SerializeField] private Vector2 bottomExitLocalOffset = new Vector2(0f, -0.15f);

    [Header("Editor")]
    [SerializeField] private bool autoApplyInEditor = true;

    private void Reset()
    {
        ResolveRefs();
        Apply();
    }

    private void OnEnable()
    {
        ResolveRefs();
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveRefs();

        if (!Application.isPlaying && autoApplyInEditor)
            Apply();
    }
#endif

    [ContextMenu("Apply Ladder Marker Layout")]
    public void Apply()
    {
        ResolveRefs();

        float height = ResolveHeight();
        float centerY = ResolveCenterY();

        float half = height * 0.5f;

        float topY = centerY + half - topInset;
        float bottomY = centerY - half + bottomInset;
        float middleY = centerY;

        SetLocalY(top, topY);
        SetLocalY(middle, middleY);
        SetLocalY(bottom, bottomY);

        if (topExitPoint != null)
            topExitPoint.localPosition = new Vector3(
                topExitLocalOffset.x,
                topY + topExitLocalOffset.y,
                topExitPoint.localPosition.z);

        if (bottomExitPoint != null)
            bottomExitPoint.localPosition = new Vector3(
                bottomExitLocalOffset.x,
                bottomY + bottomExitLocalOffset.y,
                bottomExitPoint.localPosition.z);
    }

    private void ResolveRefs()
    {
        if (resizable == null)
            resizable = GetComponent<ResizableSegment2D>();

        if (ladderCollider == null)
            ladderCollider = GetComponent<BoxCollider2D>();

        if (top == null)
            top = transform.Find("Top");

        if (middle == null)
            middle = transform.Find("Middle");

        if (bottom == null)
            bottom = transform.Find("Bottom");

        if (topExitPoint == null)
            topExitPoint = transform.Find("TopExitPoint");

        if (bottomExitPoint == null)
            bottomExitPoint = transform.Find("BottomExitPoint");
    }

    private float ResolveHeight()
    {
        if (resizable != null)
            return Mathf.Max(0.01f, resizable.Height);

        if (ladderCollider != null)
            return Mathf.Max(0.01f, ladderCollider.size.y);

        return 1f;
    }

    private float ResolveCenterY()
    {
        if (ladderCollider != null)
            return ladderCollider.offset.y;

        return 0f;
    }

    private static void SetLocalY(Transform target, float y)
    {
        if (target == null)
            return;

        Vector3 p = target.localPosition;
        p.y = y;
        target.localPosition = p;
    }
}