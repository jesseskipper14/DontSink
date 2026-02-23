using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("Optional stable slot index (0..3). Leave -1 to auto.")]
    public int slotIndex = -1;

    [Tooltip("Optional: prefer this spawn earlier when choosing among free points.")]
    public int weight = 0;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (slotIndex < -1) slotIndex = -1;
    }
#endif
}