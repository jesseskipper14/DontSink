using UnityEngine;

[DisallowMultipleComponent]
public sealed class RackStoredCargoInteractable :
    MonoBehaviour,
    IPickupInteractable,
    IPickupPromptProvider
{
    [Header("Refs")]
    [SerializeField] private StorageModule storageModule;
    [SerializeField] private TradeCargoPrefabCatalog cargoPrefabCatalog;
    [SerializeField] private Transform promptAnchor;

    [Header("Slot")]
    [SerializeField] private int slotIndex = -1;

    [Header("Pickup")]
    [SerializeField] private int pickupPriority = 45;
    [SerializeField] private PickupInteractionMode pickupMode = PickupInteractionMode.Hold;
    [SerializeField] private float pickupHoldDuration = 0.35f;
    [SerializeField] private float maxPickupDistance = 1.5f;

    [Header("Restore")]
    [SerializeField] private Vector2 fallbackWorldOffset = new Vector2(0.25f, 0.05f);

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public int PickupPriority => pickupPriority;
    public PickupInteractionMode PickupMode => pickupMode;
    public float PickupHoldDuration => Mathf.Max(0.05f, pickupHoldDuration);

    public void Initialize(
        StorageModule module,
        int index,
        TradeCargoPrefabCatalog catalog)
    {
        storageModule = module;
        slotIndex = index;
        cargoPrefabCatalog = catalog;

        if (promptAnchor == null)
            promptAnchor = transform;
    }

    private void Awake()
    {
        if (promptAnchor == null)
            promptAnchor = transform;
    }

    public bool CanPickup(in InteractContext context)
    {
        if (!IsInRange(context))
            return false;

        if (cargoPrefabCatalog == null)
            return false;

        if (!TryPeekSnapshot(out CargoCrateStoredSnapshot snapshot))
            return false;

        if (snapshot == null)
            return false;

        PlayerCarryController2D carry = FindCarryController(context);
        if (carry == null)
            return false;

        // Cargo can only go rack -> hands here.
        // If hands are full, do not remove the cargo from the rack.
        return !carry.IsCarrying;
    }

    public void Pickup(in InteractContext context)
    {
        if (!CanPickup(context))
            return;

        if (!TryPeekSnapshot(out CargoCrateStoredSnapshot snapshot) || snapshot == null)
            return;

        PlayerCarryController2D carry = FindCarryController(context);
        if (carry == null || carry.IsCarrying)
            return;

        Vector3 worldPos = transform.position;
        Quaternion worldRot = transform.rotation;

        CargoCrate restored = CargoCrateSnapshotUtility.RestoreToWorld(
            snapshot,
            cargoPrefabCatalog,
            worldPos,
            worldRot,
            parent: null);

        if (restored == null)
        {
            Log("Pickup failed: RestoreToWorld returned null.");
            return;
        }

        // Only clear the rack after restore succeeded.
        CargoCrateStoredSnapshot removed = storageModule.RemoveCargoCrateSnapshotAt(slotIndex);
        if (removed == null)
        {
            // Rack state changed under us. Don't delete the restored crate;
            // leave it in the world rather than silently eating cargo. Deliciously ugly, but safe.
            restored.transform.position = worldPos + (Vector3)fallbackWorldOffset;
            Log("Pickup warning: restored crate, but rack slot was already empty. Leaving crate in world.");
            return;
        }

        carry.ToggleCarry(restored);

        if (carry.CarriedCargo != restored)
        {
            // Should be rare because CanPickup checks IsCarrying first.
            // Leave crate in world as a fallback instead of losing it.
            restored.transform.position = worldPos + (Vector3)fallbackWorldOffset;
            Log("Pickup warning: restored crate but carry controller did not pick it up. Leaving crate in world.");
            return;
        }

        storageModule.CargoRackState?.NotifyChanged();
        storageModule.ContainerState?.NotifyChanged();

        Log($"Picked up cargo from rack slot {slotIndex}: itemId='{snapshot.itemId}' quantity={snapshot.quantity}");
    }

    public string GetPickupPromptVerb(in InteractContext context)
    {
        if (TryPeekSnapshot(out CargoCrateStoredSnapshot snapshot) && snapshot != null)
            return $"Pick Up {FormatCargoName(snapshot)}";

        return "Pick Up Cargo";
    }

    private static string FormatCargoName(CargoCrateStoredSnapshot snapshot)
    {
        if (snapshot == null)
            return "Cargo";

        string raw = !string.IsNullOrWhiteSpace(snapshot.itemId)
            ? snapshot.itemId
            : "Cargo";

        return $"Crate of {ToDisplayName(raw)}";
    }

    private static string ToDisplayName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Cargo";

        raw = raw.Replace("_", " ").Replace("-", " ").Trim();

        if (raw.Length == 0)
            return "Cargo";

        string[] parts = raw.Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(parts[i]))
                continue;

            string p = parts[i];
            parts[i] = char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1).ToLowerInvariant() : "");
        }

        return string.Join(" ", parts);
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    private bool TryPeekSnapshot(out CargoCrateStoredSnapshot snapshot)
    {
        snapshot = null;

        if (storageModule == null)
            return false;

        storageModule.EnsureContainer();

        CargoRackState state = storageModule.CargoRackState;
        if (state == null)
            return false;

        if (slotIndex < 0 || slotIndex >= state.SlotCount)
            return false;

        CargoRackSlot slot = state.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty)
            return false;

        snapshot = slot.Crate;
        return snapshot != null;
    }

    private bool IsInRange(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        return dist <= maxPickupDistance;
    }

    private static PlayerCarryController2D FindCarryController(in InteractContext context)
    {
        if (context.InteractorGO == null)
            return null;

        PlayerCarryController2D carry =
            context.InteractorGO.GetComponentInParent<PlayerCarryController2D>();

        if (carry != null)
            return carry;

        return context.InteractorGO.GetComponentInChildren<PlayerCarryController2D>(true);
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[RackStoredCargoInteractable:{name}] {msg}", this);
    }
}