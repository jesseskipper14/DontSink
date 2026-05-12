using UnityEngine;

public sealed class EngineModule : MonoBehaviour, IPowerConsumerModule, IModuleToggleable, IInstalledModuleLifecycle
{
    private enum EngineRunMode
    {
        None,
        Fuel,
        BoatPower
    }

    [Header("State")]
    [SerializeField] private bool isOn;

    [Header("Fuel")]
    [SerializeField] private ItemDefinition fuelContainerDefinition;
    [SerializeField] private float fuelBurnRatePerSecond = 0.1f;
    [SerializeField] private float maxThrottleBurnMultiplier = 2f;

    [Header("Power Fallback")]
    [Tooltip("If true, engine can run from boat power when no fuel is available.")]
    [SerializeField] private bool allowBoatPowerFallback = true;

    [Tooltip("Power consumed per second while running from boat power at idle/zero throttle.")]
    [SerializeField] private float idlePowerDemandPerSecond = 0.5f;

    [Tooltip("Multiplier applied to power drain at full throttle.")]
    [SerializeField] private float maxThrottlePowerDemandMultiplier = 2f;

    [SerializeReference] private ItemInstance fuelContainerItem;
    private float fuelBurnAccumulator;
    private float throttleLoad01;

    private InstalledModule installedModule;
    private BoatPowerState powerState;

    private EngineRunMode currentRunMode = EngineRunMode.None;

    public bool IsOn => isOn;
    public ItemInstance FuelContainerItem => fuelContainerItem;
    public float FuelBurnRatePerSecond => Mathf.Max(0f, fuelBurnRatePerSecond);
    public float MaxThrottleBurnMultiplier => Mathf.Max(1f, maxThrottleBurnMultiplier);
    public float ThrottleLoad01 => Mathf.Clamp01(throttleLoad01);

    public bool IsUsingFuel => isOn && currentRunMode == EngineRunMode.Fuel;
    public bool IsUsingBoatPower => isOn && currentRunMode == EngineRunMode.BoatPower;

    public bool IsConsumingPower => IsUsingBoatPower;

    public float PowerDemandPerSecond =>
        Mathf.Max(0f, idlePowerDemandPerSecond) *
        Mathf.Lerp(1f, Mathf.Max(1f, maxThrottlePowerDemandMultiplier), ThrottleLoad01);

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

    private void Awake()
    {
        installedModule = GetComponent<InstalledModule>();
    }

    private void Start()
    {
        InitializeFuel();
        ResolvePowerState();
    }

    private void Update()
    {
        if (!isOn)
        {
            currentRunMode = EngineRunMode.None;
            fuelBurnAccumulator = 0f;
            return;
        }

        currentRunMode = ResolveRunMode();

        if (currentRunMode == EngineRunMode.None)
        {
            isOn = false;
            fuelBurnAccumulator = 0f;
            return;
        }

        if (currentRunMode == EngineRunMode.BoatPower)
        {
            fuelBurnAccumulator = 0f;

            if (!TryConsumeBoatPower(Time.deltaTime))
            {
                isOn = false;
                currentRunMode = EngineRunMode.None;
            }

            return;
        }

        TickFuelBurn(Time.deltaTime);
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
        return ResolveRunMode() != EngineRunMode.None;
    }

    public bool SetOn(bool value)
    {
        if (value && !CanRun())
        {
            isOn = false;
            currentRunMode = EngineRunMode.None;
            return false;
        }

        isOn = value;

        if (!isOn)
        {
            currentRunMode = EngineRunMode.None;
            fuelBurnAccumulator = 0f;
            throttleLoad01 = 0f;
        }

        return true;
    }

    public bool Toggle()
    {
        return SetOn(!isOn);
    }

    private EngineRunMode ResolveRunMode()
    {
        if (TryGetActiveFuelItem(out _))
            return EngineRunMode.Fuel;

        if (allowBoatPowerFallback && HasBoatPowerAvailable())
            return EngineRunMode.BoatPower;

        return EngineRunMode.None;
    }

    private void TickFuelBurn(float dt)
    {
        if (FuelBurnRatePerSecond <= 0f)
            return;

        float burnMultiplier = Mathf.Lerp(1f, MaxThrottleBurnMultiplier, ThrottleLoad01);
        float effectiveBurnPerSecond = FuelBurnRatePerSecond * burnMultiplier;

        fuelBurnAccumulator += dt * effectiveBurnPerSecond;

        int wholeChargesToBurn = Mathf.FloorToInt(fuelBurnAccumulator);
        if (wholeChargesToBurn <= 0)
            return;

        fuelBurnAccumulator -= wholeChargesToBurn;

        if (!TryConsumeFuelCharges(wholeChargesToBurn))
        {
            isOn = false;
            currentRunMode = EngineRunMode.None;
            fuelBurnAccumulator = 0f;
        }
    }

    private bool HasBoatPowerAvailable()
    {
        if (powerState == null)
            ResolvePowerState();

        return powerState != null && powerState.CurrentPower > 0f;
    }

    private bool TryConsumeBoatPower(float dt)
    {
        if (powerState == null)
            ResolvePowerState();

        if (powerState == null)
            return false;

        float amount = PowerDemandPerSecond * dt;
        return powerState.TryConsume(amount);
    }

    private void ResolvePowerState()
    {
        if (installedModule == null)
            installedModule = GetComponent<InstalledModule>();

        Hardpoint hp = installedModule != null ? installedModule.OwnerHardpoint : null;

        Boat boat = null;

        if (hp != null)
            boat = hp.GetComponentInParent<Boat>();

        if (boat == null)
            boat = GetComponentInParent<Boat>();

        powerState = boat != null ? boat.GetComponent<BoatPowerState>() : null;
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

    public bool TryConsumeFuelCharges(int amount)
    {
        if (amount <= 0)
            return false;

        int remaining = amount;

        if (fuelContainerItem == null || !fuelContainerItem.IsContainer)
        {
            isOn = false;
            currentRunMode = EngineRunMode.None;
            return false;
        }

        ItemContainerState state = fuelContainerItem.ContainerState;
        if (state == null)
        {
            isOn = false;
            currentRunMode = EngineRunMode.None;
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
            currentRunMode = EngineRunMode.None;
            return false;
        }

        if (!CanRun())
        {
            isOn = false;
            currentRunMode = EngineRunMode.None;
        }

        return true;
    }

    public void OnInstalled(Hardpoint owner)
    {
        installedModule = GetComponent<InstalledModule>();
        InitializeFuel();
        ResolvePowerState();
    }

    public void OnRemoved()
    {

    }

    public ItemInstanceSnapshot CaptureFuelContainerSnapshot()
    {
        return fuelContainerItem != null ? fuelContainerItem.ToSnapshot() : null;
    }

    public void RestorePersistentState(
        bool restoredIsOn,
        ItemInstanceSnapshot fuelSnapshot,
        IItemDefinitionResolver resolver)
    {
        fuelContainerItem = ItemInstance.FromSnapshot(fuelSnapshot, resolver);

        if (fuelContainerItem == null)
            InitializeFuel();

        fuelBurnAccumulator = 0f;
        throttleLoad01 = 0f;

        ResolvePowerState();
        SetOn(restoredIsOn);
    }
}