using Survival.Attributes;
using Survival.Vitals;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EquippedItemAirSource : MonoBehaviour, IAirSource
{
    [Header("Refs")]
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerAttributeState attributes;

    [Header("Slots Checked")]
    [SerializeField]
    private BottomBarSlotType[] slotsToCheck =
    {
        BottomBarSlotType.Body,
        BottomBarSlotType.Head,
        BottomBarSlotType.Backpack
    };

    private float _chargeDrainCarry;

    public float MaxAirBonus
    {
        get
        {
            ItemInstance source = FindEquippedAirSource();
            return source != null && source.Definition != null
                ? source.Definition.ExternalAirMaxAirBonus
                : 0f;
        }
    }

    private void Reset()
    {
        equipment = GetComponentInParent<PlayerEquipment>();
        attributes = GetComponentInParent<PlayerAttributeState>();
    }

    private void Awake()
    {
        if (equipment == null)
            equipment =
                GetComponent<PlayerEquipment>() ??
                GetComponentInParent<PlayerEquipment>() ??
                GetComponentInChildren<PlayerEquipment>(true);

        if (attributes == null)
            attributes =
                GetComponent<PlayerAttributeState>() ??
                GetComponentInParent<PlayerAttributeState>() ??
                GetComponentInChildren<PlayerAttributeState>(true);
    }

    public float GetAirFlowPerSecond(PlayerAirState air, float dt)
    {
        if (air == null || !air.IsUnderwater)
            return 0f;

        ItemInstance source = FindEquippedAirSource();
        if (source == null || source.Definition == null)
            return 0f;

        if (!source.HasCharges || source.CurrentCharges <= 0)
            return 0f;

        float airUseMultiplier = attributes != null
            ? attributes.GetMultiplier(PlayerAttributeId.AirConsumptionMultiplier)
            : 1f;

        float chargeUsePerSecond =
            source.Definition.ExternalAirChargeUsePerSecond *
            Mathf.Max(0.01f, airUseMultiplier);

        ConsumeChargesOverTime(source, chargeUsePerSecond, dt);

        if (source.CurrentCharges <= 0)
            return 0f;

        return source.Definition.ExternalAirSupplyPerSecond;
    }

    private ItemInstance FindEquippedAirSource()
    {
        if (equipment == null || slotsToCheck == null)
            return null;

        for (int i = 0; i < slotsToCheck.Length; i++)
        {
            ItemInstance item = equipment.Get(slotsToCheck[i]);
            if (item == null || item.Definition == null)
                continue;

            if (!item.Definition.ProvidesExternalAir)
                continue;

            return item;
        }

        return null;
    }

    private void ConsumeChargesOverTime(ItemInstance source, float chargesPerSecond, float dt)
    {
        if (source == null || chargesPerSecond <= 0f || dt <= 0f)
            return;

        _chargeDrainCarry += chargesPerSecond * dt;

        int wholeCharges = Mathf.FloorToInt(_chargeDrainCarry);
        if (wholeCharges <= 0)
            return;

        _chargeDrainCarry -= wholeCharges;

        int consumed = source.ConsumeChargesUpTo(wholeCharges);
        if (consumed > 0)
            equipment?.NotifyChanged();
    }
}