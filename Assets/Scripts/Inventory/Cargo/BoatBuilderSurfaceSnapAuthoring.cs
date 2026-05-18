using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class BoatBuilderSurfaceSnapAuthoring : MonoBehaviour
{
    public enum SnapAnchor
    {
        Bottom,
        Top,
        Center
    }

    public enum SnapBoundsSource
    {
        RendererBounds,
        ColliderBounds,
        RendererAndColliderBounds
    }

    [Header("Builder Snap")]
    [SerializeField] private bool snapOnBuilderPlace = true;

    [Tooltip("Which vertical point on this object should align to the target surface top.")]
    [SerializeField] private SnapAnchor anchor = SnapAnchor.Bottom;

    [Tooltip("What bounds should be used to find this object's top/bottom/center.")]
    [SerializeField] private SnapBoundsSource boundsSource = SnapBoundsSource.RendererBounds;

    [Tooltip("Optional explicit renderers used for bounds. If empty, child renderers are auto-discovered.")]
    [SerializeField] private Renderer[] anchorRenderers;

    [Tooltip("Optional explicit colliders used for bounds. If empty, child colliders are auto-discovered.")]
    [SerializeField] private Collider2D[] anchorColliders;

    [Tooltip("Final object anchor Y = surface top Y + this offset. Use this for sprite weirdness.")]
    [SerializeField] private float yOffsetFromSurfaceTop = 0f;

    [Header("Surface Search")]
    [Tooltip("Surface colliders must be within this vertical distance of the current object anchor.")]
    [Min(0.01f)]
    [SerializeField] private float maxVerticalSnapDistance = 1.5f;

    [Tooltip("How much to shrink this object's horizontal bounds before checking overlap with floor/deck colliders.")]
    [Min(0f)]
    [SerializeField] private float horizontalInset = 0.03f;

    [Tooltip("Layers considered valid snap surfaces.")]
    [SerializeField] private LayerMask surfaceLayerMask = ~0;

    [Tooltip("Usually false. Trigger volumes like visibility zones and secure zones should not become floors, because apparently we have to say that now.")]
    [SerializeField] private bool includeTriggerSurfaces = false;

    [Tooltip("If true, only colliders under the same boat root count as snap surfaces.")]
    [SerializeField] private bool requireBoatRootChild = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public bool SnapOnBuilderPlace => snapOnBuilderPlace;

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

        float currentAnchorY = GetAnchorY(objectBounds);

        if (!TryFindBestSurfaceTop(
                boatRoot,
                objectBounds,
                currentAnchorY,
                out float surfaceTopY,
                out Collider2D surfaceCollider))
        {
            Log("No valid snap surface found.");
            return false;
        }

        float desiredAnchorY = surfaceTopY + yOffsetFromSurfaceTop;
        float deltaY = desiredAnchorY - currentAnchorY;

        if (Mathf.Abs(deltaY) <= 0.0001f)
        {
            Log($"Already snapped to '{surfaceCollider.name}'.");
            return true;
        }

        Undo.RecordObject(transform, "Snap to Boat Surface");

        Vector3 p = transform.position;
        p.y += deltaY;
        transform.position = p;

        EditorUtility.SetDirty(transform);

        Log(
            $"Snapped anchor={anchor} to surface='{surfaceCollider.name}' " +
            $"surfaceTop={surfaceTopY:0.###} offset={yOffsetFromSurfaceTop:0.###} deltaY={deltaY:0.###}");

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

    private static void AddRendererBounds(Renderer[] renderers, ref Bounds bounds, ref bool hasBounds)
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

    private static void AddColliderBounds(Collider2D[] colliders, ref Bounds bounds, ref bool hasBounds)
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

    private float GetAnchorY(Bounds bounds)
    {
        return anchor switch
        {
            SnapAnchor.Bottom => bounds.min.y,
            SnapAnchor.Top => bounds.max.y,
            SnapAnchor.Center => bounds.center.y,
            _ => bounds.min.y
        };
    }

    private bool TryFindBestSurfaceTop(
        Transform boatRoot,
        Bounds objectBounds,
        float currentAnchorY,
        out float surfaceTopY,
        out Collider2D surfaceCollider)
    {
        surfaceTopY = 0f;
        surfaceCollider = null;

        if (boatRoot == null)
            boatRoot = FindBoatRootForContext();

        Collider2D[] candidates = boatRoot != null
            ? boatRoot.GetComponentsInChildren<Collider2D>(true)
            : FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

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

            if (!HasHorizontalOverlap(objectBounds, cb, horizontalInset))
                continue;

            float candidateTopY = cb.max.y;
            float desiredAnchorY = candidateTopY + yOffsetFromSurfaceTop;
            float verticalDistance = Mathf.Abs(desiredAnchorY - currentAnchorY);

            if (verticalDistance > maxVerticalSnapDistance)
                continue;

            float overlapWidth = HorizontalOverlapWidth(objectBounds, cb);

            // Prefer closest vertical match, then slightly prefer wider overlap.
            float score = verticalDistance - overlapWidth * 0.001f;

            if (score >= bestScore)
                continue;

            bestScore = score;
            surfaceTopY = candidateTopY;
            surfaceCollider = c;
        }

        return surfaceCollider != null;
    }

    private bool LayerAllowed(int layer)
    {
        int mask = surfaceLayerMask.value == 0
            ? Physics2D.AllLayers
            : surfaceLayerMask.value;

        return (mask & (1 << layer)) != 0;
    }

    private static bool HasHorizontalOverlap(Bounds a, Bounds b, float inset)
    {
        float aMin = a.min.x + inset;
        float aMax = a.max.x - inset;

        if (aMax < aMin)
        {
            float center = a.center.x;
            aMin = center;
            aMax = center;
        }

        return aMax >= b.min.x && aMin <= b.max.x;
    }

    private static float HorizontalOverlapWidth(Bounds a, Bounds b)
    {
        float min = Mathf.Max(a.min.x, b.min.x);
        float max = Mathf.Min(a.max.x, b.max.x);
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

        Debug.Log($"[BoatBuilderSurfaceSnap:{name}] {msg}", this);
    }
}