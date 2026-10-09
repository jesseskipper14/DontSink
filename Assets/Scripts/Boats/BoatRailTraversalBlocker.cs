using System.Collections.Generic;
using UnityEngine;

/// <summary>Explicit crossing footprint, in the authored rail's local frame.</summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class BoatRailTraversalBlocker : MonoBehaviour
{
    [Tooltip("Live traversal geometry. Uses the same collider as ResizableSegment2D; trigger colliders are supported.")]
    [SerializeField] private BoxCollider2D footprintCollider;
    [Tooltip("Optional existing door state. An open gate has no traversal footprint.")]
    [SerializeField] private DoorRuntime gate;
    private static readonly HashSet<BoatRailTraversalBlocker> Active = new();
    private Boat boat;
    private ResizableSegment2D segment;
    public bool IsBlocking => isActiveAndEnabled && gameObject.activeInHierarchy && (gate == null || !gate.IsOpen);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Active.Clear();
    private void OnEnable()
    {
        boat = GetComponentInParent<Boat>();
        ResolveGeometry();
        AlignResizableVisual(segment);
        Active.Add(this);
    }
    private void OnDisable()
    {
        Active.Remove(this);
        if (segment != null) segment.SizeApplied -= AlignResizableVisual;
    }
    private void ResolveGeometry()
    {
        if (segment != null) segment.SizeApplied -= AlignResizableVisual;
        segment = GetComponent<ResizableSegment2D>();
        if (footprintCollider == null)
            footprintCollider = segment != null && segment.BoxCollider != null
                ? segment.BoxCollider : GetComponent<BoxCollider2D>();
        if (segment != null && isActiveAndEnabled) segment.SizeApplied += AlignResizableVisual;
    }
    private void AlignResizableVisual(ResizableSegment2D resized)
    {
        if (resized == null || resized.BoxCollider == null || resized.SpriteRenderer == null) return;
        var visual = resized.SpriteRenderer;
        // Bottom-left sprite pivots must not move the artwork away from the
        // centered resize handles/collider as its dimensions change.
        if (visual.transform == resized.BoxCollider.transform) return;
        Vector3 target = resized.BoxCollider.transform.TransformPoint(resized.BoxCollider.offset);
        Vector3 current = visual.transform.TransformPoint(visual.localBounds.center);
        Vector3 correction = target - current;
        correction.z = 0;
        if (correction.sqrMagnitude > .00000001f) visual.transform.position += correction;
    }
#if UNITY_EDITOR
    private void OnValidate() => ResolveGeometry();
#endif

    public static bool HasRails(Transform boatRoot)
    {
        foreach (var rail in Active)
            if (rail != null && rail.BelongsTo(boatRoot)) return true;
        return false;
    }
    private bool BelongsTo(Transform root) => root != null &&
        (boat != null ? boat.transform == root || boat.transform.IsChildOf(root) : transform.IsChildOf(root));

    public static bool IsCrossingBlocked(Transform boatRoot, Vector2 start, Vector2 end, Vector2 actorHalfSize)
    {
        foreach (var rail in Active)
            if (rail != null && rail.IsBlocking && rail.BelongsTo(boatRoot) && rail.Intersects(start, end, actorHalfSize))
                return true;
        return false;
    }

    public bool Intersects(Vector2 start, Vector2 end, Vector2 actorHalfSize)
    {
        if (footprintCollider == null) ResolveGeometry();
        if (footprintCollider == null) return false;
        Transform geometry = footprintCollider.transform;
        Vector2 a = PhysicsFrame2D.InverseTransformPoint(geometry, start);
        Vector2 b = PhysicsFrame2D.InverseTransformPoint(geometry, end);
        // Actor dimensions are expressed in the boat frame. Conservatively rotate
        // their rectangle into the rail frame (supports end pieces / rotated rails).
        Transform frame = boat != null ? boat.transform : transform;
        Vector3 x = geometry.InverseTransformVector(frame.TransformVector(new Vector3(actorHalfSize.x, 0, 0)));
        Vector3 y = geometry.InverseTransformVector(frame.TransformVector(new Vector3(0, actorHalfSize.y, 0)));
        Vector2 padding = new Vector2(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y));
        Vector2 half = footprintCollider.size * .5f + padding;
        Vector2 min = footprintCollider.offset - half, max = footprintCollider.offset + half;
        Vector2 delta = b - a;
        float enter = 0, exit = 1;
        return ClipAxis(a.x, delta.x, min.x, max.x, ref enter, ref exit) &&
            ClipAxis(a.y, delta.y, min.y, max.y, ref enter, ref exit);
    }
    private static bool ClipAxis(float start, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(delta) < .00001f) return start >= min && start <= max;
        float a = (min - start) / delta, b = (max - start) / delta;
        if (a > b) (a, b) = (b, a);
        enter = Mathf.Max(enter, a); exit = Mathf.Min(exit, b);
        return enter <= exit;
    }
    public static Vector2 ActorHalfSize(PlayerBoardingState actor, Transform boatRoot)
    {
        // Use authored local collider dimensions rather than world AABBs, which
        // inflate at capsize. Actor orientation is converted into the boat frame.
        Collider2D col = actor.GetComponent<Collider2D>();
        Vector2 size = col is CapsuleCollider2D capsule ? capsule.size : col is BoxCollider2D box ? box.size : new Vector2(.5f, 1.6f);
        Vector3 x = boatRoot.InverseTransformVector(actor.transform.TransformVector(new Vector3(size.x * .5f, 0, 0)));
        Vector3 y = boatRoot.InverseTransformVector(actor.transform.TransformVector(new Vector3(0, size.y * .5f, 0)));
        return new Vector2(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y));
    }
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (footprintCollider == null) return;
        Gizmos.matrix = footprintCollider.transform.localToWorldMatrix;
        Gizmos.color = new Color(1, .6f, .1f, .7f);
        Gizmos.DrawWireCube(footprintCollider.offset, footprintCollider.size);
    }
#endif
}
