using UnityEngine;

[DisallowMultipleComponent]
public class HatchConnectionAuthoring : MonoBehaviour
{
    [Header("References")]
    public Boat boat;                     // optional override; auto-found if null
    public Hatch hatch;                   // optional override; auto-found if null
    public BoxCollider2D openingCollider; // recommended; defines the opening rectangle

    [Header("Resolve Settings")]
    [Min(0.01f)] public float sampleOffset = 0.6f; // how far to sample left/right to find compartments
    public bool autoResolveInEditor = false;

    // Optional: if your hatches are rotated and you want a different axis, you can swap this later.
    public Vector2 ThroughAxisWorld => transform.right;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!autoResolveInEditor) return;
        if (!UnityEditor.EditorApplication.isPlaying)
            ResolveAndApply();
    }
#endif

    /// <summary>
    /// Resolve A/B compartments and apply/update a CompartmentConnection on the owning boat.
    /// Safe to call in editor.
    /// </summary>
    public void ResolveAndApply()
    {
        if (boat == null) boat = GetComponentInParent<Boat>();
        if (hatch == null) hatch = GetComponent<Hatch>();
        if (openingCollider == null) openingCollider = GetComponent<BoxCollider2D>();

        if (boat == null)
        {
            Debug.LogWarning($"[{name}] No Boat found in parents. Select a Boat root before resolving.", this);
            return;
        }

        if (openingCollider == null)
        {
            Debug.LogWarning($"[{name}] No BoxCollider2D assigned/found for openingCollider. Add one to the hatch for connection geometry.", this);
            return;
        }

        // 1) Resolve compartments by point sampling
        Vector2 axis = ThroughAxisWorld.normalized;
        Vector2 center =
            openingCollider.transform.TransformPoint(openingCollider.offset);

        Vector2 pA = center - axis * sampleOffset;
        Vector2 pB = center + axis * sampleOffset;

        Compartment A = FindContainingCompartment(boat, pA);
        Compartment B = FindContainingCompartment(boat, pB);

        // 2) Create or update the connection entry for this hatch transform
        var conn = FindOrCreateConnectionForTransform(boat, transform);

        conn.transform = transform;
        conn.A = A;
        conn.B = B;

        // 3) Set opening bounds from BoxCollider2D in LOCAL space of this transform
        // openingCollider is on same GameObject typically; its offset/size are local.
        float left = openingCollider.offset.x - openingCollider.size.x * 0.5f;
        float right = openingCollider.offset.x + openingCollider.size.x * 0.5f;
        float bottom = openingCollider.offset.y - openingCollider.size.y * 0.5f;
        float top = openingCollider.offset.y + openingCollider.size.y * 0.5f;

        conn.openingLeftX = left;
        conn.openingRightX = right;
        conn.openingBottomY = bottom;
        conn.openingTopY = top;

        // 4) Optional: mirror hatch open/close into connection state (legacy-friendly)
        if (hatch != null)
            conn.isOpen = hatch.isOpen;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(boat);
        UnityEditor.EditorUtility.SetDirty(this);
        if (hatch != null) UnityEditor.EditorUtility.SetDirty(hatch);
#endif
    }

    private static Compartment FindContainingCompartment(Boat boat, Vector2 worldPoint)
    {
        foreach (var c in boat.Compartments)
        {
            if (c == null) continue;

            // 1) Try authoritative polygon first
            var poly = c.GetWorldCorners();
            if (poly != null && poly.Length >= 3)
            {
                if (ConvexPolygonUtil.PointInsideConvex(worldPoint, poly))
                    return c;
            }

            // 2) Fallback: BoxCollider2D bounds (THIS is the fix)
            var box = c.GetComponent<BoxCollider2D>();
            if (box != null && box.bounds.Contains(worldPoint))
                return c;
        }

        return null;
    }

    private static CompartmentConnection FindOrCreateConnectionForTransform(Boat boat, Transform t)
    {
        // Try to find existing connection referencing this transform
        foreach (var existing in boat.Connections)
        {
            if (existing != null && existing.transform == t)
                return existing;
        }

        // Create new
        var created = new CompartmentConnection
        {
            transform = t,
            isOpen = true
        };

        boat.Connections.Add(created);
        return created;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualize sampling points in editor
        Vector2 axis = ThroughAxisWorld.normalized;
        Vector2 c = transform.position;

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(c - axis * sampleOffset, 0.05f);
        Gizmos.DrawSphere(c + axis * sampleOffset, 0.05f);

        Gizmos.DrawLine(c - axis * sampleOffset, c + axis * sampleOffset);
    }
#endif
}
