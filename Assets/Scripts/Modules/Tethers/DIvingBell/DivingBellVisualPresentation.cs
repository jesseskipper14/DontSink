using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DivingBellVisualPresentation : MonoBehaviour
{
    [Header("Bell")]
    [SerializeField] private DivingBellOccupancy occupancy;
    [SerializeField] private SpriteRenderer[] interiorRenderers;

    [Tooltip(
        "Separate floor artwork. It follows the bell's current docked/deployed sorting context.")]
    [SerializeField] private SpriteRenderer[] floorRenderers;

    [SerializeField] private SpriteRenderer[] exteriorRenderers;

    [Header("Sorting Authority")]
    [Tooltip(
        "Optional explicit sorting source while docked. Normally leave blank; the source " +
        "auto-resolves from the InstalledModule that owns the TetherPayloadDock.")]
    [SerializeField] private SpriteRenderer sortingReferenceOverride;

    [Header("Orders Relative To Diving Base")]
    [SerializeField] private int interiorOrderOffset = 1;
    [SerializeField] private int occupantOrderOffset = 2;
    [SerializeField] private int exteriorOrderOffset = 3;

    [Header("Deployed Bell Sorting")]
    [Tooltip(
        "Sorting Layer used while the bell is NOT docked. This is global bell presentation: " +
        "deployment changes the bell for every viewer. It is never selected from player occupancy.")]
    [SerializeField] private string deployedSortingLayerName = "";

    [Tooltip(
        "Base sorting order used on the deployed sorting layer. Interior/occupant/exterior " +
        "offsets above are applied relative to this value.")]
    [SerializeField] private int deployedBaseOrder = 0;

    [Header("Local Interior View")]
    [Tooltip(
        "Hide the bell's exterior/front sprite only for the local viewer while that viewer occupies " +
        "this bell. This is client-local presentation and does not change shared bell state.")]
    [SerializeField] private bool hideExteriorForLocalOccupant = true;

    [Tooltip(
        "Optional explicit local player. When blank, presentation follows CameraManager's viewing " +
        "player, then uses a single-player-only fallback when exactly one player exists.")]
    [SerializeField] private PlayerBoardingState localViewingPlayer;

    [Header("Runtime Debug")]
    [SerializeField] private SpriteRenderer resolvedSortingReference;
    [SerializeField] private string resolvedSortingLayer;
    [SerializeField] private int resolvedBaseOrder;
    [SerializeField] private bool resolvedBellDocked;
    [SerializeField] private bool usingDeployedSorting;
    [SerializeField] private bool localViewerInsideBell;

    private bool _reportedMissingDeployedSortingLayer;
    private bool _occupancyEventsBound;

    // Bell sorting is state-driven, not occupant-driven. These values make that
    // explicit and avoid re-writing the bell merely because occupancy changed.
    private bool _hasAppliedBellSortingContext;
    private bool _appliedBellDocked;
    private int _appliedBellSortingLayerId;
    private int _appliedBellBaseOrder;

    private readonly HashSet<PlayerBellOccupantState> _occupantOverrides = new();
    private readonly List<PlayerBellOccupantState> _current = new();
    private readonly List<PlayerBellOccupantState> _restore = new();

    private void Awake()
    {
        ResolveRefs();
        RefreshPresentation();
    }

    private void OnEnable()
    {
        ResolveRefs();
        BindOccupancyEvents();
        RefreshPresentation();
    }

    private void LateUpdate()
    {
        RefreshPresentation();
    }

    private void OnDisable()
    {
        UnbindOccupancyEvents();
        RestoreBellRendererVisibility();
        RestoreAllTrackedOccupants();
    }

    private void OnDestroy()
    {
        UnbindOccupancyEvents();
        RestoreBellRendererVisibility();
        RestoreAllTrackedOccupants();
    }

    /// <summary>
    /// Current bell-interior sorting context. This follows the bell's GLOBAL
    /// docked/deployed presentation state, never the local player's occupancy.
    /// </summary>
    public bool TryGetInteriorSortingContext(
        out int sortingLayerId,
        out int interiorBaseOrder)
    {
        RefreshPresentation();

        sortingLayerId = 0;
        interiorBaseOrder = 0;

        if (!_hasAppliedBellSortingContext)
            return false;

        sortingLayerId = _appliedBellSortingLayerId;
        interiorBaseOrder = _appliedBellBaseOrder + interiorOrderOffset;
        return true;
    }

    public void RefreshPresentation()
    {
        ResolveRefs();
        RestoreDepartedOccupants();

        bool docked =
            occupancy != null &&
            occupancy.IsDocked;

        resolvedBellDocked = docked;

        if (TryResolveBellSortingContext(
                docked,
                out int layerId,
                out int baseOrder,
                out SpriteRenderer source,
                out bool deployedContext))
        {
            resolvedSortingReference = source;
            resolvedSortingLayer = SortingLayer.IDToName(layerId);
            resolvedBaseOrder = baseOrder;
            usingDeployedSorting = deployedContext;

            ApplyBellSortingIfContextChanged(
                docked,
                layerId,
                baseOrder);

            int interiorTargetOrder =
                baseOrder +
                interiorOrderOffset;

            int floorTargetOrder =
                interiorTargetOrder +
                1;

            int occupantTargetOrder =
                Mathf.Max(
                    baseOrder +
                    occupantOrderOffset,
                    floorTargetOrder +
                    1);

            int exteriorTargetOrder =
                Mathf.Max(
                    baseOrder +
                    exteriorOrderOffset,
                    occupantTargetOrder +
                    1);

            SyncOccupants(
                layerId,
                occupantTargetOrder);
        }
        else
        {
            usingDeployedSorting = false;
        }

        RefreshLocalVisibility();
    }

    /// <summary>
    /// Multiplayer seam: each client may explicitly assign its locally-controlled
    /// viewing player. When unset, CameraManager.ViewingPlayer is preferred.
    /// </summary>
    public void SetLocalViewingPlayer(
        PlayerBoardingState player)
    {
        localViewingPlayer = player;
        RefreshLocalVisibility();
    }

    private void BindOccupancyEvents()
    {
        if (_occupancyEventsBound || occupancy == null)
            return;

        occupancy.OccupantEntered += HandleOccupantEntered;
        occupancy.OccupantExited += HandleOccupantExited;
        occupancy.OccupantEmergencyEjected += HandleOccupantEmergencyEjected;
        _occupancyEventsBound = true;
    }

    private void UnbindOccupancyEvents()
    {
        if (!_occupancyEventsBound || occupancy == null)
        {
            _occupancyEventsBound = false;
            return;
        }

        occupancy.OccupantEntered -= HandleOccupantEntered;
        occupancy.OccupantExited -= HandleOccupantExited;
        occupancy.OccupantEmergencyEjected -= HandleOccupantEmergencyEjected;
        _occupancyEventsBound = false;
    }

    private void HandleOccupantEntered(
        DivingBellOccupancy bell,
        PlayerBellOccupantState state)
    {
        if (bell == null || !ReferenceEquals(bell, occupancy))
            return;

        // Occupancy changes occupant sorting/local visibility only. Bell sorting is
        // resolved independently from docked/deployed state in RefreshPresentation.
        RefreshPresentation();
    }

    private void HandleOccupantExited(
        DivingBellOccupancy bell,
        PlayerBellOccupantState state)
    {
        if (bell == null || !ReferenceEquals(bell, occupancy))
            return;

        ReleaseOccupantSortingOverride(state);
        RefreshLocalVisibility();
    }

    private void HandleOccupantEmergencyEjected(
        DivingBellOccupancy bell,
        PlayerBellOccupantState state)
    {
        if (bell == null || !ReferenceEquals(bell, occupancy))
            return;

        ReleaseOccupantSortingOverride(state);
        RefreshLocalVisibility();
    }

    private bool TryResolveBellSortingContext(
        bool docked,
        out int sortingLayerId,
        out int baseOrder,
        out SpriteRenderer source,
        out bool deployedContext)
    {
        sortingLayerId = 0;
        baseOrder = 0;
        source = null;
        deployedContext = false;

        if (!docked &&
            TryResolveDeployedSorting(
                out sortingLayerId,
                out baseOrder))
        {
            deployedContext = true;
            return true;
        }

        source = ResolveSortingReference();

        if (source != null)
        {
            sortingLayerId = source.sortingLayerID;
            baseOrder = source.sortingOrder;
            return true;
        }

        // Preserve the previous valid bell context rather than letting a temporary
        // missing dock reference cause occupancy/local-view changes to re-layer it.
        if (_hasAppliedBellSortingContext)
        {
            sortingLayerId = _appliedBellSortingLayerId;
            baseOrder = _appliedBellBaseOrder;
            return true;
        }

        return false;
    }

    private void ApplyBellSortingIfContextChanged(
        bool docked,
        int sortingLayerId,
        int baseOrder)
    {
        bool changed =
            !_hasAppliedBellSortingContext ||
            _appliedBellDocked != docked ||
            _appliedBellSortingLayerId != sortingLayerId ||
            _appliedBellBaseOrder != baseOrder;

        if (!changed)
            return;

        int interiorTargetOrder =
            baseOrder +
            interiorOrderOffset;

        int floorTargetOrder =
            interiorTargetOrder +
            1;

        int occupantTargetOrder =
            Mathf.Max(
                baseOrder +
                occupantOrderOffset,
                floorTargetOrder +
                1);

        int exteriorTargetOrder =
            Mathf.Max(
                baseOrder +
                exteriorOrderOffset,
                occupantTargetOrder +
                1);

        ApplyBellGroupSorting(
            interiorRenderers,
            sortingLayerId,
            interiorTargetOrder);

        ApplyBellGroupSorting(
            floorRenderers,
            sortingLayerId,
            floorTargetOrder);

        ApplyBellGroupSorting(
            exteriorRenderers,
            sortingLayerId,
            exteriorTargetOrder);

        _hasAppliedBellSortingContext = true;
        _appliedBellDocked = docked;
        _appliedBellSortingLayerId = sortingLayerId;
        _appliedBellBaseOrder = baseOrder;
    }

    private bool TryResolveDeployedSorting(
        out int sortingLayerId,
        out int baseOrder)
    {
        sortingLayerId = 0;
        baseOrder = deployedBaseOrder;

        if (string.IsNullOrWhiteSpace(deployedSortingLayerName))
            return false;

        int candidateId =
            SortingLayer.NameToID(deployedSortingLayerName);

        if (candidateId == 0 &&
            deployedSortingLayerName != "Default")
        {
            if (!_reportedMissingDeployedSortingLayer)
            {
                Debug.LogWarning(
                    $"[DivingBellVisualPresentation:{name}] " +
                    $"Deployed Sorting Layer '{deployedSortingLayerName}' was not found. " +
                    "Bell will retain its last valid sorting context.",
                    this);

                _reportedMissingDeployedSortingLayer = true;
            }

            return false;
        }

        _reportedMissingDeployedSortingLayer = false;
        sortingLayerId = candidateId;
        return true;
    }

    private void RefreshLocalVisibility()
    {
        PlayerBoardingState resolvedLocalViewer =
            ResolveLocalViewingPlayer();

        PlayerBellOccupantState localBellState = null;

        if (resolvedLocalViewer != null)
        {
            localBellState =
                resolvedLocalViewer.GetComponent<PlayerBellOccupantState>() ??
                resolvedLocalViewer.GetComponentInChildren<PlayerBellOccupantState>(true);
        }

        localViewerInsideBell =
            localBellState != null &&
            occupancy != null &&
            localBellState.IsInside(occupancy);

        SetRendererGroupEnabled(interiorRenderers, true);
        SetRendererGroupEnabled(floorRenderers, true);

        bool exteriorVisible =
            !hideExteriorForLocalOccupant ||
            !localViewerInsideBell;

        SetRendererGroupEnabled(exteriorRenderers, exteriorVisible);
    }

    private void RestoreBellRendererVisibility()
    {
        SetRendererGroupEnabled(interiorRenderers, true);
        SetRendererGroupEnabled(floorRenderers, true);
        SetRendererGroupEnabled(exteriorRenderers, true);
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

    private static void SetRendererGroupEnabled(
        SpriteRenderer[] renderers,
        bool enabled)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null)
                renderer.enabled = enabled;
        }
    }

    private void SyncOccupants(
        int sortingLayerId,
        int occupantBaseOrder)
    {
        _current.Clear();

        if (occupancy != null)
        {
            IReadOnlyList<PlayerBellOccupantState> occupants =
                occupancy.Occupants;

            if (occupants != null)
            {
                for (int i = 0; i < occupants.Count; i++)
                {
                    PlayerBellOccupantState state = occupants[i];

                    if (state == null || !state.IsInside(occupancy))
                        continue;

                    _current.Add(state);

                    PlayerBoardingState boarding =
                        ResolvePlayerBoardingState(state);

                    if (boarding == null)
                        continue;

                    // PlayerBoardingState owns all player sprite sorting. The bell
                    // merely requests a temporary override and can update that same
                    // owned override when dock/deploy sorting context changes.
                    if (boarding.TrySetPresentationSortingOverride(
                            this,
                            sortingLayerId,
                            occupantBaseOrder))
                    {
                        _occupantOverrides.Add(state);
                    }
                }
            }
        }

        RestoreDepartedOccupants();
    }

    private void RestoreDepartedOccupants()
    {
        if (_occupantOverrides.Count == 0)
            return;

        _restore.Clear();

        foreach (PlayerBellOccupantState state in _occupantOverrides)
        {
            if (state == null ||
                occupancy == null ||
                !state.IsInside(occupancy))
            {
                _restore.Add(state);
            }
        }

        for (int i = 0; i < _restore.Count; i++)
            ReleaseOccupantSortingOverride(_restore[i]);

        _restore.Clear();
    }

    private void ReleaseOccupantSortingOverride(
        PlayerBellOccupantState state)
    {
        if (state == null)
            return;

        PlayerBoardingState boarding =
            ResolvePlayerBoardingState(state);

        if (boarding != null)
        {
            boarding.ClearPresentationSortingOverride(this);

            // ClearPresentationSortingOverride already rebuilds from the current
            // boarded/unboarded state. Reassert once for defensive callers whose
            // state changed during the same exit/eject operation.
            boarding.ReapplyCurrentPresentation();
        }

        _occupantOverrides.Remove(state);
        _current.Remove(state);
    }

    private void RestoreAllTrackedOccupants()
    {
        if (_occupantOverrides.Count > 0)
        {
            _restore.Clear();

            foreach (PlayerBellOccupantState state in _occupantOverrides)
                _restore.Add(state);

            for (int i = 0; i < _restore.Count; i++)
                ReleaseOccupantSortingOverride(_restore[i]);
        }

        _occupantOverrides.Clear();
        _current.Clear();
        _restore.Clear();
    }

    private static PlayerBoardingState ResolvePlayerBoardingState(
        PlayerBellOccupantState state)
    {
        if (state == null)
            return null;

        return
            state.GetComponent<PlayerBoardingState>() ??
            state.GetComponentInParent<PlayerBoardingState>() ??
            state.GetComponentInChildren<PlayerBoardingState>(true);
    }

    private void ApplyBellGroupSorting(
        SpriteRenderer[] renderers,
        int sortingLayerId,
        int targetBaseOrder)
    {
        if (renderers == null || renderers.Length == 0)
            return;

        int minOrder = int.MaxValue;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null)
                minOrder = Mathf.Min(minOrder, renderer.sortingOrder);
        }

        if (minOrder == int.MaxValue)
            minOrder = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            int relative =
                renderer.sortingOrder -
                minOrder;

            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = targetBaseOrder + relative;
        }
    }

    private SpriteRenderer ResolveSortingReference()
    {
        if (sortingReferenceOverride != null)
            return sortingReferenceOverride;

        if (occupancy == null)
            return resolvedSortingReference;

        TetherPayloadDock dock =
            occupancy.CurrentDock;

        if (dock == null)
            return resolvedSortingReference;

        InstalledModule installed =
            dock.GetComponentInParent<InstalledModule>();

        if (installed == null)
        {
            installed =
                dock.GetComponentInChildren<InstalledModule>(true);
        }

        if (installed == null)
            return resolvedSortingReference;

        SpriteRenderer direct =
            installed.GetComponent<SpriteRenderer>();

        if (IsUsableBaseRenderer(direct))
            return direct;

        SpriteRenderer[] candidates =
            installed.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < candidates.Length; i++)
        {
            if (IsUsableBaseRenderer(candidates[i]))
                return candidates[i];
        }

        return resolvedSortingReference;
    }

    private bool IsUsableBaseRenderer(
        SpriteRenderer candidate)
    {
        if (candidate == null)
            return false;

        Transform t = candidate.transform;

        if (t == transform || t.IsChildOf(transform))
            return false;

        if (occupancy != null)
        {
            Transform bellRoot = occupancy.transform;

            if (t == bellRoot || t.IsChildOf(bellRoot))
                return false;
        }

        return true;
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

        if (isActiveAndEnabled && !_occupancyEventsBound)
            BindOccupancyEvents();
    }


}
