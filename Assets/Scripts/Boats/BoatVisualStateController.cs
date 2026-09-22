using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatVisualStateController : MonoBehaviour
{
    [Header("Renderer Groups")]
    [SerializeField] private Transform exteriorRoot;
    [SerializeField] private Transform interiorRoot;
    [SerializeField] private Transform exteriorDeckRoot;
    [SerializeField] private Transform hullRoot;
    [SerializeField] private Transform alwaysVisibleRoot;

    [Header("Default Visibility")]
    [SerializeField] private BoatVisibilityMode defaultBoardedMode = BoatVisibilityMode.BoardedExteriorDeck;
    [SerializeField] private BoatVisibilityMode unboardedMode = BoatVisibilityMode.UnboardedExterior;

    [Header("Behavior")]
    [SerializeField] private bool hideInteriorWhenUnboarded = true;
    [SerializeField] private bool hideInteriorOnExteriorDeck = true;
    [SerializeField] private bool hideExteriorWhenInterior = true;
    [SerializeField] private bool hideDeckWhenInterior = false;
    [SerializeField] private bool includeInactiveRenderers = true;

    [Header("Water Presentation")]
    [SerializeField] private OceanWaterPresentationController oceanWaterPresentation;
    [SerializeField] private bool showCompartmentWaterInInterior = true;
    [SerializeField] private bool showCompartmentWaterInTransition = true;

    [Header("Temporary Camera Layer Hiding")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask hideByCameraMaskWhenInterior = 0;
    [SerializeField] private bool autoFindMainCamera = true;

    [Header("Client-Local Viewer")]
    [Tooltip(
        "Optional explicit player whose camera/presentation this controller should serve. " +
        "When blank, CameraManager.ViewingPlayer is preferred; single-player falls back " +
        "only when exactly one PlayerBoardingState exists.")]
    [SerializeField] private PlayerBoardingState localViewingPlayer;

    private readonly Dictionary<PlayerBoardingState, List<BoatVisibilityZone>> _activeZonesByPlayer = new();
    private readonly Dictionary<PlayerBoardingState, BoatVisibilityMode> _resolvedModeByPlayer = new();

    private Renderer[] _exteriorRenderers;
    private Renderer[] _interiorRenderers;
    private Renderer[] _deckRenderers;
    private Renderer[] _hullRenderers;
    private Renderer[] _alwaysRenderers;
    private Renderer[] _compartmentWaterRenderers;

    private BoatVisibilityMode _currentMode;
    private PlayerBoardingState _lastResolvedLocalViewer;

    private int _originalCameraCullingMask;
    private bool _cachedOriginalCameraMask;

    private readonly List<IgnoredColliderPair> _ignoredExteriorModulePlayerPairs = new();

    public BoatVisibilityMode CurrentMode => _currentMode;

    [Header("Debug")]
    [SerializeField] private bool logVisibility = false;

    private void Awake()
    {
        AutoAssignRootsIfMissing();
        CacheRenderers();
        ResolveCameraIfNeeded();
        CacheOriginalCameraMaskIfNeeded();
        ApplyMode(unboardedMode);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            AutoAssignRootsIfMissing();
            CacheRenderers();
        }
    }
#endif

    /// <summary>
    /// Multiplayer/client-presentation seam. This affects only which player's zone
    /// state drives this client's renderer/camera/ocean presentation. Per-player
    /// collision handling continues to evaluate every player independently.
    /// </summary>
    public void SetLocalViewingPlayer(
        PlayerBoardingState player)
    {
        localViewingPlayer =
            player;

        _lastResolvedLocalViewer =
            player;

        RefreshPresentationForLocalViewer();
    }

    private void LateUpdate()
    {
        PlayerBoardingState resolved =
            ResolveLocalViewingPlayer();

        if (ReferenceEquals(
                resolved,
                _lastResolvedLocalViewer))
        {
            return;
        }

        _lastResolvedLocalViewer =
            resolved;

        RefreshPresentationForLocalViewer();
    }

    public void NotifyPlayerEnteredZone(PlayerBoardingState player, BoatVisibilityZone zone)
    {
        if (player == null || zone == null)
            return;

        if (!IsPlayerOnThisBoat(player))
            return;

        if (!_activeZonesByPlayer.TryGetValue(player, out List<BoatVisibilityZone> zones))
        {
            zones = new List<BoatVisibilityZone>();
            _activeZonesByPlayer[player] = zones;
        }

        if (!zones.Contains(zone))
            zones.Add(zone);

        ApplyBestModeForPlayer(player);
    }

    public void NotifyPlayerExitedZone(PlayerBoardingState player, BoatVisibilityZone zone)
    {
        if (player == null || zone == null)
            return;

        if (_activeZonesByPlayer.TryGetValue(player, out List<BoatVisibilityZone> zones))
        {
            zones.Remove(zone);

            if (zones.Count == 0)
                _activeZonesByPlayer.Remove(player);
        }

        if (!IsPlayerOnThisBoat(player))
        {
            ApplyResolvedModeForPlayer(
                player,
                unboardedMode);

            return;
        }

        RefreshZonesForPlayer(player);
    }

    public void ForceRefreshForPlayer(PlayerBoardingState player)
    {
        RefreshZonesForPlayer(player);
    }

    /// <summary>
    /// Actively scans current overlaps instead of relying only on OnTriggerEnter.
    /// This fixes cases where boarding places the player directly inside an interior zone.
    /// </summary>
    public void RefreshZonesForPlayer(PlayerBoardingState player)
    {
        if (player == null)
        {
            if (ResolveLocalViewingPlayer() == null)
                ApplyMode(unboardedMode);

            return;
        }

        if (!IsPlayerOnThisBoat(player))
        {
            _activeZonesByPlayer.Remove(player);

            ApplyResolvedModeForPlayer(
                player,
                unboardedMode);

            return;
        }

        _activeZonesByPlayer.Remove(player);

        BoatVisibilityZone[] zones = GetComponentsInChildren<BoatVisibilityZone>(true);
        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(true);

        List<BoatVisibilityZone> activeZones = null;

        foreach (BoatVisibilityZone zone in zones)
        {
            if (zone == null)
                continue;

            Collider2D zoneCollider = zone.GetComponent<Collider2D>();
            if (zoneCollider == null || !zoneCollider.enabled || !zoneCollider.isTrigger)
                continue;

            foreach (Collider2D playerCollider in playerColliders)
            {
                if (playerCollider == null || !playerCollider.enabled)
                    continue;

                if (zoneCollider.IsTouching(playerCollider))
                {
                    activeZones ??= new List<BoatVisibilityZone>();

                    if (!activeZones.Contains(zone))
                        activeZones.Add(zone);

                    break;
                }
            }
        }

        if (activeZones != null && activeZones.Count > 0)
            _activeZonesByPlayer[player] = activeZones;

        ApplyBestModeForPlayer(player);
    }

    private void ApplyBestModeForPlayer(PlayerBoardingState player)
    {
        if (player == null || !IsPlayerOnThisBoat(player))
        {
            ApplyResolvedModeForPlayer(
                player,
                unboardedMode);

            return;
        }

        BoatVisibilityZone bestZone = null;
        int bestPriority = int.MinValue;

        if (_activeZonesByPlayer.TryGetValue(player, out List<BoatVisibilityZone> zones))
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                BoatVisibilityZone zone = zones[i];

                if (zone == null)
                {
                    zones.RemoveAt(i);
                    continue;
                }

                if (zone.Priority > bestPriority)
                {
                    bestPriority = zone.Priority;
                    bestZone = zone;
                }
            }

            if (zones.Count == 0)
                _activeZonesByPlayer.Remove(player);
        }

        BoatVisibilityMode resolvedMode =
            bestZone != null
                ? bestZone.Mode
                : defaultBoardedMode;

        ApplyResolvedModeForPlayer(
            player,
            resolvedMode);
    }

    private void ApplyResolvedModeForPlayer(
        PlayerBoardingState player,
        BoatVisibilityMode mode)
    {
        bool onThisBoat =
            IsPlayerOnThisBoat(player);

        if (player != null)
        {
            if (onThisBoat)
            {
                _resolvedModeByPlayer[player] =
                    mode;
            }
            else
            {
                _resolvedModeByPlayer.Remove(
                    player);
            }

            // This is per-player physical collision context, NOT client-local
            // presentation. Remote players still need correct host-side collision
            // handling while they occupy an interior zone.
            ApplyExteriorModulePlayerCollisionPolicy(
                player,
                onThisBoat
                    ? mode
                    : unboardedMode);
        }

        if (IsLocalViewingPlayer(player))
        {
            ApplyMode(
                onThisBoat
                    ? mode
                    : unboardedMode);
        }
    }

    public void ApplyMode(BoatVisibilityMode mode)
    {
        _currentMode = mode;

        bool exteriorVisible = false;
        bool deckVisible = false;
        bool interiorVisible = false;

        // For the primitive pass, hull and always-visible groups stay visible.
        bool hullVisible = true;
        bool alwaysVisible = true;

        switch (mode)
        {
            case BoatVisibilityMode.UnboardedExterior:
                exteriorVisible = true;
                deckVisible = true;
                interiorVisible = !hideInteriorWhenUnboarded;
                break;

            case BoatVisibilityMode.BoardedExteriorDeck:
                exteriorVisible = true;
                deckVisible = true;
                interiorVisible = !hideInteriorOnExteriorDeck;
                break;

            case BoatVisibilityMode.BoardedInterior:
                exteriorVisible = !hideExteriorWhenInterior;
                deckVisible = !hideDeckWhenInterior;
                interiorVisible = true;
                break;

            case BoatVisibilityMode.Transition:
                exteriorVisible = true;
                deckVisible = true;
                interiorVisible = true;
                break;
        }

        SetRenderersEnabled(_exteriorRenderers, exteriorVisible);
        SetRenderersEnabled(_deckRenderers, deckVisible);
        SetRenderersEnabled(_interiorRenderers, interiorVisible);
        SetRenderersEnabled(_hullRenderers, hullVisible);
        SetRenderersEnabled(_alwaysRenderers, alwaysVisible);

        // Hardpoints explicitly authored as exterior may live under _Gameplay,
        // but their renderers should follow exterior visibility.
        ApplyExternalHardpointVisibility(exteriorVisible);

        bool compartmentWaterVisible =
            (mode == BoatVisibilityMode.BoardedInterior && showCompartmentWaterInInterior) ||
            (mode == BoatVisibilityMode.Transition && showCompartmentWaterInTransition);

        SetRenderersEnabled(_compartmentWaterRenderers, compartmentWaterVisible);

        if (oceanWaterPresentation != null)
            oceanWaterPresentation.ApplyMode(mode);

        // After visibility group toggles, restore stateful hatch sprite presentation.
        RefreshAccessPresentationInRoot(exteriorRoot, exteriorVisible);
        RefreshAccessPresentationInRoot(exteriorDeckRoot, deckVisible);
        RefreshAccessPresentationInRoot(interiorRoot, interiorVisible);
        RefreshAccessPresentationInRoot(hullRoot, hullVisible);
        RefreshAccessPresentationInRoot(alwaysVisibleRoot, alwaysVisible);

        ApplyTemporaryCameraLayerVisibility(mode);

        LogRendererCounts();
    }

    private void ApplyExternalHardpointVisibility(bool exteriorVisible)
    {
        HardpointInteractable[] hardpointInteractables =
            GetComponentsInChildren<HardpointInteractable>(true);

        if (hardpointInteractables == null || hardpointInteractables.Length == 0)
            return;

        for (int i = 0; i < hardpointInteractables.Length; i++)
        {
            HardpointInteractable interactable = hardpointInteractables[i];
            if (interactable == null ||
                !interactable.ExteriorModule)
            {
                continue;
            }

            // Exterior visibility owns the INSTALLED HARDWARE only.
            // The SpriteRenderer directly on the Hardpoint is a contextual
            // placement marker and is owned by HardpointVisibilityController.
            Hardpoint hardpoint =
                interactable.TargetHardpoint;

            InstalledModule installed =
                hardpoint != null
                    ? hardpoint.InstalledModule
                    : null;

            if (installed == null)
                continue;

            Renderer[] renderers =
                installed.GetComponentsInChildren<Renderer>(
                    includeInactiveRenderers);

            SetRenderersEnabled(
                renderers,
                exteriorVisible);
        }
    }

    private void ApplyExteriorModulePlayerCollisionPolicy(
        PlayerBoardingState player,
        BoatVisibilityMode mode)
    {
        if (player == null)
            return;

        if (mode != BoatVisibilityMode.BoardedInterior ||
            !IsPlayerOnThisBoat(player))
        {
            RestoreExteriorModulePlayerCollisions(
                player,
                false);

            return;
        }

        Collider2D[] exteriorModuleColliders =
            GetExteriorModuleSolidColliders();

        if (exteriorModuleColliders.Length == 0)
            return;

        Collider2D[] playerColliders =
            player.GetComponentsInChildren<Collider2D>(
                true);

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider =
                playerColliders[i];

            if (!IsUsableSolidCollider(playerCollider))
                continue;

            for (int j = 0; j < exteriorModuleColliders.Length; j++)
            {
                Collider2D moduleCollider =
                    exteriorModuleColliders[j];

                if (!IsUsableSolidCollider(moduleCollider))
                    continue;

                if (playerCollider == moduleCollider)
                    continue;

                // Another system may already own this ignored pair.
                // Only record and later restore pairs changed here.
                if (Physics2D.GetIgnoreCollision(
                        playerCollider,
                        moduleCollider))
                {
                    continue;
                }

                Physics2D.IgnoreCollision(
                    playerCollider,
                    moduleCollider,
                    true);

                _ignoredExteriorModulePlayerPairs.Add(
                    new IgnoredColliderPair(
                        player,
                        playerCollider,
                        moduleCollider));
            }
        }
    }

    private Collider2D[] GetExteriorModuleSolidColliders()
    {
        HardpointInteractable[] hardpointInteractables =
            GetComponentsInChildren<HardpointInteractable>(true);

        if (hardpointInteractables == null || hardpointInteractables.Length == 0)
            return System.Array.Empty<Collider2D>();

        List<Collider2D> result = new();

        for (int i = 0; i < hardpointInteractables.Length; i++)
        {
            HardpointInteractable interactable = hardpointInteractables[i];
            if (interactable == null || !interactable.ExteriorModule)
                continue;

            Collider2D[] colliders =
                interactable.GetComponentsInChildren<Collider2D>(true);

            for (int j = 0; j < colliders.Length; j++)
            {
                Collider2D collider = colliders[j];
                if (!IsUsableSolidCollider(collider))
                    continue;

                if (!result.Contains(collider))
                    result.Add(collider);
            }
        }

        return result.ToArray();
    }

    private static bool IsUsableSolidCollider(Collider2D collider)
    {
        return collider != null &&
               collider.enabled &&
               !collider.isTrigger;
    }

    private void RestoreExteriorModulePlayerCollisions(
        PlayerBoardingState player,
        bool force)
    {
        for (int i = _ignoredExteriorModulePlayerPairs.Count - 1; i >= 0; i--)
        {
            IgnoredColliderPair pair =
                _ignoredExteriorModulePlayerPairs[i];

            if (player != null &&
                !ReferenceEquals(
                    pair.Player,
                    player))
            {
                continue;
            }

            if (pair.PlayerCollider == null ||
                pair.ModuleCollider == null)
            {
                _ignoredExteriorModulePlayerPairs.RemoveAt(i);
                continue;
            }

            if (!force)
            {
                ColliderDistance2D distance =
                    pair.PlayerCollider.Distance(
                        pair.ModuleCollider);

                if (distance.isOverlapped)
                    continue;
            }

            Physics2D.IgnoreCollision(
                pair.PlayerCollider,
                pair.ModuleCollider,
                false);

            _ignoredExteriorModulePlayerPairs.RemoveAt(i);
        }
    }

    private void FixedUpdate()
    {
        if (_ignoredExteriorModulePlayerPairs.Count == 0)
            return;

        for (int i = _ignoredExteriorModulePlayerPairs.Count - 1; i >= 0; i--)
        {
            IgnoredColliderPair pair =
                _ignoredExteriorModulePlayerPairs[i];

            bool shouldRemainIgnored =
                pair.Player != null &&
                IsPlayerOnThisBoat(pair.Player) &&
                _resolvedModeByPlayer.TryGetValue(
                    pair.Player,
                    out BoatVisibilityMode mode) &&
                mode == BoatVisibilityMode.BoardedInterior;

            if (shouldRemainIgnored)
                continue;

            if (pair.PlayerCollider == null ||
                pair.ModuleCollider == null)
            {
                _ignoredExteriorModulePlayerPairs.RemoveAt(i);
                continue;
            }

            ColliderDistance2D distance =
                pair.PlayerCollider.Distance(
                    pair.ModuleCollider);

            if (distance.isOverlapped)
                continue;

            Physics2D.IgnoreCollision(
                pair.PlayerCollider,
                pair.ModuleCollider,
                false);

            _ignoredExteriorModulePlayerPairs.RemoveAt(i);
        }
    }

    private void OnDisable()
    {
        RestoreExteriorModulePlayerCollisions(
            null,
            true);

        _activeZonesByPlayer.Clear();
        _resolvedModeByPlayer.Clear();
    }

    private readonly struct IgnoredColliderPair
    {
        public readonly PlayerBoardingState Player;
        public readonly Collider2D PlayerCollider;
        public readonly Collider2D ModuleCollider;

        public IgnoredColliderPair(
            PlayerBoardingState player,
            Collider2D playerCollider,
            Collider2D moduleCollider)
        {
            Player =
                player;

            PlayerCollider =
                playerCollider;

            ModuleCollider =
                moduleCollider;
        }
    }

    private void RefreshPresentationForLocalViewer()
    {
        PlayerBoardingState viewer =
            ResolveLocalViewingPlayer();

        _lastResolvedLocalViewer =
            viewer;

        if (viewer != null &&
            IsPlayerOnThisBoat(viewer))
        {
            RefreshZonesForPlayer(
                viewer);

            return;
        }

        ApplyMode(
            unboardedMode);
    }

    private bool IsLocalViewingPlayer(
        PlayerBoardingState player)
    {
        if (player == null)
            return false;

        PlayerBoardingState viewer =
            ResolveLocalViewingPlayer();

        return
            viewer != null &&
            ReferenceEquals(
                player,
                viewer);
    }

    private PlayerBoardingState ResolveLocalViewingPlayer()
    {
        if (localViewingPlayer != null)
            return localViewingPlayer;

        if (CameraManager.Instance != null)
        {
            PlayerBoardingState viewed =
                CameraManager.Instance.ViewingPlayer;

            if (viewed != null)
                return viewed;
        }

        // Single-player fallback only. With multiple players present, refusing to
        // guess is safer than allowing an arbitrary remote player's trigger events
        // to control this client's cutaway/camera/ocean presentation.
        PlayerBoardingState[] players =
            FindObjectsByType<PlayerBoardingState>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        return
            players != null &&
            players.Length == 1
                ? players[0]
                : null;
    }

    private void ResolveCameraIfNeeded()
    {
        if (targetCamera == null && autoFindMainCamera)
            targetCamera = Camera.main;
    }

    private void CacheOriginalCameraMaskIfNeeded()
    {
        if (_cachedOriginalCameraMask)
            return;

        ResolveCameraIfNeeded();

        if (targetCamera != null)
        {
            _originalCameraCullingMask = targetCamera.cullingMask;
            _cachedOriginalCameraMask = true;
        }
    }

    private void ApplyTemporaryCameraLayerVisibility(BoatVisibilityMode mode)
    {
        ResolveCameraIfNeeded();
        CacheOriginalCameraMaskIfNeeded();

        if (targetCamera == null || !_cachedOriginalCameraMask)
            return;

        int hiddenBits = hideByCameraMaskWhenInterior.value;

        if (mode == BoatVisibilityMode.BoardedInterior)
        {
            targetCamera.cullingMask = _originalCameraCullingMask & ~hiddenBits;
            Log($"Camera culling mask updated for interior. Hidden layer bits={hiddenBits}");
        }
        else
        {
            targetCamera.cullingMask = _originalCameraCullingMask;
            Log("Camera culling mask restored to original.");
        }
    }

    private void Log(string message)
    {
        if (!logVisibility)
            return;

        Debug.Log($"[BoatVisualStateController:{name}] {message}", this);
    }

    private void LogRendererCounts()
    {
        if (!logVisibility)
            return;

        Debug.Log(
            $"[BoatVisualStateController:{name}] RendererCounts " +
            $"Exterior={(_exteriorRenderers != null ? _exteriorRenderers.Length : -1)}, " +
            $"Interior={(_interiorRenderers != null ? _interiorRenderers.Length : -1)}, " +
            $"Deck={(_deckRenderers != null ? _deckRenderers.Length : -1)}, " +
            $"Hull={(_hullRenderers != null ? _hullRenderers.Length : -1)}, " +
            $"Always={(_alwaysRenderers != null ? _alwaysRenderers.Length : -1)}, " +
            $"Mode={_currentMode}",
            this);
    }

    [ContextMenu("Auto Assign Roots")]
    public void AutoAssignRootsIfMissing()
    {
        if (exteriorRoot == null)
            exteriorRoot = transform.Find("_Exterior") ?? transform.Find("Exterior");

        if (interiorRoot == null)
            interiorRoot = transform.Find("_Interior") ?? transform.Find("Interior");

        if (exteriorDeckRoot == null)
            exteriorDeckRoot = transform.Find("_Deck") ?? transform.Find("Deck");

        if (hullRoot == null)
            hullRoot = transform.Find("_Hull") ?? transform.Find("Hull");

        if (alwaysVisibleRoot == null)
            alwaysVisibleRoot = transform.Find("_AlwaysVisible") ?? transform.Find("AlwaysVisible");

        if (oceanWaterPresentation == null)
        {
            oceanWaterPresentation =
                GetComponentInChildren<OceanWaterPresentationController>(true);

            if (oceanWaterPresentation == null)
                oceanWaterPresentation = FindAnyObjectByType<OceanWaterPresentationController>();
        }
    }

    [ContextMenu("Cache Renderers")]
    public void CacheRenderers()
    {
        _exteriorRenderers = GetRenderers(exteriorRoot);
        _interiorRenderers = GetRenderers(interiorRoot);
        _deckRenderers = GetRenderers(exteriorDeckRoot);
        _hullRenderers = GetRenderers(hullRoot);
        _alwaysRenderers = GetRenderers(alwaysVisibleRoot);
        _compartmentWaterRenderers = GetCompartmentWaterRenderers();
    }

    [ContextMenu("Preview Unboarded Exterior")]
    private void PreviewUnboardedExterior()
    {
        AutoAssignRootsIfMissing();
        CacheRenderers();
        ResolveCameraIfNeeded();
        CacheOriginalCameraMaskIfNeeded();
        ApplyMode(BoatVisibilityMode.UnboardedExterior);
    }

    [ContextMenu("Preview Boarded Exterior Deck")]
    private void PreviewBoardedExteriorDeck()
    {
        AutoAssignRootsIfMissing();
        CacheRenderers();
        ResolveCameraIfNeeded();
        CacheOriginalCameraMaskIfNeeded();
        ApplyMode(BoatVisibilityMode.BoardedExteriorDeck);
    }

    [ContextMenu("Preview Boarded Interior")]
    private void PreviewBoardedInterior()
    {
        AutoAssignRootsIfMissing();
        CacheRenderers();
        ResolveCameraIfNeeded();
        CacheOriginalCameraMaskIfNeeded();
        ApplyMode(BoatVisibilityMode.BoardedInterior);
    }

    private bool IsPlayerOnThisBoat(PlayerBoardingState player)
    {
        return player != null &&
               player.IsBoarded &&
               player.CurrentBoatRoot == transform;
    }

    private Renderer[] GetRenderers(Transform root)
    {
        if (root == null)
            return System.Array.Empty<Renderer>();

        return root.GetComponentsInChildren<Renderer>(includeInactiveRenderers);
    }

    private Renderer[] GetCompartmentWaterRenderers()
    {
        CompartmentWaterRenderer[] waterRenderers =
            GetComponentsInChildren<CompartmentWaterRenderer>(includeInactiveRenderers);

        if (waterRenderers == null || waterRenderers.Length == 0)
            return System.Array.Empty<Renderer>();

        List<Renderer> result = new();

        for (int i = 0; i < waterRenderers.Length; i++)
        {
            CompartmentWaterRenderer water = waterRenderers[i];
            if (water == null)
                continue;

            Renderer r = water.GetComponent<Renderer>();
            if (r != null)
                result.Add(r);
        }

        return result.ToArray();
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            // If hiding the group, hide everything.
            if (!enabled)
            {
                r.enabled = false;
                continue;
            }

            // If showing the group, do NOT blindly enable renderers controlled by stateful access runtimes.
            // Their runtime owns open/closed sprite selection.
            if (r.GetComponentInParent<HatchRuntime>() != null)
                continue;

            if (r.GetComponentInParent<DoorRuntime>() != null)
                continue;

            r.enabled = true;
        }
    }

    private static void RefreshAccessPresentationInRoot(Transform root, bool rootVisible)
    {
        if (root == null || !rootVisible)
            return;

        HatchRuntime[] hatches = root.GetComponentsInChildren<HatchRuntime>(true);
        for (int i = 0; i < hatches.Length; i++)
        {
            HatchRuntime hatch = hatches[i];
            if (hatch == null)
                continue;

            hatch.RefreshPresentation();
        }

        DoorRuntime[] doors = root.GetComponentsInChildren<DoorRuntime>(true);
        for (int i = 0; i < doors.Length; i++)
        {
            DoorRuntime door = doors[i];
            if (door == null)
                continue;

            door.RefreshPresentation();
        }
    }
}