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

    [Header("Debug")]
    [SerializeField] private bool debugSnap = false;

    private void Reset()
    {
        bodyCollider = GetComponentInChildren<Collider2D>();
    }

    public bool TrySnapToGround()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * rayStartAbove;
        float distance = rayStartAbove + rayDistanceDown;

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

            if (!found || hit.distance < bestHit.distance)
            {
                bestHit = hit;
                found = true;
            }
        }

        if (!found)
        {
            DebugSnap("Only trigger hits found.");
            return false;
        }

        float currentBottom = GetCurrentBottomY();
        float deltaY = bestHit.point.y - currentBottom + bottomOffset;

        transform.position += Vector3.up * deltaY;

        DebugSnap($"Snapped to {bestHit.collider.name}, deltaY={deltaY:0.000}");
        return true;
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