using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class MoneyChestSecureSlot :
    MonoBehaviour,
    IInteractable,
    IInteractPromptProvider,
    IInteractionLabelProvider,
    IInteractionRangeProvider
{
    [Header("Identity")]
    [SerializeField] private string stableId = "money_chest_slot_01";

    [Header("Refs")]
    [SerializeField] private Transform chestAnchor;

    [Tooltip("Assign item_money_chest. Required for securing the chest from the player's hands.")]
    [SerializeField] private ItemDefinition moneyChestItemDefinition;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 60;
    [SerializeField, Min(0f)] private float actionRange = 1.75f;
    [SerializeField, Min(0.1f)] private float nearbyChestSearchRadius = 1.5f;

    [Header("Rules")]
    [SerializeField] private bool acceptsReplacementSpawns = true;

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages = true;

    public MoneyChestState SecuredChest { get; private set; }

    public int InteractionPriority => interactionPriority;
    public bool AcceptsReplacementSpawns => acceptsReplacementSpawns;
    public string StableId => stableId;

    public Transform ChestAnchorOrSelf =>
        chestAnchor != null ? chestAnchor : transform;

    public bool HasSecuredChest =>
        SecuredChest != null &&
        !SecuredChest.IsRetired;

    public Vector3 SpawnPosition => ChestAnchorOrSelf.position;
    public Quaternion SpawnRotation => ChestAnchorOrSelf.rotation;

    private void Reset()
    {
        if (chestAnchor == null)
            chestAnchor = transform;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        if (chestAnchor == null)
            chestAnchor = transform;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        RefreshSecuredChestFromScene();
    }

    private void OnValidate()
    {
        if (chestAnchor == null)
            chestAnchor = transform;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        if (string.IsNullOrWhiteSpace(stableId))
            stableId = "money_chest_slot_01";
    }

    public static MoneyChestSecureSlot FindByStableId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        MoneyChestSecureSlot[] slots = FindObjectsByType<MoneyChestSecureSlot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < slots.Length; i++)
        {
            MoneyChestSecureSlot slot = slots[i];
            if (slot == null)
                continue;

            if (string.Equals(slot.StableId, id, System.StringComparison.Ordinal))
                return slot;
        }

        return null;
    }

    public void RefreshSecuredChestFromChildren()
    {
        RefreshSecuredChestFromScene();
    }

    public void RefreshSecuredChestFromScene()
    {
        if (!IsSceneInstance())
        {
            SecuredChest = null;
            return;
        }

        if (SecuredChest != null &&
            !SecuredChest.IsRetired &&
            IsChestSecuredAtThisSlot(SecuredChest))
        {
            return;
        }

        SecuredChest = FindSecuredChestAtThisSlot();
    }

    public bool CanInteract(in InteractContext context)
    {
        if (!IsSceneInstance())
            return false;

        if (!IsInRange(context))
            return false;

        RefreshSecuredChestFromScene();

        // Once occupied, no slot-level interaction for now.
        // The chest itself can still open/pickup normally.
        if (HasSecuredChest)
            return false;

        if (FindNearbyActiveChest() != null)
            return true;

        return TryFindActiveChestInHands(context, out _);
    }

    public void Interact(in InteractContext context)
    {
        if (!IsSceneInstance())
        {
            LogWarning("Cannot interact. Slot is not a scene instance.");
            return;
        }

        if (!IsInRange(context))
            return;

        RefreshSecuredChestFromScene();

        if (HasSecuredChest)
            return;

        MoneyChestState nearby = FindNearbyActiveChest();
        if (nearby != null)
        {
            TrySecureChest(nearby, false);
            return;
        }

        TrySecureChestFromHands(context);
    }

    public string GetPromptVerb(in InteractContext context)
    {
        RefreshSecuredChestFromScene();

        if (HasSecuredChest)
            return null;

        if (TryFindActiveChestInHands(context, out _))
            return "Secure Held Money Chest";

        return "Secure Money Chest";
    }

    public Transform GetPromptAnchor()
    {
        return ChestAnchorOrSelf;
    }

    public string GetInteractionLabel(in InteractContext context)
    {
        RefreshSecuredChestFromScene();

        return HasSecuredChest
            ? "Money Chest Slot"
            : "Empty Money Chest Slot";
    }

    public bool TryGetHoverNameRange(out float range)
    {
        range = actionRange + 1.0f;
        return true;
    }

    public bool TryGetActionRange(out float range)
    {
        range = actionRange;
        return true;
    }

    public bool TrySecureChest(MoneyChestState chest, bool isReplacementSpawn)
    {
        if (!IsSceneInstance())
        {
            LogWarning("Cannot secure chest. Slot is not a scene instance.");
            return false;
        }

        if (chest == null || chest.IsRetired)
            return false;

        RefreshSecuredChestFromScene();

        if (HasSecuredChest && SecuredChest != chest)
        {
            Log(
                $"Cannot secure chest '{chest.ChestInstanceId}'. " +
                $"Slot already contains '{SecuredChest.ChestInstanceId}'.");

            return false;
        }

        GameObject root = ResolveChestRoot(chest);
        if (root == null || !IsSceneObject(root))
            return false;

        EnsureChestIsRuntimeItem(chest);

        Boat boat = GetComponentInParent<Boat>();
        EnsureBoatOwnership(root, boat);

        root.transform.SetPositionAndRotation(SpawnPosition, SpawnRotation);

        MoneyChestSlotSecuredItem marker = root.GetComponent<MoneyChestSlotSecuredItem>();
        if (marker == null)
            marker = root.AddComponent<MoneyChestSlotSecuredItem>();

        Vector2 localPos = Vector2.zero;
        float localRotZ = 0f;

        if (boat != null)
        {
            Vector3 local = boat.transform.InverseTransformPoint(SpawnPosition);
            localPos = new Vector2(local.x, local.y);

            localRotZ = Mathf.DeltaAngle(
                boat.transform.eulerAngles.z,
                SpawnRotation.eulerAngles.z);
        }

        marker.SecureToSlot(
            boat,
            this,
            stableId,
            localPos,
            localRotZ);

        MoneyChestInteractable[] interactables =
            root.GetComponentsInChildren<MoneyChestInteractable>(true);

        for (int i = 0; i < interactables.Length; i++)
            interactables[i].RebindRuntimeRefs(chest);

        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        if (treasury != null)
            treasury.RegisterChest(chest);

        SecuredChest = chest;

        Physics2D.SyncTransforms();

        Log(
            $"Secured money chest id='{chest.ChestInstanceId}', " +
            $"replacement={isReplacementSpawn}");

        return true;
    }

    public void AdoptRestoredChest(MoneyChestState chest)
    {
        if (chest == null || chest.IsRetired)
            return;

        SecuredChest = chest;
    }

    private bool TrySecureChestFromHands(in InteractContext context)
    {
        if (!TryFindActiveChestInHands(context, out PlayerLoadoutPersistence loadout))
            return false;

        PlayerLoadoutSnapshot snapshot = loadout.CaptureSnapshot();
        ItemInstanceSnapshot handSnapshot = snapshot?.equipment?.hands;

        if (handSnapshot == null || string.IsNullOrWhiteSpace(handSnapshot.instanceId))
            return false;

        if (moneyChestItemDefinition == null)
        {
            LogWarning("Cannot secure held money chest. moneyChestItemDefinition is not assigned.");
            return false;
        }

        WorldItem prefab = moneyChestItemDefinition.WorldPrefab;
        if (prefab == null)
        {
            LogWarning("Cannot secure held money chest. moneyChestItemDefinition has no WorldPrefab.");
            return false;
        }

        ItemInstance instance = ItemInstance.Create(
            moneyChestItemDefinition,
            Mathf.Max(1, handSnapshot.quantity));

        instance.ForceSetInstanceIdForRestore(handSnapshot.instanceId);

        WorldItem spawned = Instantiate(prefab, SpawnPosition, SpawnRotation);
        spawned.Initialize(instance);

        MoneyChestState chest =
            spawned.GetComponent<MoneyChestState>() ??
            spawned.GetComponentInChildren<MoneyChestState>(true);

        if (chest == null)
        {
            LogWarning("Spawned held money chest prefab, but no MoneyChestState was found. Destroying spawned object.");
            Destroy(spawned.gameObject);
            return false;
        }

        int balance = Mathf.Max(0, MoneyService.Balance);

        chest.RestoreState(
            handSnapshot.instanceId,
            balance,
            MoneyChestLifecycleState.Active);

        bool secured = TrySecureChest(chest, false);

        if (!secured)
        {
            Destroy(spawned.gameObject);
            return false;
        }

        snapshot.equipment.hands = null;
        loadout.RestoreSnapshot(snapshot);

        if (GameState.I != null)
            GameState.I.playerLoadout = snapshot;

        Log($"Secured held money chest id='{handSnapshot.instanceId}', balance={balance}.");

        return true;
    }

    private bool TryFindActiveChestInHands(
        in InteractContext context,
        out PlayerLoadoutPersistence loadout)
    {
        loadout = null;

        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        if (treasury == null || string.IsNullOrWhiteSpace(treasury.ActiveChestInstanceId))
            return false;

        if (context.InteractorGO != null)
        {
            loadout =
                context.InteractorGO.GetComponent<PlayerLoadoutPersistence>() ??
                context.InteractorGO.GetComponentInChildren<PlayerLoadoutPersistence>(true) ??
                context.InteractorGO.GetComponentInParent<PlayerLoadoutPersistence>();
        }

        if (loadout == null && context.InteractorTransform != null)
        {
            loadout =
                context.InteractorTransform.GetComponent<PlayerLoadoutPersistence>() ??
                context.InteractorTransform.GetComponentInChildren<PlayerLoadoutPersistence>(true) ??
                context.InteractorTransform.GetComponentInParent<PlayerLoadoutPersistence>();
        }

        if (loadout == null)
            loadout = FindAnyObjectByType<PlayerLoadoutPersistence>();

        if (loadout == null)
            return false;

        PlayerLoadoutSnapshot snapshot = loadout.CaptureSnapshot();
        ItemInstanceSnapshot hands = snapshot?.equipment?.hands;

        if (hands == null || string.IsNullOrWhiteSpace(hands.instanceId))
            return false;

        return string.Equals(
            hands.instanceId,
            treasury.ActiveChestInstanceId,
            System.StringComparison.Ordinal);
    }

    private MoneyChestState FindNearbyActiveChest()
    {
        MoneyChestState[] chests =
            FindObjectsByType<MoneyChestState>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        MoneyChestState best = null;
        float bestDist = float.PositiveInfinity;

        Vector2 center = SpawnPosition;

        for (int i = 0; i < chests.Length; i++)
        {
            MoneyChestState chest = chests[i];
            if (chest == null || !chest.IsActive || chest.IsRetired)
                continue;

            if (IsChestSecuredAtThisSlot(chest))
                continue;

            GameObject root = ResolveChestRoot(chest);
            if (root == null || !IsSceneObject(root))
                continue;

            float dist = Vector2.Distance(center, root.transform.position);
            if (dist > nearbyChestSearchRadius)
                continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = chest;
            }
        }

        return best;
    }

    private MoneyChestState FindSecuredChestAtThisSlot()
    {
        MoneyChestState[] chests =
            FindObjectsByType<MoneyChestState>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        for (int i = 0; i < chests.Length; i++)
        {
            MoneyChestState chest = chests[i];
            if (chest == null || chest.IsRetired)
                continue;

            if (IsChestSecuredAtThisSlot(chest))
                return chest;
        }

        return null;
    }

    private bool IsChestSecuredAtThisSlot(MoneyChestState chest)
    {
        if (chest == null)
            return false;

        GameObject root = ResolveChestRoot(chest);
        if (root == null)
            return false;

        MoneyChestSlotSecuredItem marker = root.GetComponent<MoneyChestSlotSecuredItem>();
        if (marker == null || !marker.IsSecured)
            return false;

        return string.Equals(
            marker.SlotStableId,
            stableId,
            System.StringComparison.Ordinal);
    }

    private void EnsureChestIsRuntimeItem(MoneyChestState chest)
    {
        if (chest == null)
            return;

        chest.SyncInstanceIdFromWorldItem();
        chest.EnsureInstanceId();

        GameObject root = ResolveChestRoot(chest);
        if (root == null)
            return;

        WorldItem worldItem =
            root.GetComponent<WorldItem>() ??
            root.GetComponentInChildren<WorldItem>(true);

        if (worldItem == null)
            return;

        if (worldItem.Instance == null || worldItem.Instance.Definition == null)
        {
            if (moneyChestItemDefinition == null)
            {
                LogWarning("Money chest has no item instance and moneyChestItemDefinition is not assigned.");
                return;
            }

            ItemInstance instance = ItemInstance.Create(moneyChestItemDefinition, 1);
            instance.ForceSetInstanceIdForRestore(chest.ChestInstanceId);
            worldItem.Initialize(instance);
        }

        chest.SyncInstanceIdFromWorldItem();
        chest.EnsureInstanceId();
    }

    private void EnsureBoatOwnership(GameObject root, Boat boat)
    {
        if (root == null || boat == null)
            return;

        BoatOwnedItem owned = root.GetComponent<BoatOwnedItem>();
        if (owned == null)
            owned = root.AddComponent<BoatOwnedItem>();

        BoatOwnedItemLayerPolicy layerPolicy = root.GetComponent<BoatOwnedItemLayerPolicy>();
        if (layerPolicy == null)
            root.AddComponent<BoatOwnedItemLayerPolicy>();

        BoatOwnedItemVisualPolicy visualPolicy = root.GetComponent<BoatOwnedItemVisualPolicy>();
        if (visualPolicy == null)
            root.AddComponent<BoatOwnedItemVisualPolicy>();

        owned.AssignToBoat(boat);
    }

    private bool IsInRange(in InteractContext context)
    {
        if (context.InteractorTransform == null)
            return false;

        Vector2 origin = context.Origin;
        Vector2 target = SpawnPosition;

        return Vector2.Distance(origin, target) <= actionRange;
    }

    private bool IsSceneInstance()
    {
        return IsSceneObject(gameObject);
    }

    private static bool IsSceneObject(GameObject go)
    {
        if (go == null)
            return false;

        Scene scene = go.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    private static GameObject ResolveChestRoot(MoneyChestState chest)
    {
        if (chest == null)
            return null;

        WorldItem worldItem =
            chest.GetComponent<WorldItem>() ??
            chest.GetComponentInParent<WorldItem>();

        if (worldItem != null)
            return worldItem.gameObject;

        return chest.gameObject;
    }

    private void Log(string message)
    {
        if (!logDebugMessages)
            return;

        Debug.Log($"[MoneyChestSecureSlot:{name}] {message}", this);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MoneyChestSecureSlot:{name}] {message}", this);
    }
}