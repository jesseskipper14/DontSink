using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class BoatCargoReceiver : MonoBehaviour
{
    [Header("Deprecated")]
    [SerializeField] private bool logDeprecatedWarning = true;

    private void Awake()
    {
        if (!logDeprecatedWarning)
            return;

        Debug.LogWarning(
            "[BoatCargoReceiver] Deprecated and intentionally disabled. " +
            "Old Cargo auto-attach bypasses modern item/boat ownership persistence. " +
            "Remove this component from boat prefabs/scenes when convenient.",
            this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Intentionally disabled.
        // Modern cargo/items must use CargoManifest / WorldItem / BoatOwnedItem paths.
    }
}