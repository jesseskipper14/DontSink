using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkyClearanceRequirement : MonoBehaviour
{
    public const string BlockedMessage = "No unobstructed view of sky.";
    [SerializeField, Min(0.1f)] private float clearanceWidth = 1.25f;
    [SerializeField, Min(0.1f)] private float clearanceHeight = 3f;
    [SerializeField] private float originHeight = 0.7f;
    [SerializeField] private LayerMask obstructionLayers = ~0;
    [SerializeField, Min(0.02f)] private float recheckInterval = 0.15f;
    private readonly Collider2D[] hits = new Collider2D[128];
    public float RecheckInterval => Mathf.Max(0.02f, recheckInterval);
    private Vector2 Center => (Vector2)transform.position + Vector2.up *
        (originHeight + Mathf.Max(0.1f, clearanceHeight) * 0.5f);
    private Vector2 Size => new Vector2(Mathf.Max(0.1f, clearanceWidth), Mathf.Max(0.1f, clearanceHeight));

    public bool HasClearance()
    {
        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(obstructionLayers);
        int count = Physics2D.OverlapBox(Center, Size, 0f, filter, hits);
        if (count == hits.Length) return false; // Crowded volumes fail closed.
        for (int i = 0; i < count; i++)
            if (hits[i] != null && !hits[i].transform.IsChildOf(transform)) return false;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Center, Size);
    }
}
