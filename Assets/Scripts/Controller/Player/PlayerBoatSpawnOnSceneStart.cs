using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerBoatSpawnOnSceneStart : MonoBehaviour
{
    [Header("Deprecated")]
    [SerializeField] private bool logDeprecatedWarning = true;

    private void Start()
    {
        if (!logDeprecatedWarning)
            return;

        Debug.LogWarning(
            "[PlayerBoatSpawnOnSceneStart] This component is deprecated and intentionally does nothing. " +
            "PlayerSceneContextRestorer is now responsible for restoring player boarded/unboarded scene context. " +
            "Remove this component from player prefabs/scenes when convenient.",
            this);
    }
}