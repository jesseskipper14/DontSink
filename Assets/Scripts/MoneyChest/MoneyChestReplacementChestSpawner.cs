using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class MoneyChestReplacementChestSpawner : MonoBehaviour
{
    [Header("Replacement Chest")]
    [SerializeField] private GameObject replacementChestPrefab;

    [Header("Item Identity")]
    [Tooltip("Assign item_money_chest here. Required so runtime-spawned replacement chests are pickupable immediately.")]
    [SerializeField] private ItemDefinition moneyChestItemDefinition;

    [Header("Secure Slot Preferred Spawn")]
    [SerializeField] private bool trySecureSlotFirst = true;

    [Tooltip("If assigned, use this slot before searching the scene.")]
    [SerializeField] private MoneyChestSecureSlot explicitSecureSlot;

    [Header("Fallback Spawn")]
    [SerializeField] private Transform fallbackSpawnPoint;

    [Tooltip("Used to detect an already-spawned replacement chest so we do not stack duplicates forever like fools.")]
    [SerializeField] private Collider2D fallbackSpawnArea;

    [SerializeField, Min(0.1f)] private float fallbackExistingSearchRadius = 1.25f;

    [Header("Scene Rules")]
    [SerializeField] private bool onlySpawnInNodeScene = true;
    [SerializeField] private string nodeSceneNameContains = "Node";

    [Header("Timing")]
    [SerializeField, Min(0f)] private float maxWaitSeconds = 2f;
    [SerializeField] private bool registerExistingSceneChestsBeforeCheck = true;

    [Header("Secure Slot Wait")]
    [SerializeField, Min(0f)] private float waitForSecureSlotSeconds = 2f;

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages = true;

    private bool attemptedSpawn;

    private void Start()
    {
        StartCoroutine(SpawnWhenReady());
    }

    private IEnumerator SpawnWhenReady()
    {
        if (attemptedSpawn)
            yield break;

        attemptedSpawn = true;

        if (onlySpawnInNodeScene && !IsNodeScene())
        {
            Log("Skipped replacement chest spawn. Not a NodeScene.");
            yield break;
        }

        float elapsed = 0f;

        while ((GameState.I == null || MoneyChestTreasuryService.Instance == null) &&
               elapsed < maxWaitSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        if (treasury == null)
        {
            LogWarning("Skipped replacement chest spawn. No MoneyChestTreasuryService found.");
            yield break;
        }

        if (registerExistingSceneChestsBeforeCheck)
            treasury.RegisterExistingSceneChests();

        if (!treasury.ShouldOfferReplacementChest)
        {
            Log(
                $"Skipped replacement chest spawn. " +
                $"HasActiveChest={treasury.HasActiveChest}, LostCount={treasury.LostChestSnapshotCount}");
            yield break;
        }

        if (trySecureSlotFirst)
        {
            float slotWaitElapsed = 0f;

            while (slotWaitElapsed <= waitForSecureSlotSeconds)
            {
                if (TryAdoptExistingReplacementInSecureSlot(treasury))
                    yield break;

                if (TrySpawnReplacementIntoSecureSlot(treasury))
                    yield break;

                if (waitForSecureSlotSeconds <= 0f)
                    break;

                slotWaitElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Log($"No available money chest secure slot found after {waitForSecureSlotSeconds:0.00}s. Using fallback spawn.");
        }

        if (TryAdoptExistingReplacementInFallbackArea(treasury))
            yield break;

        SpawnReplacementAtFallback(treasury);
    }

    private bool TryAdoptExistingReplacementInSecureSlot(MoneyChestTreasuryService treasury)
    {
        MoneyChestSecureSlot[] slots = FindCandidateSecureSlots();
        if (slots == null || slots.Length == 0)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            MoneyChestSecureSlot slot = slots[i];
            if (slot == null)
                continue;

            slot.RefreshSecuredChestFromChildren();

            if (!slot.HasSecuredChest)
                continue;

            MoneyChestState chest = slot.SecuredChest;
            if (!IsReusableReplacementSceneChest(treasury, chest))
                continue;

            if (!BootstrapReplacementChestForRuntime(chest, destroyOnFailure: false))
                continue;

            bool registered = RegisterReplacementWithPreferredId(
                treasury,
                chest,
                chest.ChestInstanceId);

            if (!registered)
                continue;

            slot.TrySecureChest(chest, true);

            Log(
                $"Adopted existing replacement chest in secure slot. " +
                $"id='{chest.ChestInstanceId}', slot='{slot.name}'");

            return true;
        }

        return false;
    }

    private bool TrySpawnReplacementIntoSecureSlot(MoneyChestTreasuryService treasury)
    {
        MoneyChestSecureSlot slot = FindAvailableSecureSlot();
        if (slot == null)
            return false;

        MoneyChestSnapshot reusableSnapshot = null;
        treasury.TryFindReusableReplacementSnapshotForCurrentNode(out reusableSnapshot);

        Vector3 position = slot.SpawnPosition;
        Quaternion rotation = slot.SpawnRotation;

        GameObject spawned = SpawnReplacementObject(
            position,
            rotation,
            reusableSnapshot);

        if (spawned == null)
            return false;

        MoneyChestState chest = FindChest(spawned);
        if (chest == null)
        {
            LogWarning("Spawned replacement chest prefab, but no MoneyChestState was found. Destroying spawned object.");
            Destroy(spawned);
            return false;
        }

        if (!BootstrapReplacementChestForRuntime(chest, destroyOnFailure: true))
            return false;

        string preferredId = reusableSnapshot != null ? reusableSnapshot.chestInstanceId : null;

        bool registered = RegisterReplacementWithPreferredId(
            treasury,
            chest,
            preferredId);

        if (!registered)
        {
            LogWarning("Treasury rejected secure replacement chest. Destroying spawned object.");
            DestroyResolvedRoot(chest);
            return false;
        }

        bool secured = slot.TrySecureChest(chest, true);
        if (!secured)
        {
            LogWarning(
                $"Replacement chest registered active, but secure slot '{slot.name}' rejected it. " +
                $"Leaving it spawned at the slot position instead of deleting the active chest.");
        }

        Log(
            $"Spawned replacement chest for secure slot. " +
            $"id='{chest.ChestInstanceId}', slot='{slot.name}', secured={secured}, reusedSnapshot={reusableSnapshot != null}");

        return true;
    }

    private bool TryAdoptExistingReplacementInFallbackArea(MoneyChestTreasuryService treasury)
    {
        MoneyChestState[] sceneChests =
            FindObjectsByType<MoneyChestState>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        for (int i = 0; i < sceneChests.Length; i++)
        {
            MoneyChestState chest = sceneChests[i];
            if (!IsReusableReplacementSceneChest(treasury, chest))
                continue;

            if (!IsInsideFallbackSpawnArea(chest.transform.position))
                continue;

            if (!BootstrapReplacementChestForRuntime(chest, destroyOnFailure: false))
                continue;

            bool registered = RegisterReplacementWithPreferredId(
                treasury,
                chest,
                chest.ChestInstanceId);

            if (!registered)
                continue;

            Log(
                $"Adopted existing replacement chest in fallback spawn area. " +
                $"id='{chest.ChestInstanceId}', pos={chest.transform.position}");

            return true;
        }

        return false;
    }

    private void SpawnReplacementAtFallback(MoneyChestTreasuryService treasury)
    {
        MoneyChestSnapshot reusableSnapshot = null;
        treasury.TryFindReusableReplacementSnapshotForCurrentNode(out reusableSnapshot);

        Vector3 position =
            fallbackSpawnPoint != null
                ? fallbackSpawnPoint.position
                : transform.position;

        Quaternion rotation =
            fallbackSpawnPoint != null
                ? fallbackSpawnPoint.rotation
                : transform.rotation;

        GameObject spawned = SpawnReplacementObject(
            position,
            rotation,
            reusableSnapshot);

        if (spawned == null)
            return;

        MoneyChestState chest = FindChest(spawned);
        if (chest == null)
        {
            LogWarning("Spawned replacement chest prefab, but no MoneyChestState was found. Destroying spawned object.");
            Destroy(spawned);
            return;
        }

        if (!BootstrapReplacementChestForRuntime(chest, destroyOnFailure: true))
            return;

        string preferredId = reusableSnapshot != null ? reusableSnapshot.chestInstanceId : null;

        bool registered = RegisterReplacementWithPreferredId(
            treasury,
            chest,
            preferredId);

        if (!registered)
        {
            LogWarning("Treasury rejected fallback replacement chest. Destroying spawned object.");
            DestroyResolvedRoot(chest);
            return;
        }

        Log(
            $"Spawned fallback replacement chest. " +
            $"id='{chest.ChestInstanceId}', pos={position}, reusedSnapshot={reusableSnapshot != null}");
    }

    private GameObject SpawnReplacementObject(
        Vector3 position,
        Quaternion rotation,
        MoneyChestSnapshot reusableSnapshot)
    {
        if (replacementChestPrefab == null)
        {
            LogWarning("Cannot spawn replacement money chest. Replacement Chest Prefab is not assigned.");
            return null;
        }

        GameObject spawned = Instantiate(replacementChestPrefab, position, rotation);

        MoneyChestState chest = FindChest(spawned);
        if (chest != null && reusableSnapshot != null && !string.IsNullOrWhiteSpace(reusableSnapshot.chestInstanceId))
        {
            chest.RestoreState(
                reusableSnapshot.chestInstanceId,
                0,
                MoneyChestLifecycleState.Active);
        }

        return spawned;
    }

    private bool BootstrapReplacementChestForRuntime(MoneyChestState chest, bool destroyOnFailure)
    {
        if (chest == null)
            return false;

        GameObject root = ResolveChestRoot(chest);
        if (root == null)
            return false;

        WorldItem worldItem =
            root.GetComponent<WorldItem>() ??
            root.GetComponentInChildren<WorldItem>(true) ??
            root.GetComponentInParent<WorldItem>();

        chest.SyncInstanceIdFromWorldItem();
        chest.EnsureInstanceId();

        if (worldItem != null)
        {
            ItemInstance instance = worldItem.Instance;

            if (instance == null || instance.Definition == null)
            {
                if (moneyChestItemDefinition == null)
                {
                    LogWarning(
                        "Replacement chest cannot be pickupable because Money Chest Item Definition is not assigned.");

                    if (destroyOnFailure)
                        DestroyResolvedRoot(chest);

                    return false;
                }

                instance = ItemInstance.Create(moneyChestItemDefinition, 1);
            }

            instance.ForceSetInstanceIdForRestore(chest.ChestInstanceId);
            worldItem.Initialize(instance);

            chest.SyncInstanceIdFromWorldItem();
            chest.EnsureInstanceId();
        }
        else
        {
            LogWarning(
                "Replacement chest has no WorldItem. It can be treasury-active, but cannot be picked up/persisted as an item.");
        }

        chest.RestoreState(
            chest.ChestInstanceId,
            0,
            MoneyChestLifecycleState.Active);

        RebindMoneyChestInteractables(root, chest);
        WakePhysics(root, dynamicBody: true);

        Physics2D.SyncTransforms();

        return true;
    }

    private bool IsReusableReplacementSceneChest(
        MoneyChestTreasuryService treasury,
        MoneyChestState chest)
    {
        if (treasury == null || chest == null)
            return false;

        if (chest.IsRetired)
            return false;

        if (chest.Balance != 0)
            return false;

        if (treasury.IsReplacementChest(chest))
            return true;

        return chest.IsActive && string.IsNullOrWhiteSpace(treasury.ActiveChestInstanceId);
    }

    private MoneyChestSecureSlot FindAvailableSecureSlot()
    {
        MoneyChestSecureSlot[] slots = FindCandidateSecureSlots();
        if (slots == null || slots.Length == 0)
            return null;

        for (int i = 0; i < slots.Length; i++)
        {
            MoneyChestSecureSlot slot = slots[i];
            if (slot == null)
                continue;

            if (!slot.AcceptsReplacementSpawns)
                continue;

            slot.RefreshSecuredChestFromChildren();

            if (!slot.HasSecuredChest)
                return slot;
        }

        return null;
    }

    private MoneyChestSecureSlot[] FindCandidateSecureSlots()
    {
        if (explicitSecureSlot != null && IsSceneObject(explicitSecureSlot.gameObject))
            return new[] { explicitSecureSlot };

        if (explicitSecureSlot != null && !IsSceneObject(explicitSecureSlot.gameObject))
        {
            LogWarning(
                $"Explicit Secure Slot '{explicitSecureSlot.name}' is not a scene instance. " +
                "Ignoring it. Assign a scene/runtime slot, not the prefab asset.");
        }

        return FindObjectsByType<MoneyChestSecureSlot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    private static bool IsSceneObject(GameObject go)
    {
        if (go == null)
            return false;

        Scene scene = go.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    private bool IsInsideFallbackSpawnArea(Vector3 worldPosition)
    {
        if (fallbackSpawnArea != null)
            return fallbackSpawnArea.OverlapPoint(worldPosition);

        Vector3 center =
            fallbackSpawnPoint != null
                ? fallbackSpawnPoint.position
                : transform.position;

        return Vector2.Distance(center, worldPosition) <= fallbackExistingSearchRadius;
    }

    private bool IsNodeScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return false;

        if (string.IsNullOrWhiteSpace(nodeSceneNameContains))
            return true;

        return scene.name.IndexOf(
            nodeSceneNameContains,
            System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool RegisterReplacementWithPreferredId(
        MoneyChestTreasuryService treasury,
        MoneyChestState chest,
        string preferredId)
    {
        if (treasury == null || chest == null)
            return false;

        // This assumes your current treasury has the two-argument version,
        // because your uploaded spawner was already calling it.
        return treasury.RegisterReplacementChest(chest, preferredId);
    }

    private static MoneyChestState FindChest(GameObject root)
    {
        if (root == null)
            return null;

        return
            root.GetComponent<MoneyChestState>() ??
            root.GetComponentInChildren<MoneyChestState>(true);
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

    private static void RebindMoneyChestInteractables(GameObject root, MoneyChestState chest)
    {
        if (root == null || chest == null)
            return;

        MoneyChestInteractable[] interactables =
            root.GetComponentsInChildren<MoneyChestInteractable>(true);

        for (int i = 0; i < interactables.Length; i++)
            interactables[i].RebindRuntimeRefs(chest);
    }

    private static void WakePhysics(GameObject root, bool dynamicBody)
    {
        if (root == null)
            return;

        Rigidbody2D rb =
            root.GetComponent<Rigidbody2D>() ??
            root.GetComponentInChildren<Rigidbody2D>(true);

        if (rb == null)
            return;

        if (dynamicBody)
            rb.bodyType = RigidbodyType2D.Dynamic;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.WakeUp();
    }

    private static void DestroyResolvedRoot(MoneyChestState chest)
    {
        GameObject root = ResolveChestRoot(chest);
        if (root != null)
            Destroy(root);
    }

    private void Log(string message)
    {
        if (!logDebugMessages)
            return;

        Debug.Log($"[MoneyChestReplacementChestSpawner:{name}] {message}", this);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MoneyChestReplacementChestSpawner:{name}] {message}", this);
    }
}