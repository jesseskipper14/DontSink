using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerSceneContextRestorer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerBoardingState boardingState;

    [Header("Spawn Anchors")]
    [Tooltip("Optional local spawn point under the boat used when restoring boarded state.")]
    [SerializeField] private string boatPlayerSpawnPointName = "PlayerSpawnPoint";

    [Tooltip("Fallback local offset if no boat player spawn point exists.")]
    [SerializeField] private Vector3 boardedFallbackLocalOffset = new Vector3(0f, 1f, 0f);

    [Header("Fallback Scene Defaults")]
    [SerializeField] private string boatSceneName = "BoatScene";
    [SerializeField] private string nodeSceneName = "NodeScene";
    [SerializeField] private bool defaultBoatSceneBoarded = true;
    [SerializeField] private bool defaultNodeSceneBoarded = false;

    [Header("Timing")]
    [SerializeField] private float restoreDelaySeconds = 0.05f;
    [SerializeField] private float maxWaitForBoatSeconds = 2f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private bool _restored;

    private void Awake()
    {
        if (boardingState == null)
            boardingState = GetComponent<PlayerBoardingState>();
    }

    private void Start()
    {
        StartCoroutine(RestoreRoutine());
    }

    private IEnumerator RestoreRoutine()
    {
        if (_restored)
            yield break;

        if (restoreDelaySeconds > 0f)
            yield return new WaitForSeconds(restoreDelaySeconds);

        GameState gs = GameState.I;
        PlayerSceneContextSnapshot snapshot = gs != null ? gs.playerSceneContext : null;

        bool shouldBoard;
        string desiredBoatId = null;
        string reason;

        if (snapshot != null && snapshot.hasValue)
        {
            shouldBoard = snapshot.wasBoarded;
            desiredBoatId = snapshot.boatInstanceId;
            reason = "saved player scene context";
        }
        else
        {
            shouldBoard = GetDefaultBoardingForCurrentScene();
            reason = "scene default";
        }

        Log($"RestoreRoutine | shouldBoard={shouldBoard} desiredBoatId='{desiredBoatId}' reason='{reason}'");

        if (shouldBoard)
        {
            Boat boat = null;

            float deadline = Time.unscaledTime + Mathf.Max(0.1f, maxWaitForBoatSeconds);

            while (Time.unscaledTime < deadline)
            {
                boat = FindBoat(desiredBoatId);

                if (boat != null)
                    break;

                yield return null;
            }

            if (boat == null)
            {
                LogWarning($"Could not find boat to restore boarded state. desiredBoatId='{desiredBoatId}'. Leaving player unboarded.");
                ForceUnboard();
                _restored = true;
                yield break;
            }

            RestoreBoarded(boat);
            _restored = true;
            yield break;
        }

        ForceUnboard();
        _restored = true;
    }

    private bool GetDefaultBoardingForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == boatSceneName)
            return defaultBoatSceneBoarded;

        if (sceneName == nodeSceneName)
            return defaultNodeSceneBoarded;

        return false;
    }

    private Boat FindBoat(string desiredBoatId)
    {
        GameState gs = GameState.I;

        if (!string.IsNullOrWhiteSpace(desiredBoatId) &&
            gs != null &&
            gs.boatRegistry != null &&
            gs.boatRegistry.TryGetById(desiredBoatId, out Boat registeredBoat) &&
            registeredBoat != null)
        {
            return registeredBoat;
        }

        if (!string.IsNullOrWhiteSpace(desiredBoatId))
        {
            Boat[] boats = Object.FindObjectsByType<Boat>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < boats.Length; i++)
            {
                Boat b = boats[i];
                if (b != null && b.BoatInstanceId == desiredBoatId)
                    return b;
            }
        }

        try
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("PlayerBoat");
            if (tagged != null && tagged.TryGetComponent(out Boat taggedBoat))
                return taggedBoat;
        }
        catch (UnityException)
        {
            // Tag may not exist. Civilization limps onward.
        }

        return Object.FindAnyObjectByType<Boat>();
    }

    private void RestoreBoarded(Boat boat)
    {
        if (boat == null || boardingState == null)
            return;

        Transform spawnPoint = FindBoatPlayerSpawnPoint(boat.transform);

        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }
        else
        {
            transform.position = boat.transform.TransformPoint(boardedFallbackLocalOffset);
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        boardingState.Board(boat.transform);

        Log($"Restored boarded state on boat='{boat.name}' id='{boat.BoatInstanceId}' pos={transform.position}");
    }

    private void ForceUnboard()
    {
        if (boardingState == null)
            return;

        if (boardingState.IsBoarded)
            boardingState.Unboard();

        Log("Restored unboarded state.");
    }

    private Transform FindBoatPlayerSpawnPoint(Transform boatRoot)
    {
        if (boatRoot == null || string.IsNullOrWhiteSpace(boatPlayerSpawnPointName))
            return null;

        Transform[] all = boatRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && t.name == boatPlayerSpawnPointName)
                return t;
        }

        return null;
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[PlayerSceneContextRestorer:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[PlayerSceneContextRestorer:{name}] {msg}", this);
    }
}