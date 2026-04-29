using UnityEngine;

[DisallowMultipleComponent]
public sealed class HardpointInteractable : MonoBehaviour, IInteractable, IPickupInteractable, IInteractPromptProvider, IPickupPromptProvider, IToggleInteractable
{
    [SerializeField] private Hardpoint hardpoint;
    [SerializeField] private int interactionPriority = 20;
    [SerializeField] private int pickupPriority = 20;
    [SerializeField] private float maxDistance = 1.75f;
    [SerializeField] private Transform promptAnchor;

    [Header("Removal")]
    [SerializeField] private PickupInteractionMode pickupMode = PickupInteractionMode.Hold;
    [SerializeField] private float pickupHoldDuration = 0.5f;

    public int InteractionPriority => interactionPriority;
    public int PickupPriority => pickupPriority;
    public PickupInteractionMode PickupMode => pickupMode;
    public float PickupHoldDuration => pickupHoldDuration;

    private void Awake()
    {
        if (hardpoint == null)
            hardpoint = GetComponent<Hardpoint>();
    }

    public bool CanInteract(in InteractContext context)
    {
        if (hardpoint == null)
            return false;

        if (!IsInRange(context))
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

        if (!hardpoint.HasInstalledModule)
        {
            if (!TryFindCompatiblePlayerModule(
                context,
                out PlayerInventory inventory,
                out ItemInstance moduleItem,
                out BottomBarSlotType sourceSlotType,
                out bool sourceIsEquipment))
                return;

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
                Debug.LogWarning("[HardpointInteractable] Installed module but failed to remove source item. Rolled back install.", this);
                return;
            }

            inventory.NotifyChanged();
            return;
        }

        // OCCUPIED HARDPOINT: open module UI
        ModuleOverlayRunner runner = FindFirstObjectByType<ModuleOverlayRunner>();
        if (runner == null)
        {
            Debug.LogWarning("[HardpointInteractable] No ModuleOverlayRunner found.");
            return;
        }

        runner.OpenForHardpoint(hardpoint);

        Debug.Log($"[HardpointInteractable] Installed module has no engine fuel inventory on '{hardpoint.HardpointId}'.", this);
    }

    public bool CanPickup(in InteractContext context)
    {
        if (hardpoint == null)
            return false;

        if (!hardpoint.HasInstalledModule)
            return false;

        return IsInRange(context);
    }

    public void Pickup(in InteractContext context)
    {
        if (hardpoint == null || !hardpoint.HasInstalledModule || !IsInRange(context))
            return;

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
            Debug.LogWarning("[HardpointInteractable] Removed module had no linked ItemDefinition.");
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

        return GetInstalledEngine() != null || GetInstalledPump() != null;
    }

    public void Toggle(in InteractContext context)
    {
        EngineModule engine = GetInstalledEngine();
        if (engine != null)
        {
            engine.Toggle();
            return;
        }

        PumpModule pump = GetInstalledPump();
        if (pump != null)
        {
            pump.Toggle();
            return;
        }
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (hardpoint == null)
            return "Use Hardpoint";

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

        EngineModule engine = GetInstalledEngine();
        if (engine != null)
            return "Open Engine";

        PumpModule pump = GetInstalledPump();
        if (pump != null)
            return "Open Pump";

        return "Open Module";
    }

    public Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private bool IsInRange(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        return dist <= maxDistance;
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

        PlayerEquipment equipment = context.InteractorGO.GetComponentInChildren<PlayerEquipment>(true);
        if (equipment == null)
            return false;

        BottomBarSlotType selectedType = inventory.SelectedSlot;

        if (selectedType >= BottomBarSlotType.Hotbar0 && selectedType <= BottomBarSlotType.Hotbar7)
        {
            InventorySlot selectedHotbar = inventory.GetSlot(PlayerInventory.SlotTypeToHotbarIndex(selectedType));
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

    public string GetPickupPromptVerb(in InteractContext context)
    {
        if (hardpoint != null && hardpoint.HasInstalledModule)
            return "Remove Module";

        return "Pick Up";
    }

    public EngineModule GetInstalledEngine()
    {
        if (hardpoint == null || !hardpoint.HasInstalledModule || hardpoint.InstalledModule == null)
            return null;

        return hardpoint.InstalledModule.GetComponent<EngineModule>();
    }

    public PumpModule GetInstalledPump()
    {
        if (hardpoint == null || !hardpoint.HasInstalledModule || hardpoint.InstalledModule == null)
            return null;

        return hardpoint.InstalledModule.GetComponent<PumpModule>();
    }
}