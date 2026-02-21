using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BoatBoardedVolume : MonoBehaviour
{
    [SerializeField] private Transform boatRoot;

    private bool _isTearingDown;

    private void Awake()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;

        if (boatRoot == null) boatRoot = transform.root;
    }

    private void OnDisable()
    {
        // If we're disabling (scene load, boat despawn, pooling), do NOT auto-unboard.
        _isTearingDown = true;
    }

    private void OnEnable()
    {
        _isTearingDown = false;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // If this volume is being disabled, ignore exits. They're not "real gameplay exits".
        if (_isTearingDown) return;

        // If the boat root is disabling, also ignore.
        if (boatRoot != null && !boatRoot.gameObject.activeInHierarchy) return;

        var boarding = other.GetComponentInParent<PlayerBoardingState>();
        if (boarding == null) return;

        // Only unboard if they were boarded to THIS boat.
        if (boarding.IsBoarded && boarding.CurrentBoatRoot == boatRoot)
        {
            other.transform.SetParent(null, worldPositionStays: true);
            boarding.Unboard();
        }
    }
}
