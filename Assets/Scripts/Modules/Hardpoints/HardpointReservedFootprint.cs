using UnityEngine;

[DisallowMultipleComponent]
public sealed class HardpointReservedFootprint : MonoBehaviour
{
    [Header("Reserved Module Footprint")]
    [SerializeField] private bool enabledForBuilderBlocking = true;

    [Tooltip("World-space-ish footprint size before transform scale. Represents space reserved for installed modules.")]
    [Min(0.01f)]
    [SerializeField] private Vector2 size = new Vector2(1.5f, 1.5f);

    [Tooltip("Local offset from this transform to the footprint center.")]
    [SerializeField] private Vector2 localOffset = Vector2.zero;

    [Header("Debug")]
    [SerializeField] private bool drawGizmo = true;

    public bool EnabledForBuilderBlocking => enabledForBuilderBlocking;
    public Vector2 Size => new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
    public Vector2 LocalOffset => localOffset;

    public Bounds GetWorldBounds()
    {
        Vector3 center = transform.TransformPoint(localOffset);

        Vector3 lossy = transform.lossyScale;
        Vector3 worldSize = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(size.x * lossy.x)),
            Mathf.Max(0.01f, Mathf.Abs(size.y * lossy.y)),
            0.1f);

        return new Bounds(center, worldSize);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        size.x = Mathf.Max(0.01f, size.x);
        size.y = Mathf.Max(0.01f, size.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmo)
            return;

        Bounds b = GetWorldBounds();

        Color old = Gizmos.color;
        Gizmos.color = new Color(1f, 0.45f, 0.05f, 0.25f);
        Gizmos.DrawCube(b.center, b.size);

        Gizmos.color = new Color(1f, 0.45f, 0.05f, 0.95f);
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.color = old;
    }
#endif
}