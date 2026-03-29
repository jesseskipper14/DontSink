using UnityEngine;

public sealed class EngineModule : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isOn;

    [Header("Fuel")]
    [SerializeField] private ItemDefinition fuelContainerDefinition;
    [SerializeField] private float fuelBurnRatePerSecond = 0.1f;
    [SerializeField] private float maxThrottleBurnMultiplier = 2f;

    private ItemInstance fuelContainerItem;
    private float fuelBurnAccumulator;
    private float throttleLoad01;

    public bool IsOn => isOn;
    public ItemInstance FuelContainerItem => fuelContainerItem;
    public float FuelBurnRatePerSecond => Mathf.Max(0f, fuelBurnRatePerSecond);
    public float MaxThrottleBurnMultiplier => Mathf.Max(1f, maxThrottleBurnMultiplier);
    public float ThrottleLoad01 => Mathf.Clamp01(throttleLoad01);

    public void InitializeFuel()
    {
        if (fuelContainerItem != null)
            return;

        if (fuelContainerDefinition == null)
        {
            Debug.LogWarning("[EngineModule] No fuel container definition assigned.", this);
            return;
        }

        fuelContainerItem = ItemInstance.Create(fuelContainerDefinition, 1);
    }

    private void Update()
    {
        if (!isOn)
        {
            fuelBurnAccumulator = 0f;
            return;
        }

        if (FuelBurnRatePerSecond <= 0f)
            return;

        if (!CanRun())
        {
            isOn = false;
            fuelBurnAccumulator = 0f;
            return;
        }

        float burnMultiplier = Mathf.Lerp(1f, MaxThrottleBurnMultiplier, ThrottleLoad01);
        float effectiveBurnPerSecond = FuelBurnRatePerSecond * burnMultiplier;

        fuelBurnAccumulator += Time.deltaTime * effectiveBurnPerSecond;

        int wholeChargesToBurn = Mathf.FloorToInt(fuelBurnAccumulator);
        if (wholeChargesToBurn <= 0)
            return;

        fuelBurnAccumulator -= wholeChargesToBurn;

        if (!TryConsumeFuelCharges(wholeChargesToBurn))
        {
            isOn = false;
            fuelBurnAccumulator = 0f;
        }
    }

    public void SetThrottleLoad(float absThrottle01)
    {
        throttleLoad01 = Mathf.Clamp01(absThrottle01);
    }

    public bool CanProduceThrust()
    {
        return isOn && CanRun();
    }

    public bool CanRun()
    {
        return TryGetActiveFuelItem(out _);
    }

    public bool TryGetActiveFuelItem(out ItemInstance fuelItem)
    {
        fuelItem = null;

        if (fuelContainerItem == null || !fuelContainerItem.IsContainer)
            return false;

        ItemContainerState state = fuelContainerItem.ContainerState;
        if (state == null)
            return false;

        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            ItemInstance candidate = slot.Instance;
            if (!candidate.HasCharges)
                continue;

            if (candidate.CurrentCharges <= 0)
                continue;

            fuelItem = candidate;
            return true;
        }

        return false;
    }

    public bool SetOn(bool value)
    {
        if (value && !CanRun())
        {
            isOn = false;
            return false;
        }

        isOn = value;

        if (!isOn)
        {
            fuelBurnAccumulator = 0f;
            throttleLoad01 = 0f;
        }

        return true;
    }

    public bool Toggle()
    {
        return SetOn(!isOn);
    }

    public bool TryConsumeFuelCharges(int amount)
    {
        if (amount <= 0)
            return false;

        int remaining = amount;

        if (fuelContainerItem == null || !fuelContainerItem.IsContainer)
        {
            isOn = false;
            return false;
        }

        ItemContainerState state = fuelContainerItem.ContainerState;
        if (state == null)
        {
            isOn = false;
            return false;
        }

        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null)
                continue;

            ItemInstance candidate = slot.Instance;
            if (!candidate.HasCharges || candidate.CurrentCharges <= 0)
                continue;

            int consumed = candidate.ConsumeChargesUpTo(remaining);
            remaining -= consumed;

            if (remaining <= 0)
                break;
        }

        if (remaining > 0)
        {
            isOn = false;
            return false;
        }

        if (!CanRun())
            isOn = false;

        return true;
    }
}