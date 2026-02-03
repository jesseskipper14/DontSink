using UnityEngine;

[DisallowMultipleComponent]
public class DebugInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private int priority = 0;
    [SerializeField] private bool requireFacing = false;
    [SerializeField] private float maxDistance = 2.0f;

    [Header("Debug Output")]
    [SerializeField] private string message = "DebugInteractable: interacted!";
    [SerializeField] private bool printInteractorName = true;

    public int InteractionPriority => priority;

    public bool CanInteract(in InteractContext context)
    {
        // Distance gate (optional but useful)
        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxDistance) return false;

        if (requireFacing)
        {
            Vector2 toMe = (Vector2)transform.position - context.Origin;
            if (toMe.sqrMagnitude > 0.0001f)
            {
                float dot = Vector2.Dot(context.AimDir, toMe.normalized);
                // Must be roughly in front
                if (dot < 0.25f) return false;
            }
        }

        return true;
    }

    public void Interact(in InteractContext context)
    {
        string who = (printInteractorName && context.InteractorGO != null)
            ? $" by {context.InteractorGO.name}"
            : "";

        Debug.Log($"{message}{who} (target={name})", this);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
#endif
}
