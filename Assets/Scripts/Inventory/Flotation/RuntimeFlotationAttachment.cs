using UnityEngine;

public enum FlotationAttachmentTargetKind
{
    None = 0,
    Rigidbody = 1,
    WorldFixed = 2
}

/// <summary>
/// Physical short-tether relationship for one deployable flotation bag.
/// Supports either a Rigidbody2D-local target point or a fixed world point.
///
/// There is deliberately no line inventory, winch, load rating, or breakage
/// model here. The joint's break force is infinite for this feature pass.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class RuntimeFlotationAttachment :
    MonoBehaviour,
    IWorldItemAttachment,
    IWorldItemPickupParticipant,
    ICuttable
{
    [Header("Gameplay Authority")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("Tether")]
    [SerializeField] private Transform bagAnchor;
    [SerializeField, Min(0.01f)] private float defaultTetherLength = 0.65f;

    [Tooltip("Lift bags detach from picked-up targets after pickup commits.")]
    [SerializeField]
    private WorldItemAttachmentPickupPolicy pickupPolicy =
        WorldItemAttachmentPickupPolicy.DetachOnPickup;

    [Header("Optional Presentation")]
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Target-End Interaction Proxy")]
    [Tooltip("Creates a tiny trigger at the attachment point so deployed bags can be cut/removed from the target end without giving the whole rope a physical collider.")]
    [SerializeField] private bool createTargetInteractionProxy = true;

    [Tooltip("World-space interaction radius around the target end of the tether.")]
    [SerializeField, Min(0.03f)] private float targetInteractionRadius = 0.16f;

    [Header("Runtime")]
    [SerializeField] private FlotationAttachmentTargetKind targetKind;
    [SerializeField] private Rigidbody2D targetBody;
    [SerializeField] private Vector2 targetLocalPoint;
    [SerializeField] private Vector2 fixedWorldPoint;
    [SerializeField, Min(0.01f)] private float tetherLength = 0.65f;
    [SerializeField] private bool attached;

    private Rigidbody2D _bagBody;
    private DeployableFlotationBag _bag;
    private DistanceJoint2D _runtimeJoint;
    private WorldItemAttachmentRegistry _registeredTargetRegistry;
    private bool _jointSuppressedForAuthority;
    private GameObject _targetInteractionProxyObject;
    private CircleCollider2D _targetInteractionProxyCollider;

    public FlotationAttachmentTargetKind TargetKind => targetKind;
    public Rigidbody2D TargetBody => targetBody;
    public Vector2 TargetLocalPoint => targetLocalPoint;
    public Vector2 FixedWorldPoint => fixedWorldPoint;
    public float TetherLength => tetherLength;

    /// <summary>
    /// Magnitude of the actual DistanceJoint2D reaction force from the most
    /// recently solved physics step. A slack tether reports approximately zero;
    /// a loaded tether reports the force it is transmitting.
    /// </summary>
    public float CurrentTetherTension => GetCurrentTetherTension();

    public bool HasGameplayAuthority =>
        GameplayAuthority.CanRun(gameplayAuthorityMode);

    public bool IsAttached =>
        attached &&
        targetKind != FlotationAttachmentTargetKind.None;

    public bool IsWorldItemAttachmentActive => IsAttached;
    public WorldItemAttachmentPickupPolicy PickupPolicy => pickupPolicy;

    public bool IsCuttable => IsAttached;
    public Vector2 CutWorldPosition => GetTargetWorldPoint();

    private void Reset()
    {
        ResolveRefs();

        if (bagAnchor == null)
            bagAnchor = transform;

        tetherLength = Mathf.Max(0.01f, defaultTetherLength);
        ConfigureLineRenderer();
    }

    private void Awake()
    {
        ResolveRefs();
        ConfigureLineRenderer();

        tetherLength = Mathf.Max(0.01f, tetherLength);
        EnsureTargetInteractionProxy();
        RefreshTargetInteractionProxy();
        SetLineVisible(false);
    }

    private void OnValidate()
    {
        defaultTetherLength = Mathf.Max(0.01f, defaultTetherLength);
        tetherLength = Mathf.Max(0.01f, tetherLength);
        targetInteractionRadius = Mathf.Max(0.03f, targetInteractionRadius);
        ConfigureLineRenderer();
    }

    private void FixedUpdate()
    {
        RefreshJointAuthorityState();

        if (!IsAttached)
            return;

        if (targetKind == FlotationAttachmentTargetKind.Rigidbody &&
            targetBody == null)
        {
            // Dynamic target vanished. Fail soft: the bag remains a live world
            // object and its flotation lifecycle continues unattached.
            ApplyDetachedState();
            return;
        }

        if (_runtimeJoint == null)
            RebuildRuntimeJoint();
        else
            UpdateJointAnchors();
    }

    private void LateUpdate()
    {
        RefreshTargetInteractionProxy();

        if (!IsAttached)
        {
            SetLineVisible(false);
            return;
        }

        UpdateLineVisual();
        SetLineVisible(true);
    }

    private void OnDestroy()
    {
        UnregisterFromTargetWorldItem();
        DestroyRuntimeJoint();
    }

    /// <summary>
    /// Authority decision API for attaching to a moving Rigidbody2D.
    /// Activated bags cannot be newly re-rigged; ApplyBodyAttachment exists for
    /// persistence/replication restoration of already-activated bags.
    /// </summary>
    public bool TryAttachToBody(
        Rigidbody2D body,
        Vector2 targetWorldPoint,
        float requestedTetherLength = -1f)
    {
        ResolveRefs();

        if (!HasGameplayAuthority ||
            body == null ||
            body == _bagBody ||
            (_bag != null && _bag.HasActivated))
        {
            return false;
        }

        ApplyBodyAttachment(
            body,
            targetWorldPoint,
            ResolveRequestedTetherLength(requestedTetherLength));

        return IsAttached;
    }

    /// <summary>
    /// Authority decision API for attaching to a fixed point in the world.
    /// </summary>
    public bool TryAttachToWorld(
        Vector2 worldPoint,
        float requestedTetherLength = -1f)
    {
        ResolveRefs();

        if (!HasGameplayAuthority ||
            (_bag != null && _bag.HasActivated))
        {
            return false;
        }

        ApplyWorldAttachment(
            worldPoint,
            ResolveRequestedTetherLength(requestedTetherLength));

        return IsAttached;
    }

    public bool TryDetach()
    {
        if (!HasGameplayAuthority || !IsAttached)
            return false;

        ApplyDetachedState();
        return true;
    }

    /// <summary>
    /// State-application API for persistence/replication. The target point is
    /// converted to body-local coordinates immediately so rotation/motion are
    /// preserved without authored sockets.
    /// </summary>
    public void ApplyBodyAttachment(
        Rigidbody2D body,
        Vector2 targetWorldPoint,
        float appliedTetherLength)
    {
        ResolveRefs();
        UnregisterFromTargetWorldItem();
        DestroyRuntimeJoint();

        if (body == null || body == _bagBody)
        {
            ApplyDetachedState();
            return;
        }

        targetKind = FlotationAttachmentTargetKind.Rigidbody;
        targetBody = body;
        targetLocalPoint =
            body.transform.InverseTransformPoint(targetWorldPoint);
        fixedWorldPoint = Vector2.zero;
        tetherLength = Mathf.Max(0.01f, appliedTetherLength);
        attached = true;

        RegisterWithTargetWorldItem();
        RebuildRuntimeJoint();
    }

    /// <summary>
    /// Restore/apply overload when a persisted body-local point is already known.
    /// </summary>
    public void ApplyBodyAttachmentLocal(
        Rigidbody2D body,
        Vector2 bodyLocalPoint,
        float appliedTetherLength)
    {
        ResolveRefs();
        UnregisterFromTargetWorldItem();
        DestroyRuntimeJoint();

        if (body == null || body == _bagBody)
        {
            ApplyDetachedState();
            return;
        }

        targetKind = FlotationAttachmentTargetKind.Rigidbody;
        targetBody = body;
        targetLocalPoint = bodyLocalPoint;
        fixedWorldPoint = Vector2.zero;
        tetherLength = Mathf.Max(0.01f, appliedTetherLength);
        attached = true;

        RegisterWithTargetWorldItem();
        RebuildRuntimeJoint();
    }

    public void ApplyWorldAttachment(
        Vector2 worldPoint,
        float appliedTetherLength)
    {
        ResolveRefs();
        UnregisterFromTargetWorldItem();
        DestroyRuntimeJoint();

        targetKind = FlotationAttachmentTargetKind.WorldFixed;
        targetBody = null;
        targetLocalPoint = Vector2.zero;
        fixedWorldPoint = worldPoint;
        tetherLength = Mathf.Max(0.01f, appliedTetherLength);
        attached = true;

        RebuildRuntimeJoint();
    }

    public void ApplyDetachedState()
    {
        UnregisterFromTargetWorldItem();
        DestroyRuntimeJoint();

        attached = false;
        targetKind = FlotationAttachmentTargetKind.None;
        targetBody = null;
        targetLocalPoint = Vector2.zero;
        fixedWorldPoint = Vector2.zero;
        _jointSuppressedForAuthority = false;

        SetLineVisible(false);
        RefreshTargetInteractionProxy();
    }

    public void DetachForWorldItemPickup()
    {
        // Pickup has already committed by the time the registry invokes this.
        // This is state application, not an autonomous authority decision.
        ApplyDetachedState();
    }

    public bool AllowsWorldItemPickup(in InteractContext context)
    {
        // This participant is on the BAG'S own WorldItem. Picking up an unused
        // packed bag is allowed; DeployableFlotationBag blocks pickup forever
        // once activation begins.
        return true;
    }

    public void OnWorldItemPickupCommitted()
    {
        // If the bag itself was picked up while still unused, sever its runtime
        // relationship only after inventory acquisition succeeded.
        if (IsAttached)
            ApplyDetachedState();
    }

    public void ApplyCut()
    {
        // Cutting severs only the relationship. DeployableFlotationBag keeps
        // advancing Inflating/Full/Leaking/Spent independently afterward.
        ApplyDetachedState();
    }

    public float GetCurrentTetherTension()
    {
        if (!IsAttached ||
            _runtimeJoint == null ||
            !_runtimeJoint.enabled)
        {
            return 0f;
        }

        float tension =
            _runtimeJoint.reactionForce.magnitude;

        // Match the existing project tether implementation: Unity 6's solved
        // reactionForce property is preferred, with the explicit timestep API
        // retained as a fallback for a just-created/just-woken joint.
        if (tension <= 0.0001f)
        {
            tension = _runtimeJoint.GetReactionForce(
                Mathf.Max(Time.fixedDeltaTime, 0.0001f)).magnitude;
        }

        if (float.IsNaN(tension) ||
            float.IsInfinity(tension))
        {
            return 0f;
        }

        return Mathf.Max(0f, tension);
    }

    public Vector2 GetTargetWorldPoint()
    {
        switch (targetKind)
        {
            case FlotationAttachmentTargetKind.Rigidbody:
                if (targetBody != null)
                {
                    return targetBody.transform.TransformPoint(
                        targetLocalPoint);
                }
                break;

            case FlotationAttachmentTargetKind.WorldFixed:
                return fixedWorldPoint;
        }

        return BagAnchorWorldPoint;
    }

    private Vector2 BagAnchorWorldPoint =>
        bagAnchor != null
            ? (Vector2)bagAnchor.position
            : (_bagBody != null
                ? _bagBody.position
                : (Vector2)transform.position);

    private float ResolveRequestedTetherLength(float requested)
    {
        return requested > 0f
            ? requested
            : Mathf.Max(0.01f, defaultTetherLength);
    }

    private void ResolveRefs()
    {
        if (_bagBody == null)
            _bagBody = GetComponent<Rigidbody2D>();

        if (_bag == null)
            _bag = GetComponent<DeployableFlotationBag>();

        if (bagAnchor == null)
            bagAnchor = transform;
    }

    private void RebuildRuntimeJoint()
    {
        ResolveRefs();
        DestroyRuntimeJoint();

        if (!IsAttached || _bagBody == null)
            return;

        if (targetKind == FlotationAttachmentTargetKind.Rigidbody &&
            targetBody == null)
        {
            ApplyDetachedState();
            return;
        }

        _runtimeJoint =
            _bagBody.gameObject.AddComponent<DistanceJoint2D>();

        _runtimeJoint.autoConfigureConnectedAnchor = false;
        _runtimeJoint.autoConfigureDistance = false;
        _runtimeJoint.enableCollision = false;
        _runtimeJoint.maxDistanceOnly = true;
        _runtimeJoint.distance = Mathf.Max(0.01f, tetherLength);
        _runtimeJoint.breakForce = Mathf.Infinity;
        _runtimeJoint.connectedBody =
            targetKind == FlotationAttachmentTargetKind.Rigidbody
                ? targetBody
                : null;

        UpdateJointAnchors();
        RefreshJointAuthorityState(force: true);
    }

    private void DestroyRuntimeJoint()
    {
        if (_runtimeJoint == null)
            return;

        _runtimeJoint.enabled = false;

        if (Application.isPlaying)
            Destroy(_runtimeJoint);
        else
            DestroyImmediate(_runtimeJoint);

        _runtimeJoint = null;
    }

    private void UpdateJointAnchors()
    {
        if (_runtimeJoint == null || _bagBody == null)
            return;

        _runtimeJoint.anchor =
            _bagBody.transform.InverseTransformPoint(
                BagAnchorWorldPoint);

        if (targetKind == FlotationAttachmentTargetKind.Rigidbody &&
            targetBody != null)
        {
            _runtimeJoint.connectedBody = targetBody;
            _runtimeJoint.connectedAnchor = targetLocalPoint;
        }
        else if (targetKind == FlotationAttachmentTargetKind.WorldFixed)
        {
            _runtimeJoint.connectedBody = null;
            _runtimeJoint.connectedAnchor = fixedWorldPoint;
        }

        if (!Mathf.Approximately(_runtimeJoint.distance, tetherLength))
            _runtimeJoint.distance = tetherLength;
    }

    private void RefreshJointAuthorityState(bool force = false)
    {
        if (_runtimeJoint == null)
        {
            _jointSuppressedForAuthority = false;
            return;
        }

        if (!HasGameplayAuthority)
        {
            if (_runtimeJoint.enabled)
                _runtimeJoint.enabled = false;

            _jointSuppressedForAuthority = true;
            return;
        }

        if (!force && !_jointSuppressedForAuthority)
            return;

        _runtimeJoint.enabled = true;
        _jointSuppressedForAuthority = false;
        UpdateJointAnchors();
    }

    private void RegisterWithTargetWorldItem()
    {
        if (targetKind != FlotationAttachmentTargetKind.Rigidbody ||
            targetBody == null)
        {
            return;
        }

        WorldItem targetWorldItem =
            targetBody.GetComponent<WorldItem>() ??
            targetBody.GetComponentInParent<WorldItem>();

        if (targetWorldItem == null)
            return;

        WorldItemAttachmentRegistry registry =
            targetWorldItem.GetComponent<WorldItemAttachmentRegistry>();

        if (registry == null)
        {
            registry =
                targetWorldItem.gameObject.AddComponent<WorldItemAttachmentRegistry>();
        }

        registry.Register(this);
        _registeredTargetRegistry = registry;
    }

    private void UnregisterFromTargetWorldItem()
    {
        if (_registeredTargetRegistry != null)
            _registeredTargetRegistry.Unregister(this);

        _registeredTargetRegistry = null;
    }

    private void EnsureTargetInteractionProxy()
    {
        if (!Application.isPlaying ||
            !createTargetInteractionProxy ||
            _targetInteractionProxyCollider != null)
        {
            return;
        }

        _targetInteractionProxyObject =
            new GameObject("FlotationTetherTargetInteraction");

        _targetInteractionProxyObject.transform.SetParent(
            transform,
            worldPositionStays: true);

        _targetInteractionProxyObject.layer = gameObject.layer;

        _targetInteractionProxyCollider =
            _targetInteractionProxyObject.AddComponent<CircleCollider2D>();

        _targetInteractionProxyCollider.isTrigger = true;
        _targetInteractionProxyCollider.radius =
            Mathf.Max(0.03f, targetInteractionRadius);
        _targetInteractionProxyCollider.enabled = false;
    }

    private void RefreshTargetInteractionProxy()
    {
        if (!createTargetInteractionProxy)
        {
            if (_targetInteractionProxyCollider != null)
                _targetInteractionProxyCollider.enabled = false;
            return;
        }

        EnsureTargetInteractionProxy();

        if (_targetInteractionProxyCollider == null ||
            _targetInteractionProxyObject == null)
        {
            return;
        }

        _targetInteractionProxyObject.layer = gameObject.layer;
        _targetInteractionProxyCollider.radius =
            Mathf.Max(0.03f, targetInteractionRadius);

        bool shouldEnable =
            IsAttached &&
            _bag != null &&
            _bag.HasActivated;

        _targetInteractionProxyCollider.enabled = shouldEnable;

        if (shouldEnable)
            _targetInteractionProxyObject.transform.position = GetTargetWorldPoint();
    }

    private void ConfigureLineRenderer()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
    }

    private void UpdateLineVisual()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, BagAnchorWorldPoint);
        lineRenderer.SetPosition(1, GetTargetWorldPoint());
    }

    private void SetLineVisible(bool visible)
    {
        if (lineRenderer != null)
            lineRenderer.enabled = visible;
    }
}
