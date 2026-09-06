using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class HatchLedge : MonoBehaviour
{
    [Header("One-Way Platform")]
    [SerializeField] private bool configureOneWayPlatformOnReset = true;

    [SerializeField, Range(1f, 360f)]
    private float surfaceArc = 160f;

    [Tooltip("Keep the one-way blocking face opposite world gravity instead of rotating with the boat. " +
             "This lets an open hatch remain passable from below and supportive from above even when the boat capsizes.")]
    [SerializeField] private bool orientOneWaySurfaceAgainstGravity = true;

    [Header("Optional Connection Collider Sync")]
    [SerializeField] private bool autoSyncConnectionOpeningCollider = true;
    [SerializeField] private ResizableSegment2D resizableSegment;
    [SerializeField] private BoxCollider2D connectionOpeningCollider;
    [SerializeField] private float openingWidthPadding = 0f;
    [SerializeField] private float openingHeight = 0.5f;
    [SerializeField] private float openingVerticalOffset = 0f;

    private Collider2D _collider;
    private PlatformEffector2D _effector;

    public Collider2D Collider
    {
        get
        {
            if (_collider == null)
                _collider = GetComponent<Collider2D>();

            return _collider;
        }
    }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _effector = GetComponent<PlatformEffector2D>();

        if (resizableSegment == null)
            resizableSegment = GetComponent<ResizableSegment2D>();

        SubscribeToResize();
        SyncConnectionOpeningCollider();
        SyncOneWaySurfaceOrientation();
    }

    private void OnEnable()
    {
        if (_collider == null)
            _collider = GetComponent<Collider2D>();

        if (_effector == null)
            _effector = GetComponent<PlatformEffector2D>();

        if (resizableSegment == null)
            resizableSegment = GetComponent<ResizableSegment2D>();

        SubscribeToResize();
        SyncConnectionOpeningCollider();
        SyncOneWaySurfaceOrientation();
    }

    private void FixedUpdate()
    {
        SyncOneWaySurfaceOrientation();
    }

    private void OnDisable()
    {
        UnsubscribeFromResize();
    }

    private void SubscribeToResize()
    {
        if (resizableSegment != null)
            resizableSegment.SizeApplied -= HandleResizableSegmentSizeApplied;

        if (resizableSegment != null)
            resizableSegment.SizeApplied += HandleResizableSegmentSizeApplied;
    }

    private void UnsubscribeFromResize()
    {
        if (resizableSegment != null)
            resizableSegment.SizeApplied -= HandleResizableSegmentSizeApplied;
    }

    private void HandleResizableSegmentSizeApplied(ResizableSegment2D _)
    {
        SyncConnectionOpeningCollider();
    }

    private void SyncOneWaySurfaceOrientation()
    {
        if (!orientOneWaySurfaceAgainstGravity)
            return;

        if (_effector == null)
            _effector = GetComponent<PlatformEffector2D>();

        if (_effector == null || !_effector.useOneWay)
            return;

        Vector2 gravity = Physics2D.gravity;
        Vector2 desiredSurfaceNormal =
            gravity.sqrMagnitude > 0.000001f
                ? -gravity.normalized
                : Vector2.up;

        Vector2 authoredSurfaceNormal = transform.up;
        float desiredOffset =
            Vector2.SignedAngle(
                authoredSurfaceNormal,
                desiredSurfaceNormal);

        if (Mathf.Abs(
                Mathf.DeltaAngle(
                    _effector.rotationalOffset,
                    desiredOffset)) >
            0.01f)
        {
            _effector.rotationalOffset = desiredOffset;
        }
    }

    public void SyncConnectionOpeningCollider()
    {
        if (!autoSyncConnectionOpeningCollider)
            return;

        if (connectionOpeningCollider == null)
            return;

        BoxCollider2D sourceBox = ResolveSourceBoxCollider();
        if (sourceBox == null)
        {
            Debug.LogWarning(
                $"[HatchLedge] Cannot sync connection opening collider on '{name}' because no source BoxCollider2D was found.",
                this);
            return;
        }

        // Convert source box center into this transform's local space.
        Vector3 sourceWorldCenter = sourceBox.transform.TransformPoint(sourceBox.offset);
        Vector3 localCenter = transform.InverseTransformPoint(sourceWorldCenter);

        float sourceWorldWidth = Mathf.Abs(sourceBox.size.x * sourceBox.transform.lossyScale.x);
        float targetLocalWidth = Mathf.Max(0.01f, sourceWorldWidth + openingWidthPadding);

        // Convert the desired opening height from world-ish intent into local size on this transform.
        // Assuming no weird scale games here, which is a dangerous assumption in Unity but acceptable for now.
        float targetLocalHeight = Mathf.Max(0.01f, openingHeight);

        connectionOpeningCollider.offset = new Vector2(
            localCenter.x,
            localCenter.y + openingVerticalOffset);

        connectionOpeningCollider.size = new Vector2(
            targetLocalWidth,
            targetLocalHeight);
    }

    private BoxCollider2D ResolveSourceBoxCollider()
    {
        if (_collider is BoxCollider2D selfBox)
            return selfBox;

        if (resizableSegment != null && resizableSegment.BoxCollider != null)
            return resizableSegment.BoxCollider;

        return GetComponent<BoxCollider2D>();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _collider = GetComponent<Collider2D>();

        if (resizableSegment == null)
            resizableSegment = GetComponent<ResizableSegment2D>();

        if (!configureOneWayPlatformOnReset)
        {
            SyncConnectionOpeningCollider();
            return;
        }

        _collider.isTrigger = false;
        _collider.usedByEffector = true;

        _effector = GetComponent<PlatformEffector2D>();
        if (_effector == null)
            _effector = gameObject.AddComponent<PlatformEffector2D>();

        _effector.useOneWay = true;
        _effector.useOneWayGrouping = true;
        _effector.surfaceArc = surfaceArc;

        SyncConnectionOpeningCollider();
        SyncOneWaySurfaceOrientation();
    }

    private void OnValidate()
    {
        _collider = GetComponent<Collider2D>();
        _effector = GetComponent<PlatformEffector2D>();

        if (_collider != null)
            _collider.isTrigger = false;

        if (resizableSegment == null)
            resizableSegment = GetComponent<ResizableSegment2D>();

        if (!Application.isPlaying)
        {
            SyncConnectionOpeningCollider();
            SyncOneWaySurfaceOrientation();
        }
    }

    [ContextMenu("Sync Connection Opening Collider")]
    private void EditorSyncConnectionOpeningCollider()
    {
        SyncConnectionOpeningCollider();
    }
#endif
}