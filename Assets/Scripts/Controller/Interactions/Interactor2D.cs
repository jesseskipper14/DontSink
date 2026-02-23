using UnityEngine;

/// <summary>
/// World-authoritative interaction resolver.
/// Evaluates overlap + optional raycast, then selects best IInteractable by priority + score.
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

    private IInteractionIntentSource intentSource;

    /// <summary>
    /// Fired only when an interaction actually occurs (E pressed, target found, Interact called).
    /// </summary>
    public event System.Action<IInteractable> OnInteracted;

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

        ctx = new InteractContext(gameObject, transform, origin, aimDir);
        return TryResolveBest(ctx, out best);
    }

    /// <summary>
    /// Returns true if this target is currently in range per our overlap query.
    /// Used by prompt logic to know when the player has "left and re-entered".
    /// </summary>
    public bool IsCandidatePresent(IInteractable target)
    {
        if (target == null) return false;

        // Fast path: if target is a MonoBehaviour, check distance first.
        if (target is MonoBehaviour mb)
        {
            float d = Vector2.Distance((Vector2)mb.transform.position, (Vector2)transform.position);
            if (d > overlapRadius * 1.25f) // small slack
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

    private void Update()
    {
        var intent = intentSource.Current;
        if (!intent.InteractPressed) return;

        Vector2 origin = transform.position;

        // Determine aim direction:
        // - If we have a world aim point, use it.
        // - Else fall back to facing based on localScale.x
        Vector2 aimDir = GetAimDir(origin, intent.AimWorld);

        var ctx = new InteractContext(gameObject, transform, origin, aimDir);

        if (TryResolveBest(ctx, out var target))
        {
            target.Interact(ctx);

            // NEW: tell UI (and other listeners) that we interacted
            OnInteracted?.Invoke(target);
        }
    }

    private Vector2 GetAimDir(Vector2 origin, Vector2 aimWorld)
    {
        if (aimWorld != Vector2.zero)
        {
            var d = aimWorld - origin;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }

        // cheap fallback: left/right based on scale
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        return new Vector2(facing, 0f);
    }

    public bool TryResolveBest(in InteractContext ctx, out IInteractable best)
    {
        best = null;

        float bestScore = float.NegativeInfinity;

        // Optional raycast: great for "what I'm looking at"
        if (useRaycast)
        {
            var hit = Physics2D.Raycast(ctx.Origin, ctx.AimDir, rayDistance, interactableMask);
            if (hit.collider != null)
            {
                if (TryGetInteractable(hit.collider, out var i) && i.CanInteract(ctx))
                {
                    // Give ray hit a big base advantage
                    float score = 1000f + i.InteractionPriority;
                    best = i;
                    bestScore = score;
                }
            }
        }

        // Overlap: great for "what I'm near"
        var hits = Physics2D.OverlapCircleAll(ctx.Origin, overlapRadius, interactableMask);
        for (int n = 0; n < hits.Length; n++)
        {
            var col = hits[n];
            if (!TryGetInteractable(col, out var i)) continue;
            if (!i.CanInteract(ctx)) continue;

            Vector2 to = (Vector2)col.transform.position - ctx.Origin;
            float dist = to.magnitude;
            float distScore = -dist; // closer is better

            float front = 0f;
            if (to.sqrMagnitude > 0.0001f)
                front = Vector2.Dot(ctx.AimDir, to.normalized); // -1..1

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

    private bool TryGetInteractable(Collider2D col, out IInteractable interactable)
    {
        // allow either the collider object or a parent to host the interactable
        interactable = col.GetComponent<IInteractable>();
        if (interactable != null) return true;

        interactable = col.GetComponentInParent<IInteractable>();
        return interactable != null;
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