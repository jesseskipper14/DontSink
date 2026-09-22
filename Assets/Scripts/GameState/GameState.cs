using System.Collections.Generic;
using UnityEngine;

public sealed class GameState : MonoBehaviour
{
    public const string DefaultPlayerPersistenceKey = "local";

    public static GameState I { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    [Tooltip("Logs major singleton/bootstrap lifecycle events.")]
    [SerializeField] private bool logSingletonLifecycle = false;

    [Tooltip("Logs full multi-line GameState snapshots. Very noisy.")]
    [SerializeField] private bool logStateSnapshots = false;

    [Tooltip("Warn when scene-authored duplicate GameState destroys itself. Usually expected after loading from menu.")]
    [SerializeField] private bool warnOnDuplicateSingleton = false;

    [Header("Authoritative State")]
    public WorldMapPlayerState player = new WorldMapPlayerState();
    public WorldMapSimState worldMap = new WorldMapSimState();

    [Header("World Map Persistence")]
    public WorldMapSaveSnapshot worldMapSnapshot = new WorldMapSaveSnapshot();

    [Header("Active Travel (null when not traveling)")]
    public TravelPayload activeTravel;

    [Header("Player Persistence (Legacy Local-Player Mirrors)")]
    [Tooltip(
        "Backward-compatible local-player mirror. New player-aware code should use " +
        "Get/SetPlayerLoadout with a persistence key.")]
    public PlayerLoadoutSnapshot playerLoadout;

    [Tooltip(
        "Backward-compatible local-player mirror. New player-aware code should use " +
        "Get/SetPlayerSceneContext with a persistence key.")]
    public PlayerSceneContextSnapshot playerSceneContext;

    [Header("Player Persistence Seam")]
    [Tooltip(
        "Persistence key represented by the legacy playerLoadout/playerSceneContext mirrors. " +
        "Single-player defaults to 'local'. Future multiplayer bootstrap may replace this " +
        "with the authenticated local player's stable persistence key.")]
    [SerializeField] private string localPlayerPersistenceKey = DefaultPlayerPersistenceKey;

    [Tooltip(
        "Additive keyed player persistence records. Existing singular fields remain populated " +
        "for backward compatibility with current systems and old saves.")]
    public List<PlayerPersistenceStateSnapshot> playerPersistenceStates =
        new List<PlayerPersistenceStateSnapshot>();

    public string LocalPlayerPersistenceKey =>
        NormalizePlayerPersistenceKey(localPlayerPersistenceKey);

    [Header("Boat Registry")]
    public BoatRegistry boatRegistry;

    [Header("Money")]
    public MoneyChestTreasurySnapshot moneyChestTreasuryState = new MoneyChestTreasurySnapshot();
    public MoneyChestTreasuryService moneyChestTreasury;

    public BoatSaveState boat = new BoatSaveState
    {
        boatPrefabGuid = "",
        boatInstanceId = "boat_001",
        //cargo = new List<CargoManifest.Snapshot>(),
        looseItems = new BoatLooseItemManifest(),
        tetherState = new BoatTetherStateManifest()
    };

    private void Awake()
    {
        if (I != null && I != this)
        {
            string msg =
                "Duplicate GameState detected. Destroying this instance. " +
                $"Existing={I.name}, Duplicate={name}";

            if (warnOnDuplicateSingleton)
                Debug.LogWarning($"[GameState:{name}] {msg}", this);
            else
                LogLifecycle(msg);

            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        EnsureBoatStateDefaults();
        EnsureWorldMapSnapshotDefaults();
        EnsureMoneyChestTreasuryDefaults();
        EnsurePlayerPersistenceDefaults();

        LogLifecycle("Awake accepted as singleton.");
        LogState("Awake BEFORE registry check");

        if (boatRegistry == null)
        {
            boatRegistry = gameObject.AddComponent<BoatRegistry>();
            LogLifecycle("BoatRegistry was NULL. Added BoatRegistry component to GameState.");
        }
        else
        {
            LogLifecycle($"BoatRegistry already assigned: {boatRegistry.name}");
        }

        if (moneyChestTreasury == null)
        {
            moneyChestTreasury = GetComponent<MoneyChestTreasuryService>();

            if (moneyChestTreasury == null)
            {
                moneyChestTreasury = gameObject.AddComponent<MoneyChestTreasuryService>();
                LogLifecycle("MoneyChestTreasuryService was NULL. Added MoneyChestTreasuryService component to GameState.");
            }
            else
            {
                LogLifecycle($"MoneyChestTreasuryService found on GameState: {moneyChestTreasury.name}");
            }
        }
        else
        {
            LogLifecycle($"MoneyChestTreasuryService already assigned: {moneyChestTreasury.name}");
        }

        LogState("Awake END");
    }

    public void BeginTravel(TravelPayload payload)
    {
        EnsureBoatStateDefaults();

        Log("BeginTravel called.");
        Log($"Incoming payload: {DescribeTravel(payload)}");

        activeTravel = payload;

        if (payload == null)
        {
            LogWarning("BeginTravel received NULL payload. activeTravel is now NULL.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(payload.boatPrefabGuid))
                LogWarning("BeginTravel payload has EMPTY boatPrefabGuid. BoatSpawner may fall back.");

            if (string.IsNullOrWhiteSpace(payload.boatInstanceId))
                LogWarning("BeginTravel payload has EMPTY boatInstanceId.");

            if (string.IsNullOrWhiteSpace(payload.fromNodeStableId))
                LogWarning("BeginTravel payload has EMPTY fromNodeStableId.");

            if (string.IsNullOrWhiteSpace(payload.toNodeStableId))
                LogWarning("BeginTravel payload has EMPTY toNodeStableId.");

            //if (payload.cargoManifest == null)
            //    LogWarning("BeginTravel payload cargoManifest is NULL.");
        }

        LogState("BeginTravel END");
    }

    public void ClearTravel()
    {
        Log("ClearTravel called.");
        Log($"Clearing activeTravel: {DescribeTravel(activeTravel)}");

        activeTravel = null;

        LogState("ClearTravel END");
    }

    public void SetBoatSaveState(BoatSaveState newBoatState, string reason = "")
    {
        Log($"SetBoatSaveState called. reason='{reason}'");
        Log($"Incoming boat state: {DescribeBoat(newBoatState)}");

        boat = newBoatState;
        EnsureBoatStateDefaults();

        if (boat == null)
        {
            LogWarning("BoatSaveState was set to NULL.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(boat.boatPrefabGuid))
                LogWarning("BoatSaveState has EMPTY boatPrefabGuid.");

            if (string.IsNullOrWhiteSpace(boat.boatInstanceId))
                LogWarning("BoatSaveState has EMPTY boatInstanceId.");

            //if (boat.cargo == null)
            //    LogWarning("BoatSaveState cargo list is NULL.");

            if (boat.looseItems == null)
                LogWarning("BoatSaveState looseItems is NULL.");

            if (boat.tetherState == null)
                LogWarning("BoatSaveState tetherState is NULL.");
        }

        LogState("SetBoatSaveState END");
    }

    public void SetBoatLooseItems(BoatLooseItemManifest manifest, string reason = "")
    {
        EnsureBoatStateDefaults();

        if (boat == null)
        {
            Debug.LogWarning(
                $"[GameState:{name}] SetBoatLooseItems failed because boat state is NULL. reason='{reason}'",
                this);
            return;
        }

        boat.looseItems = manifest ?? new BoatLooseItemManifest();

        LogState($"SetBoatLooseItems reason='{reason}'");
    }

    public void SetBoatTetherState(
        BoatTetherStateManifest manifest,
        string reason = "")
    {
        EnsureBoatStateDefaults();

        if (boat == null)
        {
            Debug.LogWarning(
                $"[GameState:{name}] SetBoatTetherState failed because boat state is NULL. reason='{reason}'",
                this);

            return;
        }

        boat.tetherState =
            manifest ??
            new BoatTetherStateManifest();

        LogState(
            $"SetBoatTetherState reason='{reason}'");
    }

    public void SetBoatAccessStates(BoatAccessStateManifest manifest, string reason = "")
    {
        EnsureBoatStateDefaults();

        if (boat == null)
        {
            Debug.LogWarning($"[GameState:{name}] SetBoatAccessStates failed: boat state null. reason='{reason}'", this);
            return;
        }

        boat.accessStates = manifest ?? new BoatAccessStateManifest();

        LogState($"SetBoatAccessStates reason='{reason}'");
    }

    public void SetMoneyChestTreasuryState(
    MoneyChestTreasurySnapshot snapshot,
    string reason = "")
    {
        moneyChestTreasuryState = snapshot ?? new MoneyChestTreasurySnapshot();
        moneyChestTreasuryState.EnsureDefaults();

        if (verboseLogging)
        {
            Debug.Log(
                $"[GameState:{name}] SetMoneyChestTreasuryState reason='{reason}' " +
                $"active='{moneyChestTreasuryState.activeChestInstanceId}' " +
                $"count={(moneyChestTreasuryState.chests != null ? moneyChestTreasuryState.chests.Count : -1)}",
                this);
        }
    }

    public void LogState(string label)
    {
        if (!logStateSnapshots)
            return;

        Debug.Log(
            $"[GameState:{name}] STATE [{label}]\n" +
            $"  activeTravel={DescribeTravel(activeTravel)}\n" +
            $"  boat={DescribeBoat(boat)}\n" +
            $"  playerLoadout={(playerLoadout != null ? "OK" : "NULL")}\n" +
            $"  localPlayerKey='{LocalPlayerPersistenceKey}' playerRecords={(playerPersistenceStates != null ? playerPersistenceStates.Count : -1)}\n" +
            $"  boatRegistry={(boatRegistry != null ? boatRegistry.name : "NULL")}\n" +
            $"  moneyChestTreasury={(moneyChestTreasury != null ? moneyChestTreasury.name : "NULL")}\n" +
            $"  moneyChestState={(moneyChestTreasuryState != null ? $"active='{moneyChestTreasuryState.activeChestInstanceId}', count={(moneyChestTreasuryState.chests != null ? moneyChestTreasuryState.chests.Count : -1)}" : "NULL")}",
            this);
    }

    private void EnsureWorldMapSnapshotDefaults()
    {
        if (worldMapSnapshot == null)
            worldMapSnapshot = new WorldMapSaveSnapshot();

        worldMapSnapshot.EnsureDefaults();
    }

    public void SetWorldMapSnapshot(WorldMapSaveSnapshot snapshot, string reason = "")
    {
        worldMapSnapshot = snapshot ?? new WorldMapSaveSnapshot();
        EnsureWorldMapSnapshotDefaults();

        if (verboseLogging)
        {
            int nodeCount =
                worldMapSnapshot.graph != null && worldMapSnapshot.graph.nodes != null
                    ? worldMapSnapshot.graph.nodes.Count
                    : -1;

            int poiCount =
                worldMapSnapshot.pois != null && worldMapSnapshot.pois.pois != null
                    ? worldMapSnapshot.pois.pois.Count
                    : -1;

            Debug.Log(
                $"[GameState:{name}] SetWorldMapSnapshot reason='{reason}' " +
                $"nodes={nodeCount} pois={poiCount}",
                this);
        }
    }

    private void EnsureBoatStateDefaults()
    {
        if (boat == null)
        {
            boat = new BoatSaveState
            {
                boatPrefabGuid = "",
                boatInstanceId = "boat_001",
                //cargo = new List<CargoManifest.Snapshot>(),
                looseItems = new BoatLooseItemManifest(),
                tetherState = new BoatTetherStateManifest()
            };

            return;
        }

        //if (boat.cargo == null)
        //    boat.cargo = new List<CargoManifest.Snapshot>();

        if (boat.looseItems == null)
            boat.looseItems = new BoatLooseItemManifest();

        if (boat.tetherState == null)
            boat.tetherState = new BoatTetherStateManifest();

        if (boat.moduleStates == null)
            boat.moduleStates = new BoatModuleStateManifest();

        if (boat.compartmentStates == null)
            boat.compartmentStates = new BoatCompartmentStateManifest();

        if (boat.accessStates == null)
            boat.accessStates = new BoatAccessStateManifest();
    }

    private string DescribeTravel(TravelPayload payload)
    {
        if (payload == null)
            return "NULL";

        return
            $"from='{payload.fromNodeStableId}', " +
            $"to='{payload.toNodeStableId}', " +
            $"seed={payload.seed}, " +
            $"routeLength={payload.routeLength}, " +
            $"boatInstanceId='{payload.boatInstanceId}', " +
            $"boatPrefabGuid='{payload.boatPrefabGuid}'";
        //$"cargoCount={(payload.cargoManifest != null ? payload.cargoManifest.Count : -1)}";
    }

    private string DescribeBoat(BoatSaveState state)
    {
        if (state == null)
            return "NULL";

        return
            $"boatInstanceId='{state.boatInstanceId}', " +
            $"boatPrefabGuid='{state.boatPrefabGuid}', " +
            //$"cargoCount={(state.cargo != null ? state.cargo.Count : -1)}, " +
            $"looseItems={(state.looseItems != null ? DescribeLooseItems(state.looseItems) : "NULL")}, " +
            $"tether={(state.tetherState != null ? DescribeTetherState(state.tetherState) : "NULL")}";
    }

    private string DescribeLooseItems(BoatLooseItemManifest manifest)
    {
        if (manifest == null)
            return "NULL";

        return $"version={manifest.version}, count={(manifest.looseItems != null ? manifest.looseItems.Count : -1)}";
    }

    private string DescribeTetherState(
        BoatTetherStateManifest manifest)
    {
        if (manifest == null)
            return "NULL";

        return
            $"version={manifest.version}, " +
            $"winches={(manifest.winches != null ? manifest.winches.Count : -1)}, " +
            $"deployments={(manifest.deployments != null ? manifest.deployments.Count : -1)}";
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[GameState:{name}] {msg}", this);
    }

    private void LogLifecycle(string msg)
    {
        if (!verboseLogging && !logSingletonLifecycle)
            return;

        Debug.Log($"[GameState:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        // Real warnings should stay loud. A warning hidden behind verboseLogging
        // is not a warning, it is a diary entry.
        Debug.LogWarning($"[GameState:{name}] {msg}", this);
    }

    public void SetBoatModuleStates(BoatModuleStateManifest manifest, string reason = "")
    {
        EnsureBoatStateDefaults();

        if (boat == null)
        {
            Debug.LogWarning($"[GameState:{name}] SetBoatModuleStates failed: boat state null. reason='{reason}'", this);
            return;
        }

        boat.moduleStates = manifest ?? new BoatModuleStateManifest();

        LogState($"SetBoatModuleStates reason='{reason}'");
    }

    public void SetBoatCompartmentStates(BoatCompartmentStateManifest manifest, string reason = "")
    {
        EnsureBoatStateDefaults();

        if (boat == null)
        {
            Debug.LogWarning($"[GameState:{name}] SetBoatCompartmentStates failed: boat state null. reason='{reason}'", this);
            return;
        }

        boat.compartmentStates = manifest ?? new BoatCompartmentStateManifest();

        LogState($"SetBoatCompartmentStates reason='{reason}'");
    }

    private void EnsureMoneyChestTreasuryDefaults()
    {
        if (moneyChestTreasuryState == null)
            moneyChestTreasuryState = new MoneyChestTreasurySnapshot();

        moneyChestTreasuryState.EnsureDefaults();
    }

    public void SetBoatPowerSnapshot(BoatPowerSnapshot snapshot, string reason = "")
    {
        EnsureBoatStateDefaults();

        if (boat == null)
        {
            Debug.LogWarning($"[GameState:{name}] SetBoatPowerSnapshot failed: boat state null. reason='{reason}'", this);
            return;
        }

        boat.power = snapshot;

        LogState($"SetBoatPowerSnapshot reason='{reason}'");
    }

    public void SetBoatTransformState(BoatTransformSnapshot snapshot, string reason = "")
    {
        EnsureBoatStateDefaults();

        if (boat == null)
        {
            Debug.LogWarning($"[GameState:{name}] SetBoatTransformState failed: boat state null. reason='{reason}'", this);
            return;
        }

        boat.transformState = snapshot;

        LogState($"SetBoatTransformState reason='{reason}'");
    }

    public static string NormalizePlayerPersistenceKey(string playerKey)
    {
        return
            string.IsNullOrWhiteSpace(playerKey)
                ? DefaultPlayerPersistenceKey
                : playerKey.Trim();
    }

    public void SetLocalPlayerPersistenceKey(
        string playerKey,
        string reason = "")
    {
        EnsurePlayerPersistenceDefaults();

        string oldKey =
            LocalPlayerPersistenceKey;

        PlayerPersistenceStateSnapshot oldRecord =
            GetOrCreatePlayerPersistenceState(
                oldKey);

        oldRecord.loadout =
            playerLoadout;

        oldRecord.sceneContext =
            playerSceneContext;

        localPlayerPersistenceKey =
            NormalizePlayerPersistenceKey(
                playerKey);

        PlayerPersistenceStateSnapshot newRecord =
            GetOrCreatePlayerPersistenceState(
                LocalPlayerPersistenceKey);

        playerLoadout =
            newRecord.loadout;

        playerSceneContext =
            newRecord.sceneContext;

        if (verboseLogging)
        {
            Debug.Log(
                $"[GameState:{name}] SetLocalPlayerPersistenceKey " +
                $"old='{oldKey}' new='{LocalPlayerPersistenceKey}' reason='{reason}'",
                this);
        }
    }

    public bool TryGetPlayerPersistenceState(
        string playerKey,
        out PlayerPersistenceStateSnapshot state)
    {
        EnsurePlayerPersistenceCollection();

        string normalized =
            NormalizePlayerPersistenceKey(
                playerKey);

        for (int i = 0;
             i < playerPersistenceStates.Count;
             i++)
        {
            PlayerPersistenceStateSnapshot candidate =
                playerPersistenceStates[i];

            if (candidate == null)
                continue;

            if (string.Equals(
                    NormalizePlayerPersistenceKey(candidate.playerKey),
                    normalized,
                    System.StringComparison.Ordinal))
            {
                candidate.playerKey =
                    normalized;

                state =
                    candidate;

                return true;
            }
        }

        state =
            null;

        return false;
    }

    public PlayerPersistenceStateSnapshot GetOrCreatePlayerPersistenceState(
        string playerKey)
    {
        string normalized =
            NormalizePlayerPersistenceKey(
                playerKey);

        if (TryGetPlayerPersistenceState(
                normalized,
                out PlayerPersistenceStateSnapshot existing))
        {
            return existing;
        }

        PlayerPersistenceStateSnapshot created =
            new PlayerPersistenceStateSnapshot
            {
                version = 1,
                playerKey = normalized
            };

        playerPersistenceStates.Add(
            created);

        return created;
    }

    public PlayerLoadoutSnapshot GetPlayerLoadout(
        string playerKey)
    {
        EnsurePlayerPersistenceDefaults();

        string normalized =
            NormalizePlayerPersistenceKey(
                playerKey);

        if (TryGetPlayerPersistenceState(
                normalized,
                out PlayerPersistenceStateSnapshot state))
        {
            return state.loadout;
        }

        return
            string.Equals(
                normalized,
                LocalPlayerPersistenceKey,
                System.StringComparison.Ordinal)
                ? playerLoadout
                : null;
    }

    public void SetPlayerLoadout(
        string playerKey,
        PlayerLoadoutSnapshot snapshot,
        string reason = "")
    {
        string normalized =
            NormalizePlayerPersistenceKey(
                playerKey);

        PlayerPersistenceStateSnapshot state =
            GetOrCreatePlayerPersistenceState(
                normalized);

        state.loadout =
            snapshot;

        if (string.Equals(
                normalized,
                LocalPlayerPersistenceKey,
                System.StringComparison.Ordinal))
        {
            playerLoadout =
                snapshot;
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[GameState:{name}] SetPlayerLoadout key='{normalized}' " +
                $"hasValue={(snapshot != null)} reason='{reason}'",
                this);
        }
    }

    public PlayerSceneContextSnapshot GetPlayerSceneContext(
        string playerKey)
    {
        EnsurePlayerPersistenceDefaults();

        string normalized =
            NormalizePlayerPersistenceKey(
                playerKey);

        if (TryGetPlayerPersistenceState(
                normalized,
                out PlayerPersistenceStateSnapshot state))
        {
            return state.sceneContext;
        }

        return
            string.Equals(
                normalized,
                LocalPlayerPersistenceKey,
                System.StringComparison.Ordinal)
                ? playerSceneContext
                : null;
    }

    public void SetPlayerSceneContext(
        string playerKey,
        PlayerSceneContextSnapshot snapshot,
        string reason = "")
    {
        string normalized =
            NormalizePlayerPersistenceKey(
                playerKey);

        PlayerPersistenceStateSnapshot state =
            GetOrCreatePlayerPersistenceState(
                normalized);

        state.sceneContext =
            snapshot;

        if (string.Equals(
                normalized,
                LocalPlayerPersistenceKey,
                System.StringComparison.Ordinal))
        {
            playerSceneContext =
                snapshot;
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[GameState:{name}] SetPlayerSceneContext key='{normalized}' reason='{reason}' " +
                $"hasValue={(snapshot != null && snapshot.hasValue)} " +
                $"wasBoarded={(snapshot != null && snapshot.wasBoarded)} " +
                $"boatInstanceId='{(snapshot != null ? snapshot.boatInstanceId : "NULL")}'",
                this);
        }
    }

    public void SetPlayerSceneContext(
        PlayerSceneContextSnapshot snapshot,
        string reason = "")
    {
        SetPlayerSceneContext(
            LocalPlayerPersistenceKey,
            snapshot,
            reason);
    }

    public void SetPlayerPersistenceStates(
        List<PlayerPersistenceStateSnapshot> states,
        string reason = "")
    {
        playerPersistenceStates =
            states ??
            new List<PlayerPersistenceStateSnapshot>();

        EnsurePlayerPersistenceDefaults();

        if (verboseLogging)
        {
            Debug.Log(
                $"[GameState:{name}] SetPlayerPersistenceStates reason='{reason}' " +
                $"count={playerPersistenceStates.Count} localKey='{LocalPlayerPersistenceKey}'",
                this);
        }
    }

    public void SyncLocalPlayerPersistenceMirrors(
        string reason = "")
    {
        EnsurePlayerPersistenceDefaults();

        PlayerPersistenceStateSnapshot local =
            GetOrCreatePlayerPersistenceState(
                LocalPlayerPersistenceKey);

        local.loadout =
            playerLoadout;

        local.sceneContext =
            playerSceneContext;

        if (verboseLogging)
        {
            Debug.Log(
                $"[GameState:{name}] SyncLocalPlayerPersistenceMirrors " +
                $"key='{LocalPlayerPersistenceKey}' reason='{reason}'",
                this);
        }
    }

    private void EnsurePlayerPersistenceCollection()
    {
        if (playerPersistenceStates == null)
        {
            playerPersistenceStates =
                new List<PlayerPersistenceStateSnapshot>();
        }
    }

    private void EnsurePlayerPersistenceDefaults()
    {
        EnsurePlayerPersistenceCollection();

        localPlayerPersistenceKey =
            NormalizePlayerPersistenceKey(
                localPlayerPersistenceKey);

        // Remove null/duplicate keyed records deterministically. Keep the first
        // valid record for a key because older/additive JSON may be hand-edited.
        HashSet<string> seen =
            new HashSet<string>(
                System.StringComparer.Ordinal);

        for (int i = playerPersistenceStates.Count - 1;
             i >= 0;
             i--)
        {
            PlayerPersistenceStateSnapshot state =
                playerPersistenceStates[i];

            if (state == null)
            {
                playerPersistenceStates.RemoveAt(i);
                continue;
            }

            state.playerKey =
                NormalizePlayerPersistenceKey(
                    state.playerKey);
        }

        for (int i = 0;
             i < playerPersistenceStates.Count;)
        {
            PlayerPersistenceStateSnapshot state =
                playerPersistenceStates[i];

            if (!seen.Add(state.playerKey))
            {
                playerPersistenceStates.RemoveAt(i);
                continue;
            }

            i++;
        }

        PlayerPersistenceStateSnapshot local =
            GetOrCreatePlayerPersistenceState(
                LocalPlayerPersistenceKey);

        // Old schema-v1 saves have only the singular fields. Hydrate the additive
        // keyed record from those fields when necessary. New saves contain both.
        if (local.loadout == null &&
            playerLoadout != null)
        {
            local.loadout =
                playerLoadout;
        }

        if (local.sceneContext == null &&
            playerSceneContext != null)
        {
            local.sceneContext =
                playerSceneContext;
        }

        // Keep the old public fields alive as the local-player compatibility view.
        playerLoadout =
            local.loadout;

        playerSceneContext =
            local.sceneContext;
    }
}

[System.Serializable]
public sealed class TravelPayload
{
    public string fromNodeStableId;
    public string toNodeStableId;
    public int seed;
    public float routeLength;

    public string boatInstanceId;
    public string boatPrefabGuid;

    //public List<CargoManifest.Snapshot> cargoManifest;

    public TravelPayload(
        string from,
        string to,
        int seed,
        float len,
        string boatInstanceId,
        string boatPrefabGuid)
    //List<CargoManifest.Snapshot> cargoManifest)
    {
        fromNodeStableId = from;
        toNodeStableId = to;
        this.seed = seed;
        routeLength = len;

        this.boatInstanceId = boatInstanceId;
        this.boatPrefabGuid = boatPrefabGuid;
        //this.cargoManifest = cargoManifest;
    }
}

[System.Serializable]
public sealed class BoatSaveState
{
    public string boatPrefabGuid;
    public string boatInstanceId;

    //public List<CargoManifest.Snapshot> cargo;
    public BoatLooseItemManifest looseItems;
    public BoatTetherStateManifest tetherState;

    public BoatModuleStateManifest moduleStates;
    public BoatPowerSnapshot power;

    public BoatCompartmentStateManifest compartmentStates;
    public BoatTransformSnapshot transformState;

    public BoatAccessStateManifest accessStates;
}

[System.Serializable]
public sealed class BoatTransformSnapshot
{
    // v1 stored only worldY. v2 adds an exact pose that is valid only in the
    // scene/context it was captured from. Keeping worldY preserves the existing
    // cross-scene spawn behavior and compatibility with older saves.
    public int version = 2;

    public float worldY;

    public bool hasWorldPose;
    public string sceneName;
    public Vector2 worldPosition;
    public float worldRotationZ;
}

[System.Serializable]
public sealed class PlayerSceneContextSnapshot
{
    public int version = 1;

    public bool hasValue;
    public bool wasBoarded;
    public string boatInstanceId;
}