using UnityEngine;

[DisallowMultipleComponent]
public sealed class GeneratorModule : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isOn;

    [Header("Generation")]
    [SerializeField] private float powerGeneratedPerSecond = 5f;

    [Header("Fuel")]
    [SerializeField] private ItemDefinition fuelContainerDefinition;
    [SerializeField] private float fuelBurnRatePerSecond = 0.05f;

    private ItemInstance fuelContainerItem;
    private float fuelBurnAccumulator;

    private InstalledModule installedModule;
    private Hardpoint ownerHardpoint;
    private Boat ownerBoat;
    private BoatPowerState powerState;

    public bool IsOn => isOn;
    public float PowerGeneratedPerSecond => Mathf.Max(0f, powerGeneratedPerSecond);
    public float FuelBurnRatePerSecond => Mathf.Max(0f, fuelBurnRatePerSecond);
    public ItemInstance FuelContainerItem => fuelContainerItem;
    public BoatPowerState PowerState => powerState;

    public bool HasUsableFuel => TryGetActiveFuelItem(out _);

    public void InitializeFuel()
    {
        if (fuelContainerItem != null)
            return;

        if (fuelContainerDefinition == null)
        {
            Debug.LogWarning("[GeneratorModule] No fuel container definition assigned.", this);
            return;
        }

        fuelContainerItem = ItemInstance.Create(fuelContainerDefinition, 1);
    }

    private void Awake()
    {
        installedModule = GetComponent<InstalledModule>();
    }

    private void Start()
    {
        InitializeFuel();
        ResolveOwnership();
    }

    private void Update()
    {
        if (!isOn)
        {
            fuelBurnAccumulator = 0f;
            return;
        }

        if (!CanRun())
        {
            isOn = false;
            fuelBurnAccumulator = 0f;
            return;
        }

        powerState.AddPower(PowerGeneratedPerSecond * Time.deltaTime);

        fuelBurnAccumulator += Time.deltaTime * FuelBurnRatePerSecond;

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

    public bool SetOn(bool value)
    {
        if (value && !CanRun())
        {
            isOn = false;
            fuelBurnAccumulator = 0f;
            return false;
        }

        isOn = value;

        if (!isOn)
            fuelBurnAccumulator = 0f;

        return true;
    }

    public bool Toggle()
    {
        return SetOn(!isOn);
    }

    public bool CanRun()
    {
        ResolveOwnership();
        return powerState != null && TryGetActiveFuelItem(out _);
    }

    public void ResolveOwnership()
    {
        if (installedModule == null)
            installedModule = GetComponent<InstalledModule>();

        if (installedModule != null)
            ownerHardpoint = installedModule.OwnerHardpoint;

        if (ownerHardpoint != null)
            ownerBoat = ownerHardpoint.GetComponentInParent<Boat>();

        if (ownerBoat == null)
            ownerBoat = GetComponentInParent<Boat>();

        if (ownerBoat != null)
        {
            powerState = ownerBoat.GetComponent<BoatPowerState>();

            if (powerState == null)
                powerState = ownerBoat.gameObject.AddComponent<BoatPowerState>();
        }
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
            if (!candidate.HasCharges || candidate.CurrentCharges <= 0)
                continue;

            fuelItem = candidate;
            return true;
        }

        return false;
    }

    public bool TryConsumeFuelCharges(int amount)
    {
        if (amount <= 0)
            return false;

        int remaining = amount;

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
            if (!candidate.HasCharges || candidate.CurrentCharges <= 0)
                continue;

            int consumed = candidate.ConsumeChargesUpTo(remaining);
            remaining -= consumed;

            if (remaining <= 0)
                break;
        }

        return remaining <= 0;
    }
}