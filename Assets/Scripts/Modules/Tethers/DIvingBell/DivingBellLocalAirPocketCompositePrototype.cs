using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PASS 12E PROTOTYPE ONLY.
///
/// Tests the desired "local dry air pocket" presentation WITHOUT modifying the
/// ocean shader, SpriteMask/stencil state, WaterMeshRenderer, or the existing
/// DivingBellOceanPresentationBridge implementation.
///
/// While the local viewer occupies this submerged bell, this component:
/// 1) temporarily disables the legacy global ocean presentation bridge so the
///    normal FrontWater ocean remains visible everywhere else;
/// 2) composites this bell's renderers above the FrontWater renderer;
/// 3) leaves DivingBellInternalWaterRenderer above the bell content, so only the
///    bell-local flooded region re-applies the underwater tint.
///
/// Visually this is equivalent to locally clearing FrontWater from the bell and
/// then drawing the bell's own water back over the flooded portion. It is meant
/// to prove the presentation model before any shader/mask infrastructure is
/// replaced.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(DivingBellOccupancy))]
public sealed class DivingBellLocalAirPocketCompositePrototype : MonoBehaviour
{
    [Header("Bell")]
    [SerializeField] private DivingBellOccupancy occupancy;
    [SerializeField] private DivingBellAirVolume airVolume;

    [Header("Local Viewer")]
    [Tooltip(
        "Optional explicit locally-controlled player. In single-player this may remain blank. " +
        "Future multiplayer bootstrap should assign the local player explicitly.")]
    [SerializeField] private PlayerBoardingState localViewingPlayer;

    [Header("Prototype Composition")]
    [Tooltip(
        "Sorting layer containing Ocean_Front. Bell content is temporarily moved to this layer " +
        "at a higher order so FrontWater remains visible everywhere except through the bell composition.")]
    [SerializeField] private string compositeSortingLayerName = "FrontWater";

    [Tooltip(
        "Ocean_Front currently renders at order 0. Use a modest positive base so the bell renders after it " +
        "while preserving all relative bell/player/water ordering above this base.")]
    [SerializeField] private int compositeBaseOrder = 20;

    [Tooltip(
        "Only test the composite while the bell opening is actually submerged. " +
        "This keeps ordinary above-water/docked presentation completely untouched.")]
    [SerializeField] private bool onlyWhileOpeningSubmerged = true;

    [Tooltip(
        "Temporarily disable DivingBellOceanPresentationBridge while this prototype is active. " +
        "The bridge itself is not modified or removed and is restored automatically afterward.")]
    [SerializeField] private bool suppressLegacyOceanBridgeWhileActive = true;

    [Tooltip(
        "Also include loose DivingBellContainedItem renderers even if they are not transform-parented under the bell.")]
    [SerializeField] private bool includeContainedBellItems = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool prototypeActive;
    [SerializeField] private int compositedRendererCount;
    [SerializeField] private bool legacyBridgeSuppressed;
    [SerializeField] private bool verboseLogging = false;

    private DivingBellOceanPresentationBridge _legacyBridge;
    private bool _legacyBridgeWasEnabled;
    private bool _legacyBridgeStateCaptured;

    private readonly Dictionary<Renderer, RendererSortingSnapshot> _snapshots =
        new Dictionary<Renderer, RendererSortingSnapshot>();

    private readonly List<Renderer> _scratch =
        new List<Renderer>(64);

    private void Awake()
    {
        ResolveRefs();
    }

    private void OnEnable()
    {
        ResolveRefs();
        RefreshPrototype();
    }

    private void LateUpdate()
    {
        // Very intentionally runs late. DivingBellVisualPresentation,
        // DivingBellContainedItem, and DivingBellInternalWaterRenderer remain the
        // authorities for RELATIVE ordering; this prototype only remaps the final
        // composite above FrontWater after they finish their normal work.
        RefreshPrototype();
    }

    private void OnDisable()
    {
        DeactivatePrototype();
    }

    private void OnDestroy()
    {
        DeactivatePrototype();
    }

    public void SetLocalViewingPlayer(PlayerBoardingState player)
    {
        if (ReferenceEquals(localViewingPlayer, player))
            return;

        DeactivatePrototype();
        localViewingPlayer = player;
        RefreshPrototype();
    }

    [ContextMenu("Refresh Prototype Now")]
    public void RefreshPrototype()
    {
        ResolveRefs();

        bool shouldBeActive =
            IsLocalViewerInsideThisBell() &&
            (!onlyWhileOpeningSubmerged ||
             (airVolume != null && airVolume.OpeningSubmerged));

        if (!shouldBeActive)
        {
            DeactivatePrototype();
            return;
        }

        ActivatePrototype();
        ApplyCompositeSorting();
    }

    [ContextMenu("Restore Normal Presentation")]
    public void RestoreNormalPresentation()
    {
        DeactivatePrototype();
    }

    private void ActivatePrototype()
    {
        if (!prototypeActive)
        {
            prototypeActive = true;
            CaptureAndSuppressLegacyBridgeIfNeeded();
            Log("Prototype activated.");
        }
        else if (suppressLegacyOceanBridgeWhileActive)
        {
            // Defensive reassertion in case another setup script re-enabled it.
            SuppressLegacyBridgeIfNeeded();
        }
    }

    private void DeactivatePrototype()
    {
        if (_snapshots.Count > 0)
            RestoreRendererSorting();

        RestoreLegacyBridgeIfNeeded();

        if (prototypeActive)
            Log("Prototype deactivated; normal presentation restored.");

        prototypeActive = false;
        compositedRendererCount = 0;
    }

    private void ApplyCompositeSorting()
    {
        _scratch.Clear();
        CollectBellHierarchyRenderers(_scratch);

        if (includeContainedBellItems)
            CollectContainedItemRenderers(_scratch);

        if (_scratch.Count == 0)
        {
            compositedRendererCount = 0;
            return;
        }

        int targetLayerId =
            SortingLayer.NameToID(compositeSortingLayerName);

        if (targetLayerId == 0 &&
            compositeSortingLayerName != "Default")
        {
            LogWarning(
                $"Sorting layer '{compositeSortingLayerName}' was not found. " +
                "Prototype cannot composite above Ocean_Front.");

            return;
        }

        // Preserve the relative order authored by the normal bell presentation.
        // We intentionally calculate this from the CURRENT frame, after the normal
        // presentation scripts have already reapplied their own order values.
        int minOrder = int.MaxValue;

        for (int i = 0; i < _scratch.Count; i++)
        {
            Renderer renderer = _scratch[i];
            if (!IsUsableRenderer(renderer))
                continue;

            if (!_snapshots.ContainsKey(renderer))
            {
                _snapshots.Add(
                    renderer,
                    RendererSortingSnapshot.Capture(renderer));
            }

            minOrder =
                Mathf.Min(
                    minOrder,
                    renderer.sortingOrder);
        }

        if (minOrder == int.MaxValue)
        {
            compositedRendererCount = 0;
            return;
        }

        int applied = 0;

        for (int i = 0; i < _scratch.Count; i++)
        {
            Renderer renderer = _scratch[i];
            if (!IsUsableRenderer(renderer))
                continue;

            int relativeOrder =
                renderer.sortingOrder -
                minOrder;

            renderer.sortingLayerID =
                targetLayerId;

            renderer.sortingOrder =
                compositeBaseOrder +
                relativeOrder;

            applied++;
        }

        compositedRendererCount = applied;
    }

    private void CollectBellHierarchyRenderers(List<Renderer> result)
    {
        if (occupancy == null || result == null)
            return;

        Renderer[] renderers =
            occupancy.GetComponentsInChildren<Renderer>(true);

        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
            AddUnique(result, renderers[i]);
    }

    private void CollectContainedItemRenderers(List<Renderer> result)
    {
        if (occupancy == null || result == null)
            return;

        DivingBellContainedItem[] items =
            FindObjectsByType<DivingBellContainedItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        if (items == null)
            return;

        for (int i = 0; i < items.Length; i++)
        {
            DivingBellContainedItem item = items[i];

            if (item == null ||
                !item.IsContainedInBell ||
                item.CurrentBell != occupancy)
            {
                continue;
            }

            Renderer[] renderers =
                item.GetComponentsInChildren<Renderer>(true);

            if (renderers == null)
                continue;

            for (int r = 0; r < renderers.Length; r++)
                AddUnique(result, renderers[r]);
        }
    }

    private static void AddUnique(
        List<Renderer> result,
        Renderer renderer)
    {
        if (result == null || renderer == null)
            return;

        if (!result.Contains(renderer))
            result.Add(renderer);
    }

    private static bool IsUsableRenderer(Renderer renderer)
    {
        return
            renderer != null &&
            renderer.gameObject.activeInHierarchy;
    }

    private void RestoreRendererSorting()
    {
        foreach (KeyValuePair<Renderer, RendererSortingSnapshot> pair in _snapshots)
        {
            Renderer renderer = pair.Key;
            if (renderer == null)
                continue;

            pair.Value.Restore(renderer);
        }

        _snapshots.Clear();
    }

    private bool IsLocalViewerInsideThisBell()
    {
        if (occupancy == null ||
            localViewingPlayer == null)
        {
            return false;
        }

        PlayerBellOccupantState state =
            localViewingPlayer.GetComponent<PlayerBellOccupantState>() ??
            localViewingPlayer.GetComponentInChildren<PlayerBellOccupantState>(true);

        return
            state != null &&
            state.IsInside(occupancy);
    }

    private void CaptureAndSuppressLegacyBridgeIfNeeded()
    {
        if (!suppressLegacyOceanBridgeWhileActive)
            return;

        ResolveLegacyBridge();

        if (_legacyBridge == null)
            return;

        if (!_legacyBridgeStateCaptured)
        {
            _legacyBridgeWasEnabled =
                _legacyBridge.enabled;

            _legacyBridgeStateCaptured =
                true;
        }

        SuppressLegacyBridgeIfNeeded();
    }

    private void SuppressLegacyBridgeIfNeeded()
    {
        if (!suppressLegacyOceanBridgeWhileActive)
            return;

        ResolveLegacyBridge();

        if (_legacyBridge == null)
            return;

        if (_legacyBridge.enabled)
            _legacyBridge.enabled = false;

        legacyBridgeSuppressed =
            !_legacyBridge.enabled;
    }

    private void RestoreLegacyBridgeIfNeeded()
    {
        if (!_legacyBridgeStateCaptured)
        {
            legacyBridgeSuppressed = false;
            return;
        }

        ResolveLegacyBridge();

        if (_legacyBridge != null)
        {
            _legacyBridge.enabled =
                _legacyBridgeWasEnabled;
        }

        _legacyBridgeStateCaptured = false;
        legacyBridgeSuppressed = false;
    }

    private void ResolveLegacyBridge()
    {
        if (_legacyBridge != null)
            return;

        _legacyBridge =
            GetComponent<DivingBellOceanPresentationBridge>() ??
            GetComponentInParent<DivingBellOceanPresentationBridge>() ??
            GetComponentInChildren<DivingBellOceanPresentationBridge>(true);
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

        if (airVolume == null)
        {
            airVolume =
                GetComponent<DivingBellAirVolume>() ??
                GetComponentInParent<DivingBellAirVolume>() ??
                GetComponentInChildren<DivingBellAirVolume>(true);
        }

        if (localViewingPlayer == null)
        {
            // Single-player fallback only. Future multiplayer bootstrap should set
            // the locally-controlled player explicitly.
            localViewingPlayer =
                FindFirstObjectByType<PlayerBoardingState>();
        }

        ResolveLegacyBridge();
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[DivingBellLocalAirPocketCompositePrototype:{name}] {message}",
            this);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[DivingBellLocalAirPocketCompositePrototype:{name}] {message}",
            this);
    }

    private readonly struct RendererSortingSnapshot
    {
        private readonly int _sortingLayerId;
        private readonly int _sortingOrder;

        private RendererSortingSnapshot(
            int sortingLayerId,
            int sortingOrder)
        {
            _sortingLayerId = sortingLayerId;
            _sortingOrder = sortingOrder;
        }

        public static RendererSortingSnapshot Capture(Renderer renderer)
        {
            return new RendererSortingSnapshot(
                renderer != null
                    ? renderer.sortingLayerID
                    : 0,
                renderer != null
                    ? renderer.sortingOrder
                    : 0);
        }

        public void Restore(Renderer renderer)
        {
            if (renderer == null)
                return;

            renderer.sortingLayerID =
                _sortingLayerId;

            renderer.sortingOrder =
                _sortingOrder;
        }
    }
}
