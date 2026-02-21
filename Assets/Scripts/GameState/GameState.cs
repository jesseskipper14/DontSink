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
        boatPrefabId = "DefaultBoat",
        boatInstanceId = "boat_001"
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

    // Convenience helpers
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

    public string boatInstanceId; // NEW

    public TravelPayload(string from, string to, int seed, float len, string boatInstanceId)
    {
        fromNodeStableId = from;
        toNodeStableId = to;
        this.seed = seed;
        routeLength = len;
        this.boatInstanceId = boatInstanceId;
    }
}

[System.Serializable]
public sealed class BoatSaveState
{
    public string boatPrefabId;   // or addressable key
    public string boatInstanceId; // stable per run/save
    // later: cargo, damage, upgrades...
}
