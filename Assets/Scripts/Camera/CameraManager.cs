using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-200)]
public class CameraManager : MonoBehaviour, ILocalPlayerAuthority
{
    // Compatibility for scene-wide presentation on a one-human-per-client process.
    // Ambiguity is an error to resolve at bootstrap, never permission to pick a player.
    private static readonly List<CameraManager> Managers = new List<CameraManager>();
    public static CameraManager Instance
    {
        get
        {
            CameraManager result = null;
            foreach (var manager in Managers)
            {
                if (manager == null || !manager.IsLocal) continue;
                if (result != null) return null;
                result = manager;
            }
            return result;
        }
    }

    [Header("Presentation Ownership")]
    [Tooltip("Local on this client only. A networking bootstrap must set remote instances false before activation.")]
    [SerializeField] private bool localPresentation = true;
    [Tooltip("Optional explicit owner. Existing scenes use Follow Target as the initial owner.")]
    [SerializeField] private Transform presentationOwner;
    private PlayerBoardingState ownerPlayer;
    private PlayerBoardingState viewedPlayer;
    private Survival.Death.PlayerDeathSystem ownerDeath;
    private readonly List<ILocalPlayerAuthority> ownerAuthority = new List<ILocalPlayerAuthority>();
    private readonly List<Camera> cameraCache = new List<Camera>();
    private readonly Dictionary<Camera, AudioListener[]> listeners = new Dictionary<Camera, AudioListener[]>();
    private CameraWASDController activeFreeController;
    public enum ViewMode { PlayerFollow, SpectatorFollow, SpectatorFree }
    public ViewMode Mode { get; private set; }
    public PlayerBoardingState OwnerPlayer => ownerPlayer;
    public Transform OwnerTarget => presentationOwner;
    public int BindingVersion { get; private set; }
    public bool IsLocal
    {
        get
        {
            if (!localPresentation || !isActiveAndEnabled || presentationOwner == null) return false;
            foreach (var manager in Managers)
                if (manager != null && manager != this && manager.localPresentation && manager.isActiveAndEnabled &&
                    (ownerPlayer != null ? manager.ownerPlayer == ownerPlayer : manager.presentationOwner == presentationOwner))
                    return false;
            foreach (var authority in ownerAuthority)
                if (authority is Object obj && obj == null || !authority.IsLocal) return false;
            return true;
        }
    }
    public bool CanProvideGameplayInput => IsLocal && Mode == ViewMode.PlayerFollow &&
        (ownerDeath == null || !ownerDeath.IsDead);
    private Camera preSpectatorCamera;
    private float preSpectatorZoom;
    private Vector2 temporaryOffset;
    private Vector2 impulseOffset;
    private float impulseRecovery = 8f;
    private Survival.Death.PlayerDeathSystem spectatedDeath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Managers.Clear();

    public static CameraManager ForActor(Component actor) => actor != null ? ForActor(actor.gameObject) : null;
    public static CameraManager ForActor(GameObject actor)
    {
        if (actor == null) return null;
        CameraManager result = null;
        foreach (var manager in Managers)
        {
            if (manager == null || manager.presentationOwner == null) continue;
            Transform root = manager.ownerPlayer != null ? manager.ownerPlayer.transform : manager.presentationOwner;
            if (actor.transform != root && !actor.transform.IsChildOf(root)) continue;
            if (result != null) return null;
            result = manager;
        }
        return result;
    }
    public static Camera CameraForActor(Component actor)
    {
        var manager = ForActor(actor);
        return manager != null && manager.IsLocal ? manager.ActiveCamera : null;
    }
    public static bool HasGameplayInput(Component actor)
    {
        var manager = ForActor(actor);
        return manager != null && manager.CanProvideGameplayInput;
    }
    public static CameraManager ForCamera(Camera camera)
    {
        if (camera == null) return null;
        foreach (var manager in Managers)
            if (manager != null && manager.GetAllCameras().Contains(camera)) return manager;
        return null;
    }

    [Header("Cameras")]
    public Camera mainCamera;
    public Camera internalCamera;
    public List<Camera> otherCameras = new List<Camera>();

    [Header("Wave System")]
    public Transform waveSystem;
    public float mainCameraWaveZ = 0f;
    public float internalCameraWaveZ = 10f;

    [Header("Defaults")]
    [Tooltip("Zoom level to reset to when this controller activates.")]
    public float defaultOrthoSize = 10f;

    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
    [Min(0f)][SerializeField] private float followSmooth = 12f;
    [SerializeField] private bool followActiveCameraOnly = true;

    [Header("Focus Soft Pan")]
    [Tooltip("Optional intent source. If left empty, this will search the follow target and its parents.")]
    [SerializeField] private MonoBehaviour intentSourceComponent;

    [Tooltip("Enables soft camera panning while focus/right-click is held.")]
    [SerializeField] private bool focusSoftPanEnabled = true;

    [Tooltip("How much of the player-to-cursor offset is applied to the camera.")]
    [Min(0f)][SerializeField] private float focusPanStrength = 0.35f;

    [Tooltip("Maximum world-space camera offset from focus panning.")]
    [Min(0f)][SerializeField] private float focusPanMaxOffset = 3f;

    [Tooltip("How quickly the soft pan offset catches up.")]
    [Min(0f)][SerializeField] private float focusPanSmooth = 10f;

    private Camera activeCamera;
    private ICharacterIntentSource _intentSource;
    private Vector2 _focusPanOffset;

    public Transform FollowTarget => followTarget;
    public Camera ActiveCamera => activeCamera;
    public Vector2 FocusPanOffset => _focusPanOffset;
    public void RestoreFocusPanOffset(Vector2 offset) { _focusPanOffset = offset; }

    /// <summary>
    /// Viewed actor for cutaway/bell presentation; differs from OwnerPlayer while
    /// spectating. Spawn/bootstrap binds ownership through BindPlayer.
    /// </summary>
    public PlayerBoardingState ViewingPlayer => viewedPlayer;

    private void Awake()
    {
        RebuildCameraCache();
        CacheOwner(presentationOwner != null ? presentationOwner : followTarget);

        ActivateCamera(mainCamera);

        if (mainCamera != null)
            mainCamera.orthographic = true;

        if (defaultOrthoSize <= 0f && mainCamera != null)
            defaultOrthoSize = mainCamera.orthographicSize;

        ResetZoom();
    }

    private void OnEnable()
    {
        RebuildCameraCache();
        if (!Managers.Contains(this)) Managers.Add(this);
        ResetZoom();
        ActivateCamera(activeCamera != null ? activeCamera : mainCamera);
        RefreshListeners();
    }

    public void SetFollowTarget(Transform t)
    {
        BindPlayer(t, localPresentation);
    }

    /// <summary>Explicit spawn/rebind seam. Ownership never changes when spectating.</summary>
    public void BindPlayer(Transform player, bool isLocal)
    {
        BindingVersion++;
        RebuildCameraCache();
        localPresentation = isLocal;
        CacheOwner(player);
        Mode = ViewMode.PlayerFollow;
        spectatedDeath = null;
        preSpectatorCamera = null;
        ClearEffects();
        ResetZoom();
        ActivateCamera(mainCamera);
        RefreshListeners();
    }

    private void CacheOwner(Transform player)
    {
        presentationOwner = player;
        ownerPlayer = player != null ? player.GetComponentInParent<PlayerBoardingState>() : null;
        if (ownerPlayer == null && player != null) ownerPlayer = player.GetComponentInChildren<PlayerBoardingState>(true);
        Transform root = ownerPlayer != null ? ownerPlayer.transform : player;
        ownerDeath = root != null ? root.GetComponentInChildren<Survival.Death.PlayerDeathSystem>(true) : null;
        ownerAuthority.Clear();
        if (root != null)
            foreach (var component in root.GetComponentsInParent<MonoBehaviour>(true))
                if (component != this && component is ILocalPlayerAuthority authority) ownerAuthority.Add(authority);
        SetViewTarget(player);
    }

    private void SetViewTarget(Transform target)
    {
        followTarget = target;
        viewedPlayer = target != null ? target.GetComponentInParent<PlayerBoardingState>() : null;
        if (viewedPlayer == null && target != null) viewedPlayer = target.GetComponentInChildren<PlayerBoardingState>(true);
        _focusPanOffset = Vector2.zero;
        // A source resolved for the previous player must never survive a rebind.
        if (intentSourceComponent != null && target != null &&
            !intentSourceComponent.transform.IsChildOf(target) &&
            !target.IsChildOf(intentSourceComponent.transform)) intentSourceComponent = null;
        ResolveIntentSource();
    }

    public bool BeginSpectator()
    {
        if (!IsLocal || ownerDeath == null || !ownerDeath.IsDead) return false;
        if (Mode == ViewMode.PlayerFollow)
        {
            preSpectatorCamera = activeCamera;
            preSpectatorZoom = activeCamera != null ? activeCamera.orthographicSize : defaultOrthoSize;
        }
        EnterFreeSpectator();
        return true;
    }

    public void EnterFreeSpectator()
    {
        if (!IsLocal || Mode == ViewMode.PlayerFollow && (ownerDeath == null || !ownerDeath.IsDead)) return;
        Mode = ViewMode.SpectatorFree;
        spectatedDeath = null;
        SetViewTarget(null);
        ClearEffects();
    }

    public bool SpectatePlayer(PlayerBoardingState player)
    {
        if (!IsLocal || Mode == ViewMode.PlayerFollow || !IsLivingSpectatorTarget(player)) return false;
        Mode = ViewMode.SpectatorFollow;
        spectatedDeath = player.GetComponentInChildren<Survival.Death.PlayerDeathSystem>(true);
        SetViewTarget(player.transform);
        return true;
    }

    private bool IsLivingSpectatorTarget(PlayerBoardingState player)
    {
        if (player == null || player == ownerPlayer || !player.gameObject.activeInHierarchy) return false;
        var death = player.GetComponentInChildren<Survival.Death.PlayerDeathSystem>(true);
        return death == null || !death.IsDead;
    }

    // Global actor enumeration occurs on a deliberate target-switch action only.
    public bool CycleSpectatorTarget()
    {
        if (!IsLocal || Mode == ViewMode.PlayerFollow) return false;
        var players = FindObjectsByType<PlayerBoardingState>(FindObjectsSortMode.InstanceID);
        int start = System.Array.IndexOf(players, ViewingPlayer);
        for (int i = 1; i <= players.Length; i++)
        {
            var player = players[(start + i) % players.Length];
            if (IsLivingSpectatorTarget(player)) return SpectatePlayer(player);
        }
        EnterFreeSpectator();
        return false;
    }

    public void EndSpectator()
    {
        if (!IsLocal) return;
        Mode = ViewMode.PlayerFollow;
        spectatedDeath = null;
        SetViewTarget(presentationOwner);
        ClearEffects();
        if (preSpectatorCamera != null)
        {
            ActivateCamera(preSpectatorCamera);
            preSpectatorCamera.orthographicSize = preSpectatorZoom;
        }
        preSpectatorCamera = null;
    }

    public void SetTemporaryOffset(Vector2 offset) { if (IsLocal) temporaryOffset = offset; }
    public void AddImpulse(Vector2 offset, float recovery = 8f)
    {
        if (!IsLocal) return;
        impulseOffset += offset;
        impulseRecovery = Mathf.Max(0f, recovery);
    }
    public void ClearEffects() { temporaryOffset = impulseOffset = Vector2.zero; }

    public void MoveFreeSpectator(Vector2 delta)
    {
        if (IsLocal && Mode == ViewMode.SpectatorFree && activeCamera != null)
            activeCamera.transform.position += new Vector3(delta.x, delta.y, 0f);
    }

    public void SetFocusPanOverride(float strength, float maxOffset, float smooth)
    {
        focusPanStrength = Mathf.Max(0f, strength);
        focusPanMaxOffset = Mathf.Max(0f, maxOffset);
        focusPanSmooth = Mathf.Max(0f, smooth);
    }

    public void SetFocusPanEnabled(bool enabled)
    {
        focusSoftPanEnabled = enabled;

        if (!enabled)
            _focusPanOffset = Vector2.zero;
    }

    public void ActivateCamera(Camera cam)
    {
        if (cam == null || !GetAllCameras().Contains(cam)) return;

        foreach (var c in GetAllCameras())
            if (c != null) c.enabled = false;

        cam.enabled = IsLocal;
        activeCamera = cam;
        activeFreeController = cam.GetComponent<CameraWASDController>();
        RefreshListeners();

        if (IsLocal && Instance == this && waveSystem != null)
        {
            if (cam == internalCamera) SetWaveZ(internalCameraWaveZ);
            else SetWaveZ(mainCameraWaveZ);
        }
    }

    public void ToggleNextCamera()
    {
        var cameras = GetAllCameras();
        if (cameras.Count == 0) return;

        int index = cameras.IndexOf(activeCamera);
        index = (index + 1) % cameras.Count;

        ActivateCamera(cameras[index]);
    }

    private List<Camera> GetAllCameras()
    {
        return cameraCache;
    }

    /// <summary>Call after changing the configured camera set at runtime.</summary>
    public void RebuildCameraCache()
    {
        cameraCache.Clear();
        if (mainCamera != null) cameraCache.Add(mainCamera);
        if (internalCamera != null && !cameraCache.Contains(internalCamera)) cameraCache.Add(internalCamera);
        for (int i = 0; i < otherCameras.Count; i++)
            if (otherCameras[i] != null && !cameraCache.Contains(otherCameras[i])) cameraCache.Add(otherCameras[i]);
        listeners.Clear();
        foreach (var camera in cameraCache) listeners.Add(camera, camera.GetComponents<AudioListener>());
    }

    private void LateUpdate()
    {
        if (!IsLocal)
        {
            foreach (var camera in GetAllCameras()) if (camera != null) camera.enabled = false;
            RefreshListeners();
            return;
        }
        if (Mode == ViewMode.SpectatorFollow && (followTarget == null ||
            !followTarget.gameObject.activeInHierarchy || spectatedDeath != null && spectatedDeath.IsDead))
            EnterFreeSpectator();
        if (followTarget == null) return;

        UpdateFocusPanOffset();

        if (followActiveCameraOnly)
        {
            if (activeCamera != null)
                Follow(activeCamera.transform);
        }
        else
        {
            foreach (var c in GetAllCameras())
                if (c != null) Follow(c.transform);
        }
        impulseOffset *= Mathf.Exp(-impulseRecovery * Time.unscaledDeltaTime);
    }

    private void Follow(Transform camXform)
    {
        Vector3 desired =
            followTarget.position +
            followOffset +
            new Vector3(_focusPanOffset.x + temporaryOffset.x + impulseOffset.x,
                _focusPanOffset.y + temporaryOffset.y + impulseOffset.y, 0f);

        if (followSmooth <= 0.0001f)
        {
            camXform.position = desired;
            return;
        }

        float t = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
        camXform.position = Vector3.Lerp(camXform.position, desired, t);
    }

    private void UpdateFocusPanOffset()
    {
        Vector2 targetOffset = Vector2.zero;

        if (focusSoftPanEnabled && Mode == ViewMode.PlayerFollow)
        {
            if (_intentSource != null)
            {
                CharacterIntent intent = _intentSource.Current;

                if (intent.FocusHeld)
                {
                    Vector2 fromPlayerToFocus =
                        intent.FocusWorldPoint - (Vector2)followTarget.position;

                    targetOffset = fromPlayerToFocus * focusPanStrength;

                    if (targetOffset.magnitude > focusPanMaxOffset)
                        targetOffset = targetOffset.normalized * focusPanMaxOffset;
                }
            }
        }

        if (focusPanSmooth <= 0.0001f)
        {
            _focusPanOffset = targetOffset;
            return;
        }

        float t = 1f - Mathf.Exp(-focusPanSmooth * Time.deltaTime);
        _focusPanOffset = Vector2.Lerp(_focusPanOffset, targetOffset, t);
    }

    private void ResolveIntentSource()
    {
        // Unity's destroyed-object null semantics do not survive an interface cast.
        _intentSource = intentSourceComponent != null ? intentSourceComponent as ICharacterIntentSource : null;

        if (_intentSource != null)
            return;

        if (followTarget == null)
            return;

        foreach (MonoBehaviour mb in followTarget.GetComponentsInParent<MonoBehaviour>())
        {
            if (mb is ICharacterIntentSource source)
            {
                _intentSource = source;
                intentSourceComponent = mb;
                return;
            }
        }

        foreach (MonoBehaviour mb in followTarget.GetComponentsInChildren<MonoBehaviour>())
        {
            if (mb is ICharacterIntentSource source)
            {
                _intentSource = source;
                intentSourceComponent = mb;
                return;
            }
        }
    }

    private void SetWaveZ(float z)
    {
        Vector3 pos = waveSystem.localPosition;
        waveSystem.localPosition = new Vector3(pos.x, pos.y, z);
    }

    private void Update()
    {
        if (!IsLocal) return;
        if (Mode != ViewMode.PlayerFollow)
        {
            if (Input.GetKeyDown(KeyCode.Tab)) CycleSpectatorTarget();
            if (Input.GetKeyDown(KeyCode.F)) EnterFreeSpectator();
            if (Mode == ViewMode.SpectatorFree && activeCamera != null &&
                (activeFreeController == null || !activeFreeController.isActiveAndEnabled))
            {
                float speed = Input.GetKey(KeyCode.LeftShift) ? 45f : 15f;
                MoveFreeSpectator(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized *
                    speed * Time.unscaledDeltaTime);
            }
            return;
        }
        if (!GameplayInputBlocker.IsBlocked && Input.GetKeyDown(KeyCode.C))
            ToggleNextCamera();
    }

    private static void RefreshListeners()
    {
        var primary = Instance;
        foreach (var manager in Managers)
            if (manager != null)
                foreach (var camera in manager.GetAllCameras())
                    if (camera != null && manager.listeners.TryGetValue(camera, out var cameraListeners))
                        foreach (var listener in cameraListeners)
                            if (listener != null) listener.enabled = manager == primary && camera == manager.activeCamera && camera.isActiveAndEnabled;
    }

    private void OnDisable()
    {
        foreach (var camera in GetAllCameras())
        {
            if (camera == null) continue;
            camera.enabled = false;
            if (listeners.TryGetValue(camera, out var cameraListeners))
                foreach (var listener in cameraListeners) if (listener != null) listener.enabled = false;
        }
        Managers.Remove(this);
        RefreshListeners();
    }

    private void OnDestroy() { Managers.Remove(this); }

    private void ResetZoom()
    {
        if (mainCamera)
            mainCamera.orthographicSize = defaultOrthoSize;
    }
}
