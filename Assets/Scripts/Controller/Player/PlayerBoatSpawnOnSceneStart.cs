using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerBoatSpawnOnSceneStart : MonoBehaviour
{
    [Header("Identity (for MP later)")]
    [SerializeField] private string playerId = "local";

    [Header("Scene Rules")]
    [SerializeField] private string nodeSceneName = "NodeScene";
    [SerializeField] private string boatSceneName = "BoatScene";

    [Header("Placement")]
    [SerializeField] private bool applyRotation = false;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;

    [Header("Search / Timing")]
    [Tooltip("How long to wait for a boat prefab to spawn before giving up.")]
    [SerializeField] private float waitSeconds = 3.0f;

    [Tooltip("Poll interval while waiting.")]
    [SerializeField] private float pollInterval = 0.05f;

    [Tooltip("If true, require BoatRootMarker. If false, fall back to name contains 'boat'.")]
    [SerializeField] private bool requireBoatRootMarker = false;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private bool _done;

    private void OnEnable()
    {
        _done = false;
    }

    private void Start()
    {
        Log($"Start | scene='{SceneManager.GetActiveScene().name}'");

        InvokeRepeating(nameof(TrySpawnNow), 0f, Mathf.Max(0.01f, pollInterval));
        Invoke(nameof(Timeout), Mathf.Max(0.05f, waitSeconds));
    }

    private void Timeout()
    {
        if (_done)
            return;

        CancelInvoke(nameof(TrySpawnNow));

        Debug.LogWarning(
            $"[{nameof(PlayerBoatSpawnOnSceneStart)}] Timed out waiting for boat spawn points. " +
            $"scene='{SceneManager.GetActiveScene().name}'",
            this);

        _done = true;
    }

    private void TrySpawnNow()
    {
        if (_done)
            return;

        Transform boatRoot = FindBoatRoot();
        if (boatRoot == null)
            return;

        PlayerSpawnPoint any = boatRoot.GetComponentInChildren<PlayerSpawnPoint>(true);
        if (any == null)
            return;

        Transform spawn = SpawnPointClaimService.ChooseAndClaimSpawn(boatRoot, playerId, out bool reused);
        if (spawn == null)
            return;

        TeleportTo(spawn, reused);
        ApplySceneBoardingRule(boatRoot);

        Finish();
    }

    private void Finish()
    {
        CancelInvoke(nameof(TrySpawnNow));
        CancelInvoke(nameof(Timeout));
        _done = true;
    }

    private void ApplySceneBoardingRule(Transform boatRoot)
    {
        string sceneName = SceneManager.GetActiveScene().name;

        PlayerBoardingState boarding =
            GetComponent<PlayerBoardingState>() ??
            GetComponentInChildren<PlayerBoardingState>(true) ??
            GetComponentInParent<PlayerBoardingState>();

        if (boarding == null)
        {
            LogWarning($"ApplySceneBoardingRule skipped because PlayerBoardingState was not found. scene='{sceneName}'");
            return;
        }

        if (sceneName == boatSceneName)
        {
            if (boatRoot == null)
            {
                LogWarning("BoatScene requires boarded spawn, but boatRoot is NULL.");
                return;
            }

            boarding.Board(boatRoot);
            Log($"BoatScene → spawned boarded on '{boatRoot.name}'");
            return;
        }

        if (sceneName == nodeSceneName)
        {
            boarding.Unboard();
            Log("NodeScene → spawned unboarded");
            return;
        }

        LogWarning(
            $"Scene '{sceneName}' matched neither '{nodeSceneName}' nor '{boatSceneName}'. " +
            "Leaving boarding state unchanged.");
    }

    private Transform FindBoatRoot()
    {
        BoatRootMarker marker = FindAnyObjectByType<BoatRootMarker>();
        if (marker != null)
            return marker.transform;

        if (requireBoatRootMarker)
            return null;

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            string n = roots[i].name.ToLowerInvariant();
            if (n.Contains("boat"))
                return roots[i].transform;
        }

        return null;
    }

    private void TeleportTo(Transform spawn, bool reused)
    {
        Rigidbody2D rb2 = GetComponent<Rigidbody2D>();
        if (rb2 != null)
            rb2.linearVelocity = Vector2.zero;

        transform.position = spawn.position + positionOffset;

        if (applyRotation)
            transform.rotation = spawn.rotation;

        Log($"Teleported '{name}' to '{spawn.name}' reused={reused}");
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[{nameof(PlayerBoatSpawnOnSceneStart)}:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[{nameof(PlayerBoatSpawnOnSceneStart)}:{name}] {msg}", this);
    }
}