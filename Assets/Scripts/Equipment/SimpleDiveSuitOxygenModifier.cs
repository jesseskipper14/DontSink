using UnityEngine;

public sealed class SimpleDiveSuitOxygenModifier : MonoBehaviour
{
    [SerializeField] private UnderwaterHarvestActor harvestActor;

    [SerializeField] private bool equipped;

    [Min(1f)]
    [SerializeField] private float equippedOxygenMultiplier = 2f;

    private void Awake()
    {
        if (harvestActor == null)
            harvestActor = GetComponentInParent<UnderwaterHarvestActor>();

        Apply();
    }

    private void OnValidate()
    {
        equippedOxygenMultiplier = Mathf.Max(1f, equippedOxygenMultiplier);
    }

    [ContextMenu("Equip Dive Suit")]
    public void Equip()
    {
        equipped = true;
        Apply();
    }

    [ContextMenu("Unequip Dive Suit")]
    public void Unequip()
    {
        equipped = false;
        Apply();
    }

    public void SetEquipped(bool value)
    {
        equipped = value;
        Apply();
    }

    private void Apply()
    {
        if (harvestActor == null)
            return;

        harvestActor.SetOxygenCapacityMultiplier(
            equipped ? equippedOxygenMultiplier : 1f);
    }
}