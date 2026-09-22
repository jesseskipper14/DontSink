using UnityEngine;

/// <summary>
/// Client-local presentation bridge for a diving-bell occupant.
///
/// The existing boat interior presentation does not use a true spatial water mask.
/// Instead it presents the background ocean and suppresses the foreground ocean.
/// This component deliberately reuses that exact presentation mode while the LOCAL
/// viewer occupies this bell, then restores the viewer's ordinary boat/world mode.
///
/// It changes presentation only. Bell flooding, buoyancy, breathing, and occupancy
/// remain authoritative in their existing runtime systems.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DivingBellOccupancy))]
public sealed class DivingBellOceanPresentationBridge : MonoBehaviour
{
    [Header("Bell")]
    [SerializeField] private DivingBellOccupancy occupancy;

    [Header("Local Viewer")]
    [Tooltip(
        "Optional explicit locally-controlled player. When blank, this follows CameraManager's " +
        "viewing player, then uses a single-player-only fallback when exactly one player exists.")]
    [SerializeField] private PlayerBoardingState localViewingPlayer;

    [Header("Ocean Presentation")]
    [Tooltip(
        "Optional explicit global ocean presentation controller. Leave blank to auto-find the scene controller.")]
    [SerializeField] private OceanWaterPresentationController oceanWaterPresentation;

    [Tooltip(
        "Optional explicit bell air/water state. Leave blank to auto-resolve from this bell.")]
    [SerializeField] private DivingBellAirVolume airVolume;

    [Tooltip(
        "Do not suppress the foreground ocean until the bell opening is actually submerged. " +
        "This preserves the boat's normal hull-water presentation while the occupied bell is still hanging in air.")]
    [SerializeField] private bool onlyOverrideWhenOpeningSubmerged = true;

    [Tooltip(
        "BoatVisibilityMode used while the local viewer is inside this bell. BoardedInterior reuses the " +
        "same background-water / foreground-water presentation already used for dry boat interiors.")]
    [SerializeField]
    private BoatVisibilityMode bellInteriorOceanMode =
        BoatVisibilityMode.BoardedInterior;

    [Header("Debug")]
    [SerializeField] private bool localViewerInsideBell;
    [SerializeField] private bool bellNeedsOceanOverride;
    [SerializeField] private bool presentationOverrideActive;
    [SerializeField] private bool verboseLogging = false;

    private bool _reportedMissingOceanController;

    private void Awake()
    {
        ResolveRefs();
        RefreshPresentation();
    }

    private void OnEnable()
    {
        ResolveRefs();
        RefreshPresentation();
    }

    private void LateUpdate()
    {
        RefreshPresentation();
    }

    private void OnDisable()
    {
        RestoreNormalPresentationIfNeeded();
    }

    private void OnDestroy()
    {
        RestoreNormalPresentationIfNeeded();
    }

    /// <summary>
    /// Multiplayer seam. Each client should assign its locally-controlled player.
    /// </summary>
    public void SetLocalViewingPlayer(PlayerBoardingState player)
    {
        if (ReferenceEquals(localViewingPlayer, player))
            return;

        RestoreNormalPresentationIfNeeded();
        localViewingPlayer = player;
        RefreshPresentation();
    }

    public void RefreshPresentation()
    {
        ResolveRefs();

        localViewerInsideBell =
            IsLocalViewerInsideThisBell();

        bellNeedsOceanOverride =
            localViewerInsideBell &&
            (!onlyOverrideWhenOpeningSubmerged ||
             (airVolume != null &&
              airVolume.OpeningSubmerged));

        if (bellNeedsOceanOverride)
        {
            if (oceanWaterPresentation == null)
            {
                if (!_reportedMissingOceanController)
                {
                    Debug.LogWarning(
                        $"[DivingBellOceanPresentationBridge:{name}] " +
                        "No OceanWaterPresentationController could be resolved. " +
                        "Bell physics/air still work, but the foreground ocean cannot be suppressed for the local interior view.",
                        this);

                    _reportedMissingOceanController = true;
                }

                presentationOverrideActive = false;
                return;
            }

            _reportedMissingOceanController = false;

            // Reassert every LateUpdate while occupied. Boat visibility changes are
            // event-driven, so this cheaply guarantees the bell's local view wins
            // if another presentation owner refreshed earlier in the frame.
            oceanWaterPresentation.ApplyMode(
                bellInteriorOceanMode);

            if (!presentationOverrideActive)
            {
                Log(
                    $"Applied local bell ocean presentation mode={bellInteriorOceanMode}.");
            }

            presentationOverrideActive = true;
            return;
        }

        RestoreNormalPresentationIfNeeded();
    }

    private bool IsLocalViewerInsideThisBell()
    {
        PlayerBoardingState resolvedLocalViewer =
            ResolveLocalViewingPlayer();

        if (occupancy == null ||
            resolvedLocalViewer == null)
        {
            return false;
        }

        PlayerBellOccupantState bellState =
            resolvedLocalViewer.GetComponent<PlayerBellOccupantState>() ??
            resolvedLocalViewer.GetComponentInChildren<PlayerBellOccupantState>(true);

        return
            bellState != null &&
            bellState.IsInside(occupancy);
    }

    private void RestoreNormalPresentationIfNeeded()
    {
        if (!presentationOverrideActive)
            return;

        presentationOverrideActive = false;

        ResolveRefs();

        if (oceanWaterPresentation == null)
            return;

        BoatVisibilityMode restoreMode =
            ResolveNormalViewerMode();

        oceanWaterPresentation.ApplyMode(
            restoreMode);

        Log(
            $"Restored local ocean presentation mode={restoreMode}.");
    }

    private BoatVisibilityMode ResolveNormalViewerMode()
    {
        PlayerBoardingState resolvedLocalViewer =
            ResolveLocalViewingPlayer();

        if (resolvedLocalViewer == null ||
            !resolvedLocalViewer.IsBoarded ||
            resolvedLocalViewer.CurrentBoatRoot == null)
        {
            return BoatVisibilityMode.UnboardedExterior;
        }

        BoatVisualStateController boatVisual =
            resolvedLocalViewer.CurrentBoatRoot.GetComponent<BoatVisualStateController>() ??
            resolvedLocalViewer.CurrentBoatRoot.GetComponentInChildren<BoatVisualStateController>(true);

        if (boatVisual != null)
            return boatVisual.CurrentMode;

        return BoatVisibilityMode.BoardedExteriorDeck;
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

        // Single-player fallback only. With multiple players present, a missing local
        // viewer is safer than letting an arbitrary remote player drive client-local
        // ocean presentation.
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

    private void ResolveRefs()
    {
        if (occupancy == null)
        {
            occupancy =
                GetComponent<DivingBellOccupancy>() ??
                GetComponentInParent<DivingBellOccupancy>() ??
                GetComponentInChildren<DivingBellOccupancy>(true);
        }

        // Local viewing-player resolution is intentionally deferred to
        // ResolveLocalViewingPlayer(). Do not cache an arbitrary scene-global player here.

        if (airVolume == null)
        {
            airVolume =
                GetComponent<DivingBellAirVolume>() ??
                GetComponentInParent<DivingBellAirVolume>() ??
                GetComponentInChildren<DivingBellAirVolume>(true);
        }

        if (oceanWaterPresentation == null)
        {
            oceanWaterPresentation =
                FindFirstObjectByType<OceanWaterPresentationController>();
        }
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[DivingBellOceanPresentationBridge:{name}] {message}",
            this);
    }
}
