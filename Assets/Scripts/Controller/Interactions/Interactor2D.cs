using UnityEngine;

/// <summary>
/// World-authoritative interaction resolver.
/// Evaluates overlap + optional raycast, then selects best target by priority + score.
/// Supports separate intent lanes for Interact and Pickup.
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
    [SerializeField] private float aimBias = 0.35f; // how much "in front" matters vs distance

    [Header("Mouse Hover Override")]
    [SerializeField] private bool preferMouseHoveredInteractable = true;
    [SerializeField, Min(0f)] private float mouseHoverRadius = 0.08f;
    [SerializeField] private bool applyMouseHoverToPickup = true;

    private IPickupInteractable _activeHoldPickupTarget;
    private float _activeHoldPickupElapsed;
    private bool _holdPickupTriggered;

    public IPickupInteractable ActiveHoldPickupTarget => _activeHoldPickupTarget;
    public float ActiveHoldPickupElapsed => _activeHoldPickupElapsed;
    public float ActiveHoldPickupDuration =>
        _activeHoldPickupTarget != null ? Mathf.Max(0.0001f, _activeHoldPickupTarget.PickupHoldDuration) : 0f;
    public float ActiveHoldPickupProgress =>
        _activeHoldPickupTarget != null
            ? Mathf.Clamp01(_activeHoldPickupElapsed / Mathf.Max(0.0001f, _activeHoldPickupTarget.PickupHoldDuration))
            : 0f;
    public bool IsHoldingPickup =>
        _activeHoldPickupTarget != null && !_holdPickupTriggered;

    private IInteractionIntentSource intentSource;

    /// <summary>
    /// Fired only when a normal interaction actually occurs.
    /// </summary>
    public event System.Action<IInteractable> OnInteracted;

    /// <summary>
    /// Fired only when a pickup actually occurs.
    /// </summary>
    public event System.Action<IPickupInteractable> OnPickedUp;

    private void Awake()
    {
        intentSource = GetComponent<IInteractionIntentSource>();
        if (intentSource == null)
        {
            Debug.LogError("Interactor2D requires IInteractionIntentSource (e.g., LocalInteractionIntentSource).");
            enabled = false;
        }
    }

    public bool TryGetBestTarget(out IInteractable best, out InteractContext ctx)
    {
        best = null;

        if (intentSource == null)
        {
            ctx = default;
            return false;
        }

        var intent = intentSource.Current;
        Vector2 origin = transform.position;
        Vector2 aimDir = GetAimDir(origin, intent.AimWorld);

        ctx = new InteractContext(
            gameObject,
            transform,
            origin,
            aimDir,
            intent.AimWorld,
            intent.HasAimWorld);

        return TryResolveBest(ctx, out best);
    }

    public bool TryGetBestPickupTarget(out IPickupInteractable best, out InteractContext ctx)
    {
        best = null;

        if (intentSource == null)
        {
            ctx = default;
            return false;
        }

        var intent = intentSource.Current;
        Vector2 origin = transform.position;
        Vector2 aimDir = GetAimDir(origin, intent.AimWorld);

        ctx = new InteractContext(gameObject, transform, origin, aimDir);
        return TryResolveBestPickup(ctx, out best);
    }

    /// <summary>
    /// Returns true if this target is currently in range per our overlap query.
    /// Used by prompt logic to know when the player has "left and re-entered".
    /// </summary>
    public bool IsCandidatePresent(IInteractable target)
    {
        if (target == null) return false;

        if (target is MonoBehaviour mb)
        {
            if (mb == null) return false;

            float d = Vector2.Distance((Vector2)mb.transform.position, (Vector2)transform.position);
            if (d > overlapRadius * 1.25f)
                return false;
        }

        var hits = Physics2D.OverlapCircleAll(transform.position, overlapRadius, interactableMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryGetInteractable(hits[i], out var it) && ReferenceEquals(it, target))
                return true;
        }

        return false;
    }

    public bool IsPickupCandidatePresent(IPickupInteractable target)
    {
        if (target == null) return false;

        if (target is MonoBehaviour mb)
        {
            if (mb == null) return false;

            float d = Vector2.Distance((Vector2)mb.transform.position, (Vector2)transform.position);
            if (d > overlapRadius * 1.25f)
                return false;
        }

        var hits = Physics2D.OverlapCircleAll(transform.position, overlapRadius, interactableMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryGetPickupInteractable(hits[i], out var it) && ReferenceEquals(it, target))
                return true;
        }

        return false;
    }

    private void Update()
    {
        var intent = intentSource.Current;
        Vector2 origin = transform.position;
        Vector2 aimDir = GetAimDir(origin, intent.AimWorld);
        var ctx = new InteractContext(
            gameObject,
            transform,
            origin,
            aimDir,
            intent.AimWorld,
            intent.HasAimWorld);

        if (intent.InteractPressed && TryResolveBest(ctx, out var interactTarget))
        {
            interactTarget.Interact(ctx);
            OnInteracted?.Invoke(interactTarget);
        }

        if (intent.TogglePressed && TryResolveBest(ctx, out var toggleTarget))
        {
            if (toggleTarget is IToggleInteractable toggleInteractable && toggleInteractable.CanToggle(ctx))
                toggleInteractable.Toggle(ctx);
        }

        HandlePickupIntent(intent, ctx);
    }

    private Vector2 GetAimDir(Vector2 origin, Vector2 aimWorld)
    {
        if (aimWorld != Vector2.zero)
        {
            var d = aimWorld - origin;
            if (d.sqrMagnitude > 0.0001f)
                return d.normalized;
        }

        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        return new Vector2(facing, 0f);
    }

    public bool TryResolveBest(in InteractContext ctx, out IInteractable best)
    {
        best = null;
        float bestScore = float.NegativeInfinity;

        if (preferMouseHoveredInteractable &&
        TryResolveMouseHoveredInteractable(ctx, out best))
            {
                return true;
            }

        if (useRaycast)
        {
            var hit = Physics2D.Raycast(ctx.Origin, ctx.AimDir, rayDistance, interactableMask);
            if (hit.collider != null)
            {
                if (TryGetInteractable(hit.collider, out var i) && i.CanInteract(ctx))
                {
                    float score = 1000f + i.InteractionPriority;
                    best = i;
                    bestScore = score;
                }
            }
        }

        var hits = Physics2D.OverlapCircleAll(ctx.Origin, overlapRadius, interactableMask);
        for (int n = 0; n < hits.Length; n++)
        {
            var col = hits[n];
            if (!TryGetInteractable(col, out var i)) continue;
            if (!i.CanInteract(ctx)) continue;

            Vector2 to = (Vector2)col.transform.position - ctx.Origin;
            float dist = to.magnitude;
            float distScore = -dist;

            float front = 0f;
            if (to.sqrMagnitude > 0.0001f)
                front = Vector2.Dot(ctx.AimDir, to.normalized);

            float score =
                (i.InteractionPriority * 10f) +
                (distScore * (1f - aimBias)) +
                (front * aimBias * 2f);

            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best != null;
    }

    public bool TryResolveBestPickup(in InteractContext ctx, out IPickupInteractable best)
    {
        best = null;
        float bestScore = float.NegativeInfinity;

        if (applyMouseHoverToPickup &&
        preferMouseHoveredInteractable &&
        TryResolveMouseHoveredPickup(ctx, out best))
            {
                return true;
            }

        if (useRaycast)
        {
            var hit = Physics2D.Raycast(ctx.Origin, ctx.AimDir, rayDistance, interactableMask);
            if (hit.collider != null)
            {
                if (TryGetPickupInteractable(hit.collider, out var i) && i.CanPickup(ctx))
                {
                    float score = 1000f + i.PickupPriority;
                    best = i;
                    bestScore = score;
                }
            }
        }

        var hits = Physics2D.OverlapCircleAll(ctx.Origin, overlapRadius, interactableMask);
        for (int n = 0; n < hits.Length; n++)
        {
            var col = hits[n];
            if (!TryGetPickupInteractable(col, out var i)) continue;
            if (!i.CanPickup(ctx)) continue;

            Vector2 to = (Vector2)col.transform.position - ctx.Origin;
            float dist = to.magnitude;
            float distScore = -dist;

            float front = 0f;
            if (to.sqrMagnitude > 0.0001f)
                front = Vector2.Dot(ctx.AimDir, to.normalized);

            float score =
                (i.PickupPriority * 10f) +
                (distScore * (1f - aimBias)) +
                (front * aimBias * 2f);

            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best != null;
    }

    private bool TryGetInteractable(Collider2D col, out IInteractable interactable)
    {
        interactable = col.GetComponent<IInteractable>();
        if (interactable != null) return true;

        interactable = col.GetComponentInParent<IInteractable>();
        return interactable != null;
    }

    private bool TryGetPickupInteractable(Collider2D col, out IPickupInteractable interactable)
    {
        interactable = col.GetComponent<IPickupInteractable>();
        if (interactable != null) return true;

        interactable = col.GetComponentInParent<IPickupInteractable>();
        return interactable != null;
    }

    private void HandlePickupIntent(InteractionIntent intent, in InteractContext ctx)
    {
        bool hasPickupTarget = TryResolveBestPickup(ctx, out var pickupTarget);

        if (!hasPickupTarget || pickupTarget == null)
        {
            ResetHoldPickup();
            return;
        }

        if (pickupTarget.PickupMode == PickupInteractionMode.Instant)
        {
            if (intent.PickupPressed)
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

    private bool TryResolveMouseHoveredInteractable(
    in InteractContext ctx,
    out IInteractable best)
    {
        best = null;

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

            if (!TryGetInteractable(col, out IInteractable interactable))
                continue;

            if (!interactable.CanInteract(ctx))
                continue;

            float distToMouse = Vector2.Distance(ctx.AimWorld, col.ClosestPoint(ctx.AimWorld));

            // Mouse hover wins over normal priority, but if multiple hovered things overlap,
            // still prefer priority and closeness. Because chaos needs at least a filing cabinet.
            float score =
                (interactable.InteractionPriority * 10f) -
                distToMouse;

            if (score > bestScore)
            {
                bestScore = score;
                best = interactable;
            }
        }

        return best != null;
    }

    private bool TryResolveMouseHoveredPickup(
        in InteractContext ctx,
        out IPickupInteractable best)
    {
        best = null;

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

            if (!TryGetPickupInteractable(col, out IPickupInteractable pickup))
                continue;

            if (!pickup.CanPickup(ctx))
                continue;

            float distToMouse = Vector2.Distance(ctx.AimWorld, col.ClosestPoint(ctx.AimWorld));

            float score =
                (pickup.PickupPriority * 10f) -
                distToMouse;

            if (score > bestScore)
            {
                bestScore = score;
                best = pickup;
            }
        }

        return best != null;
    }

    private Collider2D[] GetMouseHoverHits(Vector2 mouseWorld)
    {
        Collider2D[] pointHits = Physics2D.OverlapPointAll(mouseWorld, interactableMask);
        if (pointHits != null && pointHits.Length > 0)
            return pointHits;

        if (mouseHoverRadius <= 0f)
            return pointHits;

        return Physics2D.OverlapCircleAll(mouseWorld, mouseHoverRadius, interactableMask);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, overlapRadius);

        if (useRaycast)
        {
            Vector3 dir = (transform.localScale.x >= 0f) ? Vector3.right : Vector3.left;
            Gizmos.DrawLine(transform.position, transform.position + dir * rayDistance);
        }
    }
#endif
}