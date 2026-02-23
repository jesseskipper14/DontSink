using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class DockTrigger : MonoBehaviour
{
    public enum DockKind { Source, Destination }

    public DockKind kind;

    [Tooltip("Optional: if set, only objects with this tag will count as docking.")]
    public string requiredTag = "Boat";

    // NEW: range events (no instant scene transition)
    public System.Action<DockTrigger, Collider2D> OnEnteredRange;
    public System.Action<DockTrigger, Collider2D> OnExitedRange;

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    private bool IsValid(Collider2D other)
    {
        if (string.IsNullOrEmpty(requiredTag)) return true;
        return other.CompareTag(requiredTag);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsValid(other)) return;
        OnEnteredRange?.Invoke(this, other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsValid(other)) return;
        OnExitedRange?.Invoke(this, other);
    }
}