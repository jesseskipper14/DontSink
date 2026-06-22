using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum MoneyChestLossContext
{
    Auto = 0,
    Node = 1,
    Route = 2
}

[DisallowMultipleComponent]
public sealed class MoneyChestTreasuryService : MonoBehaviour
{
    public static MoneyChestTreasuryService Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages = false;

    private readonly Dictionary<string, MoneyChestState> liveChestsById = new();

    private MoneyChestTreasurySnapshot TreasuryState
    {
        get
        {
            GameState gs = GameState.I;
            if (gs == null)
                return null;

            if (gs.moneyChestTreasuryState == null)
                gs.moneyChestTreasuryState = new MoneyChestTreasurySnapshot();

            gs.moneyChestTreasuryState.EnsureDefaults();
            return gs.moneyChestTreasuryState;
        }
    }

    public string ActiveChestInstanceId
    {
        get
        {
            MoneyChestTreasurySnapshot state = TreasuryState;
            return state != null ? state.activeChestInstanceId : null;
        }
    }

    public MoneyChestState ActiveChest
    {
        get
        {
            MoneyChestTreasurySnapshot state = TreasuryState;
            if (state == null || string.IsNullOrWhiteSpace(state.activeChestInstanceId))
                return null;

            liveChestsById.TryGetValue(state.activeChestInstanceId, out MoneyChestState chest);
            return chest;
        }
    }

    public bool HasActiveChest
    {
        get
        {
            MoneyChestSnapshot snapshot = GetActiveSnapshot();
            return snapshot != null && snapshot.IsActive;
        }
    }

    public int ActiveBalance
    {
        get
        {
            MoneyChestState liveChest = ActiveChest;
            if (liveChest != null && liveChest.IsActive && !liveChest.IsRetired)
                return liveChest.Balance;

            MoneyChestSnapshot snapshot = GetActiveSnapshot();
            return snapshot != null && snapshot.IsActive ? Mathf.Max(0, snapshot.balance) : 0;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"Duplicate {nameof(MoneyChestTreasuryService)} found on {name}. " +
                "Destroying duplicate component only, not the whole GameObject.",
                this);

            enabled = false;
            Destroy(this);
            return;
        }

        Instance = this;

        MoneyChestTreasurySnapshot state = TreasuryState;
        state?.EnsureDefaults();
    }

    private void Start()
    {
        RegisterExistingSceneChests();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ---------------------------------------------------------------------
    // Registration
    // ---------------------------------------------------------------------

    public void RegisterExistingSceneChests()
    {
        MoneyChestState[] sceneChests = FindObjectsByType<MoneyChestState>(FindObjectsSortMode.None);

        for (int i = 0; i < sceneChests.Length; i++)
            RegisterChest(sceneChests[i]);

        AdoptFirstActiveChestIfNeeded();

        if (logDebugMessages)
        {
            MoneyChestTreasurySnapshot state = TreasuryState;

            Debug.Log(
                $"Money treasury scene scan complete. LiveCount={liveChestsById.Count}, " +
                $"SnapshotCount={(state?.chests != null ? state.chests.Count : -1)}, " +
                $"ActiveChestId='{state?.activeChestInstanceId}'",
                this);
        }
    }

    public void RegisterChest(MoneyChestState chest)
    {
        if (chest == null)
            return;

        chest.SyncInstanceIdFromWorldItem();
        chest.EnsureInstanceId();

        string chestId = chest.ChestInstanceId;
        if (string.IsNullOrWhiteSpace(chestId))
        {
            Debug.LogWarning($"Tried to register money chest with no instance ID: {chest.name}", chest);
            return;
        }

        liveChestsById[chestId] = chest;

        MoneyChestSnapshot snapshot = FindSnapshot(chestId);
        bool createdSnapshot = false;

        if (snapshot == null)
        {
            snapshot = CreateSnapshotFromChest(chest);
            AddSnapshot(snapshot);
            createdSnapshot = true;
        }

        if (!createdSnapshot)
        {
            chest.RestoreState(
                snapshot.chestInstanceId,
                snapshot.balance,
                snapshot.lifecycleState);
        }
        else
        {
            UpdateSnapshotFromChest(snapshot, chest);
        }

        chest.Changed -= HandleChestChanged;
        chest.Changed += HandleChestChanged;

        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null)
            return;

        if (chest.IsActive)
        {
            if (string.IsNullOrWhiteSpace(state.activeChestInstanceId))
            {
                state.activeChestInstanceId = chestId;
                snapshot.lifecycleState = MoneyChestLifecycleState.Active;
                chest.MarkActive();

                if (logDebugMessages)
                {
                    Debug.Log(
                        $"Registered active money chest as treasury active chest. id='{chestId}', balance={chest.Balance}",
                        chest);
                }

                return;
            }

            if (state.activeChestInstanceId == chestId)
            {
                snapshot.lifecycleState = MoneyChestLifecycleState.Active;
                snapshot.balance = chest.Balance;
                return;
            }

            chest.MarkLost();
            UpdateSnapshotFromChest(snapshot, chest);

            Debug.LogWarning(
                $"Extra active money chest registered. Keeping active chest '{state.activeChestInstanceId}' " +
                $"and marking newcomer '{chestId}' Lost.",
                chest);

            return;
        }

        if (logDebugMessages)
        {
            Debug.Log(
                $"Registered money chest. id='{chestId}', state={chest.LifecycleState}, balance={chest.Balance}",
                chest);
        }
    }

    public void UnregisterChest(MoneyChestState chest)
    {
        if (chest == null || string.IsNullOrWhiteSpace(chest.ChestInstanceId))
            return;

        // Do NOT sync here.
        //
        // Scene unload calls MoneyChestState.OnDisable(), which calls UnregisterChest().
        // By then SceneTransitionController has already performed the authoritative
        // persistence pass through CaptureAfterScenePersistence().
        //
        // Syncing here was the route-clearing goblin. It got one last shot during
        // teardown and used it to stomp useful state. Fired. No severance.
        if (logDebugMessages)
        {
            MoneyChestSnapshot snapshot = FindSnapshot(chest.ChestInstanceId);

            Debug.Log(
                $"Unregister money chest without snapshot sync. id='{chest.ChestInstanceId}', " +
                $"chestState={chest.LifecycleState}, " +
                $"snapshotState={(snapshot != null ? snapshot.lifecycleState.ToString() : "NULL")}, " +
                $"node='{snapshot?.nodeStableId}', " +
                $"route='{snapshot?.routeFromNodeId}'→'{snapshot?.routeToNodeId}'",
                chest);
        }

        if (liveChestsById.TryGetValue(chest.ChestInstanceId, out MoneyChestState registered) &&
            registered == chest)
        {
            liveChestsById.Remove(chest.ChestInstanceId);
        }

        chest.Changed -= HandleChestChanged;
    }

    private void HandleChestChanged(MoneyChestState chest)
    {
        if (chest == null)
            return;

        SyncSnapshotFromLiveChest(chest);
    }

    // ---------------------------------------------------------------------
    // Active chest authority
    // ---------------------------------------------------------------------

    public void SetActiveChest(MoneyChestState chest)
    {
        if (chest == null || chest.IsRetired)
            return;

        chest.SyncInstanceIdFromWorldItem();
        chest.EnsureInstanceId();

        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null)
            return;

        string newActiveId = chest.ChestInstanceId;

        string oldActiveId = state.activeChestInstanceId;
        if (!string.IsNullOrWhiteSpace(oldActiveId) && oldActiveId != newActiveId)
        {
            MoneyChestSnapshot oldSnapshot = FindSnapshot(oldActiveId);
            if (oldSnapshot != null && oldSnapshot.IsActive)
            {
                oldSnapshot.lifecycleState = MoneyChestLifecycleState.Lost;
                StampCurrentLossContext(
                    oldSnapshot,
                    null,
                    null,
                    MoneyChestLossContext.Auto);
            }

            if (liveChestsById.TryGetValue(oldActiveId, out MoneyChestState oldLiveChest) &&
                oldLiveChest != null &&
                oldLiveChest.IsActive)
            {
                oldLiveChest.MarkLost();
            }
        }

        if (!liveChestsById.ContainsKey(newActiveId))
            RegisterChest(chest);

        MoneyChestSnapshot snapshot = GetOrCreateSnapshot(chest);
        snapshot.lifecycleState = MoneyChestLifecycleState.Active;
        snapshot.balance = chest.Balance;

        state.activeChestInstanceId = newActiveId;
        chest.MarkActive();

        if (logDebugMessages)
        {
            Debug.Log(
                $"Active money chest set. id='{newActiveId}', balance={chest.Balance}",
                chest);
        }
    }

    public bool CanSpend(int amount)
    {
        if (amount <= 0)
            return true;

        return HasActiveChest && ActiveBalance >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
            return true;

        MoneyChestSnapshot snapshot = GetActiveSnapshot();
        if (snapshot == null || !snapshot.IsActive)
        {
            if (logDebugMessages)
                Debug.LogWarning($"Cannot spend {amount}. No active money chest snapshot exists.", this);

            return false;
        }

        MoneyChestState liveChest = ActiveChest;
        if (liveChest != null && liveChest.IsActive && !liveChest.IsRetired)
        {
            bool spent = liveChest.TrySpend(amount);
            SyncSnapshotFromLiveChest(liveChest);

            if (logDebugMessages)
            {
                Debug.Log(
                    $"TrySpend {amount}. Result={spent}. LiveChest='{liveChest.ChestInstanceId}', Balance={liveChest.Balance}",
                    liveChest);
            }

            return spent;
        }

        if (snapshot.balance < amount)
        {
            if (logDebugMessages)
                Debug.Log($"Cannot spend {amount}. Active balance is {snapshot.balance}.", this);

            return false;
        }

        snapshot.balance -= amount;

        if (logDebugMessages)
        {
            Debug.Log(
                $"TrySpend {amount} from snapshot. Result=True. ActiveChest='{snapshot.chestInstanceId}', Balance={snapshot.balance}",
                this);
        }

        return true;
    }

    public bool AddMoney(int amount)
    {
        if (amount <= 0)
            return true;

        MoneyChestSnapshot snapshot = GetActiveSnapshot();
        if (snapshot == null || !snapshot.IsActive)
        {
            if (logDebugMessages)
                Debug.LogWarning($"Cannot add {amount}. No active money chest snapshot exists.", this);

            return false;
        }

        MoneyChestState liveChest = ActiveChest;
        if (liveChest != null && liveChest.IsActive && !liveChest.IsRetired)
        {
            liveChest.AddMoney(amount);
            SyncSnapshotFromLiveChest(liveChest);

            if (logDebugMessages)
            {
                Debug.Log(
                    $"Added {amount} to live active money chest. id='{liveChest.ChestInstanceId}', Balance={liveChest.Balance}",
                    liveChest);
            }

            return true;
        }

        snapshot.balance = Mathf.Max(0, snapshot.balance + amount);

        if (logDebugMessages)
        {
            Debug.Log(
                $"Added {amount} to active money chest snapshot. id='{snapshot.chestInstanceId}', Balance={snapshot.balance}",
                this);
        }

        return true;
    }

    public void MarkActiveChestLost(
        string reason = "",
        string routeFromNodeHint = null,
        string routeToNodeHint = null,
        MoneyChestLossContext lossContext = MoneyChestLossContext.Auto)
    {
        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null || string.IsNullOrWhiteSpace(state.activeChestInstanceId))
        {
            if (logDebugMessages)
                Debug.LogWarning($"Cannot mark active chest Lost. No active chest id exists. reason='{reason}'", this);

            return;
        }

        string activeId = state.activeChestInstanceId;
        MoneyChestSnapshot snapshot = FindSnapshot(activeId);

        if (snapshot == null)
        {
            snapshot = new MoneyChestSnapshot
            {
                chestInstanceId = activeId,
                lifecycleState = MoneyChestLifecycleState.Lost
            };

            AddSnapshot(snapshot);
        }

        MoneyChestState liveChest = null;

        if (liveChestsById.TryGetValue(activeId, out MoneyChestState foundLiveChest) &&
            foundLiveChest != null)
        {
            liveChest = foundLiveChest;

            snapshot.balance = liveChest.Balance;
            snapshot.itemId = liveChest.GetItemId();
            snapshot.lastWorldPosition = liveChest.transform.position;

            Boat boat = liveChest.GetComponentInParent<Boat>();
            if (boat != null)
                snapshot.lastBoatLocalPosition = boat.transform.InverseTransformPoint(liveChest.transform.position);

            // This fires Changed, but that is okay. We stamp the final loss context after it.
            liveChest.MarkLost();
        }

        snapshot.lifecycleState = MoneyChestLifecycleState.Lost;

        RecordCurrentLocation(snapshot, liveChest, routeFromNodeHint, routeToNodeHint);

        // Critical distinction:
        // - If activeTravel exists, this is travel/route loss.
        // - If activeTravel does NOT exist, even if StartTravelToBoatScene passed hints,
        //   the chest was left behind at the node before departure, so this is node loss.
        StampCurrentLossContext(snapshot, routeFromNodeHint, routeToNodeHint, lossContext);

        state.activeChestInstanceId = null;

        if (logDebugMessages)
        {
            Debug.Log(
                $"Marked active money chest Lost. id='{activeId}', balance={snapshot.balance}, " +
                $"lossContext={lossContext}, " +
                $"node='{snapshot.nodeStableId}', " +
                $"route='{snapshot.routeFromNodeId}'→'{snapshot.routeToNodeId}', " +
                $"reason='{reason}'",
                liveChest != null ? liveChest : this);
        }
    }

    // ---------------------------------------------------------------------
    // Recovery
    // ---------------------------------------------------------------------

    public bool RecoverLostChest(MoneyChestState lostChest)
    {
        if (lostChest == null)
            return false;

        lostChest.SyncInstanceIdFromWorldItem();
        lostChest.EnsureInstanceId();

        MoneyChestSnapshot lostSnapshot = GetOrCreateSnapshot(lostChest);

        if (!lostSnapshot.IsLost && !lostChest.IsLost)
            return false;

        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null)
            return false;

        MoneyChestSnapshot activeSnapshot = GetActiveSnapshot();

        // Snapshot is authoritative for lost chest money.
        // The live object can be stale/zero after spawn/reload/registration weirdness.
        int recoveredFunds = Mathf.Max(0, lostSnapshot.balance);

        // Fallback only if snapshot is empty but live chest still has money.
        // This preserves older cases where the live chest was the only source.
        if (recoveredFunds <= 0 && lostChest.Balance > 0)
            recoveredFunds = lostChest.Balance;

        if (activeSnapshot == null || !activeSnapshot.IsActive)
        {
            state.activeChestInstanceId = lostChest.ChestInstanceId;

            lostSnapshot.lifecycleState = MoneyChestLifecycleState.Active;
            lostSnapshot.balance = recoveredFunds;

            lostChest.RestoreState(
                lostChest.ChestInstanceId,
                recoveredFunds,
                MoneyChestLifecycleState.Active);

            liveChestsById[lostChest.ChestInstanceId] = lostChest;
            lostChest.Changed -= HandleChestChanged;
            lostChest.Changed += HandleChestChanged;

            if (logDebugMessages)
            {
                Debug.Log(
                    $"Recovered lost money chest as new active chest. " +
                    $"id='{lostChest.ChestInstanceId}', recoveredFunds={recoveredFunds}",
                    lostChest);
            }

            return true;
        }

        MoneyChestState activeLiveChest = ActiveChest;

        if (activeLiveChest != null && activeLiveChest.IsActive && !activeLiveChest.IsRetired)
        {
            activeLiveChest.AddMoney(recoveredFunds);
            SyncSnapshotFromLiveChest(activeLiveChest);
        }
        else
        {
            activeSnapshot.balance = Mathf.Max(0, activeSnapshot.balance + recoveredFunds);
        }

        lostChest.MarkRetired();

        lostSnapshot.balance = 0;
        lostSnapshot.lifecycleState = MoneyChestLifecycleState.Retired;
        RecordCurrentLocation(lostSnapshot, lostChest);

        if (logDebugMessages)
        {
            Debug.Log(
                $"Recovered lost chest '{lostChest.ChestInstanceId}'. " +
                $"Merged {recoveredFunds} into active chest '{activeSnapshot.chestInstanceId}'. " +
                $"New active balance={ActiveBalance}",
                lostChest);
        }

        return true;
    }

    // ---------------------------------------------------------------------
    // Scene transition integration
    // ---------------------------------------------------------------------

    public void PrepareActiveChestForBoatCapture(Boat boat)
    {
        if (boat == null)
            return;

        MoneyChestState activeChest = ActiveChest;
        if (activeChest == null || !activeChest.IsActive || activeChest.IsRetired)
            return;

        WorldItem worldItem = ResolveWorldItem(activeChest);
        if (worldItem == null)
        {
            if (logDebugMessages)
            {
                Debug.LogWarning(
                    $"Active money chest '{activeChest.ChestInstanceId}' has no WorldItem. " +
                    "BoatLooseItemPersistence cannot capture it as a loose boat item.",
                    activeChest);
            }

            return;
        }

        bool isSlotSecured = IsMoneyChestSlotSecuredOnBoat(worldItem, boat);
        bool isAlreadyBoatOwned = IsWorldItemOwnedByBoat(worldItem, boat);

        if (!isSlotSecured && !isAlreadyBoatOwned)
        {
            // Important:
            // Do NOT promote the chest into boat ownership just because it overlaps BoatBoardedVolume.
            // If it is not in hands, not already boat-owned, and not in the money chest slot,
            // the post-capture pass should mark it Lost.
            ClearMoneyChestBoatOwnershipIfOwnedBy(worldItem, boat);

            if (logDebugMessages)
            {
                Debug.Log(
                    $"Active money chest is NOT explicitly aboard. " +
                    $"It will be Lost if not found in player loadout. " +
                    $"id='{activeChest.ChestInstanceId}', boat='{boat.BoatInstanceId}', " +
                    $"slotSecured={isSlotSecured}, alreadyBoatOwned={isAlreadyBoatOwned}, " +
                    $"pos={worldItem.transform.position}",
                    activeChest);
            }

            return;
        }

        EnsureWorldItemBoatCapturePolicies(worldItem, boat);

        if (logDebugMessages)
        {
            Debug.Log(
                $"Prepared active money chest for boat capture. " +
                $"id='{activeChest.ChestInstanceId}', boat='{boat.BoatInstanceId}', " +
                $"slotSecured={isSlotSecured}, alreadyBoatOwned={isAlreadyBoatOwned}",
                activeChest);
        }
    }

    public void CaptureAfterScenePersistence(
        string reason = "",
        string routeFromNodeHint = null,
        string routeToNodeHint = null,
        MoneyChestLossContext lossContext = MoneyChestLossContext.Auto)
    {
        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null)
            return;

        MoneyChestLossContext resolvedLossContext =
            ResolveLossContext(reason, lossContext);

        SyncAllLiveSnapshots();

        if (string.IsNullOrWhiteSpace(state.activeChestInstanceId))
        {
            if (logDebugMessages)
                Debug.Log($"Money chest post-capture skipped. No active chest id. reason='{reason}'", this);

            return;
        }

        string activeId = state.activeChestInstanceId;

        GameState gs = GameState.I;
        bool foundInLoadout = ContainsItemInstanceId(gs?.playerLoadout, activeId);
        bool foundInBoatLooseItems = ContainsItemInstanceId(gs?.boat?.looseItems, activeId);

        if (foundInLoadout || foundInBoatLooseItems)
        {
            MoneyChestSnapshot activeSnapshot = GetActiveSnapshot();
            if (activeSnapshot != null)
            {
                activeSnapshot.lifecycleState = MoneyChestLifecycleState.Active;

                // Safe chests are traveling with the player/boat, so route context is valid here.
                // StartTravelToBoatScene passes hints before GameState.activeTravel exists.
                ApplyRouteToSnapshot(activeSnapshot, routeFromNodeHint, routeToNodeHint);
            }

            if (logDebugMessages)
            {
                Debug.Log(
                    $"Money chest post-capture SAFE. id='{activeId}', " +
                    $"foundInLoadout={foundInLoadout}, foundInBoatLooseItems={foundInBoatLooseItems}, " +
                    $"node='{activeSnapshot?.nodeStableId}', " +
                    $"route='{activeSnapshot?.routeFromNodeId}'→'{activeSnapshot?.routeToNodeId}', " +
                    $"reason='{reason}'",
                    this);
            }

            return;
        }

        MarkActiveChestLost(
            $"Post scene-persistence capture did not find active chest in loadout or boat loose items. reason='{reason}'",
            routeFromNodeHint,
            routeToNodeHint,
            resolvedLossContext);
    }

    // ---------------------------------------------------------------------
    // Queries
    // ---------------------------------------------------------------------

    public IReadOnlyCollection<MoneyChestState> GetKnownLiveChests()
    {
        return liveChestsById.Values;
    }

    public IReadOnlyList<MoneyChestSnapshot> GetChestSnapshots()
    {
        MoneyChestTreasurySnapshot state = TreasuryState;
        return state != null ? state.chests : null;
    }

    public bool HasLostChestSnapshots
    {
        get
        {
            MoneyChestTreasurySnapshot state = TreasuryState;
            if (state == null || state.chests == null)
                return false;

            for (int i = 0; i < state.chests.Count; i++)
            {
                MoneyChestSnapshot snapshot = state.chests[i];
                if (snapshot != null && snapshot.IsLost)
                    return true;
            }

            return false;
        }
    }

    public bool RegisterReplacementChest(MoneyChestState replacementChest)
    {
        if (replacementChest == null)
            return false;

        if (!ShouldOfferReplacementChest)
        {
            if (logDebugMessages)
            {
                Debug.Log(
                    $"Replacement money chest rejected. HasActiveChest={HasActiveChest}, " +
                    $"LostCount={LostChestSnapshotCount}",
                    replacementChest);
            }

            return false;
        }

        replacementChest.SyncInstanceIdFromWorldItem();
        replacementChest.EnsureInstanceId();

        string chestId = replacementChest.ChestInstanceId;
        if (string.IsNullOrWhiteSpace(chestId))
            return false;

        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null)
            return false;

        MoneyChestSnapshot snapshot = FindSnapshot(chestId);

        if (snapshot == null)
        {
            snapshot = new MoneyChestSnapshot
            {
                version = 1,
                chestInstanceId = chestId,
                itemId = replacementChest.GetItemId(),
                balance = 0,
                lifecycleState = MoneyChestLifecycleState.Active,
                isReplacement = true,
                lastWorldPosition = replacementChest.transform.position
            };

            AddSnapshot(snapshot);
        }

        snapshot.chestInstanceId = chestId;
        snapshot.itemId = replacementChest.GetItemId();
        snapshot.balance = 0;
        snapshot.lifecycleState = MoneyChestLifecycleState.Active;
        snapshot.isReplacement = true;

        RecordCurrentLocation(snapshot, replacementChest);

        replacementChest.RestoreState(
            chestId,
            0,
            MoneyChestLifecycleState.Active);

        liveChestsById[chestId] = replacementChest;

        replacementChest.Changed -= HandleChestChanged;
        replacementChest.Changed += HandleChestChanged;

        state.activeChestInstanceId = chestId;

        if (logDebugMessages)
        {
            Debug.Log(
                $"Replacement money chest registered as active. " +
                $"id='{chestId}', LostCount={LostChestSnapshotCount}",
                replacementChest);
        }

        return true;
    }

    public int LostChestSnapshotCount
    {
        get
        {
            MoneyChestTreasurySnapshot state = TreasuryState;
            if (state == null || state.chests == null)
                return 0;

            int count = 0;

            for (int i = 0; i < state.chests.Count; i++)
            {
                MoneyChestSnapshot snapshot = state.chests[i];
                if (snapshot != null && snapshot.IsLost)
                    count++;
            }

            return count;
        }
    }

    public bool ShouldOfferReplacementChest
    {
        get
        {
            return !HasActiveChest && HasLostChestSnapshots;
        }
    }

    public bool IsReplacementChest(MoneyChestState chest)
    {
        if (chest == null)
            return false;

        chest.SyncInstanceIdFromWorldItem();
        chest.EnsureInstanceId();

        MoneyChestSnapshot snapshot = FindSnapshot(chest.ChestInstanceId);
        return snapshot != null && snapshot.isReplacement;
    }

    public bool TryFindReusableReplacementSnapshotForCurrentNode(out MoneyChestSnapshot replacementSnapshot)
    {
        replacementSnapshot = null;

        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null || state.chests == null)
            return false;

        string currentNodeId =
            GameState.I != null && GameState.I.player != null
                ? GameState.I.player.currentNodeId
                : null;

        for (int i = 0; i < state.chests.Count; i++)
        {
            MoneyChestSnapshot snapshot = state.chests[i];
            if (snapshot == null)
                continue;

            if (!snapshot.isReplacement)
                continue;

            if (snapshot.IsRetired)
                continue;

            if (snapshot.balance != 0)
                continue;

            // Keep replacement chests node-local when possible.
            // If your MoneyChestSnapshot somehow does not have nodeStableId yet,
            // add: public string nodeStableId;
            if (!string.IsNullOrWhiteSpace(currentNodeId) &&
                !string.IsNullOrWhiteSpace(snapshot.nodeStableId) &&
                !string.Equals(snapshot.nodeStableId, currentNodeId, System.StringComparison.Ordinal))
            {
                continue;
            }

            replacementSnapshot = snapshot;
            return true;
        }

        return false;
    }

    public bool RegisterReplacementChest(
        MoneyChestState replacementChest,
        string preferredInstanceId = null)
    {
        if (replacementChest == null)
            return false;

        if (!ShouldOfferReplacementChest)
        {
            if (logDebugMessages)
            {
                Debug.Log(
                    $"Replacement money chest rejected. HasActiveChest={HasActiveChest}, " +
                    $"LostCount={LostChestSnapshotCount}",
                    replacementChest);
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(preferredInstanceId))
        {
            replacementChest.RestoreState(
                preferredInstanceId,
                0,
                MoneyChestLifecycleState.Active);
        }
        else
        {
            replacementChest.SyncInstanceIdFromWorldItem();
            replacementChest.EnsureInstanceId();

            replacementChest.RestoreState(
                replacementChest.ChestInstanceId,
                0,
                MoneyChestLifecycleState.Active);
        }

        RegisterChest(replacementChest);
        SetActiveChest(replacementChest);

        MoneyChestSnapshot snapshot = GetOrCreateSnapshot(replacementChest);
        if (snapshot != null)
        {
            snapshot.balance = 0;
            snapshot.lifecycleState = MoneyChestLifecycleState.Active;
            snapshot.isReplacement = true;
            snapshot.itemId = replacementChest.GetItemId();

            RecordCurrentLocation(snapshot, replacementChest);
        }

        if (logDebugMessages)
        {
            Debug.Log(
                $"Replacement money chest registered as active. " +
                $"id='{replacementChest.ChestInstanceId}', LostCount={LostChestSnapshotCount}",
                replacementChest);
        }

        return true;
    }

    // ---------------------------------------------------------------------
    // Snapshot helpers
    // ---------------------------------------------------------------------

    private MoneyChestSnapshot GetActiveSnapshot()
    {
        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null || string.IsNullOrWhiteSpace(state.activeChestInstanceId))
            return null;

        return FindSnapshot(state.activeChestInstanceId);
    }

    private MoneyChestSnapshot FindSnapshot(string chestInstanceId)
    {
        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null || state.chests == null || string.IsNullOrWhiteSpace(chestInstanceId))
            return null;

        for (int i = 0; i < state.chests.Count; i++)
        {
            MoneyChestSnapshot snapshot = state.chests[i];
            if (snapshot != null && snapshot.chestInstanceId == chestInstanceId)
                return snapshot;
        }

        return null;
    }

    private MoneyChestSnapshot GetOrCreateSnapshot(MoneyChestState chest)
    {
        if (chest == null)
            return null;

        chest.EnsureInstanceId();

        MoneyChestSnapshot snapshot = FindSnapshot(chest.ChestInstanceId);
        if (snapshot != null)
            return snapshot;

        snapshot = CreateSnapshotFromChest(chest);
        AddSnapshot(snapshot);

        return snapshot;
    }

    private MoneyChestSnapshot CreateSnapshotFromChest(MoneyChestState chest)
    {
        var snapshot = new MoneyChestSnapshot
        {
            version = 1,
            chestInstanceId = chest.ChestInstanceId,
            itemId = chest.GetItemId(),
            balance = chest.Balance,
            lifecycleState = chest.LifecycleState,
            lastWorldPosition = chest.transform.position
        };

        RecordCurrentLocation(snapshot, chest);
        return snapshot;
    }

    private void AddSnapshot(MoneyChestSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.chestInstanceId))
            return;

        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null)
            return;

        state.EnsureDefaults();

        MoneyChestSnapshot existing = FindSnapshot(snapshot.chestInstanceId);
        if (existing != null)
            return;

        state.chests.Add(snapshot);
    }

    private void UpdateSnapshotFromChest(MoneyChestSnapshot snapshot, MoneyChestState chest)
    {
        if (snapshot == null || chest == null)
            return;

        string previousRouteFrom = snapshot.routeFromNodeId;
        string previousRouteTo = snapshot.routeToNodeId;
        string previousNodeStableId = snapshot.nodeStableId;

        snapshot.chestInstanceId = chest.ChestInstanceId;
        snapshot.itemId = chest.GetItemId();
        snapshot.balance = Mathf.Max(0, chest.Balance);
        snapshot.lifecycleState = chest.LifecycleState;
        snapshot.lastWorldPosition = chest.transform.position;

        RecordCurrentLocation(snapshot, chest);

        bool routeWasLost =
            string.IsNullOrWhiteSpace(snapshot.routeFromNodeId) &&
            string.IsNullOrWhiteSpace(snapshot.routeToNodeId) &&
            (!string.IsNullOrWhiteSpace(previousRouteFrom) ||
             !string.IsNullOrWhiteSpace(previousRouteTo));

        if (routeWasLost)
        {
            snapshot.routeFromNodeId = previousRouteFrom;
            snapshot.routeToNodeId = previousRouteTo;
        }

        bool nodeWasLost =
            string.IsNullOrWhiteSpace(snapshot.nodeStableId) &&
            !string.IsNullOrWhiteSpace(previousNodeStableId);

        if (nodeWasLost)
            snapshot.nodeStableId = previousNodeStableId;
    }

    private void SyncSnapshotFromLiveChest(MoneyChestState chest)
    {
        if (chest == null)
            return;

        MoneyChestSnapshot snapshot = GetOrCreateSnapshot(chest);
        UpdateSnapshotFromChest(snapshot, chest);
    }

    private void SyncAllLiveSnapshots()
    {
        foreach (MoneyChestState chest in liveChestsById.Values)
        {
            if (chest != null)
                SyncSnapshotFromLiveChest(chest);
        }
    }

    private void AdoptFirstActiveChestIfNeeded()
    {
        MoneyChestTreasurySnapshot state = TreasuryState;
        if (state == null)
            return;

        if (!string.IsNullOrWhiteSpace(state.activeChestInstanceId))
            return;

        for (int i = 0; i < state.chests.Count; i++)
        {
            MoneyChestSnapshot snapshot = state.chests[i];
            if (snapshot == null || !snapshot.IsActive)
                continue;

            state.activeChestInstanceId = snapshot.chestInstanceId;

            if (logDebugMessages)
            {
                Debug.Log(
                    $"Adopted first active money chest snapshot. id='{snapshot.chestInstanceId}', balance={snapshot.balance}",
                    this);
            }

            return;
        }
    }

    private void RecordCurrentLocation(
        MoneyChestSnapshot snapshot,
        MoneyChestState chest,
        string routeFromNodeHint = null,
        string routeToNodeHint = null)
    {
        if (snapshot == null)
            return;

        Scene scene = SceneManager.GetActiveScene();
        snapshot.lastSceneName = scene.IsValid() ? scene.name : string.Empty;

        GameState gs = GameState.I;
        if (gs != null)
        {
            if (gs.boat != null)
                snapshot.boatInstanceId = gs.boat.boatInstanceId;

            bool hasRouteHint =
                !string.IsNullOrWhiteSpace(routeFromNodeHint) ||
                !string.IsNullOrWhiteSpace(routeToNodeHint);

            if (hasRouteHint)
            {
                snapshot.routeFromNodeId = routeFromNodeHint;
                snapshot.routeToNodeId = routeToNodeHint;
                snapshot.nodeStableId = null;
            }
            else if (gs.activeTravel != null)
            {
                snapshot.routeFromNodeId = gs.activeTravel.fromNodeStableId;
                snapshot.routeToNodeId = gs.activeTravel.toNodeStableId;
                snapshot.nodeStableId = null;
            }
            else if (gs.player != null && !string.IsNullOrWhiteSpace(gs.player.currentNodeId))
            {
                // Only stamp node context when not traveling.
                // Do not clear route here. Explicit loss-context stamping decides exclusivity.
                if (string.IsNullOrWhiteSpace(snapshot.routeFromNodeId) &&
                    string.IsNullOrWhiteSpace(snapshot.routeToNodeId))
                {
                    snapshot.nodeStableId = gs.player.currentNodeId;
                }
            }
        }

        if (chest == null)
            return;

        snapshot.lastWorldPosition = chest.transform.position;

        Boat boat = chest.GetComponentInParent<Boat>();
        if (boat != null)
            snapshot.lastBoatLocalPosition = boat.transform.InverseTransformPoint(chest.transform.position);
    }

    private void ApplyRouteToSnapshot(
        MoneyChestSnapshot snapshot,
        string routeFromNodeHint = null,
        string routeToNodeHint = null)
    {
        if (snapshot == null)
            return;

        if (!string.IsNullOrWhiteSpace(routeFromNodeHint) ||
            !string.IsNullOrWhiteSpace(routeToNodeHint))
        {
            snapshot.routeFromNodeId = routeFromNodeHint;
            snapshot.routeToNodeId = routeToNodeHint;
            snapshot.nodeStableId = null;
            return;
        }

        GameState gs = GameState.I;
        TravelPayload travel = gs != null ? gs.activeTravel : null;

        if (travel == null)
            return;

        snapshot.routeFromNodeId = travel.fromNodeStableId;
        snapshot.routeToNodeId = travel.toNodeStableId;
        snapshot.nodeStableId = null;
    }

    private void ApplyNodeToSnapshot(MoneyChestSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        GameState gs = GameState.I;
        if (gs == null || gs.player == null)
            return;

        if (string.IsNullOrWhiteSpace(gs.player.currentNodeId))
            return;

        snapshot.nodeStableId = gs.player.currentNodeId;

        snapshot.routeFromNodeId = null;
        snapshot.routeToNodeId = null;
    }

    private void StampCurrentLossContext(
    MoneyChestSnapshot snapshot,
    string routeFromNodeHint,
    string routeToNodeHint,
    MoneyChestLossContext lossContext)
    {
        if (snapshot == null)
            return;

        switch (lossContext)
        {
            case MoneyChestLossContext.Node:
                ApplyNodeToSnapshot(snapshot);
                return;

            case MoneyChestLossContext.Route:
                ApplyRouteToSnapshot(snapshot, routeFromNodeHint, routeToNodeHint);
                return;

            case MoneyChestLossContext.Auto:
            default:
                {
                    GameState gs = GameState.I;

                    if (gs != null && gs.activeTravel != null)
                        ApplyRouteToSnapshot(snapshot, routeFromNodeHint, routeToNodeHint);
                    else
                        ApplyNodeToSnapshot(snapshot);

                    return;
                }
        }
    }

    private void ClearMoneyChestBoatOwnershipIfOwnedBy(WorldItem worldItem, Boat boat)
    {
        if (worldItem == null || boat == null)
            return;

        BoatOwnedItem owned = worldItem.GetComponent<BoatOwnedItem>();
        if (owned == null || !owned.IsOwnedByBoat)
            return;

        if (!string.Equals(owned.OwningBoatInstanceId, boat.BoatInstanceId, System.StringComparison.Ordinal))
            return;

        owned.ClearOwnership();
    }

    // ---------------------------------------------------------------------
    // Snapshot search helpers
    // ---------------------------------------------------------------------

    private MoneyChestLossContext ResolveLossContext(
    string reason,
    MoneyChestLossContext requestedContext)
    {
        // Explicit caller wins if it actually arrived.
        if (requestedContext != MoneyChestLossContext.Auto)
            return requestedContext;

        // Fallback: infer from transition reason.
        // This protects us from stale optional-parameter call paths or older callers.
        if (!string.IsNullOrWhiteSpace(reason))
        {
            if (reason.Contains("StartTravelToBoatScene"))
                return MoneyChestLossContext.Node;

            if (reason.Contains("CompleteTravelToDestination") ||
                reason.Contains("AbortTravelToSource"))
                return MoneyChestLossContext.Route;
        }

        return MoneyChestLossContext.Auto;
    }

    private static bool ContainsItemInstanceId(PlayerLoadoutSnapshot snapshot, string instanceId)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(instanceId))
            return false;

        if (ContainsItemInstanceId(snapshot.inventory, instanceId))
            return true;

        if (ContainsItemInstanceId(snapshot.equipment, instanceId))
            return true;

        return false;
    }

    private static bool ContainsItemInstanceId(InventorySnapshot snapshot, string instanceId)
    {
        if (snapshot == null || snapshot.hotbarSlots == null)
            return false;

        for (int i = 0; i < snapshot.hotbarSlots.Count; i++)
        {
            if (ContainsItemInstanceId(snapshot.hotbarSlots[i], instanceId))
                return true;
        }

        return false;
    }

    private static bool ContainsItemInstanceId(EquipmentSnapshot snapshot, string instanceId)
    {
        if (snapshot == null)
            return false;

        return
            ContainsItemInstanceId(snapshot.hands, instanceId) ||
            ContainsItemInstanceId(snapshot.head, instanceId) ||
            ContainsItemInstanceId(snapshot.feet, instanceId) ||
            ContainsItemInstanceId(snapshot.toolbelt, instanceId) ||
            ContainsItemInstanceId(snapshot.backpack, instanceId) ||
            ContainsItemInstanceId(snapshot.body, instanceId);
    }

    private static bool ContainsItemInstanceId(BoatLooseItemManifest manifest, string instanceId)
    {
        if (manifest == null || manifest.looseItems == null || string.IsNullOrWhiteSpace(instanceId))
            return false;

        for (int i = 0; i < manifest.looseItems.Count; i++)
        {
            BoatLooseItemSnapshot snapshot = manifest.looseItems[i];
            if (snapshot == null)
                continue;

            if (ContainsItemInstanceId(snapshot.item, instanceId))
                return true;
        }

        return false;
    }

    private static bool ContainsItemInstanceId(ItemInstanceSnapshot snapshot, string instanceId)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(instanceId))
            return false;

        if (snapshot.instanceId == instanceId)
            return true;

        if (snapshot.container == null || snapshot.container.slots == null)
            return false;

        for (int i = 0; i < snapshot.container.slots.Count; i++)
        {
            if (ContainsItemInstanceId(snapshot.container.slots[i], instanceId))
                return true;
        }

        return false;
    }

    private static WorldItem ResolveWorldItem(MoneyChestState chest)
    {
        if (chest == null)
            return null;

        WorldItem worldItem =
            chest.GetComponent<WorldItem>() ??
            chest.GetComponentInParent<WorldItem>() ??
            chest.GetComponentInChildren<WorldItem>(true);

        return worldItem;
    }

    private static bool IsWorldItemOwnedByBoat(WorldItem worldItem, Boat boat)
    {
        if (worldItem == null || boat == null)
            return false;

        BoatOwnedItem owned = worldItem.GetComponent<BoatOwnedItem>();
        if (owned == null || !owned.IsOwnedByBoat)
            return false;

        return string.Equals(
            owned.OwningBoatInstanceId,
            boat.BoatInstanceId,
            System.StringComparison.Ordinal);
    }

    private static bool IsMoneyChestSlotSecuredOnBoat(WorldItem worldItem, Boat boat)
    {
        if (worldItem == null || boat == null)
            return false;

        MoneyChestSlotSecuredItem marker = worldItem.GetComponent<MoneyChestSlotSecuredItem>();
        if (marker == null || !marker.IsSecured)
            return false;

        MoneyChestSecureSlot slot = MoneyChestSecureSlot.FindByStableId(marker.SlotStableId);
        if (slot == null)
            return false;

        Boat slotBoat =
            slot.GetComponentInParent<Boat>() ??
            slot.GetComponentInChildren<Boat>(true);

        if (slotBoat == null)
            return false;

        return string.Equals(
            slotBoat.BoatInstanceId,
            boat.BoatInstanceId,
            System.StringComparison.Ordinal);
    }

    private static void EnsureWorldItemBoatCapturePolicies(WorldItem worldItem, Boat boat)
    {
        if (worldItem == null || boat == null)
            return;

        BoatOwnedItem owned = worldItem.GetComponent<BoatOwnedItem>();
        if (owned == null)
            owned = worldItem.gameObject.AddComponent<BoatOwnedItem>();

        BoatOwnedItemLayerPolicy layerPolicy = worldItem.GetComponent<BoatOwnedItemLayerPolicy>();
        if (layerPolicy == null)
            worldItem.gameObject.AddComponent<BoatOwnedItemLayerPolicy>();

        BoatOwnedItemVisualPolicy visualPolicy = worldItem.GetComponent<BoatOwnedItemVisualPolicy>();
        if (visualPolicy == null)
            worldItem.gameObject.AddComponent<BoatOwnedItemVisualPolicy>();

        // This re-registers with BoatItemRegistry if needed.
        owned.AssignToBoat(boat);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Register Existing Scene Chests")]
    private void DebugRegisterExistingSceneChests()
    {
        RegisterExistingSceneChests();
    }

    [ContextMenu("Debug/Add 100 To Active Chest")]
    private void DebugAdd100()
    {
        bool added = AddMoney(100);
        Debug.Log($"Add 100 result: {added}. Active balance: {ActiveBalance}", this);
    }

    [ContextMenu("Debug/Spend 25 From Active Chest")]
    private void DebugSpend25()
    {
        bool spent = TrySpend(25);
        Debug.Log($"Spend 25 result: {spent}. Active balance: {ActiveBalance}", this);
    }

    [ContextMenu("Debug/Mark Active Chest Lost")]
    private void DebugMarkActiveLost()
    {
        MarkActiveChestLost("Debug context menu");
        Debug.Log($"Marked active chest lost. ActiveChestId='{ActiveChestInstanceId}', ActiveBalance={ActiveBalance}", this);
    }

    [ContextMenu("Debug/Capture After Scene Persistence")]
    private void DebugCaptureAfterScenePersistence()
    {
        CaptureAfterScenePersistence("Debug context menu");
    }

    [ContextMenu("Debug/List Money Chest Treasury")]
    private void DebugListMoneyChestTreasury()
    {
        MoneyChestTreasurySnapshot state = TreasuryState;

        Debug.Log(
            $"Money chest treasury:\n" +
            $"ActiveChestInstanceId='{state?.activeChestInstanceId}'\n" +
            $"ActiveBalance={ActiveBalance}\n" +
            $"LiveCount={liveChestsById.Count}\n" +
            $"SnapshotCount={(state?.chests != null ? state.chests.Count : -1)}",
            this);

        if (state?.chests != null)
        {
            for (int i = 0; i < state.chests.Count; i++)
            {
                MoneyChestSnapshot snapshot = state.chests[i];
                if (snapshot == null)
                    continue;

                Debug.Log(
                    $"Snapshot[{i}] id='{snapshot.chestInstanceId}', itemId='{snapshot.itemId}', " +
                    $"state={snapshot.lifecycleState}, balance={snapshot.balance}, " +
                    $"scene='{snapshot.lastSceneName}', boat='{snapshot.boatInstanceId}', " +
                    $"node='{snapshot.nodeStableId}', " +
                    $"route='{snapshot.routeFromNodeId}'→'{snapshot.routeToNodeId}', " +
                    $"worldPos={snapshot.lastWorldPosition}",
                    this);
            }
        }

        foreach (MoneyChestState chest in liveChestsById.Values)
        {
            if (chest == null)
                continue;

            Debug.Log(
                $"Live chest: name='{chest.name}', id='{chest.ChestInstanceId}', " +
                $"state={chest.LifecycleState}, balance={chest.Balance}",
                chest);
        }
    }
#endif
}