using UnityEngine;

/// <summary>
/// Runtime lifecycle + displacement authority for one deployable lift bag.
///
/// The bag never applies an artificial upward force. While packed, ForceBody2D
/// uses the authored packed geometry normally. Once activated, the ForceBody2D
/// geometry grows with the inflated collider so buoyancy submersion and water
/// drag measure the physical bag the player can actually see. Authoritative
/// displacement is supplied separately through IVolumeContribution so growing
/// geometry cannot double-count displacement. Ordinary buoyancy acts on the bag
/// Rigidbody and the short tether transfers that physical load to the target.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(ForceBody2D))]
public sealed class DeployableFlotationBag :
    MonoBehaviour,
    IVolumeContribution,
    IBuoyancyVolumePolicy,
    IWorldItemPickupParticipant,
    IInteractable,
    IInteractPromptProvider,
    IInteractPromptActionProvider,
    IInteractionLabelProvider,
    IInteractionRangeProvider,
    IInteractionColliderScope,
    IHoldInteractable
{
    [Header("Gameplay Authority")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("Flotation")]
    [Tooltip("Maximum ADDITIONAL displacement volume contributed by this bag when fully inflated.")]
    [SerializeField, Min(0f)] private float maxDisplacementVolume = 1f;

    [Tooltip("Seconds from activation to full inflation.")]
    [SerializeField, Min(0.01f)] private float inflationDurationSeconds = 1.5f;

    [Tooltip("How flotation rises during inflation. X=time 0..1, Y=flotation 0..1.")]
    [SerializeField]
    private AnimationCurve inflationCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Seconds spent at full displacement before leakage begins.")]
    [SerializeField, Min(0f)] private float fullDurationSeconds = 120f;

    [Tooltip("Seconds from the start of leakage until the bag is spent.")]
    [SerializeField, Min(0.01f)] private float leakDurationSeconds = 420f;

    [Tooltip("Seconds for an already-activated bag to collapse after its tether is detached. This is intentionally much faster than the normal leak so free bags do not remain giant visual obstructions.")]
    [SerializeField, Min(0.01f)] private float detachedDeflationSeconds = 3f;

    [Tooltip("Remaining BASE displacement volume of the collapsed bag when fully spent, as a fraction of its authored ForceBody2D base volume. Runtime gas displacement still falls all the way to zero. 0.80 means the spent shell keeps 80% of its original base displacement so it is more likely to sink.")]
    [SerializeField, Range(0.05f, 1f)] private float spentBaseDisplacementVolumeMultiplier = 0.80f;

    [Tooltip("Remaining flotation during leakage. X=leak time 0..1, Y=remaining flotation 0..1.")]
    [SerializeField]
    private AnimationCurve leakCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Tethered Buoyancy")]
    [Tooltip("At or below this BAG submersion fraction, the normal buoyant-acceleration limiter is fully active. Above it, tethered bags smoothly blend toward fully uncapped buoyancy at 100% submersion. Set 0 for the simplest behavior: bypass rises directly with submersion.")]
    [SerializeField, Range(0f, 0.99f)] private float limiterFullyActiveAtSubmersion = 0f;

    [Tooltip("Extra angular damping applied only while an activated bag is tethered. This damps joint/contact-driven spin without affecting normal world items.")]
    [SerializeField, Min(0f)] private float tetheredAngularDampingPerSecond = 5f;

    [Header("Presentation - Packed")]
    [Tooltip("Sprite shown while the bag is still unused/packed. Optional if presentation is handled elsewhere.")]
    [SerializeField] private SpriteRenderer packedSpriteRenderer;

    [Tooltip("Physical collider used while packed. This is what lets the unused bag rest on floors instead of falling through them.")]
    [SerializeField] private Collider2D packedCollider;

    [Header("Presentation - Inflated")]
    [Tooltip("Sprite shown immediately when activation begins.")]
    [SerializeField] private SpriteRenderer inflatedSpriteRenderer;

    [Tooltip("Collider used from the first frame of inflation onward.")]
    [SerializeField] private Collider2D inflatedCollider;

    [Tooltip("Child transform scaled during inflation/leakage. Put both the inflated sprite and inflated collider under this transform. NEVER assign the Rigidbody/root transform here.")]
    [SerializeField] private Transform inflatedScaleRoot;

    [Tooltip("Scale of the inflated sprite/collider on the first frame of activation. Tune this so the visual swap starts near the packed bag's apparent size.")]
    [SerializeField] private Vector3 inflatedStartScale = new Vector3(0.35f, 0.35f, 1f);

    [Tooltip("Scale reached at Flotation01 = 1. This is intentionally Inspector-tunable; 1 means the authored inflated sprite size, 2 means twice that size.")]
    [SerializeField] private Vector3 inflatedFullScale = Vector3.one;

    [Tooltip("Final visual size of a completely spent bag as a fraction of its just-activated inflated start size. This is visual/collider scale only; spent base buoyancy volume is controlled separately above.")]
    [SerializeField, Range(0.1f, 1f)] private float spentScaleMultiplier = 0.80f;

    [Header("Inflate / Cut Interaction")]
    [SerializeField] private int interactionPriority = 80;
    [SerializeField, Min(0f)] private float hoverNameRange = 4f;
    [SerializeField, Min(0f)] private float actionRange = 1.75f;
    [SerializeField] private Transform promptAnchor;

    [Tooltip("Capability required to cut an activated bag tether. Assign the same ToolCapabilityDefinition to knife/cutter ItemDefinitions. Leave empty to disable cutting interaction on this prefab; null never means bare-hand cutting.")]
    [SerializeField] private ToolCapabilityDefinition cuttingCapability;

    [Tooltip("Seconds the attached player must hold Interact to manually work an activated bag loose when no cutting tool is being used.")]
    [SerializeField, Min(0.1f)] private float selfRemovalHoldSeconds = 3f;

    [SerializeField] private bool showSelfRemovalProgressInPrompt = true;

    [Header("Runtime")]
    [SerializeField]
    private FlotationBagState state =
        FlotationBagState.AttachedPacked;

    [SerializeField] private bool hasActivated;
    [SerializeField, Min(0f)] private float stateElapsedSeconds;
    [SerializeField, Range(0f, 1f)] private float flotation01;

    private ForceBody2D _forceBody;
    private RuntimeFlotationAttachment _attachment;
    private Rigidbody2D _rb;
    private ForceSystem _forceSystem;
    private BuoyancyPolygonForce _buoyancy;

    // Capture the prefab-authored packed dimensions once. After activation,
    // ForceBody2D geometry tracks the inflated collider for water contact/drag,
    // while VolumeContribution remains the authoritative displacement amount.
    private bool _capturedAuthoredForceBodyDimensions;
    private float _authoredForceBodyWidth;
    private float _authoredForceBodyHeight;

    private bool _authorityBodySuppressed;
    private RigidbodyType2D _bodyTypeBeforeAuthoritySuppression =
        RigidbodyType2D.Dynamic;

    public FlotationBagState State => state;
    public bool HasActivated => hasActivated;
    public float StateElapsedSeconds => stateElapsedSeconds;
    public float Flotation01 => flotation01;
    public float MaxDisplacementVolume => Mathf.Max(0f, maxDisplacementVolume);
    public int InteractionPriority => interactionPriority;

    public bool HasGameplayAuthority =>
        GameplayAuthority.CanRun(gameplayAuthorityMode);

    public float VolumeContribution
    {
        get
        {
            // Packed bags use ForceBody2D's authored base displacement directly.
            // Once activation begins, ForceBody geometry is repurposed as pure
            // water-contact geometry and its base displacement scale becomes 0.
            // Therefore this contribution owns the ENTIRE physical displacement
            // of an activated bag: shell + remaining inflation gas.
            if (!hasActivated)
                return 0f;

            ResolveRefs();
            CaptureAuthoredForceBodyDimensions();

            float packedShellVolume =
                Mathf.Max(0.0001f,
                    _authoredForceBodyWidth * _authoredForceBodyHeight);

            float shellVolumeMultiplier = 1f;

            if (state == FlotationBagState.Leaking)
            {
                shellVolumeMultiplier = Mathf.Lerp(
                    spentBaseDisplacementVolumeMultiplier,
                    1f,
                    Mathf.Clamp01(flotation01));
            }
            else if (state == FlotationBagState.Spent)
            {
                shellVolumeMultiplier =
                    spentBaseDisplacementVolumeMultiplier;
            }

            float shellVolume =
                packedShellVolume * shellVolumeMultiplier;

            float gasVolume =
                MaxDisplacementVolume * Mathf.Clamp01(flotation01);

            return Mathf.Max(0f, shellVolume + gasVolume);
        }
    }

    /// <summary>
    /// Lift-bag runtime volume always participates at full authored strength.
    /// The only policy decision is whether that volume travels through the
    /// normal capped buoyancy path or the uncapped center-of-mass path.
    /// </summary>
    public float VolumeEffectiveness01 => 1f;

    /// <summary>
    /// Simple flotation rule:
    /// - detached bags are always fully limited;
    /// - tethered + fully submerged bags are fully uncapped;
    /// - tethered + partially submerged bags blend between those states using
    ///   limiterFullyActiveAtSubmersion as the Inspector-tunable threshold.
    ///
    /// Example with threshold 0.50:
    /// <= 50% submerged -> bypass 0 (fully limited)
    /// 75% submerged    -> bypass 0.5
    /// 100% submerged   -> bypass 1 (fully uncapped)
    /// </summary>
    public float BodyAccelerationCapBypass01
    {
        get
        {
            ResolveRefs();

            if (!hasActivated ||
                flotation01 <= 0f ||
                _attachment == null ||
                !_attachment.IsAttached)
            {
                return 0f;
            }

            float submerged01 = GetLimiterSubmersion01();

            float fullyLimitedAt = Mathf.Clamp(
                limiterFullyActiveAtSubmersion,
                0f,
                0.99f);

            return Mathf.Clamp01(
                Mathf.InverseLerp(
                    fullyLimitedAt,
                    1f,
                    submerged01));
        }
    }

    /// <summary>
    /// The normal capped path uses the project's configured buoyant-acceleration
    /// ceiling unchanged. Surface behavior comes from the bag's physical geometry.
    /// </summary>
    public float BodyAccelerationCapScale01 => 1f;

    private void Reset()
    {
        ResolveRefs();

        if (promptAnchor == null)
            promptAnchor = transform;

        ApplyPresentation();
    }

    private void Awake()
    {
        ResolveRefs();
        CaptureAuthoredForceBodyDimensions();
        ConfigureForceSystemAuthorityGate();
        RefreshGameplayAuthorityBodyState();
        ClampRuntimeState();
        ApplyPresentation();
        ApplyForceBodyPhysicalState();
    }

    private void OnEnable()
    {
        ResolveRefs();
        CaptureAuthoredForceBodyDimensions();
        ConfigureForceSystemAuthorityGate();
        RefreshGameplayAuthorityBodyState();
        ApplyPresentation();
        ApplyForceBodyPhysicalState();

        if (_forceBody != null)
            _forceBody.RegisterVolumeContribution(this);
    }

    private void OnDisable()
    {
        if (_forceBody != null)
            _forceBody.UnregisterVolumeContribution(this);

        RestoreAuthoredForceBodyPhysicalState();
        RestoreAuthorityBodyState();
    }

    private void OnValidate()
    {
        maxDisplacementVolume = Mathf.Max(0f, maxDisplacementVolume);
        inflationDurationSeconds = Mathf.Max(0.01f, inflationDurationSeconds);
        fullDurationSeconds = Mathf.Max(0f, fullDurationSeconds);
        leakDurationSeconds = Mathf.Max(0.01f, leakDurationSeconds);
        detachedDeflationSeconds = Mathf.Max(0.01f, detachedDeflationSeconds);
        spentBaseDisplacementVolumeMultiplier = Mathf.Clamp(
            spentBaseDisplacementVolumeMultiplier,
            0.05f,
            1f);
        limiterFullyActiveAtSubmersion = Mathf.Clamp(
            limiterFullyActiveAtSubmersion,
            0f,
            0.99f);
        tetheredAngularDampingPerSecond =
            Mathf.Max(0f, tetheredAngularDampingPerSecond);
        spentScaleMultiplier = Mathf.Clamp(spentScaleMultiplier, 0.1f, 1f);
        selfRemovalHoldSeconds = Mathf.Max(0.1f, selfRemovalHoldSeconds);

        if (inflationCurve == null || inflationCurve.length == 0)
        {
            inflationCurve =
                AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (leakCurve == null || leakCurve.length == 0)
        {
            leakCurve =
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        }

        ClampRuntimeState();
        ApplyPresentation();
        ApplyForceBodyPhysicalState();
    }

    private void FixedUpdate()
    {
        RefreshGameplayAuthorityBodyState();

        if (!HasGameplayAuthority)
            return;

        float deltaTime = Mathf.Max(0f, Time.fixedDeltaTime);

        AdvanceLifecycle(deltaTime);
        ApplyPresentation();
        ApplyForceBodyPhysicalState();
        ApplyTetheredAngularStabilization(deltaTime);
    }

    private void LateUpdate()
    {
        // Presentation follows the same authoritative/replicated flotation
        // scalar that owns displacement. It never invents a visual state.
        ApplyPresentation();
    }

    /// <summary>
    /// Authority decision API. Inflation may only begin while the bag is still
    /// unused and physically attached.
    /// </summary>
    public bool TryBeginInflation()
    {
        ResolveRefs();

        if (!HasGameplayAuthority ||
            hasActivated ||
            state != FlotationBagState.AttachedPacked ||
            _attachment == null ||
            !_attachment.IsAttached)
        {
            return false;
        }

        hasActivated = true;
        state = FlotationBagState.Inflating;
        stateElapsedSeconds = 0f;
        flotation01 = 0f;
        ApplyPresentation();
        ApplyForceBodyPhysicalState();
        return true;
    }

    /// <summary>
    /// Explicit state-application hook for future persistence/replication.
    /// This does not decide whether the transition is legal; authority owns
    /// that decision before applying replicated/restored state.
    /// </summary>
    public void ApplyRuntimeState(
        FlotationBagState nextState,
        bool activated,
        float elapsedSeconds,
        float nextFlotation01)
    {
        state = nextState;
        hasActivated = activated || nextState != FlotationBagState.AttachedPacked;
        stateElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
        flotation01 = Mathf.Clamp01(nextFlotation01);
        ClampRuntimeState();
        ApplyPresentation();
        ApplyForceBodyPhysicalState();
    }

    public bool CanInteract(in InteractContext context)
    {
        ResolveRefs();

        // Range is deliberately NOT checked here. Interactor2D already applies
        // this component's IInteractionRangeProvider against the actual source
        // collider. Activated bags also expose a tiny target-end interaction
        // proxy so the player can work at the attachment point instead of
        // needing to reach the balloon itself.
        if (!HasGameplayAuthority ||
            _attachment == null ||
            !_attachment.IsAttached)
        {
            return false;
        }

        if (!hasActivated)
            return state == FlotationBagState.AttachedPacked;

        if (HasUsableCuttingTool(context))
            return true;

        // A player may also work a bag off their own body by hand. This is slow
        // rather than free/instant and does not recover the already-activated bag.
        return IsAttachedToInteractingPlayer(context);
    }

    public float GetInteractionHoldDuration(
        in InteractContext context)
    {
        if (!hasActivated ||
            HasUsableCuttingTool(context) ||
            !IsAttachedToInteractingPlayer(context))
        {
            return 0f;
        }

        return Mathf.Max(0.1f, selfRemovalHoldSeconds);
    }

    public void Interact(in InteractContext context)
    {
        if (!CanInteract(context))
            return;

        if (!hasActivated)
        {
            TryBeginInflation();
            return;
        }

        if (_attachment == null)
            return;

        if (HasUsableCuttingTool(context))
        {
            CuttingAuthority.TryCut(
                _attachment,
                context,
                cuttingCapability,
                Mathf.Max(0f, actionRange),
                out _);
            return;
        }

        if (IsAttachedToInteractingPlayer(context))
            _attachment.TryDetach();
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (!hasActivated)
            return "Inflate";

        if (HasUsableCuttingTool(context))
            return "Cut Tether";

        return IsAttachedToInteractingPlayer(context)
            ? "Remove Bag"
            : "Cut Tether";
    }

    public void GetPromptActions(
        in InteractContext context,
        System.Collections.Generic.List<PromptAction> actions)
    {
        if (actions == null || !CanInteract(context))
            return;

        if (!hasActivated)
        {
            actions.Add(
                new PromptAction(
                    "Press E to Inflate",
                    priority: 100));
            return;
        }

        if (HasUsableCuttingTool(context))
        {
            actions.Add(
                new PromptAction(
                    "Press E to Cut Tether",
                    priority: 100));
            return;
        }

        if (!IsAttachedToInteractingPlayer(context))
            return;

        float progress = GetHoldInteractionProgress(context);

        actions.Add(
            new PromptAction(
                "Hold E to Remove Bag",
                priority: 100,
                showProgress: showSelfRemovalProgressInPrompt,
                progress01: showSelfRemovalProgressInPrompt
                    ? progress
                    : 0f));
    }

    public Transform GetPromptAnchor()
    {
        // Once deployed, allow the prompt driver to fall back to whichever
        // collider the player actually hovered. That puts the prompt at the
        // balloon when hovering the balloon and at the target attachment point
        // when hovering the target-end interaction proxy.
        if (hasActivated)
            return null;

        return promptAnchor != null ? promptAnchor : transform;
    }

    public string GetInteractionLabel(in InteractContext context)
    {
        WorldItem worldItem = GetComponent<WorldItem>();

        if (worldItem != null &&
            worldItem.Instance != null &&
            worldItem.Instance.Definition != null &&
            !string.IsNullOrWhiteSpace(worldItem.Instance.Definition.DisplayName))
        {
            return worldItem.Instance.Definition.DisplayName;
        }

        return "Lift Bag";
    }

    public bool TryGetHoverNameRange(out float range)
    {
        range = Mathf.Max(0f, hoverNameRange);
        return true;
    }

    public bool TryGetActionRange(out float range)
    {
        range = Mathf.Max(0f, actionRange);
        return true;
    }

    public bool AllowsInteractionCollider(Collider2D sourceCollider)
    {
        if (sourceCollider == null)
            return false;

        // Interaction identity belongs to the lift-bag WorldItem, not to one
        // particular physics shape. A normal WorldItem prefab may still carry a
        // root collider in addition to the packed/inflated presentation
        // colliders. If we accept only the exact state collider, Interactor2D
        // can legitimately hover the root collider, keep the WorldItem pickup
        // channel, and strip the Inflate channel. The player then sees only
        // "Press F to Pick Up" even though the bag itself is valid.
        //
        // Disabled packed/inflated colliders cannot be returned by Physics2D,
        // so accepting any collider inside this bag hierarchy does not make the
        // inactive presentation shape interactable. It simply makes the bag's
        // interaction robust to ordinary multi-collider WorldItem prefabs and
        // collider hit ordering.
        Transform sourceTransform = sourceCollider.transform;

        return
            sourceTransform == transform ||
            sourceTransform.IsChildOf(transform);
    }

    public bool AllowsWorldItemPickup(in InteractContext context)
    {
        // The irreversible boundary is the instant activation succeeds.
        return !hasActivated;
    }

    public void OnWorldItemPickupCommitted()
    {
        // The attachment component owns physical relationship cleanup. Nothing
        // about the persistent ItemInstance is changed here.
    }

    private bool HasUsableCuttingTool(in InteractContext context)
    {
        if (cuttingCapability == null)
            return false;

        return ToolUseChargeUtility.CanUseHeldTool(
            context,
            cuttingCapability,
            out _,
            out _);
    }

    private bool IsAttachedToInteractingPlayer(
        in InteractContext context)
    {
        ResolveRefs();

        if (_attachment == null ||
            !_attachment.IsAttached ||
            _attachment.TargetKind != FlotationAttachmentTargetKind.Rigidbody ||
            _attachment.TargetBody == null)
        {
            return false;
        }

        CharacterPlayer player = null;

        if (context.InteractorGO != null)
        {
            player = context.InteractorGO.GetComponent<CharacterPlayer>();

            if (player == null)
                player = context.InteractorGO.GetComponentInParent<CharacterPlayer>();

            if (player == null)
                player = context.InteractorGO.GetComponentInChildren<CharacterPlayer>(true);
        }

        if (player == null && context.InteractorTransform != null)
        {
            player = context.InteractorTransform.GetComponentInParent<CharacterPlayer>();

            if (player == null)
                player = context.InteractorTransform.GetComponentInChildren<CharacterPlayer>(true);
        }

        if (player == null)
            return false;

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        return playerBody != null && _attachment.TargetBody == playerBody;
    }

    private float GetHoldInteractionProgress(
        in InteractContext context)
    {
        Interactor2D interactor = ResolveInteractor(context);

        if (interactor == null ||
            !ReferenceEquals(interactor.ActiveHoldInteractTarget, this))
        {
            return 0f;
        }

        return interactor.ActiveHoldInteractProgress;
    }

    private static Interactor2D ResolveInteractor(
        in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            Interactor2D direct = context.InteractorGO.GetComponent<Interactor2D>();
            if (direct != null)
                return direct;

            Interactor2D parent = context.InteractorGO.GetComponentInParent<Interactor2D>();
            if (parent != null)
                return parent;

            Interactor2D child = context.InteractorGO.GetComponentInChildren<Interactor2D>(true);
            if (child != null)
                return child;
        }

        if (context.InteractorTransform != null)
        {
            Interactor2D parent = context.InteractorTransform.GetComponentInParent<Interactor2D>();
            if (parent != null)
                return parent;

            return context.InteractorTransform.GetComponentInChildren<Interactor2D>(true);
        }

        return null;
    }

    private void AdvanceLifecycle(float deltaTime)
    {
        ResolveRefs();

        // Once an activated bag loses its tether, collapse it quickly instead
        // of letting a full-size free bag remain visually obstructive for the
        // normal multi-minute leak duration. Packed/unused bags are unaffected.
        if (hasActivated &&
            state != FlotationBagState.Spent &&
            (_attachment == null || !_attachment.IsAttached))
        {
            if (state != FlotationBagState.Leaking)
            {
                state = FlotationBagState.Leaking;
                stateElapsedSeconds = 0f;
            }

            stateElapsedSeconds += deltaTime;
            flotation01 = Mathf.MoveTowards(
                flotation01,
                0f,
                deltaTime / Mathf.Max(0.01f, detachedDeflationSeconds));

            if (flotation01 <= 0f)
            {
                state = FlotationBagState.Spent;
                stateElapsedSeconds = 0f;
                flotation01 = 0f;
            }

            return;
        }

        switch (state)
        {
            case FlotationBagState.AttachedPacked:
                flotation01 = 0f;
                stateElapsedSeconds = 0f;
                return;

            case FlotationBagState.Inflating:
                stateElapsedSeconds += deltaTime;

                float inflationT = Mathf.Clamp01(
                    stateElapsedSeconds /
                    Mathf.Max(0.01f, inflationDurationSeconds));

                flotation01 = Mathf.Clamp01(
                    inflationCurve.Evaluate(inflationT));

                if (inflationT >= 1f)
                {
                    state = FlotationBagState.Full;
                    stateElapsedSeconds = 0f;
                    flotation01 = 1f;
                }
                return;

            case FlotationBagState.Full:
                flotation01 = 1f;
                stateElapsedSeconds += deltaTime;

                if (stateElapsedSeconds >= fullDurationSeconds)
                {
                    state = FlotationBagState.Leaking;
                    stateElapsedSeconds = 0f;
                }
                return;

            case FlotationBagState.Leaking:
                stateElapsedSeconds += deltaTime;

                float leakT = Mathf.Clamp01(
                    stateElapsedSeconds /
                    Mathf.Max(0.01f, leakDurationSeconds));

                flotation01 = Mathf.Clamp01(
                    leakCurve.Evaluate(leakT));

                if (leakT >= 1f)
                {
                    state = FlotationBagState.Spent;
                    stateElapsedSeconds = 0f;
                    flotation01 = 0f;
                }
                return;

            case FlotationBagState.Spent:
                flotation01 = 0f;
                stateElapsedSeconds = 0f;
                return;
        }
    }

    private void CaptureAuthoredForceBodyDimensions()
    {
        if (_capturedAuthoredForceBodyDimensions || _forceBody == null)
            return;

        _authoredForceBodyWidth = Mathf.Max(0.01f, _forceBody.Width);
        _authoredForceBodyHeight = Mathf.Max(0.01f, _forceBody.Height);
        _capturedAuthoredForceBodyDimensions = true;
    }

    private void ApplyForceBodyPhysicalState()
    {
        ResolveRefs();
        CaptureAuthoredForceBodyDimensions();

        if (_forceBody == null || !_capturedAuthoredForceBodyDimensions)
            return;

        if (!hasActivated)
        {
            // Packed state: preserve the prefab-authored physical rectangle and
            // let it contribute its normal base displacement.
            _forceBody.SetDimensions(
                _authoredForceBodyWidth,
                _authoredForceBodyHeight);
            _forceBody.SetBaseDisplacementScale(1f);
            return;
        }

        // Activated state: the geometry now tracks the real inflated collider so
        // buoyancy submersion and DragForce see the same large object as the
        // player. The geometry itself contributes ZERO displacement here because
        // VolumeContribution supplies the full shell + gas displacement. This is
        // what prevents physical growth from double-counting buoyancy.
        _forceBody.SetBaseDisplacementScale(0f);

        if (TryGetInflatedColliderDimensionsInBodyLocal(
                out float physicalWidth,
                out float physicalHeight))
        {
            _forceBody.SetDimensions(physicalWidth, physicalHeight);
            return;
        }

        // Safe fallback for incomplete prefabs. Keep useful geometry rather than
        // collapsing the solver to nothing.
        float fallbackScale = Mathf.Max(
            0.01f,
            ResolveInflatedPresentationScale().x);

        _forceBody.SetDimensions(
            _authoredForceBodyWidth * fallbackScale,
            _authoredForceBodyHeight * fallbackScale);
    }

    private bool TryGetInflatedColliderDimensionsInBodyLocal(
        out float width,
        out float height)
    {
        width = 0f;
        height = 0f;

        if (inflatedCollider == null || !inflatedCollider.enabled)
            return false;

        if (inflatedCollider is CircleCollider2D circle)
        {
            float diameter = Mathf.Max(0.0001f, circle.radius * 2f);

            // TransformVector reads the child's current presentation scale
            // immediately, without depending on Physics2D transform sync. Bring
            // those world-space diameter vectors back into the bag root's local
            // space so ForceBody2D's root-local rectangle tracks the real ball.
            Vector3 localDiameterX = transform.InverseTransformVector(
                circle.transform.TransformVector(Vector3.right * diameter));
            Vector3 localDiameterY = transform.InverseTransformVector(
                circle.transform.TransformVector(Vector3.up * diameter));

            width = Mathf.Max(0.01f, localDiameterX.magnitude);
            height = Mathf.Max(0.01f, localDiameterY.magnitude);
        }
        else
        {
            Bounds bounds = inflatedCollider.bounds;

            Vector3 rootScale = transform.lossyScale;
            float rootScaleX = Mathf.Max(0.0001f, Mathf.Abs(rootScale.x));
            float rootScaleY = Mathf.Max(0.0001f, Mathf.Abs(rootScale.y));

            width = Mathf.Max(0.01f, bounds.size.x / rootScaleX);
            height = Mathf.Max(0.01f, bounds.size.y / rootScaleY);
        }

        return
            !float.IsNaN(width) &&
            !float.IsInfinity(width) &&
            !float.IsNaN(height) &&
            !float.IsInfinity(height);
    }

    private void RestoreAuthoredForceBodyPhysicalState()
    {
        if (_forceBody == null || !_capturedAuthoredForceBodyDimensions)
            return;

        _forceBody.SetDimensions(
            _authoredForceBodyWidth,
            _authoredForceBodyHeight);
        _forceBody.SetBaseDisplacementScale(1f);
    }

    private void ResolveRefs()
    {
        if (_forceBody == null)
            _forceBody = GetComponent<ForceBody2D>();

        if (_attachment == null)
            _attachment = GetComponent<RuntimeFlotationAttachment>();

        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        if (_forceSystem == null)
            _forceSystem = GetComponent<ForceSystem>();

        if (_buoyancy == null)
            _buoyancy = GetComponent<BuoyancyPolygonForce>();
    }

    /// <summary>
    /// Submersion used by the tethered acceleration-cap bypass.
    /// ForceBody2D already tracks the inflated collider for ordinary buoyancy and
    /// drag; the limiter uses the collider directly so a CircleCollider2D gets a
    /// true submerged-area fraction instead of a rectangular approximation.
    /// </summary>
    private float GetLimiterSubmersion01()
    {
        ResolveRefs();

        if (_buoyancy == null ||
            inflatedCollider == null ||
            !inflatedCollider.enabled)
        {
            return _buoyancy != null
                ? Mathf.Clamp01(_buoyancy.SubmergedFraction)
                : 0f;
        }

        Bounds bounds = inflatedCollider.bounds;
        float height = bounds.size.y;

        if (height <= 0.000001f)
            return 0f;

        float surfaceY = _buoyancy.LastSurfaceYAtBodyX;

        // True submerged-area fraction for the inflated circle.
        if (inflatedCollider is CircleCollider2D)
        {
            float radius = height * 0.5f;
            if (radius <= 0.000001f)
                return 0f;

            float centerY = bounds.center.y;
            float normalizedCut = Mathf.Clamp(
                (surfaceY - centerY) / radius,
                -1f,
                1f);

            if (normalizedCut <= -1f)
                return 0f;

            if (normalizedCut >= 1f)
                return 1f;

            float inside = Mathf.Max(
                0f,
                1f - normalizedCut * normalizedCut);

            float areaFraction =
                (Mathf.Asin(normalizedCut) +
                 (Mathf.PI * 0.5f) +
                 normalizedCut * Mathf.Sqrt(inside)) /
                Mathf.PI;

            return Mathf.Clamp01(areaFraction);
        }

        // Fallback for a future non-circle inflated collider.
        return Mathf.Clamp01(
            (surfaceY - bounds.min.y) /
            height);
    }

    private void ApplyTetheredAngularStabilization(float deltaTime)
    {
        ResolveRefs();

        if (_rb == null ||
            _attachment == null ||
            !_attachment.IsAttached ||
            !hasActivated ||
            flotation01 <= 0f ||
            tetheredAngularDampingPerSecond <= 0f ||
            deltaTime <= 0f)
        {
            return;
        }

        // Bag-specific hydrodynamic angular damping. DragForce currently has no
        // useful global angular drag in the project's PhysicsGlobals, so a taut
        // short tether can otherwise preserve a great deal of rotational energy.
        // This is a smooth exponential damping, not a hard angular-velocity clamp.
        float dampingRate =
            tetheredAngularDampingPerSecond *
            Mathf.Clamp01(flotation01);

        float retain = Mathf.Exp(-dampingRate * deltaTime);
        _rb.angularVelocity *= retain;
    }

    private void ConfigureForceSystemAuthorityGate()
    {
        if (_forceSystem == null)
            return;

        _forceSystem.ConfigureGameplayAuthorityGate(
            true,
            gameplayAuthorityMode);
    }

    private void RefreshGameplayAuthorityBodyState()
    {
        ResolveRefs();

        if (_rb == null)
            return;

        if (!HasGameplayAuthority)
        {
            if (!_authorityBodySuppressed)
            {
                _bodyTypeBeforeAuthoritySuppression =
                    _rb.bodyType;

                _authorityBodySuppressed = true;
            }

            if (_rb.bodyType != RigidbodyType2D.Kinematic)
                _rb.bodyType = RigidbodyType2D.Kinematic;

            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            return;
        }

        if (!_authorityBodySuppressed)
            return;

        _rb.bodyType =
            _bodyTypeBeforeAuthoritySuppression;

        _authorityBodySuppressed = false;

        if (_rb.simulated)
            _rb.WakeUp();
    }

    private void RestoreAuthorityBodyState()
    {
        if (!_authorityBodySuppressed || _rb == null)
            return;

        _rb.bodyType =
            _bodyTypeBeforeAuthoritySuppression;

        _authorityBodySuppressed = false;
    }

    private void ClampRuntimeState()
    {
        stateElapsedSeconds = Mathf.Max(0f, stateElapsedSeconds);
        flotation01 = Mathf.Clamp01(flotation01);

        if (state != FlotationBagState.AttachedPacked)
            hasActivated = true;

        if (!hasActivated)
        {
            state = FlotationBagState.AttachedPacked;
            flotation01 = 0f;
            stateElapsedSeconds = 0f;
        }

        if (state == FlotationBagState.Full)
            flotation01 = 1f;

        if (state == FlotationBagState.Spent)
            flotation01 = 0f;
    }

    private bool UsesInflatedPresentation =>
        hasActivated ||
        state != FlotationBagState.AttachedPacked;

    private void ApplyPresentation()
    {
        bool inflatedPresentation = UsesInflatedPresentation;

        if (packedSpriteRenderer != null)
            packedSpriteRenderer.enabled = !inflatedPresentation;

        if (inflatedSpriteRenderer != null)
            inflatedSpriteRenderer.enabled = inflatedPresentation;

        if (packedCollider != null)
            packedCollider.enabled = !inflatedPresentation;

        if (inflatedCollider != null)
            inflatedCollider.enabled = inflatedPresentation;

        if (inflatedScaleRoot != null && inflatedScaleRoot != transform)
            inflatedScaleRoot.localScale = ResolveInflatedPresentationScale();
    }

    private Vector3 ResolveInflatedPresentationScale()
    {
        Vector3 spentScale = new Vector3(
            inflatedStartScale.x * spentScaleMultiplier,
            inflatedStartScale.y * spentScaleMultiplier,
            inflatedStartScale.z);

        switch (state)
        {
            case FlotationBagState.Inflating:
                return Vector3.LerpUnclamped(
                    inflatedStartScale,
                    inflatedFullScale,
                    Mathf.Clamp01(flotation01));

            case FlotationBagState.Full:
                return inflatedFullScale;

            case FlotationBagState.Leaking:
                return Vector3.LerpUnclamped(
                    spentScale,
                    inflatedFullScale,
                    Mathf.Clamp01(flotation01));

            case FlotationBagState.Spent:
                return spentScale;

            default:
                return inflatedStartScale;
        }
    }
}
