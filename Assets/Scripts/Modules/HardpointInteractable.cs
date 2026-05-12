using UnityEngine;

[DisallowMultipleComponent]
public sealed class HardpointInteractable :
    MonoBehaviour,
    IInteractable,
    IPickupInteractable,
    IInteractPromptProvider,
    IPickupPromptProvider,
    IToggleInteractable
{
    [Header("Refs")]
    [SerializeField] private Hardpoint hardpoint;
    [SerializeField] private Transform promptAnchor;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 20;
    [SerializeField] private int pickupPriority = 20;
    [SerializeField] private float maxDistance = 1.75f;

    [Header("Boat Access")]
    [Tooltip("If true, hardpoints/modules that belong to a Boat can only be used by players boarded on that same boat.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    [Tooltip("If true, hardpoints not under a Boat remain usable. This keeps future dock/ruin/world modules possible until they get their own access context.")]
    [SerializeField] private bool allowAccessWhenNotPartOfBoat = true;

    [Header("Removal")]
    [SerializeField] private PickupInteractionMode pickupMode = PickupInteractionMode.Hold;
    [SerializeField] private float pickupHoldDuration = 0.5f;

    public int InteractionPriority => interactionPriority;
    public int PickupPriority => pickupPriority;
    public PickupInteractionMode PickupMode => pickupMode;
    public float PickupHoldDuration => pickupHoldDuration;

    private Boat _cachedBoat;

    private void Reset()
    {
        if (hardpoint == null)
            hardpoint = GetComponent<Hardpoint>();

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoat();
    }

    private void Awake()
    {
        if (hardpoint == null)
            hardpoint = GetComponent<Hardpoint>();

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoat();
    }

    public bool CanInteract(in InteractContext context)
    {
        if (hardpoint == null)
            return false;

        if (!IsInRange(context))
            return false;

        if (!CanAccessHardpointByContext(context))
            return false;

        if (hardpoint.HasInstalledModule)
            return true;

        return TryFindCompatiblePlayerModule(
            context,
            out _,
            out _,
            out _,
            out _);
    }

    public void Interact(in InteractContext context)
    {
        if (hardpoint == null || !IsInRange(context))
            return;

        if (!CanAccessHardpointByContext(context))
            return;

        if (!hardpoint.HasInstalledModule)
        {
            TryInstallFromPlayerInventory(context);
            return;
        }

        OpenInstalledModuleUI();
    }

    public bool CanPickup(in InteractContext context)
    {
        if (hardpoint == null)
            return false;

        if (!hardpoint.HasInstalledModule)
            return false;

        if (!IsInRange(context))
            return false;

        if (!CanAccessHardpointByContext(context))
            return false;

        return true;
    }

    public void Pickup(in InteractContext context)
    {
        if (hardpoint == null || !hardpoint.HasInstalledModule || !IsInRange(context))
            return;

        if (!CanAccessHardpointByContext(context))
            return;

        if (InstalledModuleHasContents())
        {
            Debug.Log("[HardpointInteractable] Cannot remove module while it contains items.", this);
            return;
        }

        PlayerInventory inventory = context.InteractorGO != null
            ? context.InteractorGO.GetComponentInChildren<PlayerInventory>()
            : null;

        if (inventory == null)
            return;

        if (!hardpoint.TryRemove(out ModuleDefinition removedDefinition))
            return;

        ItemDefinition itemDef = removedDefinition != null ? removedDefinition.ItemDefinition : null;
        if (itemDef == null)
        {
            Debug.LogWarning("[HardpointInteractable] Removed module had no linked ItemDefinition.", this);
            return;
        }

        ItemInstance returnedItem = ItemInstance.Create(itemDef, 1);

        if (!inventory.TryAutoInsert(returnedItem, out ItemInstance remainder))
        {
            hardpoint.TryInstall(removedDefinition, out _);
            return;
        }

        if (remainder != null && !remainder.IsDepleted())
        {
            hardpoint.TryInstall(removedDefinition, out _);
            return;
        }

        inventory.NotifyChanged();
    }

    public bool CanToggle(in InteractContext context)
    {
        if (hardpoint == null || !hardpoint.HasInstalledModule || !IsInRange(context))
            return false;

        if (!CanAccessHardpointByContext(context))
            return false;

        return TryGetInstalledToggleable(out _);
    }

    public void Toggle(in InteractContext context)
    {
        if (!CanAccessHardpointByContext(context))
            return;

        if (!TryGetInstalledToggleable(out IModuleToggleable toggleable))
            return;

        toggleable.Toggle();
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (hardpoint == null)
            return "Use Hardpoint";

        if (!CanAccessHardpointByContext(context))
            return "Board Boat";

        if (!hardpoint.HasInstalledModule)
        {
            if (TryFindCompatiblePlayerModule(
                    context,
                    out _,
                    out ItemInstance moduleItem,
                    out _,
                    out _)
                && moduleItem?.Definition?.ModuleDefinition != null)
            {
                return $"Install {moduleItem.Definition.DisplayName}";
            }

            return "Install Module";
        }

        return "Open Module";
    }

    public string GetPickupPromptVerb(in InteractContext context)
    {
        if (!CanAccessHardpointByContext(context))
            return "Board Boat";

        if (hardpoint != null && hardpoint.HasInstalledModule)
        {
            if (InstalledModuleHasContents())
                return "Remove Module (Empty First)";

            return "Remove Module";
        }

        return "Pick Up";
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    public EngineModule GetInstalledEngine()
    {
        if (hardpoint == null || !hardpoint.HasInstalledModule || hardpoint.InstalledModule == null)
            return null;

        return hardpoint.InstalledModule.GetComponent<EngineModule>();
    }

    private bool TryGetInstalledToggleable(out IModuleToggleable toggleable)
    {
        toggleable = null;

        if (hardpoint == null || !hardpoint.HasInstalledModule || hardpoint.InstalledModule == null)
            return false;

        MonoBehaviour[] behaviours = hardpoint.InstalledModule.GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IModuleToggleable candidate)
            {
                toggleable = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetInstalledToggleState(out bool isOn, out string label)
    {
        isOn = false;
        label = "Module";

        if (hardpoint == null || !hardpoint.HasInstalledModule || hardpoint.InstalledModule == null)
            return false;

        InstalledModule installed = hardpoint.InstalledModule;
        ModuleDefinition def = installed.Definition;

        label = def != null ? def.DisplayName : "Module";

        if (installed.TryGetComponent(out EngineModule engine))
        {
            isOn = engine.IsOn;
            return true;
        }

        if (installed.TryGetComponent(out GeneratorModule generator))
        {
            isOn = generator.IsOn;
            return true;
        }

        if (installed.TryGetComponent(out PumpModule pump))
        {
            isOn = pump.IsOn;
            return true;
        }

        if (installed.TryGetComponent(out TurretModule turret))
        {
            isOn = turret.IsOn;
            return true;
        }

        return false;
    }

    private void TryInstallFromPlayerInventory(in InteractContext context)
    {
        if (!TryFindCompatiblePlayerModule(
                context,
                out PlayerInventory inventory,
                out ItemInstance moduleItem,
                out BottomBarSlotType sourceSlotType,
                out bool sourceIsEquipment))
        {
            return;
        }

        if (moduleItem == null || moduleItem.Definition == null)
            return;

        ModuleDefinition moduleDefinition = moduleItem.Definition.ModuleDefinition;
        if (moduleDefinition == null)
            return;

        if (!hardpoint.TryInstall(moduleDefinition, out _))
            return;

        bool removedFromSource = false;

        if (sourceIsEquipment)
        {
            PlayerEquipment equipment = context.InteractorGO != null
                ? context.InteractorGO.GetComponentInChildren<PlayerEquipment>(true)
                : null;

            if (equipment != null)
            {
                ItemInstance equipped = equipment.Get(sourceSlotType);
                if (ReferenceEquals(equipped, moduleItem))
                {
                    equipment.Remove(sourceSlotType);
                    removedFromSource = true;
                }
            }
        }
        else
        {
            int hotbarIndex = PlayerInventory.SlotTypeToHotbarIndex(sourceSlotType);
            InventorySlot sourceSlot = inventory.GetSlot(hotbarIndex);

            if (sourceSlot != null && ReferenceEquals(sourceSlot.Instance, moduleItem))
            {
                sourceSlot.Clear();
                removedFromSource = true;
            }
        }

        if (!removedFromSource)
        {
            hardpoint.TryRemove(out _);
            Debug.LogWarning(
                "[HardpointInteractable] Installed module but failed to remove source item. Rolled back install.",
                this);
            return;
        }

        inventory.NotifyChanged();
    }

    private void OpenInstalledModuleUI()
    {
        ModuleOverlayRunner runner = FindFirstObjectByType<ModuleOverlayRunner>();
        if (runner == null)
        {
            Debug.LogWarning("[HardpointInteractable] No ModuleOverlayRunner found.", this);
            return;
        }

        runner.OpenForHardpoint(hardpoint);
    }

    private bool IsInRange(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        return dist <= maxDistance;
    }

    private bool CanAccessHardpointByContext(in InteractContext context)
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        CacheBoat();

        // Future-friendly behavior:
        // Boat modules require matching boat boarding.
        // Non-boat hardpoints are allowed for now, so dock/ruin/world modules don't get blocked
        // before we have a broader access-domain system.
        if (_cachedBoat == null)
            return allowAccessWhenNotPartOfBoat;

        PlayerBoardingState boarding = FindBoardingState(context);
        if (boarding == null)
            return false;

        if (!boarding.IsBoarded)
            return false;

        return boarding.CurrentBoatRoot == _cachedBoat.transform;
    }

    private PlayerBoardingState FindBoardingState(in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerBoardingState fromGO =
                context.InteractorGO.GetComponentInParent<PlayerBoardingState>();

            if (fromGO != null)
                return fromGO;

            fromGO = context.InteractorGO.GetComponentInChildren<PlayerBoardingState>(true);

            if (fromGO != null)
                return fromGO;
        }

        if (context.InteractorTransform != null)
        {
            PlayerBoardingState fromTransform =
                context.InteractorTransform.GetComponentInParent<PlayerBoardingState>();

            if (fromTransform != null)
                return fromTransform;

            fromTransform =
                context.InteractorTransform.GetComponentInChildren<PlayerBoardingState>(true);

            if (fromTransform != null)
                return fromTransform;
        }

        return null;
    }

    private void CacheBoat()
    {
        if (_cachedBoat == null)
            _cachedBoat = GetComponentInParent<Boat>();
    }

    private bool TryFindCompatiblePlayerModule(
        in InteractContext context,
        out PlayerInventory inventory,
        out ItemInstance sourceItem,
        out BottomBarSlotType sourceSlotType,
        out bool sourceIsEquipment)
    {
        inventory = context.InteractorGO != null
            ? context.InteractorGO.GetComponentInChildren<PlayerInventory>()
            : null;

        sourceItem = null;
        sourceSlotType = BottomBarSlotType.None;
        sourceIsEquipment = false;

        if (inventory == null || hardpoint == null)
            return false;

        PlayerEquipment equipment = context.InteractorGO != null
            ? context.InteractorGO.GetComponentInChildren<PlayerEquipment>(true)
            : null;

        if (equipment == null)
            return false;

        BottomBarSlotType selectedType = inventory.SelectedSlot;

        if (selectedType >= BottomBarSlotType.Hotbar0 &&
            selectedType <= BottomBarSlotType.Hotbar7)
        {
            InventorySlot selectedHotbar =
                inventory.GetSlot(PlayerInventory.SlotTypeToHotbarIndex(selectedType));

            if (IsCompatibleModuleItem(selectedHotbar?.Instance))
            {
                sourceItem = selectedHotbar.Instance;
                sourceSlotType = selectedType;
                sourceIsEquipment = false;
                return true;
            }
        }
        else
        {
            ItemInstance equippedItem = equipment.Get(selectedType);
            if (IsCompatibleModuleItem(equippedItem))
            {
                sourceItem = equippedItem;
                sourceSlotType = selectedType;
                sourceIsEquipment = true;
                return true;
            }
        }

        for (int i = 0; i < inventory.HotbarSlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (IsCompatibleModuleItem(slot?.Instance))
            {
                sourceItem = slot.Instance;
                sourceSlotType = PlayerInventory.HotbarIndexToSlotType(i);
                sourceIsEquipment = false;
                return true;
            }
        }

        return false;
    }

    private bool IsCompatibleModuleItem(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        if (!item.Definition.IsModule)
            return false;

        ModuleDefinition moduleDefinition = item.Definition.ModuleDefinition;
        if (moduleDefinition == null)
            return false;

        return hardpoint != null && hardpoint.CanInstall(moduleDefinition);
    }

    private bool InstalledModuleHasContents()
    {
        if (hardpoint == null || !hardpoint.HasInstalledModule || hardpoint.InstalledModule == null)
            return false;

        InstalledModule installed = hardpoint.InstalledModule;

        if (installed.TryGetComponent(out EngineModule engine))
            return ContainerHasContents(engine.FuelContainerItem);

        if (installed.TryGetComponent(out GeneratorModule generator))
            return ContainerHasContents(generator.FuelContainerItem);

        // Later:
        // if (installed.TryGetComponent(out AmmoModule ammo)) ...
        // if (installed.TryGetComponent(out StorageModule storage)) ...

        return false;
    }

    private static bool ContainerHasContents(ItemInstance container)
    {
        if (container == null || !container.IsContainer || container.ContainerState == null)
            return false;

        ItemContainerState state = container.ContainerState;

        for (int i = 0; i < state.SlotCount; i++)
        {
            InventorySlot slot = state.GetSlot(i);
            if (slot != null && !slot.IsEmpty && slot.Instance != null)
                return true;
        }

        return false;
    }
}