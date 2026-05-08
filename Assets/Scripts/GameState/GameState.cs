using System.Collections.Generic;
using UnityEngine;

public sealed class GameState : MonoBehaviour
{
    public static GameState I { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    [Header("Authoritative State")]
    public WorldMapPlayerState player = new WorldMapPlayerState();
    public WorldMapSimState worldMap = new WorldMapSimState();

    [Header("Active Travel (null when not traveling)")]
    public TravelPayload activeTravel;

    [Header("Player Loadout")]
    public PlayerLoadoutSnapshot playerLoadout;

    [Header("Boat Registry")]
    public BoatRegistry boatRegistry;

    public BoatSaveState boat = new BoatSaveState
    {
        boatPrefabGuid = "",
        boatInstanceId = "boat_001",
        cargo = new List<CargoManifest.Snapshot>(),
        looseItems = new BoatLooseItemManifest()
    };

    private void Awake()
    {
        if (I != null && I != this)
        {
            LogWarning(
                "Duplicate GameState detected. Destroying this instance. " +
                $"Existing={I.name}, Duplicate={name}");

            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        EnsureBoatStateDefaults();

        Log("Awake accepted as singleton.");
        LogState("Awake BEFORE registry check");

        if (boatRegistry == null)
        {
            boatRegistry = gameObject.AddComponent<BoatRegistry>();
            Log("BoatRegistry was NULL. Added BoatRegistry component to GameState.");
        }
        else
        {
            Log($"BoatRegistry already assigned: {boatRegistry.name}");
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

            if (payload.cargoManifest == null)
                LogWarning("BeginTravel payload cargoManifest is NULL.");
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

            if (boat.cargo == null)
                LogWarning("BoatSaveState cargo list is NULL.");

            if (boat.looseItems == null)
                LogWarning("BoatSaveState looseItems is NULL.");
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

    public void LogState(string label)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[GameState:{name}] STATE [{label}]\n" +
            $"  activeTravel={DescribeTravel(activeTravel)}\n" +
            $"  boat={DescribeBoat(boat)}\n" +
            $"  playerLoadout={(playerLoadout != null ? "OK" : "NULL")}\n" +
            $"  boatRegistry={(boatRegistry != null ? boatRegistry.name : "NULL")}",
            this);
    }

    private void EnsureBoatStateDefaults()
    {
        if (boat == null)
        {
            boat = new BoatSaveState
            {
                boatPrefabGuid = "",
                boatInstanceId = "boat_001",
                cargo = new List<CargoManifest.Snapshot>(),
                looseItems = new BoatLooseItemManifest()
            };

            return;
        }

        if (boat.cargo == null)
            boat.cargo = new List<CargoManifest.Snapshot>();

        if (boat.looseItems == null)
            boat.looseItems = new BoatLooseItemManifest();
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
            $"boatPrefabGuid='{payload.boatPrefabGuid}', " +
            $"cargoCount={(payload.cargoManifest != null ? payload.cargoManifest.Count : -1)}";
    }

    private string DescribeBoat(BoatSaveState state)
    {
        if (state == null)
            return "NULL";

        return
            $"boatInstanceId='{state.boatInstanceId}', " +
            $"boatPrefabGuid='{state.boatPrefabGuid}', " +
            $"cargoCount={(state.cargo != null ? state.cargo.Count : -1)}, " +
            $"looseItems={(state.looseItems != null ? DescribeLooseItems(state.looseItems) : "NULL")}";
    }

    private string DescribeLooseItems(BoatLooseItemManifest manifest)
    {
        if (manifest == null)
            return "NULL";

        return $"version={manifest.version}, count={(manifest.looseItems != null ? manifest.looseItems.Count : -1)}";
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[GameState:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[GameState:{name}] {msg}", this);
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

    public List<CargoManifest.Snapshot> cargoManifest;

    public TravelPayload(
        string from,
        string to,
        int seed,
        float len,
        string boatInstanceId,
        string boatPrefabGuid,
        List<CargoManifest.Snapshot> cargoManifest)
    {
        fromNodeStableId = from;
        toNodeStableId = to;
        this.seed = seed;
        routeLength = len;

        this.boatInstanceId = boatInstanceId;
        this.boatPrefabGuid = boatPrefabGuid;
        this.cargoManifest = cargoManifest;
    }
}

[System.Serializable]
public sealed class BoatSaveState
{
    public string boatPrefabGuid;
    public string boatInstanceId;

    public List<CargoManifest.Snapshot> cargo;
    public BoatLooseItemManifest looseItems;
}