using UnityEngine;

[DisallowMultipleComponent]
public sealed class StairAccessTriggerRelay : MonoBehaviour
{
    [SerializeField] private StairSlopeAccess access;

    private void Reset()
    {
        access = GetComponentInParent<StairSlopeAccess>();
    }

    private void Awake()
    {
        if (access == null)
            access = GetComponentInParent<StairSlopeAccess>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (access != null)
            access.NotifyTriggerEnter(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (access != null)
            access.NotifyTriggerStay(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (access != null)
            access.NotifyTriggerExit(other);
    }
}