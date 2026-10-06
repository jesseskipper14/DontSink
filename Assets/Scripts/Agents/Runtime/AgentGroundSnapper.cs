using UnityEngine;

[DisallowMultipleComponent]
public sealed class AgentGroundSnapper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Collider2D bodyCollider;

    [Header("Ground Snap")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField, Min(0f)] private float rayStartAbove = 2f;
    [SerializeField, Min(0f)] private float rayDistanceDown = 8f;
    [SerializeField] private float bottomOffset = 0.02f;
    [SerializeField, Min(0f)] private float maximumStepUp = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugSnap = false;

    private void Reset()
    {
        bodyCollider = GetComponentInChildren<Collider2D>();
    }

    public bool TrySnapToGround()
    {
        // Spawn/parent placement can precede the physics transform update.
        Physics2D.SyncTransforms();
        if (!TryGetGroundedPosition(transform.position, true, out var grounded)) return false;
        var body = GetComponent<Rigidbody2D>();
        if (body != null) body.position = grounded;
        transform.position = grounded;
        return true;
    }

    /// <summary>Resolve support at a proposed position before a kinematic MovePosition.</summary>
    public bool TryGetGroundedPosition(Vector3 proposed, out Vector3 grounded)
        => TryGetGroundedPosition(proposed, false, out grounded);

    private bool TryGetGroundedPosition(Vector3 proposed, bool initialSpawn, out Vector3 grounded)
    {
        grounded = proposed;
        if (bodyCollider == null)
            foreach (var collider in GetComponentsInChildren<Collider2D>())
                if (!collider.isTrigger) { bodyCollider = collider; break; }
        Vector2 origin = (Vector2)proposed + Vector2.up * rayStartAbove;
        float distance = rayStartAbove + rayDistanceDown;
        float bottomRelativeToRoot = GetCurrentBottomY() - transform.position.y;
        if (initialSpawn)
        {
            // A composite quay may contain the spawn point and the original ray origin.
            // Start outside those solids, then cast onto the actual surface at this X.
            float top = origin.y;
            RaiseAboveContainingGround(new Vector2(proposed.x, proposed.y), ref top);
            RaiseAboveContainingGround(new Vector2(proposed.x, proposed.y + bottomRelativeToRoot + .01f), ref top);
            distance += top - origin.y;
            origin.y = top;
        }

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, distance, groundMask);

        if (hits == null || hits.Length == 0)
        {
            DebugSnap("No ground hit.");
            return false;
        }

        RaycastHit2D bestHit = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];

            if (hit.collider == null)
                continue;

            if (hit.collider.isTrigger)
                continue;
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform) ||
                hit.distance <= .0001f || hit.normal.y < .25f ||
                (!initialSpawn && hit.point.y > proposed.y + bottomRelativeToRoot + maximumStepUp))
                continue;

            if (!found || hit.distance < bestHit.distance)
            {
                bestHit = hit;
                found = true;
            }
        }

        if (!found)
        {
            DebugSnap("No valid support surface found.");
            return false;
        }

        grounded.y = bestHit.point.y - bottomRelativeToRoot + bottomOffset;

        DebugSnap($"Supported by {bestHit.collider.name}, deltaY={grounded.y - proposed.y:0.000}");
        return true;
    }

    private void RaiseAboveContainingGround(Vector2 point, ref float originY)
    {
        foreach (var collider in Physics2D.OverlapPointAll(point, groundMask))
        {
            if (collider.isTrigger || collider.transform == transform || collider.transform.IsChildOf(transform)) continue;
            originY = Mathf.Max(originY, collider.bounds.max.y + Mathf.Max(.1f, rayStartAbove));
        }
    }

    private float GetCurrentBottomY()
    {
        if (bodyCollider != null)
            return bodyCollider.bounds.min.y;

        return transform.position.y;
    }

    private void DebugSnap(string message)
    {
        if (!debugSnap)
            return;

        Debug.Log($"[AgentGroundSnapper:{name}] {message}", this);
    }
}
