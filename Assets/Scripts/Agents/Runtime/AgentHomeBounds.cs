using UnityEngine;

[DisallowMultipleComponent]
public class AgentHomeBounds : MonoBehaviour
{
    [SerializeField] private BoxCollider2D boundsCollider;

    public bool HasBounds => boundsCollider != null;

    public Bounds WorldBounds
    {
        get
        {
            if (boundsCollider != null)
                return boundsCollider.bounds;

            return new Bounds(transform.position, Vector3.zero);
        }
    }

    private void Reset()
    {
        boundsCollider = GetComponent<BoxCollider2D>();
    }

    public Vector3 ClampToBounds(Vector3 worldPosition)
    {
        if (boundsCollider == null)
            return worldPosition;

        Bounds b = boundsCollider.bounds;

        worldPosition.x = Mathf.Clamp(worldPosition.x, b.min.x, b.max.x);
        worldPosition.y = Mathf.Clamp(worldPosition.y, b.min.y, b.max.y);

        return worldPosition;
    }

    public bool Contains(Vector3 worldPosition)
    {
        if (boundsCollider == null)
            return true;

        return boundsCollider.bounds.Contains(worldPosition);
    }

    private void OnDrawGizmosSelected()
    {
        if (boundsCollider == null)
            boundsCollider = GetComponent<BoxCollider2D>();

        if (boundsCollider == null)
            return;

        Gizmos.DrawWireCube(boundsCollider.bounds.center, boundsCollider.bounds.size);
    }
}