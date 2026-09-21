using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Briefly disables automatic Physics2D simulation while a newly loaded scene
/// reconstructs runtime state.
///
/// Why this exists:
/// - BoatSpawner restores the boat, modules, tether payloads, compartments, and
///   loose items during Start().
/// - Runtime collision helpers such as GhostCollisionProxy may also finish their
///   own setup during Start().
/// - If Box2D is allowed to simulate in the middle of that reconstruction, restored
///   dynamic objects can fall, collide, or receive impulses before their final
///   container/joint/collision context exists.
///
/// The barrier changes Physics2D.simulationMode to Script for the scene's setup
/// frame, then releases it at the end of that frame after synchronizing transforms
/// and any built GhostCollisionProxy bodies.
///
/// This does NOT:
/// - set Time.timeScale to zero;
/// - modify Rigidbody2D body types;
/// - manually simulate a physics step;
/// - hide genuinely invalid saved positions.
///
/// Remove this one class to revert the feature entirely.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-32000)]
public sealed class ScenePhysicsLoadBarrier : MonoBehaviour
{
    private static ScenePhysicsLoadBarrier _instance;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private SimulationMode2D _restoreSimulationMode;
    private bool _barrierActive;
    private bool _ownsSimulationPause;
    private bool _applicationQuitting;
    private Coroutine _releaseRoutine;
    private int _barrierGeneration;

    public static bool IsActive =>
        _instance != null &&
        _instance._barrierActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallBeforeFirstScene()
    {
        if (_instance != null)
            return;

        GameObject host =
            new GameObject("ScenePhysicsLoadBarrier(Runtime)");

        DontDestroyOnLoad(host);

        ScenePhysicsLoadBarrier barrier =
            host.AddComponent<ScenePhysicsLoadBarrier>();

        // The first scene has no previous scene-unload event to give us an early
        // warning, so enter the barrier before the first scene starts loading.
        barrier.BeginBarrier(
            "Initial scene bootstrap");
    }

    private void Awake()
    {
        if (_instance != null &&
            _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance =
            this;

        DontDestroyOnLoad(
            gameObject);
    }

    private void OnEnable()
    {
        if (_instance != null &&
            _instance != this)
        {
            return;
        }

        SceneManager.sceneUnloaded +=
            HandleSceneUnloaded;

        SceneManager.sceneLoaded +=
            HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -=
            HandleSceneUnloaded;

        SceneManager.sceneLoaded -=
            HandleSceneLoaded;
    }

    private void OnApplicationQuit()
    {
        _applicationQuitting =
            true;
    }

    private void OnDestroy()
    {
        if (!ReferenceEquals(
                _instance,
                this))
        {
            return;
        }

        _instance =
            null;

        if (!_applicationQuitting)
            ReleaseBarrierImmediate(
                "Barrier object destroyed");
    }

    private void HandleSceneUnloaded(
        Scene scene)
    {
        // For ordinary LoadScene(Single), this gets us into Script mode before the
        // incoming scene has an opportunity to reach its first physics tick.
        BeginBarrier(
            $"Scene unloading: '{scene.name}'");
    }

    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        // Restart the release timer now that the incoming scene exists. Unity calls
        // sceneLoaded after Awake/OnEnable and before Start, so the entire Start-time
        // restoration pass remains protected.
        BeginBarrier(
            $"Scene loaded: '{scene.name}' ({mode})");
    }

    /// <summary>
    /// Future restore systems may call this if they deliberately reconstruct state
    /// later in the same frame and need the release moved to that frame's end.
    /// Existing scene loading uses it automatically.
    /// </summary>
    public static void ExtendThroughEndOfFrame(
        string reason = null)
    {
        if (_instance == null)
            return;

        _instance.BeginBarrier(
            string.IsNullOrWhiteSpace(reason)
                ? "Explicit extension"
                : reason);
    }

    private void BeginBarrier(
        string reason)
    {
        _barrierGeneration++;

        if (!_barrierActive)
        {
            _restoreSimulationMode =
                Physics2D.simulationMode;

            _ownsSimulationPause =
                _restoreSimulationMode !=
                SimulationMode2D.Script;

            if (_ownsSimulationPause)
            {
                Physics2D.simulationMode =
                    SimulationMode2D.Script;
            }

            _barrierActive =
                true;
        }
        else if (_ownsSimulationPause &&
                 Physics2D.simulationMode !=
                 SimulationMode2D.Script)
        {
            // Something changed the global mode during our very short setup window.
            // Keep the barrier authoritative until its scheduled release.
            Physics2D.simulationMode =
                SimulationMode2D.Script;
        }

        if (_releaseRoutine != null)
        {
            StopCoroutine(
                _releaseRoutine);
        }

        int generation =
            _barrierGeneration;

        _releaseRoutine =
            StartCoroutine(
                ReleaseAtEndOfFrame(
                    generation,
                    reason));

        Log(
            $"ENTER generation={generation} reason='{reason}' " +
            $"restoreMode={_restoreSimulationMode} ownsPause={_ownsSimulationPause}");
    }

    private IEnumerator ReleaseAtEndOfFrame(
        int generation,
        string reason)
    {
        // This permits every Start/Update/LateUpdate in the incoming scene to finish,
        // but releases before the next frame begins its normal FixedUpdate/physics work.
        yield return new WaitForEndOfFrame();

        if (!_barrierActive ||
            generation != _barrierGeneration)
        {
            yield break;
        }

        FinalizeRestoredPhysicsState();

        ReleaseBarrierImmediate(
            $"End-of-frame release after {reason}");
    }

    private void FinalizeRestoredPhysicsState()
    {
        // Push Transform-authored restoration into Physics2D before touching proxy
        // rigidbodies. This matters for objects whose transform was restored after
        // their Rigidbody2D/Collider2D already existed.
        Physics2D.SyncTransforms();

        int syncedGhosts =
            SynchronizeBuiltGhostProxies();

        // Proxy Rigidbody2D pose changes above should also be visible to collider
        // queries immediately when automatic simulation resumes.
        Physics2D.SyncTransforms();

        Log(
            $"FINALIZE syncedGhosts={syncedGhosts}");
    }

    private static int SynchronizeBuiltGhostProxies()
    {
        GhostCollisionProxy[] proxies =
            FindObjectsByType<GhostCollisionProxy>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        if (proxies == null ||
            proxies.Length == 0)
        {
            return 0;
        }

        int synced =
            0;

        for (int i = 0;
             i < proxies.Length;
             i++)
        {
            GhostCollisionProxy proxy =
                proxies[i];

            if (proxy == null ||
                !proxy.IsBuilt ||
                proxy.FollowBody == null ||
                proxy.ProxyBody == null)
            {
                continue;
            }

            Rigidbody2D source =
                proxy.FollowBody;

            Rigidbody2D ghost =
                proxy.ProxyBody;

            ghost.position =
                source.position;

            ghost.rotation =
                source.rotation;

            ghost.linearVelocity =
                source.linearVelocity;

            ghost.angularVelocity =
                source.angularVelocity;

            synced++;
        }

        return synced;
    }

    private void ReleaseBarrierImmediate(
        string reason)
    {
        if (!_barrierActive)
            return;

        if (_releaseRoutine != null)
        {
            StopCoroutine(
                _releaseRoutine);

            _releaseRoutine =
                null;
        }

        SimulationMode2D modeBeforeRelease =
            Physics2D.simulationMode;

        if (_ownsSimulationPause)
        {
            Physics2D.simulationMode =
                _restoreSimulationMode;
        }

        _barrierActive =
            false;

        _ownsSimulationPause =
            false;

        Log(
            $"EXIT reason='{reason}' " +
            $"modeBefore={modeBeforeRelease} modeAfter={Physics2D.simulationMode}");
    }

    private void Log(
        string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[ScenePhysicsLoadBarrier] {message}",
            this);
    }
}
