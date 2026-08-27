using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class BoatBuilderSurfaceSnapAuthoring : MonoBehaviour
{
    public enum SnapAnchor
    {
        // Existing numeric values are intentionally preserved for Unity serialization.
        Bottom = 0,
        Top = 1,
        Center = 2,

        // Added for side-mounted parts such as rudders.
        Left = 3,
        Right = 4
    }

    public enum SnapTargetEdge
    {
        // Top is zero so existing components get the old behavior when this new field
        // is absent from serialized data.
        Top = 0,
        Bottom = 1,
        Left = 2,
        Right = 3
    }

    public enum SnapBoundsSource
    {
        RendererBounds,
        ColliderBounds,
        RendererAndColliderBounds
    }

    [Header("Builder Snap")]
    [SerializeField] private bool snapOnBuilderPlace = true;

    [Tooltip("Which edge/point on this object should align to the selected target edge.")]
    [SerializeField] private SnapAnchor anchor = SnapAnchor.Bottom;

    [Tooltip("Which edge of the target surface this object should attach to. " +
             "Top preserves the original floor/deck snapping behavior.")]
    [SerializeField] private SnapTargetEdge targetEdge = SnapTargetEdge.Top;

    [Tooltip("What bounds should be used to find this object's edges.")]
    [SerializeField] private SnapBoundsSource boundsSource = SnapBoundsSource.RendererBounds;

    [Tooltip("Optional explicit renderers used for bounds. If empty, child renderers are auto-discovered.")]
    [SerializeField] private Renderer[] anchorRenderers;

    [Tooltip("Optional explicit colliders used for bounds. If empty, child colliders are auto-discovered.")]
    [SerializeField] private Collider2D[] anchorColliders;

    [Tooltip("Vertical offset from the selected target Top/Bottom edge. Kept under the old serialized field name so existing snap authoring does not lose its offset.")]
    [SerializeField] private float yOffsetFromSurfaceTop = 0f;

    [Tooltip("Horizontal offset from the selected target Left/Right edge.")]
    [SerializeField] private float xOffsetFromSurfaceEdge = 0f;

    [Header("Surface Search")]
    [Tooltip("Maximum vertical distance when snapping to a Top/Bottom edge.")]
    [Min(0.01f)]
    [SerializeField] private float maxVerticalSnapDistance = 1.5f;

    [Tooltip("Maximum horizontal distance when snapping to a Left/Right edge.")]
    [Min(0.01f)]
    [SerializeField] private float maxHorizontalSnapDistance = 1.5f;

    [Tooltip("How much to shrink this object's horizontal bounds before checking overlap for vertical snapping.")]
    [Min(0f)]
    [SerializeField] private float horizontalInset = 0.03f;

    [Tooltip("How much to shrink this object's vertical bounds before checking overlap for horizontal snapping.")]
    [Min(0f)]
    [SerializeField] private float verticalInset = 0.03f;

    [Tooltip("Layers considered valid snap surfaces. A zero mask keeps the legacy behavior of allowing all layers.")]
    [SerializeField] private LayerMask surfaceLayerMask = ~0;

    [Tooltip("Usually false. Trigger volumes like visibility zones and secure zones should not become floors, because apparently we have to say that now.")]
    [SerializeField] private bool includeTriggerSurfaces = false;

    [Tooltip("If true, only colliders under the same boat root count as snap surfaces.")]
    [SerializeField] private bool requireBoatRootChild = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public bool SnapOnBuilderPlace => snapOnBuilderPlace;
    public SnapAnchor Anchor => anchor;
    public SnapTargetEdge TargetEdge => targetEdge;

#if UNITY_EDITOR
    [ContextMenu("Snap To Boat Surface Now")]
    private void ContextSnapToBoatSurfaceNow()
    {
        Transform boatRoot = FindBoatRootForContext();

        if (!EditorSnapToSurface(boatRoot))
        {
            Debug.LogWarning(
                $"[BoatBuilderSurfaceSnap:{name}] Could not snap to a valid boat surface.",
                this);
        }
    }

    public bool EditorSnapToSurface(Transform boatRoot)
    {
        if (!snapOnBuilderPlace)
            return false;

        if (!TryResolveAnchorBounds(out Bounds objectBounds))
        {
            Log("No usable object bounds found.");
            return false;
        }

        bool horizontalSnap = IsHorizontalTarget(targetEdge);
        float currentAnchorCoordinate =
            GetObjectAnchorCoordinate(objectBounds, horizontalSnap);

        if (!TryFindBestSurfaceEdge(
                boatRoot,
                objectBounds,
                currentAnchorCoordinate,
                horizontalSnap,
                out float targetCoordinate,
                out Collider2D surfaceCollider))
        {
            Log("No valid target surface edge found.");
            return false;
        }

        float offset = horizontalSnap
            ? xOffsetFromSurfaceEdge
            : yOffsetFromSurfaceTop;

        float desiredAnchorCoordinate = targetCoordinate + offset;
        float delta = desiredAnchorCoordinate - currentAnchorCoordinate;

        if (Mathf.Abs(delta) <= 0.0001f)
        {
            Log($"Already snapped to '{surfaceCollider.name}'.");
            return true;
        }

        Undo.RecordObject(transform, "Snap to Boat Surface");

        Vector3 p = transform.position;

        if (horizontalSnap)
            p.x += delta;
        else
            p.y += delta;

        transform.position = p;

        EditorUtility.SetDirty(transform);

        Log(
            $"Snapped objectAnchor={anchor} to targetEdge={targetEdge} " +
            $"surface='{surfaceCollider.name}' target={targetCoordinate:0.###} " +
            $"offset={offset:0.###} delta={delta:0.###}");

        return true;
    }
#endif

    private bool TryResolveAnchorBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (boundsSource == SnapBoundsSource.RendererBounds ||
            boundsSource == SnapBoundsSource.RendererAndColliderBounds)
        {
            Renderer[] renderers = anchorRenderers != null && anchorRenderers.Length > 0
                ? anchorRenderers
                : GetComponentsInChildren<Renderer>(true);

            AddRendererBounds(renderers, ref bounds, ref hasBounds);
        }

        if (boundsSource == SnapBoundsSource.ColliderBounds ||
            boundsSource == SnapBoundsSource.RendererAndColliderBounds)
        {
            Collider2D[] colliders = anchorColliders != null && anchorColliders.Length > 0
                ? anchorColliders
                : GetComponentsInChildren<Collider2D>(true);

            AddColliderBounds(colliders, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static void AddRendererBounds(
        Renderer[] renderers,
        ref Bounds bounds,
        ref bool hasBounds)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
    }

    private static void AddColliderBounds(
        Collider2D[] colliders,
        ref Bounds bounds,
        ref bool hasBounds)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null || !c.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = c.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }
    }

    private float GetObjectAnchorCoordinate(
        Bounds bounds,
        bool horizontalSnap)
    {
        if (horizontalSnap)
        {
            return anchor switch
            {
                SnapAnchor.Left => bounds.min.x,
                SnapAnchor.Right => bounds.max.x,
                SnapAnchor.Center => bounds.center.x,

                // A vertical anchor has no meaningful X edge. Center is the least
                // surprising fallback and keeps malformed authoring from exploding.
                _ => bounds.center.x
            };
        }

        return anchor switch
        {
            SnapAnchor.Bottom => bounds.min.y,
            SnapAnchor.Top => bounds.max.y,
            SnapAnchor.Center => bounds.center.y,

            // Same fallback for side anchors used with a vertical target.
            _ => bounds.center.y
        };
    }

    private bool TryFindBestSurfaceEdge(
        Transform boatRoot,
        Bounds objectBounds,
        float currentAnchorCoordinate,
        bool horizontalSnap,
        out float targetCoordinate,
        out Collider2D surfaceCollider)
    {
        targetCoordinate = 0f;
        surfaceCollider = null;

        if (boatRoot == null)
            boatRoot = FindBoatRootForContext();

        Collider2D[] candidates = boatRoot != null
            ? boatRoot.GetComponentsInChildren<Collider2D>(true)
            : FindObjectsByType<Collider2D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D c = candidates[i];
            if (c == null || !c.enabled)
                continue;

            if (c.transform == transform || c.transform.IsChildOf(transform))
                continue;

            if (c.isTrigger && !includeTriggerSurfaces)
                continue;

            if (!LayerAllowed(c.gameObject.layer))
                continue;

            if (requireBoatRootChild && boatRoot != null)
            {
                Transform ct = c.transform;
                if (ct != boatRoot && !ct.IsChildOf(boatRoot))
                    continue;
            }

            Bounds cb = c.bounds;

            float overlap;
            float candidateCoordinate;

            if (horizontalSnap)
            {
                if (!HasVerticalOverlap(objectBounds, cb, verticalInset))
                    continue;

                overlap = VerticalOverlapHeight(objectBounds, cb);
                candidateCoordinate = GetTargetCoordinate(cb, targetEdge);

                float desiredAnchor =
                    candidateCoordinate + xOffsetFromSurfaceEdge;

                float distance =
                    Mathf.Abs(desiredAnchor - currentAnchorCoordinate);

                if (distance > maxHorizontalSnapDistance)
                    continue;

                float score = distance - overlap * 0.001f;
                if (score >= bestScore)
                    continue;

                bestScore = score;
            }
            else
            {
                if (!HasHorizontalOverlap(objectBounds, cb, horizontalInset))
                    continue;

                overlap = HorizontalOverlapWidth(objectBounds, cb);
                candidateCoordinate = GetTargetCoordinate(cb, targetEdge);

                float desiredAnchor =
                    candidateCoordinate + yOffsetFromSurfaceTop;

                float distance =
                    Mathf.Abs(desiredAnchor - currentAnchorCoordinate);

                if (distance > maxVerticalSnapDistance)
                    continue;

                float score = distance - overlap * 0.001f;
                if (score >= bestScore)
                    continue;

                bestScore = score;
            }

            targetCoordinate = candidateCoordinate;
            surfaceCollider = c;
        }

        return surfaceCollider != null;
    }

    private static bool IsHorizontalTarget(SnapTargetEdge edge)
    {
        return edge == SnapTargetEdge.Left ||
               edge == SnapTargetEdge.Right;
    }

    private static float GetTargetCoordinate(
        Bounds bounds,
        SnapTargetEdge edge)
    {
        return edge switch
        {
            SnapTargetEdge.Top => bounds.max.y,
            SnapTargetEdge.Bottom => bounds.min.y,
            SnapTargetEdge.Left => bounds.min.x,
            SnapTargetEdge.Right => bounds.max.x,
            _ => bounds.max.y
        };
    }

    private bool LayerAllowed(int layer)
    {
        int mask = surfaceLayerMask.value == 0
            ? Physics2D.AllLayers
            : surfaceLayerMask.value;

        return (mask & (1 << layer)) != 0;
    }

    private static bool HasHorizontalOverlap(
        Bounds a,
        Bounds b,
        float inset)
    {
        float aMin = a.min.x + inset;
        float aMax = a.max.x - inset;

        if (aMax < aMin)
        {
            float center = a.center.x;
            aMin = center;
            aMax = center;
        }

        return aMax >= b.min.x &&
               aMin <= b.max.x;
    }

    private static bool HasVerticalOverlap(
        Bounds a,
        Bounds b,
        float inset)
    {
        float aMin = a.min.y + inset;
        float aMax = a.max.y - inset;

        if (aMax < aMin)
        {
            float center = a.center.y;
            aMin = center;
            aMax = center;
        }

        return aMax >= b.min.y &&
               aMin <= b.max.y;
    }

    private static float HorizontalOverlapWidth(
        Bounds a,
        Bounds b)
    {
        float min = Mathf.Max(a.min.x, b.min.x);
        float max = Mathf.Min(a.max.x, b.max.x);
        return Mathf.Max(0f, max - min);
    }

    private static float VerticalOverlapHeight(
        Bounds a,
        Bounds b)
    {
        float min = Mathf.Max(a.min.y, b.min.y);
        float max = Mathf.Min(a.max.y, b.max.y);
        return Mathf.Max(0f, max - min);
    }

    private Transform FindBoatRootForContext()
    {
        Boat boat = GetComponentInParent<Boat>();
        if (boat != null)
            return boat.transform;

        return transform.root;
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[BoatBuilderSurfaceSnap:{name}] {msg}",
            this);
    }
}
