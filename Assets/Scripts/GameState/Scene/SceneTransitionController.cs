using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneTransitionController : MonoBehaviour
{
    public static SceneTransitionController I { get; private set; }

    [Header("Scenes")]
    [SerializeField] private string nodeSceneName = "NodeScene";
    [SerializeField] private string boatSceneName = "BoatScene";

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        Log($"Awake | nodeScene='{nodeSceneName}' | boatScene='{boatSceneName}'");
    }

    public bool CanDepartCurrentScene(out string reason)
    {
        GameState gs = GameState.I;
        if (gs == null)
        {
            reason = "GameState is unavailable.";
            return false;
        }

        Boat boat = FindCurrentBoat(gs);

        if (!BoatDepartureGate.CanDepart(boat, out reason))
        {
            LogWarning($"Departure blocked | {reason}");
            return false;
        }

        reason = null;
        return true;
    }

    public void StartTravelToBoatScene(
        string fromNodeStableId,
        string toNodeStableId,
        int seed,
        float routeLength,
        string boatInstanceId,
        string boatPrefabGuid)
    //System.Collections.Generic.List<CargoManifest.Snapshot> cargoManifest)
    {
        GameState gs = GameState.I;
        if (gs == null)
        {
            LogError("StartTravelToBoatScene failed because GameState is null.");
            return;
        }

        if (!CanDepartCurrentScene(out string departureBlockReason))
        {
            ReportDepartureBlocked(
                $"StartTravelToBoatScene from='{fromNodeStableId}' to='{toNodeStableId}'",
                departureBlockReason);
            return;
        }

        SaveCurrentPlayerLoadout();
        CapturePlayerSceneContext(gs, "StartTravelToBoatScene");

        // Capture current boat state before leaving NodeScene.
        // This matters if loose items/cargo exist on the boat while docked.
        SaveCurrentBoatState(
            "StartTravelToBoatScene before loading BoatScene",
            fromNodeStableId,
            toNodeStableId,
            MoneyChestLossContext.Node);

        //System.Collections.Generic.List<CargoManifest.Snapshot> payloadCargo =
        //    cargoManifest ?? gs.boat?.cargo;

        var payload = new TravelPayload(
            fromNodeStableId,
            toNodeStableId,
            seed,
            routeLength,
            boatInstanceId,
            boatPrefabGuid);
        //payloadCargo);

        gs.BeginTravel(payload);

        string sourceName =
            GameMessageLocationResolver.ResolveDisplayName(
                fromNodeStableId);

        string destinationName =
            GameMessageLocationResolver.ResolveDisplayName(
                toNodeStableId);

        GameMessageService.PostInfo(
            $"Travel started: {sourceName} → {destinationName}.");

        Log(
            $"StartTravelToBoatScene | from={fromNodeStableId} | to={toNodeStableId} " +
            $"| seed={seed} | routeLength={routeLength} | boatId={boatInstanceId} ");
        //$"| boatGuid={boatPrefabGuid} | cargoCount={(payloadCargo != null ? payloadCargo.Count : 0)}");

        PrepareDivingBellOccupantsForSceneTransition(
            "StartTravelToBoatScene");

        SceneManager.LoadScene(boatSceneName);
    }

    public void CompleteTravelToDestination()
    {
        GameState gs = GameState.I;
        if (gs == null)
        {
            LogError("CompleteTravelToDestination failed because GameState is null.");
            return;
        }

        TravelPayload payload = gs.activeTravel;
        if (payload == null)
        {
            LogError("CompleteTravelToDestination failed because activeTravel is null.");
            return;
        }

        if (!CanDepartCurrentScene(out string departureBlockReason))
        {
            ReportDepartureBlocked(
                "CompleteTravelToDestination",
                departureBlockReason);
            return;
        }

        SaveCurrentPlayerLoadout();
        CapturePlayerSceneContext(gs, "CompleteTravelToDestination");
        SaveCurrentBoatState(
            "CompleteTravelToDestination before loading NodeScene",
            payload.fromNodeStableId,
            payload.toNodeStableId,
            MoneyChestLossContext.Route);

        string destinationName =
            GameMessageLocationResolver.ResolveDisplayName(
                payload.toNodeStableId);

        gs.player.currentNodeId =
            payload.toNodeStableId;

        gs.ClearTravel();

        GameMessageService.PostInfo(
            $"Arrived at {destinationName}.");

        Log($"CompleteTravelToDestination | currentNodeId={gs.player.currentNodeId}");

        PrepareDivingBellOccupantsForSceneTransition(
            "CompleteTravelToDestination");

        SceneManager.LoadScene(nodeSceneName);
    }

    public void AbortTravelToSource()
    {
        GameState gs = GameState.I;
        if (gs == null)
        {
            LogError("AbortTravelToSource failed because GameState is null.");
            return;
        }

        TravelPayload payload = gs.activeTravel;
        if (payload == null)
        {
            LogError("AbortTravelToSource failed because activeTravel is null.");
            return;
        }

        if (!CanDepartCurrentScene(out string departureBlockReason))
        {
            ReportDepartureBlocked(
                "AbortTravelToSource",
                departureBlockReason);
            return;
        }

        SaveCurrentPlayerLoadout();
        CapturePlayerSceneContext(gs, "AbortTravelToSource");
        SaveCurrentBoatState(
            "AbortTravelToSource before loading NodeScene",
            payload.fromNodeStableId,
            payload.toNodeStableId,
            MoneyChestLossContext.Route);

        string sourceName =
            GameMessageLocationResolver.ResolveDisplayName(
                payload.fromNodeStableId);

        gs.player.currentNodeId =
            payload.fromNodeStableId;

        gs.ClearTravel();

        GameMessageService.PostInfo(
            $"Returned to {sourceName}.");

        Log($"AbortTravelToSource | currentNodeId={gs.player.currentNodeId}");

        PrepareDivingBellOccupantsForSceneTransition(
            "AbortTravelToSource");

        SceneManager.LoadScene(nodeSceneName);
    }

    /// <summary>
    /// Detaches diving-bell occupants while the live scene hierarchy is still active.
    /// Call this immediately before any scene unload or application exit path that may
    /// destroy an occupied bell. OnDisable is too late for safe Transform reparenting.
    /// </summary>
    public int PrepareDivingBellOccupantsForSceneTransition(
        string reason)
    {
        DivingBellOccupancy[] bells =
            Object.FindObjectsByType<DivingBellOccupancy>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        if (bells == null ||
            bells.Length == 0)
        {
            return 0;
        }

        int ejectedCount =
            0;

        for (int i = 0;
             i < bells.Length;
             i++)
        {
            DivingBellOccupancy bell =
                bells[i];

            if (bell == null ||
                !bell.HasOccupants)
            {
                continue;
            }

            ejectedCount +=
                bell.EmergencyEjectAllOccupants(
                    $"Scene transition: {reason}");
        }

        if (ejectedCount > 0)
        {
            Log(
                $"PrepareDivingBellOccupantsForSceneTransition | " +
                $"reason='{reason}' ejected={ejectedCount}");
        }

        return ejectedCount;
    }

    public void ReportDepartureBlocked(
        string context,
        string reason)
    {
        string safeReason =
            string.IsNullOrWhiteSpace(reason)
                ? "Departure is currently blocked."
                : reason.Trim();

        LogWarning(
            $"Departure blocked | context='{context}' | {safeReason}");

        GameMessageService.PostWarning(
            $"Cannot depart: {safeReason}");
    }

    public bool SaveCurrentPlayerLoadout()
    {
        if (GameState.I == null)
        {
            LogError("SaveCurrentPlayerLoadout failed because GameState is null.");
            return false;
        }

        PlayerLoadoutPersistence persistence =
            ResolveCurrentPlayerPersistence();

        if (persistence == null)
        {
            LogWarning(
                "SaveCurrentPlayerLoadout failed because no unambiguous current-player " +
                "PlayerLoadoutPersistence was found in scene.");
            return false;
        }

        persistence.SaveToGameState();
        Log(
            $"SaveCurrentPlayerLoadout | key='{persistence.PersistenceKey}' saved to GameState.");
        return true;
    }

    public bool RestoreCurrentPlayerLoadout()
    {
        if (GameState.I == null)
        {
            LogError("RestoreCurrentPlayerLoadout failed because GameState is null.");
            return false;
        }

        PlayerLoadoutPersistence persistence =
            ResolveCurrentPlayerPersistence();

        if (persistence == null)
        {
            LogWarning(
                "RestoreCurrentPlayerLoadout failed because no unambiguous current-player " +
                "PlayerLoadoutPersistence was found in scene.");
            return false;
        }

        if (GameState.I.GetPlayerLoadout(
                persistence.PersistenceKey) == null)
        {
            LogWarning(
                $"RestoreCurrentPlayerLoadout skipped because no loadout exists for " +
                $"key='{persistence.PersistenceKey}'.");
            return false;
        }

        persistence.RestoreFromGameState();
        Log(
            $"RestoreCurrentPlayerLoadout | key='{persistence.PersistenceKey}' restored from GameState.");
        return true;
    }

    public bool SaveCurrentBoatState(
        string reason = "",
        string routeFromNodeHint = null,
        string routeToNodeHint = null,
        MoneyChestLossContext moneyChestLossContext = MoneyChestLossContext.Auto)
    {
        GameState gs = GameState.I;
        if (gs == null)
        {
            LogError($"SaveCurrentBoatState failed because GameState is null. reason='{reason}'");
            return false;
        }

        if (gs.boat == null)
        {
            LogError($"SaveCurrentBoatState failed because GameState.boat is null. reason='{reason}'");
            return false;
        }

        Boat boat = FindCurrentBoat(gs);
        if (boat == null)
        {
            LogWarning($"SaveCurrentBoatState skipped because no current boat was found. reason='{reason}'");
            return false;
        }

        Transform boatRoot = boat.transform;

        BoatIdentity boatId = boatRoot.GetComponent<BoatIdentity>();
        if (boatId != null)
        {
            gs.boat.boatPrefabGuid = boatId.BoatGuid;
            Log($"SaveCurrentBoatState | captured BoatIdentity guid='{boatId.BoatGuid}'.");
        }
        else
        {
            LogWarning($"SaveCurrentBoatState | boat '{boat.name}' has no BoatIdentity.");
        }

        if (!string.IsNullOrWhiteSpace(boat.BoatInstanceId))
        {
            gs.boat.boatInstanceId = boat.BoatInstanceId;
            Log($"SaveCurrentBoatState | captured BoatInstanceId='{boat.BoatInstanceId}'.");
        }
        else
        {
            LogWarning($"SaveCurrentBoatState | boat '{boat.name}' has empty BoatInstanceId.");
        }

        //CaptureCargo(gs, boatRoot);

        // Before loose item capture, make sure a physically-on-boat active money chest
        // is registered as boat-owned so BoatLooseItemPersistence can capture it.
        gs.moneyChestTreasury?.PrepareActiveChestForBoatCapture(boat);

        CaptureLooseItems(gs, boat);
        CaptureModulesAndPower(gs, boat);
        CaptureTetherState(gs, boat);
        CaptureCompartments(gs, boat);
        CaptureAccessStates(gs, boat);
        CaptureBoatTransform(gs, boat);

        // After normal persistence snapshots exist, decide whether the active chest
        // was actually carried forward. If not found in player loadout or boat loose items,
        // it becomes Lost.
        gs.moneyChestTreasury?.CaptureAfterScenePersistence(
            reason,
            routeFromNodeHint,
            routeToNodeHint,
            moneyChestLossContext);

        gs.LogState($"SaveCurrentBoatState reason='{reason}'");
        return true;
    }

    private Boat FindCurrentBoat(GameState gs)
    {
        if (gs == null)
            return null;

        string desiredId = null;

        if (gs.activeTravel != null && !string.IsNullOrWhiteSpace(gs.activeTravel.boatInstanceId))
            desiredId = gs.activeTravel.boatInstanceId;
        else if (gs.boat != null && !string.IsNullOrWhiteSpace(gs.boat.boatInstanceId))
            desiredId = gs.boat.boatInstanceId;

        if (!string.IsNullOrWhiteSpace(desiredId) &&
            gs.boatRegistry != null &&
            gs.boatRegistry.TryGetById(desiredId, out Boat registeredBoat) &&
            registeredBoat != null)
        {
            Log($"FindCurrentBoat | found registered boat by id='{desiredId}' → '{registeredBoat.name}'.");
            return registeredBoat;
        }

        GameObject tagged = null;
        try
        {
            tagged = GameObject.FindGameObjectWithTag("PlayerBoat");
        }
        catch (UnityException)
        {
            // Tag may not exist yet. Humanity continues.
        }

        if (tagged != null && tagged.TryGetComponent(out Boat taggedBoat))
        {
            Log($"FindCurrentBoat | found tagged PlayerBoat → '{taggedBoat.name}'.");
            return taggedBoat;
        }

        Boat fallback = Object.FindAnyObjectByType<Boat>();
        if (fallback != null)
            LogWarning($"FindCurrentBoat | using fallback FindAnyObjectByType boat='{fallback.name}'.");

        return fallback;
    }

    //private void CaptureCargo(GameState gs, Transform boatRoot)
    //{
    //    if (gs == null || gs.boat == null || boatRoot == null)
    //        return;

    //    BoatBoardedVolume boarded = boatRoot.GetComponentInChildren<BoatBoardedVolume>(true);
    //    Collider2D volumeCol = boarded != null ? boarded.GetComponent<Collider2D>() : null;

    //    //gs.boat.cargo = CargoManifest.Capture(boatRoot, volumeCol);

    //    Log(
    //        //$"CaptureCargo | cargoCount={(gs.boat.cargo != null ? gs.boat.cargo.Count : -1)} " +
    //        $"| boardedVolume={(boarded != null ? boarded.name : "NULL")} " +
    //        $"| volumeCol={(volumeCol != null ? volumeCol.name : "NULL")}");
    //}

    private void CaptureLooseItems(GameState gs, Boat boat)
    {
        if (gs == null || boat == null)
            return;

        BoatLooseItemPersistence persistence = boat.GetComponent<BoatLooseItemPersistence>();
        if (persistence == null)
        {
            LogWarning($"CaptureLooseItems skipped because boat '{boat.name}' has no BoatLooseItemPersistence.");
            gs.SetBoatLooseItems(new BoatLooseItemManifest(), "No BoatLooseItemPersistence found");
            return;
        }

        BoatLooseItemManifest manifest = persistence.CaptureManifest();

        gs.SetBoatLooseItems(
            manifest,
            $"Captured from boat '{boat.name}' via SceneTransitionController");

        int count = manifest?.looseItems != null ? manifest.looseItems.Count : -1;
        Log($"CaptureLooseItems | count={count}");
    }

    private void CaptureModulesAndPower(GameState gs, Boat boat)
    {
        if (gs == null || boat == null)
            return;

        BoatModuleStatePersistence persistence = boat.GetComponent<BoatModuleStatePersistence>();
        if (persistence == null)
        {
            LogWarning($"CaptureModulesAndPower skipped: boat '{boat.name}' has no BoatModuleStatePersistence.");
            gs.SetBoatModuleStates(new BoatModuleStateManifest(), "No BoatModuleStatePersistence found");
            gs.SetBoatPowerSnapshot(null, "No BoatModuleStatePersistence found");
            return;
        }

        BoatModuleStateManifest modules = persistence.CaptureModuleManifest();
        BoatPowerSnapshot power = persistence.CapturePowerSnapshot();

        gs.SetBoatModuleStates(modules, $"Captured from boat '{boat.name}'");
        gs.SetBoatPowerSnapshot(power, $"Captured from boat '{boat.name}'");

        int moduleCount = modules?.modules != null ? modules.modules.Count : -1;
        Log($"CaptureModulesAndPower | moduleCount={moduleCount} | power={(power != null ? $"{power.currentPower:F1}/{power.maxPower:F1}" : "NULL")}");
    }

    private void CaptureTetherState(
        GameState gs,
        Boat boat)
    {
        if (gs == null ||
            boat == null)
        {
            return;
        }

        BoatTetherStatePersistence persistence =
            boat.GetComponent<BoatTetherStatePersistence>();

        if (persistence == null)
            persistence =
                boat.gameObject
                    .AddComponent<BoatTetherStatePersistence>();

        BoatTetherStateManifest manifest =
            persistence.CaptureManifest();

        gs.SetBoatTetherState(
            manifest,
            $"Captured from boat '{boat.name}'");

        Log(
            $"CaptureTetherState | " +
            $"winches={(manifest?.winches != null ? manifest.winches.Count : -1)} " +
            $"deployments={(manifest?.deployments != null ? manifest.deployments.Count : -1)}");
    }

    private void CaptureCompartments(GameState gs, Boat boat)
    {
        if (gs == null || boat == null)
            return;

        BoatCompartmentStatePersistence persistence = boat.GetComponent<BoatCompartmentStatePersistence>();
        if (persistence == null)
        {
            LogWarning($"CaptureCompartments skipped: boat '{boat.name}' has no BoatCompartmentStatePersistence.");
            gs.SetBoatCompartmentStates(new BoatCompartmentStateManifest(), "No BoatCompartmentStatePersistence found");
            return;
        }

        BoatCompartmentStateManifest manifest = persistence.CaptureManifest();

        gs.SetBoatCompartmentStates(
            manifest,
            $"Captured from boat '{boat.name}'");

        int count = manifest?.compartments != null ? manifest.compartments.Count : -1;
        Log($"CaptureCompartments | count={count}");
    }

    private void CaptureAccessStates(GameState gs, Boat boat)
    {
        if (gs == null || boat == null)
            return;

        BoatAccessStatePersistence persistence = boat.GetComponent<BoatAccessStatePersistence>();
        if (persistence == null)
        {
            LogWarning($"CaptureAccessStates skipped: boat '{boat.name}' has no BoatAccessStatePersistence.");
            gs.SetBoatAccessStates(new BoatAccessStateManifest(), "No BoatAccessStatePersistence found");
            return;
        }

        BoatAccessStateManifest manifest = persistence.CaptureManifest();

        gs.SetBoatAccessStates(
            manifest,
            $"Captured from boat '{boat.name}'");

        int count = manifest?.accessPoints != null ? manifest.accessPoints.Count : -1;
        Log($"CaptureAccessStates | count={count}");
    }

    private void CaptureBoatTransform(GameState gs, Boat boat)
    {
        if (gs == null || boat == null)
            return;

        Vector3 worldPosition =
            boat.transform.position;

        string sceneName =
            boat.gameObject.scene.IsValid()
                ? boat.gameObject.scene.name
                : SceneManager.GetActiveScene().name;

        BoatTransformSnapshot snapshot = new BoatTransformSnapshot
        {
            version = 2,

            // Preserve the original v1 field because normal NodeScene <-> BoatScene
            // transitions intentionally keep only vertical placement while the
            // destination scene remains authoritative for X/rotation.
            worldY =
                worldPosition.y,

            hasWorldPose =
                true,

            sceneName =
                sceneName,

            worldPosition =
                new Vector2(
                    worldPosition.x,
                    worldPosition.y),

            worldRotationZ =
                boat.transform.eulerAngles.z
        };

        gs.SetBoatTransformState(
            snapshot,
            $"Captured from boat '{boat.name}'");

        Log(
            $"CaptureBoatTransform | scene='{snapshot.sceneName}' " +
            $"pos={snapshot.worldPosition} rotZ={snapshot.worldRotationZ:F3}");
    }

    public bool CaptureCurrentPlayerSceneContext(
        string reason = "")
    {
        GameState gs =
            GameState.I;

        if (gs == null)
        {
            LogError(
                "CaptureCurrentPlayerSceneContext failed because GameState is null.");
            return false;
        }

        return CapturePlayerSceneContext(
            gs,
            reason);
    }

    private bool CapturePlayerSceneContext(
        GameState gs,
        string reason)
    {
        if (gs == null)
            return false;

        PlayerBoardingState boarding =
            ResolveCurrentPlayerBoardingState(
                out string playerKey);

        if (boarding == null)
        {
            LogWarning(
                $"CapturePlayerSceneContext: no unambiguous current-player " +
                $"PlayerBoardingState found. reason='{reason}'");

            gs.SetPlayerSceneContext(
                playerKey,
                new PlayerSceneContextSnapshot
                {
                    version = 1,
                    hasValue = false,
                    wasBoarded = false,
                    boatInstanceId = null
                },
                reason);

            return false;
        }

        string boatInstanceId =
            null;

        if (boarding.IsBoarded &&
            boarding.CurrentBoatRoot != null)
        {
            Boat boat =
                boarding.CurrentBoatRoot.GetComponent<Boat>() ??
                boarding.CurrentBoatRoot.GetComponentInParent<Boat>();

            if (boat != null)
            {
                boatInstanceId =
                    boat.BoatInstanceId;
            }
        }

        PlayerSceneContextSnapshot snapshot =
            new PlayerSceneContextSnapshot
            {
                version = 1,
                hasValue = true,
                wasBoarded = boarding.IsBoarded,
                boatInstanceId = boatInstanceId
            };

        gs.SetPlayerSceneContext(
            playerKey,
            snapshot,
            reason);

        Log(
            $"CapturePlayerSceneContext | key='{playerKey}' reason='{reason}' " +
            $"wasBoarded={snapshot.wasBoarded} boatInstanceId='{snapshot.boatInstanceId}'");

        return true;
    }

    private PlayerLoadoutPersistence ResolveCurrentPlayerPersistence()
    {
        PlayerLoadoutPersistence[] all =
            Object.FindObjectsByType<PlayerLoadoutPersistence>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        if (all == null ||
            all.Length == 0)
        {
            return null;
        }

        string desiredKey =
            GameState.I != null
                ? GameState.I.LocalPlayerPersistenceKey
                : GameState.DefaultPlayerPersistenceKey;

        PlayerLoadoutPersistence matched =
            null;

        int matchCount =
            0;

        for (int i = 0;
             i < all.Length;
             i++)
        {
            PlayerLoadoutPersistence candidate =
                all[i];

            if (candidate == null ||
                candidate.PersistenceKey != desiredKey)
            {
                continue;
            }

            matched =
                candidate;

            matchCount++;
        }

        if (matchCount == 1)
            return matched;

        if (matchCount > 1)
        {
            LogWarning(
                $"ResolveCurrentPlayerPersistence is ambiguous: {matchCount} active " +
                $"PlayerLoadoutPersistence components resolve to key='{desiredKey}'. " +
                "Future multiplayer bootstrap must assign distinct player persistence keys.");

            return null;
        }

        return null;
    }

    private PlayerBoardingState ResolveCurrentPlayerBoardingState(
        out string playerKey)
    {
        playerKey =
            GameState.I != null
                ? GameState.I.LocalPlayerPersistenceKey
                : GameState.DefaultPlayerPersistenceKey;

        PlayerLoadoutPersistence persistence =
            ResolveCurrentPlayerPersistence();

        if (persistence != null)
        {
            playerKey =
                persistence.PersistenceKey;

            PlayerBoardingState fromPersistence =
                persistence.ResolveBoardingState();

            if (fromPersistence != null)
                return fromPersistence;
        }

        PlayerBoardingState[] all =
            Object.FindObjectsByType<PlayerBoardingState>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        if (all == null ||
            all.Length == 0)
        {
            return null;
        }

        if (all.Length == 1)
            return all[0];

        LogWarning(
            $"ResolveCurrentPlayerBoardingState is ambiguous: {all.Length} active players exist " +
            "and no unique current-player persistence owner could be resolved.");

        return null;
    }

#if UNITY_EDITOR
    [Header("Debug Travel Controls")]
    [SerializeField] private bool enableDebugTravelHotkeys = true;
    [SerializeField] private KeyCode debugCompleteTravelKey = KeyCode.F1;
    [SerializeField] private KeyCode debugAbortTravelKey = KeyCode.F2;

    private void Update()
    {
        if (!enableDebugTravelHotkeys)
            return;

        if (Input.GetKeyDown(debugCompleteTravelKey))
        {
            DebugCompleteTravelToDestination();
            return;
        }

        if (Input.GetKeyDown(debugAbortTravelKey))
        {
            DebugAbortTravelToSource();
            return;
        }
    }

    [ContextMenu("DEBUG Complete Travel To Destination")]
    private void DebugCompleteTravelToDestination()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SceneTransitionController] Cannot complete travel while not playing.", this);
            return;
        }

        CompleteTravelToDestination();
    }

    [ContextMenu("DEBUG Abort Travel To Source")]
    private void DebugAbortTravelToSource()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SceneTransitionController] Cannot abort travel while not playing.", this);
            return;
        }

        AbortTravelToSource();
    }
#endif

    private void Log(string msg)
    {
        if (!verboseLogging) return;
        Debug.Log($"[SceneTransitionController] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging) return;
        Debug.LogWarning($"[SceneTransitionController] {msg}", this);
    }

    private void LogError(string msg)
    {
        Debug.LogError($"[SceneTransitionController] {msg}", this);
    }
}