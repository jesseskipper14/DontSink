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
    [SerializeField] private bool verboseLogging = true;

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

    public void StartTravelToBoatScene(
        string fromNodeStableId,
        string toNodeStableId,
        int seed,
        float routeLength,
        string boatInstanceId,
        string boatPrefabGuid,
        System.Collections.Generic.List<CargoManifest.Snapshot> cargoManifest)
    {
        GameState gs = GameState.I;
        if (gs == null)
        {
            LogError("StartTravelToBoatScene failed because GameState is null.");
            return;
        }

        SaveCurrentPlayerLoadout();

        // Capture current boat state before leaving NodeScene.
        // This matters if loose items/cargo exist on the boat while docked.
        SaveCurrentBoatState("StartTravelToBoatScene before loading BoatScene");

        System.Collections.Generic.List<CargoManifest.Snapshot> payloadCargo =
            cargoManifest ?? gs.boat?.cargo;

        var payload = new TravelPayload(
            fromNodeStableId,
            toNodeStableId,
            seed,
            routeLength,
            boatInstanceId,
            boatPrefabGuid,
            payloadCargo);

        gs.BeginTravel(payload);

        Log(
            $"StartTravelToBoatScene | from={fromNodeStableId} | to={toNodeStableId} " +
            $"| seed={seed} | routeLength={routeLength} | boatId={boatInstanceId} " +
            $"| boatGuid={boatPrefabGuid} | cargoCount={(payloadCargo != null ? payloadCargo.Count : 0)}");

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

        SaveCurrentPlayerLoadout();
        SaveCurrentBoatState("CompleteTravelToDestination before loading NodeScene");

        gs.player.currentNodeId = payload.toNodeStableId;
        gs.ClearTravel();

        Log($"CompleteTravelToDestination | currentNodeId={gs.player.currentNodeId}");
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

        SaveCurrentPlayerLoadout();
        SaveCurrentBoatState("AbortTravelToSource before loading NodeScene");

        gs.player.currentNodeId = payload.fromNodeStableId;
        gs.ClearTravel();

        Log($"AbortTravelToSource | currentNodeId={gs.player.currentNodeId}");
        SceneManager.LoadScene(nodeSceneName);
    }

    public bool SaveCurrentPlayerLoadout()
    {
        if (GameState.I == null)
        {
            LogError("SaveCurrentPlayerLoadout failed because GameState is null.");
            return false;
        }

        PlayerLoadoutPersistence persistence = Object.FindAnyObjectByType<PlayerLoadoutPersistence>();
        if (persistence == null)
        {
            LogWarning("SaveCurrentPlayerLoadout failed because no PlayerLoadoutPersistence was found in scene.");
            return false;
        }

        persistence.SaveToGameState();
        Log("SaveCurrentPlayerLoadout | saved to GameState.");
        return true;
    }

    public bool RestoreCurrentPlayerLoadout()
    {
        if (GameState.I == null)
        {
            LogError("RestoreCurrentPlayerLoadout failed because GameState is null.");
            return false;
        }

        if (GameState.I.playerLoadout == null)
        {
            LogWarning("RestoreCurrentPlayerLoadout skipped because GameState.playerLoadout is null.");
            return false;
        }

        PlayerLoadoutPersistence persistence = Object.FindAnyObjectByType<PlayerLoadoutPersistence>();
        if (persistence == null)
        {
            LogWarning("RestoreCurrentPlayerLoadout failed because no PlayerLoadoutPersistence was found in scene.");
            return false;
        }

        persistence.RestoreFromGameState();
        Log("RestoreCurrentPlayerLoadout | restored from GameState.");
        return true;
    }

    public bool SaveCurrentBoatState(string reason = "")
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

        CaptureCargo(gs, boatRoot);
        CaptureLooseItems(gs, boat);

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

    private void CaptureCargo(GameState gs, Transform boatRoot)
    {
        if (gs == null || gs.boat == null || boatRoot == null)
            return;

        BoatBoardedVolume boarded = boatRoot.GetComponentInChildren<BoatBoardedVolume>(true);
        Collider2D volumeCol = boarded != null ? boarded.GetComponent<Collider2D>() : null;

        gs.boat.cargo = CargoManifest.Capture(boatRoot, volumeCol);

        Log(
            $"CaptureCargo | cargoCount={(gs.boat.cargo != null ? gs.boat.cargo.Count : -1)} " +
            $"| boardedVolume={(boarded != null ? boarded.name : "NULL")} " +
            $"| volumeCol={(volumeCol != null ? volumeCol.name : "NULL")}");
    }

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