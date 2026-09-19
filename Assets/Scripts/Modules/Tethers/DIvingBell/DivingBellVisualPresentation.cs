using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DivingBellVisualPresentation : MonoBehaviour
{
    [Header("Bell")]
    [SerializeField] private DivingBellOccupancy occupancy;
    [SerializeField] private SpriteRenderer[] interiorRenderers;

    [Tooltip(
        "Separate floor artwork. It always follows the interior Sorting Layer and " +
        "renders exactly one order above the interior group.")]
    [SerializeField] private SpriteRenderer[] floorRenderers;

    [SerializeField] private SpriteRenderer[] exteriorRenderers;

    [Header("Sorting Authority")]
    [Tooltip("Optional explicit sorting source. Normally leave blank; while docked this auto-resolves from the InstalledModule that owns the TetherPayloadDock.")]
    [SerializeField] private SpriteRenderer sortingReferenceOverride;

    [Header("Orders Relative To Diving Base")]
    [SerializeField] private int interiorOrderOffset = 1;
    [SerializeField] private int occupantOrderOffset = 2;
    [SerializeField] private int exteriorOrderOffset = 3;

    [Header("Deployed Bell Sorting")]
    [Tooltip(
        "Sorting Layer used while the bell is NOT docked. Set this to a layer that " +
        "renders in front of the boat. Blank/invalid falls back to the last docked layer.")]
    [SerializeField] private string deployedSortingLayerName = "";

    [Tooltip(
        "Base sorting order used on the deployed sorting layer. Interior/occupant/" +
        "exterior offsets above are applied relative to this value.")]
    [SerializeField] private int deployedBaseOrder = 0;

    [Header("Local Interior View")]
    [Tooltip(
        "Hide the bell's exterior/front sprite only for the local viewer while that " +
        "viewer occupies this bell. This is client-local presentation and does not " +
        "change shared bell state.")]
    [SerializeField] private bool hideExteriorForLocalOccupant = true;

    [Tooltip(
        "Optional explicit local player. In single-player this may remain blank and " +
        "auto-resolve. In multiplayer, the client/player bootstrap should assign the " +
        "locally-controlled PlayerBoardingState through SetLocalViewingPlayer().")]
    [SerializeField] private PlayerBoardingState localViewingPlayer;

    [Header("Runtime Debug")]
    [SerializeField] private SpriteRenderer resolvedSortingReference;
    [SerializeField] private string resolvedSortingLayer;
    [SerializeField] private int resolvedBaseOrder;
    [SerializeField] private bool resolvedBellDocked;
    [SerializeField] private bool usingDeployedSorting;
    [SerializeField] private bool localViewerInsideBell;

    private bool _reportedMissingDeployedSortingLayer;

    private readonly Dictionary<PlayerBellOccupantState, PlayerSortingSnapshot> _snapshots = new();
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
        RefreshPresentation();
    }

    private void LateUpdate()
    {
        RefreshPresentation();
    }

    private void OnDisable()
    {
        SetRendererGroupEnabled(
            exteriorRenderers,
            true);

        RestoreAllTrackedOccupants();
    }

    private void OnDestroy()
    {
        SetRendererGroupEnabled(
            exteriorRenderers,
            true);

        RestoreAllTrackedOccupants();
    }

    /// <summary>
    /// Authoritative sorting context for objects visually contained by this bell.
    /// Consumers should use this instead of inferring the bell state from an
    /// arbitrary SpriteRenderer in the hierarchy.
    /// </summary>
    public bool TryGetInteriorSortingContext(
        out int sortingLayerId,
        out int interiorBaseOrder)
    {
        RefreshPresentation();

        sortingLayerId = 0;
        interiorBaseOrder = 0;

        if (string.IsNullOrWhiteSpace(resolvedSortingLayer))
            return false;

        sortingLayerId = SortingLayer.NameToID(resolvedSortingLayer);
        interiorBaseOrder = resolvedBaseOrder + interiorOrderOffset;

        return true;
    }

    public void RefreshPresentation()
    {
        ResolveRefs();

        bool docked =
            occupancy != null &&
            occupancy.IsDocked;

        resolvedBellDocked =
            docked;

        int layerId;
        int baseOrder;

        if (!docked &&
            TryResolveDeployedSorting(
                out layerId,
                out baseOrder))
        {
            usingDeployedSorting =
                true;

            resolvedSortingReference =
                null;

            resolvedSortingLayer =
                SortingLayer.IDToName(
                    layerId);

            resolvedBaseOrder =
                baseOrder;
        }
        else
        {
            usingDeployedSorting =
                false;

            SpriteRenderer source =
                ResolveSortingReference();

            if (source == null)
            {
                RefreshLocalExteriorVisibility();
                return;
            }

            resolvedSortingReference =
                source;

            resolvedSortingLayer =
                source.sortingLayerName;

            resolvedBaseOrder =
                source.sortingOrder;

            layerId =
                source.sortingLayerID;

            baseOrder =
                source.sortingOrder;
        }

        int interiorTargetOrder =
            baseOrder +
            interiorOrderOffset;

        int floorTargetOrder =
            interiorTargetOrder +
            1;

        // Keep occupants above the floor even if an older prefab still carries
        // the original occupantOrderOffset=2 value.
        int occupantTargetOrder =
            Mathf.Max(
                baseOrder +
                occupantOrderOffset,
                floorTargetOrder +
                1);

        // Preserve the authored exterior offset when it is already higher, but
        // never let the front shell fall behind an occupant.
        int exteriorTargetOrder =
            Mathf.Max(
                baseOrder +
                exteriorOrderOffset,
                occupantTargetOrder +
                1);

        ApplyBellGroupSorting(
            interiorRenderers,
            layerId,
            interiorTargetOrder);

        ApplyBellGroupSorting(
            floorRenderers,
            layerId,
            floorTargetOrder);

        ApplyBellGroupSorting(
            exteriorRenderers,
            layerId,
            exteriorTargetOrder);

        SyncOccupants(
            layerId,
            occupantTargetOrder);

        RefreshLocalExteriorVisibility();
    }

    private bool TryResolveDeployedSorting(
        out int sortingLayerId,
        out int baseOrder)
    {
        sortingLayerId =
            0;

        baseOrder =
            deployedBaseOrder;

        if (string.IsNullOrWhiteSpace(
                deployedSortingLayerName))
        {
            return false;
        }

        int candidateId =
            SortingLayer.NameToID(
                deployedSortingLayerName);

        if (candidateId == 0 &&
            deployedSortingLayerName != "Default")
        {
            if (!_reportedMissingDeployedSortingLayer)
            {
                Debug.LogWarning(
                    $"[DivingBellVisualPresentation:{name}] " +
                    $"Deployed Sorting Layer '{deployedSortingLayerName}' was not found. " +
                    "Bell will temporarily fall back to its docked sorting context.",
                    this);

                _reportedMissingDeployedSortingLayer =
                    true;
            }

            return false;
        }

        _reportedMissingDeployedSortingLayer =
            false;

        sortingLayerId =
            candidateId;

        return true;
    }

    /// <summary>
    /// Multiplayer seam: each client should assign its locally-controlled player.
    /// In single-player, leaving this unset falls back to FindFirstObjectByType.
    /// </summary>
    public void SetLocalViewingPlayer(
        PlayerBoardingState player)
    {
        localViewingPlayer =
            player;

        RefreshLocalExteriorVisibility();
    }

    private void RefreshLocalExteriorVisibility()
    {
        ResolveLocalViewingPlayer();

        PlayerBellOccupantState localBellState =
            null;

        if (localViewingPlayer != null)
        {
            localBellState =
                localViewingPlayer.GetComponent<PlayerBellOccupantState>() ??
                localViewingPlayer.GetComponentInChildren<PlayerBellOccupantState>(
                    true);
        }

        localViewerInsideBell =
            localBellState != null &&
            occupancy != null &&
            localBellState.IsInside(
                occupancy);

        bool exteriorVisible =
            !hideExteriorForLocalOccupant ||
            !localViewerInsideBell;

        SetRendererGroupEnabled(
            exteriorRenderers,
            exteriorVisible);
    }

    private void ResolveLocalViewingPlayer()
    {
        if (localViewingPlayer != null)
            return;

        // Single-player fallback only. Future multiplayer bootstrap should call
        // SetLocalViewingPlayer so a remote player's occupancy never controls
        // this client's bell shell visibility.
        localViewingPlayer =
            FindFirstObjectByType<PlayerBoardingState>();
    }

    private static void SetRendererGroupEnabled(
        SpriteRenderer[] renderers,
        bool enabled)
    {
        if (renderers == null)
            return;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            SpriteRenderer renderer =
                renderers[i];

            if (renderer != null)
                renderer.enabled = enabled;
        }
    }

    private void SyncOccupants(int sortingLayerId, int occupantBaseOrder)
    {
        _current.Clear();

        if (occupancy != null)
        {
            IReadOnlyList<PlayerBellOccupantState> occupants = occupancy.Occupants;

            if (occupants != null)
            {
                for (int i = 0; i < occupants.Count; i++)
                {
                    PlayerBellOccupantState state = occupants[i];

                    if (state == null || !state.IsInside(occupancy))
                        continue;

                    _current.Add(state);

                    if (!_snapshots.TryGetValue(state, out PlayerSortingSnapshot snapshot))
                    {
                        snapshot = PlayerSortingSnapshot.Capture(state);
                        _snapshots.Add(state, snapshot);
                    }

                    snapshot.ApplyBellSorting(
                        sortingLayerId,
                        occupantBaseOrder);
                }
            }
        }

        _restore.Clear();

        foreach (var pair in _snapshots)
        {
            if (pair.Key == null || !_current.Contains(pair.Key))
                _restore.Add(pair.Key);
        }

        for (int i = 0; i < _restore.Count; i++)
        {
            PlayerBellOccupantState state = _restore[i];

            if (_snapshots.TryGetValue(state, out PlayerSortingSnapshot snapshot))
            {
                snapshot.Restore();
                ReapplyAuthoritativePlayerPresentation(
                    state);
            }

            _snapshots.Remove(state);
        }
    }

    private void RestoreAllTrackedOccupants()
    {
        foreach (var pair in _snapshots)
        {
            pair.Value?.Restore();

            ReapplyAuthoritativePlayerPresentation(
                pair.Key);
        }

        _snapshots.Clear();
        _current.Clear();
        _restore.Clear();
    }

    private static void ReapplyAuthoritativePlayerPresentation(
        PlayerBellOccupantState state)
    {
        if (state == null)
            return;

        PlayerBoardingState boarding =
            state.GetComponent<PlayerBoardingState>() ??
            state.GetComponentInChildren<PlayerBoardingState>(
                true);

        if (boarding != null)
            boarding.ReapplyCurrentPresentation();
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

            int relative = renderer.sortingOrder - minOrder;

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

        TetherPayloadDock dock = occupancy.CurrentDock;
        if (dock == null)
            return resolvedSortingReference;

        InstalledModule installed = dock.GetComponentInParent<InstalledModule>();
        if (installed == null)
            installed = dock.GetComponentInChildren<InstalledModule>(true);

        if (installed == null)
            return resolvedSortingReference;

        SpriteRenderer direct = installed.GetComponent<SpriteRenderer>();
        if (IsUsableBaseRenderer(direct))
            return direct;

        SpriteRenderer[] candidates = installed.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < candidates.Length; i++)
        {
            if (IsUsableBaseRenderer(candidates[i]))
                return candidates[i];
        }

        return resolvedSortingReference;
    }

    private bool IsUsableBaseRenderer(SpriteRenderer candidate)
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

        ResolveLocalViewingPlayer();
    }

    private sealed class PlayerSortingSnapshot
    {
        private readonly SpriteRenderer[] _renderers;
        private readonly int[] _layerIds;
        private readonly int[] _orders;
        private readonly int[] _relativeOrders;

        private PlayerSortingSnapshot(
            SpriteRenderer[] renderers,
            int[] layerIds,
            int[] orders,
            int[] relativeOrders)
        {
            _renderers = renderers;
            _layerIds = layerIds;
            _orders = orders;
            _relativeOrders = relativeOrders;
        }

        public static PlayerSortingSnapshot Capture(PlayerBellOccupantState state)
        {
            if (state == null)
            {
                return new PlayerSortingSnapshot(
                    System.Array.Empty<SpriteRenderer>(),
                    System.Array.Empty<int>(),
                    System.Array.Empty<int>(),
                    System.Array.Empty<int>());
            }

            SpriteRenderer[] renderers = state.GetComponentsInChildren<SpriteRenderer>(true);
            int count = renderers != null ? renderers.Length : 0;

            int[] layerIds = new int[count];
            int[] orders = new int[count];
            int[] relative = new int[count];

            int minOrder = int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer != null)
                    minOrder = Mathf.Min(minOrder, renderer.sortingOrder);
            }

            if (minOrder == int.MaxValue)
                minOrder = 0;

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                layerIds[i] = renderer.sortingLayerID;
                orders[i] = renderer.sortingOrder;
                relative[i] = renderer.sortingOrder - minOrder;
            }

            return new PlayerSortingSnapshot(
                renderers,
                layerIds,
                orders,
                relative);
        }

        public void ApplyBellSorting(
            int sortingLayerId,
            int occupantBaseOrder)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                SpriteRenderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = occupantBaseOrder + _relativeOrders[i];
            }
        }

        public void Restore()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                SpriteRenderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                renderer.sortingLayerID = _layerIds[i];
                renderer.sortingOrder = _orders[i];
            }
        }
    }
}
