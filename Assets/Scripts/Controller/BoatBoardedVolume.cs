using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BoatBoardedVolume : MonoBehaviour
{
    [SerializeField] private Transform boatRoot;

    private void Awake()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;

        if (boatRoot == null) boatRoot = transform.root;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var boarding = other.GetComponentInParent<PlayerBoardingState>();
        if (boarding == null) return;

        // Only unboard if they were boarded to THIS boat.
        if (boarding.IsBoarded && boarding.CurrentBoatRoot == boatRoot)
        {
            // They left the boat volume (fell off, launched off, etc.)
            // Ensure they aren't parented anymore.
            other.transform.SetParent(null, worldPositionStays: true);
            boarding.Unboard();
        }
    }
}
