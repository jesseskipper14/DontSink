using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerBoatSpawnOnSceneStart : MonoBehaviour
{
    [Header("Identity (for MP later)")]
    [SerializeField] private string playerId = "local";

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

    private bool _done;

    private void OnEnable()
    {
        _done = false;
    }

    private void Start()
    {
        // If boat already exists, we snap immediately.
        // Otherwise, we wait a bit for BoatSpawner to instantiate it.
        InvokeRepeating(nameof(TrySpawnNow), 0f, Mathf.Max(0.01f, pollInterval));
        Invoke(nameof(Timeout), Mathf.Max(0.05f, waitSeconds));
    }

    private void Timeout()
    {
        if (_done) return;
        CancelInvoke(nameof(TrySpawnNow));

        // Optional: warning only (don’t hard error)
        Debug.LogWarning($"[{nameof(PlayerBoatSpawnOnSceneStart)}] Timed out waiting for boat spawn points.", this);
        _done = true;
    }

    private void TrySpawnNow()
    {
        if (_done) return;

        var boatRoot = FindBoatRoot();
        if (boatRoot == null) return;

        // Must have at least one PlayerSpawnPoint component.
        var any = boatRoot.GetComponentInChildren<PlayerSpawnPoint>(true);
        if (any == null) return;

        var spawn = SpawnPointClaimService.ChooseAndClaimSpawn(boatRoot, playerId, out var reused);
        if (spawn == null) return;

        TeleportTo(spawn, reused);

        CancelInvoke(nameof(TrySpawnNow));
        CancelInvoke(nameof(Timeout));
        _done = true;
    }

    private Transform FindBoatRoot()
    {
        var marker = FindAnyObjectByType<BoatRootMarker>();
        if (marker != null) return marker.transform;

        if (requireBoatRootMarker) return null;

        // Fallback: name heuristic
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var n = roots[i].name.ToLowerInvariant();
            if (n.Contains("boat"))
                return roots[i].transform;
        }

        return null;
    }

    private void TeleportTo(Transform spawn, bool reused)
    {
        var rb2 = GetComponent<Rigidbody2D>();
        if (rb2 != null) rb2.linearVelocity = Vector2.zero;

        transform.position = spawn.position + positionOffset;

        if (applyRotation)
            transform.rotation = spawn.rotation;

        // Debug.Log($"Spawned '{name}' at '{spawn.name}' (reused={reused})");
    }
}