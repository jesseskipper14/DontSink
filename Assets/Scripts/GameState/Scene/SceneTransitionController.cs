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

        gs.activeTravel = new TravelPayload(
            fromNodeStableId,
            toNodeStableId,
            seed,
            routeLength,
            boatInstanceId,
            boatPrefabGuid,
            cargoManifest);

        SaveCurrentPlayerLoadout();

        Log($"StartTravelToBoatScene | from={fromNodeStableId} | to={toNodeStableId} | seed={seed} | routeLength={routeLength} | boatId={boatInstanceId} | boatGuid={boatPrefabGuid} | cargoCount={(cargoManifest != null ? cargoManifest.Count : 0)}");
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

        gs.player.currentNodeId = payload.toNodeStableId;
        SaveCurrentPlayerLoadout();
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

        gs.player.currentNodeId = payload.fromNodeStableId;
        SaveCurrentPlayerLoadout();
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