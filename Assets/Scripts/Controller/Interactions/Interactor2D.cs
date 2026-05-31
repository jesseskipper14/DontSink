using System;
using UnityEngine;

/// <summary>
/// World-authoritative interaction resolver.
///
/// Current model:
/// - Mouse hover chooses a single world target.
/// - A larger hover/name range controls whether that target can be identified.
/// - A smaller action range controls whether interact/pickup/toggle/unsecure actions are available.
/// - Optional legacy fallback can still resolve by raycast/nearby overlap when mouseOnlyTargeting is disabled.
/// </summary>
[DisallowMultipleComponent]
public class Interactor2D : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private float overlapRadius = 1.2f;
    [SerializeField] private float rayDistance = 1.6f;
    [SerializeField] private bool useRaycast = true;

    [Header("Scoring")]
    [SerializeField] private float aimBias = 0.35f;

    [Header("Mouse Hover")]
    [SerializeField] private bool preferMouseHoveredInteractable = true;
    [SerializeField, Min(0f)] private float mouseHoverRadius = 0.08f;
    [SerializeField] private bool applyMouseHoverToPickup = true;

    [Header("Mouse Targeting Mode")]
    [Tooltip("If true, only targets under/near the mouse resolve. Disables fallback player-radius/raycast target soup.")]
    [SerializeField] private bool mouseOnlyTargeting = true;

    [Header("Prompt Range Bands")]
    [SerializeField, Min(0f)] private float defaultHoverNameRange = 4.0f;
    [SerializeField, Min(0f)] private float defaultActionRange = 1.75f;

    [Header("Debug")]
    [SerializeField] private bool debugMousePickupResolution = false;
    [SerializeField, Min(1)] private int debugMousePickupEveryNFrames = 15;

    private IInteractionIntentSource intentSource;

    private IPickupInteractable _activeHoldPickupTarget;
    private float _activeHoldPickupElapsed;
    private bool _holdPickupTriggered;

    public IPickupInteractable ActiveHoldPickupTarget => _activeHoldPickupTarget;
    public float ActiveHoldPickupElapsed => _activeHoldPickupElapsed;
    public float ActiveHoldPickupDuration =>
        _activeHoldPickupTarget != null
            ? Mathf.Max(0.0001f, _activeHoldPickupTarget.PickupHoldDuration)
            : 0f;

    public float ActiveHoldPickupProgress =>
        _activeHoldPickupTarget != null
            ? Mathf.Clamp01(_activeHoldPickupElapsed / Mathf.Max(0.0001f, _activeHoldPickupTarget.PickupHoldDuration))
            : 0f;

    public bool IsHoldingPickup => _activeHoldPickupTarget != null && !_holdPickupTriggered;

    public event Action<IInteractable> OnInteracted;
    public event Action<IPickupInteractable> OnPickedUp;
    public event Action<IUnsecureInteractable> OnUnsecured;

    private void Awake()
    {
        intentSource = GetComponent<IInteractionIntentSource>();
        if (intentSource == null)
        {
            Debug.LogError("Interactor2D requires IInteractionIntentSource (e.g., LocalInteractionIntentSource).", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (!TryBuildContext(out InteractContext ctx, out InteractionIntent intent))
            return;

        if (intent.InteractPressed && TryResolveBest(ctx, out IInteractable interactTarget))
        {
            interactTarget.Interact(ctx);
            OnInteracted?.Invoke(interactTarget);
        }

        if (intent.UnsecurePressed && TryResolveBestUnsecure(ctx, out IUnsecureInteractable unsecureTarget))
        {
            unsecureTarget.Unsecure(ctx);
            OnUnsecured?.Invoke(unsecureTarget);
        }

        if (intent.TogglePressed && TryResolveBest(ctx, out IInteractable toggleTarget))
        {
            if (toggleTarget is IToggleInteractable toggleInteractable && toggleInteractable.CanToggle(ctx))
                toggleInteractable.Toggle(ctx);
        }

        HandlePickupIntent(intent, ctx);
    }

    // ---------------------------------------------------------------------
    // Public query API used by prompt/UI code
    // ---------------------------------------------------------------------

    public bool TryGetBestTarget(out IInteractable best, out InteractContext ctx)
    {
        best = null;

        if (!TryBuildContext(out ctx, out _))
            return false;

        return TryResolveBest(ctx, out best);
    }

    public bool TryGetBestPickupTarget(out IPickupInteractable best, out InteractContext ctx)
    {
        best = null;

        if (!TryBuildContext(out ctx, out _))
            return false;

        bool result = TryResolveBestPickup(ctx, out best);

        DebugMousePickup(
            $"TryGetBestPickupTarget result={result} hasAimWorld={ctx.HasAimWorld} " +
            $"aimWorld={ctx.AimWorld} best={DescribeTarget(best)}");

        return result;
    }

    public bool TryGetBestUnsecureTarget(out IUnsecureInteractable best, out InteractContext ctx)
    {
        best = null;

        if (!TryBuildContext(out ctx, out _))
            return false;

        return TryResolveBestUnsecure(ctx, out best);
    }

    public bool TryGetMouseHoverTarget(out InteractionHoverTarget target, out InteractContext ctx)
    {
        target = default;

        if (!TryBuildContext(out ctx, out _))
            return false;

        bool result = TryResolveMouseHoverTarget(ctx, out target);

        if (result)
        {
            DebugMousePickup(
                $"MouseHoverTarget owner={DescribeTarget(target.Owner)} collider={DescribeCollider(target.SourceCollider)} " +
                $"distance={GetDistanceToTarget(target, ctx):0.00}");
        }

        return result;
    }

    public bool IsWithinHoverNameRange(in InteractionHoverTarget target, in InteractContext ctx)
    {
        if (!target.IsValid)
            return false;

        float range = defaultHoverNameRange;

        if (target.RangeProvider != null &&
            target.RangeProvider.TryGetHoverNameRange(out float overrideRange))
        {
            range = Mathf.Max(0f, overrideRange);
        }

        return GetDistanceToTarget(target, ctx) <= range;
    }

    public bool IsWithinActionRange(in InteractionHoverTarget target, in InteractContext ctx)
    {
        if (!target.IsValid)
            return false;

        float range = defaultActionRange;

        if (target.RangeProvider != null &&
            target.RangeProvider.TryGetActionRange(out float overrideRange))
        {
            range = Mathf.Max(0f, overrideRange);
        }

        return GetDistanceToTarget(target, ctx) <= range;
    }

    public float GetDistanceToTarget(in InteractionHoverTarget target, in InteractContext ctx)
    {
        if (target.SourceCollider != null)
        {
            Vector2 closest = target.SourceCollider.ClosestPoint(ctx.Origin);
            return Vector2.Distance(ctx.Origin, closest);
        }

        if (target.Owner != null)
            return Vector2.Distance(ctx.Origin, target.Owner.transform.position);

        return float.PositiveInfinity;
    }

    /// <summary>
    /// Used by suppression logic: after interacting, keep the prompt hidden until the player leaves/re-enters.
    /// This intentionally uses the legacy nearby overlap rather than mouse hover.
    /// </summary>
    public bool IsCandidatePresent(IInteractable target)
    {
        if (target == null)
            return false;

        if (target is MonoBehaviour mb)
        {
            if (mb == null)
                return false;

            float d = Vector2.Distance(mb.transform.position, transform.position);
            if (d > overlapRadius * 1.25f)
                return false;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, overlapRadius, interactableMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryGetInteractable(hits[i], out IInteractable candidate) && ReferenceEquals(candidate, target))
                return true;
        }

        return false;
    }

    public bool IsPickupCandidatePresent(IPickupInteractable target)
    {
        if (target == null)
            return false;

        if (target is MonoBehaviour mb)
        {
            if (mb == null)
                return false;

            float d = Vector2.Distance(mb.transform.position, transform.position);
            if (d > overlapRadius * 1.25f)
                return false;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, overlapRadius, interactableMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryGetPickupInteractable(hits[i], out IPickupInteractable candidate) && ReferenceEquals(candidate, target))
                return true;
        }

        return false;
    }

    // ---------------------------------------------------------------------
    // Resolution entry points
    // ---------------------------------------------------------------------

    public bool TryResolveBest(in InteractContext ctx, out IInteractable best)
    {
        best = null;

        if (mouseOnlyTargeting)
            return preferMouseHoveredInteractable && TryResolveMouseHoveredInteractable(ctx, out best);

        if (preferMouseHoveredInteractable && TryResolveMouseHoveredInteractable(ctx, out best))
            return true;

        return TryResolveLegacyInteract(ctx, out best);
    }

    public bool TryResolveBestPickup(in InteractContext ctx, out IPickupInteractable best)
    {
        best = null;

        if (mouseOnlyTargeting)
            return applyMouseHoverToPickup && preferMouseHoveredInteractable && TryResolveMouseHoveredPickup(ctx, out best);

        if (applyMouseHoverToPickup &&
            preferMouseHoveredInteractable &&
            TryResolveMouseHoveredPickup(ctx, out best))
        {
            return true;
        }

        return TryResolveLegacyPickup(ctx, out best);
    }

    public bool TryResolveBestUnsecure(in InteractContext ctx, out IUnsecureInteractable best)
    {
        best = null;

        if (mouseOnlyTargeting)
            return preferMouseHoveredInteractable && TryResolveMouseHoveredUnsecure(ctx, out best);

        if (preferMouseHoveredInteractable && TryResolveMouseHoveredUnsecure(ctx, out best))
            return true;

        return TryResolveLegacyUnsecure(ctx, out best);
    }

    // ---------------------------------------------------------------------
    // Mouse hover resolution
    // ---------------------------------------------------------------------

    private bool TryResolveMouseHoveredInteractable(in InteractContext ctx, out IInteractable best)
    {
        best = null;

        if (!TryResolveMouseHoverTarget(ctx, out InteractionHoverTarget target))
            return false;

        if (target.Interact == null)
            return false;

        if (!IsWithinActionRange(target, ctx))
            return false;

        if (!target.Interact.CanInteract(ctx))
            return false;

        best = target.Interact;
        return true;
    }

    private bool TryResolveMouseHoveredPickup(in InteractContext ctx, out IPickupInteractable best)
    {
        best = null;

        if (!TryResolveMouseHoverTarget(ctx, out InteractionHoverTarget target))
            return false;

        if (target.Pickup == null)
            return false;

        if (!IsWithinActionRange(target, ctx))
            return false;

        if (!target.Pickup.CanPickup(ctx))
            return false;

        best = target.Pickup;
        return true;
    }

    private bool TryResolveMouseHoveredUnsecure(in InteractContext ctx, out IUnsecureInteractable best)
    {
        best = null;

        if (!TryResolveMouseHoverTarget(ctx, out InteractionHoverTarget target))
            return false;

        if (target.Unsecure == null)
            return false;

        if (!IsWithinActionRange(target, ctx))
            return false;

        if (!target.Unsecure.CanUnsecure(ctx))
            return false;

        best = target.Unsecure;
        return true;
    }

    private bool TryResolveMouseHoverTarget(in InteractContext ctx, out InteractionHoverTarget best)
    {
        best = default;

        if (!ctx.HasAimWorld)
            return false;

        Collider2D[] hits = GetMouseHoverHits(ctx.AimWorld);
        if (hits == null || hits.Length == 0)
            return false;

        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null)
                continue;

            if (!TryBuildMouseHoverTarget(col, out InteractionHoverTarget candidate))
                continue;

            float score = ScoreMouseHoverCandidate(candidate, col, ctx.AimWorld);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best.IsValid;
    }

    private bool TryBuildMouseHoverTarget(Collider2D col, out InteractionHoverTarget target)
    {
        target = default;

        if (col == null)
            return false;

        Transform current = col.transform;

        while (current != null)
        {
            IInteractable interact = current.GetComponent<IInteractable>();
            IPickupInteractable pickup = current.GetComponent<IPickupInteractable>();
            IUnsecureInteractable unsecure = current.GetComponent<IUnsecureInteractable>();
            IToggleInteractable toggle = current.GetComponent<IToggleInteractable>();

            if (interact == null && pickup == null && unsecure == null && toggle == null)
            {
                current = current.parent;
                continue;
            }

            MonoBehaviour owner = interact as MonoBehaviour;
            if (owner == null) owner = pickup as MonoBehaviour;
            if (owner == null) owner = unsecure as MonoBehaviour;
            if (owner == null) owner = toggle as MonoBehaviour;
            if (owner == null) owner = current.GetComponent<MonoBehaviour>();

            IInteractPromptProvider promptProvider = interact as IInteractPromptProvider;
            if (promptProvider == null) promptProvider = pickup as IInteractPromptProvider;
            if (promptProvider == null) promptProvider = owner as IInteractPromptProvider;
            if (promptProvider == null) promptProvider = current.GetComponent<IInteractPromptProvider>();

            IPickupPromptProvider pickupPromptProvider = pickup as IPickupPromptProvider;
            if (pickupPromptProvider == null) pickupPromptProvider = owner as IPickupPromptProvider;
            if (pickupPromptProvider == null) pickupPromptProvider = current.GetComponent<IPickupPromptProvider>();

            IInteractPromptActionProvider actionProvider = interact as IInteractPromptActionProvider;
            if (actionProvider == null) actionProvider = pickup as IInteractPromptActionProvider;
            if (actionProvider == null) actionProvider = owner as IInteractPromptActionProvider;
            if (actionProvider == null) actionProvider = current.GetComponent<IInteractPromptActionProvider>();

            IInteractionLabelProvider labelProvider = interact as IInteractionLabelProvider;
            if (labelProvider == null) labelProvider = pickup as IInteractionLabelProvider;
            if (labelProvider == null) labelProvider = owner as IInteractionLabelProvider;
            if (labelProvider == null) labelProvider = current.GetComponent<IInteractionLabelProvider>();

            IInteractionRangeProvider rangeProvider = interact as IInteractionRangeProvider;
            if (rangeProvider == null) rangeProvider = pickup as IInteractionRangeProvider;
            if (rangeProvider == null) rangeProvider = owner as IInteractionRangeProvider;
            if (rangeProvider == null) rangeProvider = current.GetComponent<IInteractionRangeProvider>();

            target = new InteractionHoverTarget(
                col,
                owner,
                interact,
                pickup,
                unsecure,
                toggle,
                promptProvider,
                pickupPromptProvider,
                actionProvider,
                labelProvider,
                rangeProvider);

            return true;
        }

        return false;
    }

    private Collider2D[] GetMouseHoverHits(Vector2 mouseWorld)
    {
        Collider2D[] pointHits = Physics2D.OverlapPointAll(mouseWorld, interactableMask);
        if (ContainsMouseUsefulTarget(pointHits))
            return pointHits;

        if (mouseHoverRadius <= 0f)
            return pointHits;

        Collider2D[] radiusHits = Physics2D.OverlapCircleAll(mouseWorld, mouseHoverRadius, interactableMask);
        if (radiusHits != null && radiusHits.Length > 0)
            return radiusHits;

        return pointHits;
    }

    private bool ContainsMouseUsefulTarget(Collider2D[] hits)
    {
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && TryBuildMouseHoverTarget(hits[i], out _))
                return true;
        }

        return false;
    }

    private static float ScoreMouseHoverCandidate(in InteractionHoverTarget target, Collider2D col, Vector2 mouseWorld)
    {
        float distToMouse = Vector2.Distance(mouseWorld, col.ClosestPoint(mouseWorld));
        float distToCenter = Vector2.Distance(mouseWorld, col.bounds.center);
        float area = Mathf.Max(0.0001f, col.bounds.size.x * col.bounds.size.y);
        int sortingOrder = GetBestSortingOrder(col);
        int priority = GetHoverPriority(target);

        return
            (priority * 1000f) +
            (sortingOrder * 0.1f) -
            (distToMouse * 100f) -
            (distToCenter * 2f) -
            (area * 0.01f);
    }

    private static int GetHoverPriority(in InteractionHoverTarget target)
    {
        int priority = 0;

        if (target.Interact != null)
            priority = Mathf.Max(priority, target.Interact.InteractionPriority);

        if (target.Pickup != null)
            priority = Mathf.Max(priority, target.Pickup.PickupPriority);

        if (target.Unsecure is IInteractable unsecureAsInteract)
            priority = Mathf.Max(priority, unsecureAsInteract.InteractionPriority);

        return priority;
    }

    // ---------------------------------------------------------------------
    // Legacy fallback resolution
    // ---------------------------------------------------------------------

    private bool TryResolveLegacyInteract(in InteractContext ctx, out IInteractable best)
    {
        best = null;
        float bestScore = float.NegativeInfinity;

        if (useRaycast)
        {
            RaycastHit2D hit = Physics2D.Raycast(ctx.Origin, ctx.AimDir, rayDistance, interactableMask);
            if (hit.collider != null &&
                TryGetInteractable(hit.collider, out IInteractable interactable) &&
                interactable.CanInteract(ctx))
            {
                best = interactable;
                bestScore = 1000f + interactable.InteractionPriority;
            }
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(ctx.Origin, overlapRadius, interactableMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (!TryGetInteractable(col, out IInteractable interactable))
                continue;

            if (!interactable.CanInteract(ctx))
                continue;

            float score = ScoreLegacyCandidate(col, ctx, interactable.InteractionPriority);
            if (score > bestScore)
            {
                bestScore = score;
                best = interactable;
            }
        }

        return best != null;
    }

    private bool TryResolveLegacyPickup(in InteractContext ctx, out IPickupInteractable best)
    {
        best = null;
        float bestScore = float.NegativeInfinity;

        if (useRaycast)
        {
            RaycastHit2D hit = Physics2D.Raycast(ctx.Origin, ctx.AimDir, rayDistance, interactableMask);
            if (hit.collider != null &&
                TryGetPickupInteractable(hit.collider, out IPickupInteractable pickup) &&
                pickup.CanPickup(ctx))
            {
                best = pickup;
                bestScore = 1000f + pickup.PickupPriority;
            }
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(ctx.Origin, overlapRadius, interactableMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (!TryGetPickupInteractable(col, out IPickupInteractable pickup))
                continue;

            if (!pickup.CanPickup(ctx))
                continue;

            float score = ScoreLegacyCandidate(col, ctx, pickup.PickupPriority);
            if (score > bestScore)
            {
                bestScore = score;
                best = pickup;
            }
        }

        return best != null;
    }

    private bool TryResolveLegacyUnsecure(in InteractContext ctx, out IUnsecureInteractable best)
    {
        best = null;
        float bestScore = float.NegativeInfinity;

        if (useRaycast)
        {
            RaycastHit2D hit = Physics2D.Raycast(ctx.Origin, ctx.AimDir, rayDistance, interactableMask);
            if (hit.collider != null &&
                TryGetUnsecureInteractable(hit.collider, out IUnsecureInteractable unsecure) &&
                unsecure.CanUnsecure(ctx))
            {
                best = unsecure;
                bestScore = 1000f + GetUnsecurePriority(unsecure);
            }
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(ctx.Origin, overlapRadius, interactableMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (!TryGetUnsecureInteractable(col, out IUnsecureInteractable unsecure))
                continue;

            if (!unsecure.CanUnsecure(ctx))
                continue;

            float score = ScoreLegacyCandidate(col, ctx, GetUnsecurePriority(unsecure));
            if (score > bestScore)
            {
                bestScore = score;
                best = unsecure;
            }
        }

        return best != null;
    }

    private float ScoreLegacyCandidate(Collider2D col, in InteractContext ctx, int priority)
    {
        Vector2 to = (Vector2)col.transform.position - ctx.Origin;
        float distScore = -to.magnitude;

        float front = 0f;
        if (to.sqrMagnitude > 0.0001f)
            front = Vector2.Dot(ctx.AimDir, to.normalized);

        return
            (priority * 10f) +
            (distScore * (1f - aimBias)) +
            (front * aimBias * 2f);
    }

    // ---------------------------------------------------------------------
    // Pickup hold execution
    // ---------------------------------------------------------------------

    private void HandlePickupIntent(InteractionIntent intent, in InteractContext ctx)
    {
        bool hasPickupTarget = TryResolveBestPickup(ctx, out IPickupInteractable pickupTarget);

        if (!hasPickupTarget || pickupTarget == null)
        {
            ResetHoldPickup();
            return;
        }

        if (pickupTarget.PickupMode == PickupInteractionMode.Instant)
        {
            if (intent.PickupPressed && pickupTarget.CanPickup(ctx))
            {
                pickupTarget.Pickup(ctx);
                OnPickedUp?.Invoke(pickupTarget);
            }

            ResetHoldPickup();
            return;
        }

        if (!intent.PickupHeld)
        {
            ResetHoldPickup();
            return;
        }

        if (!ReferenceEquals(_activeHoldPickupTarget, pickupTarget))
        {
            _activeHoldPickupTarget = pickupTarget;
            _activeHoldPickupElapsed = 0f;
            _holdPickupTriggered = false;
        }

        if (_holdPickupTriggered)
            return;

        if (!pickupTarget.CanPickup(ctx))
        {
            ResetHoldPickup();
            return;
        }

        _activeHoldPickupElapsed += Time.deltaTime;

        if (_activeHoldPickupElapsed >= pickupTarget.PickupHoldDuration)
        {
            pickupTarget.Pickup(ctx);
            OnPickedUp?.Invoke(pickupTarget);
            _holdPickupTriggered = true;
            ResetHoldPickup();
        }

        if (intent.PickupReleased)
            ResetHoldPickup();
    }

    private void ResetHoldPickup()
    {
        _activeHoldPickupTarget = null;
        _activeHoldPickupElapsed = 0f;
        _holdPickupTriggered = false;
    }

    // ---------------------------------------------------------------------
    // Component lookup helpers
    // ---------------------------------------------------------------------

    private bool TryGetInteractable(Collider2D col, out IInteractable interactable)
    {
        interactable = null;

        if (col == null)
            return false;

        interactable = col.GetComponent<IInteractable>();
        if (interactable != null)
            return true;

        interactable = col.GetComponentInParent<IInteractable>();
        return interactable != null;
    }

    private bool TryGetPickupInteractable(Collider2D col, out IPickupInteractable pickup)
    {
        pickup = null;

        if (col == null)
            return false;

        pickup = col.GetComponent<IPickupInteractable>();
        if (pickup != null)
            return true;

        pickup = col.GetComponentInParent<IPickupInteractable>();
        return pickup != null;
    }

    private bool TryGetUnsecureInteractable(Collider2D col, out IUnsecureInteractable unsecure)
    {
        unsecure = null;

        if (col == null)
            return false;

        unsecure = col.GetComponent<IUnsecureInteractable>();
        if (unsecure != null)
            return true;

        unsecure = col.GetComponentInParent<IUnsecureInteractable>();
        return unsecure != null;
    }

    private static int GetUnsecurePriority(IUnsecureInteractable unsecure)
    {
        if (unsecure is IInteractable interactable)
            return interactable.InteractionPriority;

        return 0;
    }

    private bool TryBuildContext(out InteractContext ctx, out InteractionIntent intent)
    {
        ctx = default;
        intent = default;

        if (intentSource == null)
            return false;

        intent = intentSource.Current;
        Vector2 origin = transform.position;
        Vector2 aimDir = GetAimDir(origin, intent.AimWorld);

        ctx = new InteractContext(
            gameObject,
            transform,
            origin,
            aimDir,
            intent.AimWorld,
            intent.HasAimWorld);

        return true;
    }

    private Vector2 GetAimDir(Vector2 origin, Vector2 aimWorld)
    {
        if (aimWorld != Vector2.zero)
        {
            Vector2 d = aimWorld - origin;
            if (d.sqrMagnitude > 0.0001f)
                return d.normalized;
        }

        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        return new Vector2(facing, 0f);
    }

    private static int GetBestSortingOrder(Collider2D col)
    {
        if (col == null)
            return 0;

        SpriteRenderer sr = col.GetComponent<SpriteRenderer>();
        if (sr != null)
            return sr.sortingOrder;

        sr = col.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null)
            return sr.sortingOrder;

        sr = col.GetComponentInParent<SpriteRenderer>();
        if (sr != null)
            return sr.sortingOrder;

        Renderer renderer = col.GetComponent<Renderer>();
        if (renderer != null)
            return renderer.sortingOrder;

        renderer = col.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
            return renderer.sortingOrder;

        renderer = col.GetComponentInParent<Renderer>();
        if (renderer != null)
            return renderer.sortingOrder;

        return 0;
    }

    // ---------------------------------------------------------------------
    // Debug
    // ---------------------------------------------------------------------

    private void DebugMousePickup(string message)
    {
        if (!debugMousePickupResolution)
            return;

        if (debugMousePickupEveryNFrames > 1 &&
            Time.frameCount % debugMousePickupEveryNFrames != 0)
            return;

        Debug.Log($"[Interactor2D:{name}] {message}", this);
    }

    private static string DescribeTarget(object target)
    {
        if (target == null)
            return "null";

        if (target is MonoBehaviour mb)
            return $"{target.GetType().Name}('{mb.name}')";

        return target.GetType().Name;
    }

    private static string DescribeCollider(Collider2D col)
    {
        if (col == null)
            return "null";

        return $"{col.name} layer={LayerMask.LayerToName(col.gameObject.layer)} trigger={col.isTrigger}";
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, overlapRadius);

        if (useRaycast)
        {
            Vector3 dir = transform.localScale.x >= 0f ? Vector3.right : Vector3.left;
            Gizmos.DrawLine(transform.position, transform.position + dir * rayDistance);
        }
    }
#endif
}
