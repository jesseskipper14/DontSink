using System.Collections.Generic;
using UnityEngine;

public sealed class GameState : MonoBehaviour
{
    public static GameState I { get; private set; }

    [Header("Authoritative State")]
    public WorldMapPlayerState player = new WorldMapPlayerState();
    public WorldMapSimState worldMap = new WorldMapSimState();

    [Header("Active Travel (null when not traveling)")]
    public TravelPayload activeTravel;

    public BoatRegistry boatRegistry;

    public BoatSaveState boat = new BoatSaveState
    {
        boatPrefabGuid = "",      // set once you have a boat
        boatInstanceId = "boat_001",
        cargo = new List<CargoManifest.Snapshot>()
    };

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        if (boatRegistry == null)
            boatRegistry = gameObject.AddComponent<BoatRegistry>();
    }

    public void BeginTravel(TravelPayload payload) => activeTravel = payload;
    public void ClearTravel() => activeTravel = null;
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

    public TravelPayload(string from, string to, int seed, float len, string boatInstanceId, string boatPrefabGuid, List<CargoManifest.Snapshot> cargoManifest)
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
    public string boatPrefabGuid;     // stable GUID of boat prefab
    public string boatInstanceId;     // stable per run/save

    // Persistent cargo snapshot (latest known)
    public List<CargoManifest.Snapshot> cargo;
}