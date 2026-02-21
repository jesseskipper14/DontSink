using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class DockTrigger : MonoBehaviour
{
    public enum DockKind { Source, Destination }

    public DockKind kind;

    [Tooltip("Optional: if set, only objects with this tag will count as docking.")]
    public string requiredTag = "Boat";

    public System.Action<DockTrigger, Collider2D> OnDocked;

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
            return;

        OnDocked?.Invoke(this, other);
    }
}