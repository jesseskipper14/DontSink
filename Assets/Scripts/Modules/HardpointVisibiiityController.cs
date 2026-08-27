using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boat-level presentation owner for contextual Hardpoint placement markers.
///
/// The marker is not physical boat art. In normal gameplay it appears only when
/// the player is holding/selecting a module. Marker visibility intentionally
/// ignores boarding/interior/exterior access state so the player can inspect the
/// boat's available mounting options from anywhere nearby/in the scene.
///
/// Future boat editors / shop previews can call SetRevealAll(true) without
/// learning how individual Hardpoint SpriteRenderers are authored.
/// </summary>
[DisallowMultipleComponent]
public sealed class HardpointVisibilityController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerEquipment playerEquipment;

    [Header("Presentation")]
    [Tooltip("Explicit override for boat-editor/shop/debug contexts. Shows every hardpoint marker, including occupied hardpoints, even with no module selected.")]
    [SerializeField] private bool revealAll = false;

    [Tooltip("Color used when the selected/held module can mount on an empty hardpoint.")]
    [SerializeField] private Color compatibleColor = Color.green;

    [Tooltip("Color used when the selected/held module cannot mount on an empty hardpoint.")]
    [SerializeField] private Color incompatibleColor = Color.red;

    [Header("Marker Draw Priority")]
    [Tooltip("While a marker is contextually visible, force it to this very high sorting order so boat cargo/interior junk does not hide it.")]
    [SerializeField] private int activeSortingOrder = 32000;

    [Tooltip("Optional: also move visible markers onto a specific Sorting Layer. Leave off if sorting order alone is sufficient.")]
    [SerializeField] private bool overrideActiveSortingLayer = false;

    [Tooltip("Existing Unity Sorting Layer name used only while the marker is visible. Ignored when Override Active Sorting Layer is off.")]
    [SerializeField] private string activeSortingLayerName = "";

    [Header("Marker Pulse")]
    [Tooltip("Pulse the alpha of visible hardpoint markers to draw the eye without scaling the Hardpoint transform.")]
    [SerializeField] private bool pulseVisibleMarkers = true;

    [SerializeField, Min(0f)] private float pulseCyclesPerSecond = 1.5f;

    [Tooltip("Minimum alpha multiplier during the pulse.")]
    [SerializeField, Range(0f, 1f)] private float pulseMinAlpha = 0.55f;

    [Tooltip("Maximum alpha multiplier during the pulse.")]
    [SerializeField, Range(0f, 1f)] private float pulseMaxAlpha = 1f;

    [Header("Debug")]
    [SerializeField] private bool logRefresh = false;

    private Hardpoint[] _hardpoints;

    private readonly Dictionary<Hardpoint, Color>
        _defaultMarkerColors = new();

    private readonly Dictionary<Hardpoint, int>
        _defaultMarkerSortingLayerIds = new();

    private readonly Dictionary<Hardpoint, int>
        _defaultMarkerSortingOrders = new();

    private readonly Dictionary<Hardpoint, Color>
        _activeMarkerBaseColors = new();

    private PlayerInventory _boundInventory;
    private PlayerEquipment _boundEquipment;

    private ModuleDefinition _previewModuleOverride;

    private float _nextPlayerResolveTime;

    private bool _lastRevealAll;
    private bool _hasRevealAllSample;

    public bool RevealAll => revealAll;

    private void Reset()
    {
        ResolveBoat();
        CacheHardpoints();
        ResolvePlayerContext();
    }

    private void Awake()
    {
        ResolveBoat();
        CacheHardpoints();
        ResolvePlayerContext();
    }

    private void OnEnable()
    {
        ResolveBoat();
        CacheHardpoints();
        ResolvePlayerContext();
        BindPlayerEvents();
        _lastRevealAll = revealAll;
        _hasRevealAllSample = true;

        Refresh();
    }

    private void OnDisable()
    {
        UnbindPlayerEvents();

        // A disabled presentation controller should fail open rather than leave
        // authored markers mysteriously invisible.
        ShowAllNeutral();
    }

    private void Update()
    {
        // Inspector edits bypass SetRevealAll(...). Detect the serialized toggle
        // itself so Play Mode behaves exactly like callers using the public API.
        if (!_hasRevealAllSample ||
            revealAll != _lastRevealAll)
        {
            _lastRevealAll = revealAll;
            _hasRevealAllSample = true;
            Refresh();
        }

        if (playerInventory == null)
        {
            if (Time.unscaledTime >=
                _nextPlayerResolveTime)
            {
                _nextPlayerResolveTime =
                    Time.unscaledTime + 0.5f;

                ResolvePlayerContext();
                BindPlayerEvents();
                Refresh();
            }

            UpdateVisibleMarkerPulse();
            return;
        }

        // Inventory/equipment events drive contextual refreshes. Hardpoint marker
        // visibility deliberately ignores boarding/interior/exterior access state:
        // it answers "where could this module go on my boat?", not "can I reach it
        // from this exact physical position right now?"
        UpdateVisibleMarkerPulse();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        activeSortingOrder =
            Mathf.Clamp(
                activeSortingOrder,
                -32768,
                32767);

        pulseCyclesPerSecond =
            Mathf.Max(
                0f,
                pulseCyclesPerSecond);

        pulseMinAlpha =
            Mathf.Clamp01(
                pulseMinAlpha);

        pulseMaxAlpha =
            Mathf.Clamp(
                pulseMaxAlpha,
                pulseMinAlpha,
                1f);

        if (Application.isPlaying)
            return;

        ResolveBoat();
        CacheHardpoints();

        // Hardpoint markers are authoring aids too. Never make scene editing
        // depend on whatever runtime visibility state happened to be serialized.
        ShowAllNeutral();
    }
#endif

    /// <summary>
    /// Future boat editor / shop / debug screens can flip this once on entry and
    /// once on exit. They do not need to manipulate individual renderers.
    /// </summary>
    public void SetRevealAll(
        bool value)
    {
        if (revealAll == value)
            return;

        revealAll = value;
        _lastRevealAll = revealAll;
        _hasRevealAllSample = true;
        Refresh();
    }

    /// <summary>
    /// Optional future shop/build-screen preview. While supplied, compatibility
    /// coloring uses this module instead of the player's selected/held item.
    /// </summary>
    public void SetPreviewModule(
        ModuleDefinition module)
    {
        _previewModuleOverride = module;
        Refresh();
    }

    public void ClearPreviewModule()
    {
        if (_previewModuleOverride == null)
            return;

        _previewModuleOverride = null;
        Refresh();
    }

    [ContextMenu("Refresh Hardpoint Visibility")]
    public void Refresh()
    {
        if (_hardpoints == null ||
            _hardpoints.Length == 0)
        {
            CacheHardpoints();
        }

        ModuleDefinition preview =
            _previewModuleOverride != null
                ? _previewModuleOverride
                : ResolveSelectedOrHeldModule();

        int shown = 0;

        if (_hardpoints != null)
        {
            for (int i = 0;
                 i < _hardpoints.Length;
                 i++)
            {
                Hardpoint hardpoint =
                    _hardpoints[i];

                if (hardpoint == null)
                    continue;

                SpriteRenderer marker =
                    hardpoint.PlacementMarkerRenderer;

                if (marker == null)
                    continue;

                Color defaultColor =
                    GetDefaultColor(
                        hardpoint,
                        marker);

                if (revealAll)
                {
                    // Occupied hardpoints remain neutral. Green/red means
                    // compatibility, not occupancy.
                    Color revealColor =
                        preview != null &&
                        !hardpoint.HasInstalledModule
                            ? hardpoint.ModuleMatchesAcceptedTypes(
                                preview)
                                ? compatibleColor
                                : incompatibleColor
                            : defaultColor;

                    ShowMarker(
                        hardpoint,
                        marker,
                        revealColor);

                    shown++;
                    continue;
                }

                // Normal gameplay should stay visually clean.
                if (preview == null ||
                    hardpoint.HasInstalledModule)
                {
                    HideMarker(
                        hardpoint,
                        marker,
                        defaultColor);

                    continue;
                }

                Color contextColor =
                    hardpoint.ModuleMatchesAcceptedTypes(
                        preview)
                        ? compatibleColor
                        : incompatibleColor;

                ShowMarker(
                    hardpoint,
                    marker,
                    contextColor);

                shown++;
            }
        }

        if (logRefresh)
        {
            Debug.Log(
                $"[HardpointVisibility] boat={(boat != null ? boat.name : name)} " +
                $"revealAll={revealAll} " +
                $"preview={(preview != null ? preview.DisplayName : "NONE")} " +
                $"shown={shown}/{(_hardpoints != null ? _hardpoints.Length : 0)}",
                this);
        }
    }

    private ModuleDefinition ResolveSelectedOrHeldModule()
    {
        if (playerInventory == null)
            return null;

        ItemInstance selected =
            ResolveSelectedItem();

        ModuleDefinition selectedModule =
            GetModuleDefinition(
                selected);

        if (selectedModule != null)
            return selectedModule;

        // Hands are literal "currently held" context. This fallback means a pump
        // in the player's hands still reveals placement markers even if the
        // bottom-bar cursor is temporarily sitting elsewhere.
        ItemInstance hands =
            playerEquipment != null
                ? playerEquipment.Get(
                    BottomBarSlotType.Hands)
                : null;

        return GetModuleDefinition(
            hands);
    }

    private ItemInstance ResolveSelectedItem()
    {
        BottomBarSlotType selected =
            playerInventory.SelectedSlot;

        if (selected >=
                BottomBarSlotType.Hotbar0 &&
            selected <=
                BottomBarSlotType.Hotbar7)
        {
            int index =
                PlayerInventory
                    .SlotTypeToHotbarIndex(
                        selected);

            InventorySlot slot =
                playerInventory.GetSlot(
                    index);

            return slot != null
                ? slot.Instance
                : null;
        }

        return playerEquipment != null
            ? playerEquipment.Get(selected)
            : null;
    }

    private static ModuleDefinition GetModuleDefinition(
        ItemInstance item)
    {
        if (item == null ||
            item.Definition == null ||
            !item.Definition.IsModule)
        {
            return null;
        }

        return item.Definition.ModuleDefinition;
    }

    private void CacheHardpoints()
    {
        ResolveBoat();

        Transform root =
            boat != null
                ? boat.transform
                : transform;

        _hardpoints =
            root.GetComponentsInChildren<Hardpoint>(
                true);

        if (_hardpoints == null)
            return;

        for (int i = 0;
             i < _hardpoints.Length;
             i++)
        {
            Hardpoint hardpoint =
                _hardpoints[i];

            if (hardpoint == null)
                continue;

            SpriteRenderer marker =
                hardpoint.PlacementMarkerRenderer;

            if (marker != null &&
                !_defaultMarkerColors.ContainsKey(
                    hardpoint))
            {
                _defaultMarkerColors.Add(
                    hardpoint,
                    marker.color);

                _defaultMarkerSortingLayerIds.Add(
                    hardpoint,
                    marker.sortingLayerID);

                _defaultMarkerSortingOrders.Add(
                    hardpoint,
                    marker.sortingOrder);
            }

            hardpoint.StateChanged -=
                HandleHardpointStateChanged;

            hardpoint.StateChanged +=
                HandleHardpointStateChanged;
        }
    }

    private void HandleHardpointStateChanged(
        Hardpoint hardpoint)
    {
        Refresh();
    }

    private void ResolveBoat()
    {
        if (boat != null)
            return;

        boat =
            GetComponent<Boat>() ??
            GetComponentInParent<Boat>();
    }

    private void ResolvePlayerContext()
    {
        if (playerInventory == null)
        {
            playerInventory =
                FindFirstObjectByType<PlayerInventory>();
        }

        if (playerInventory == null)
            return;

        if (playerEquipment == null)
        {
            playerEquipment =
                playerInventory.Equipment ??
                playerInventory
                    .GetComponentInParent<PlayerEquipment>(
                        true);
        }

    }

    private void BindPlayerEvents()
    {
        if (_boundInventory !=
            playerInventory)
        {
            if (_boundInventory != null)
            {
                _boundInventory.SelectionChanged -=
                    HandlePlayerContextChanged;

                _boundInventory.InventoryChanged -=
                    HandlePlayerContextChanged;
            }

            _boundInventory =
                playerInventory;

            if (_boundInventory != null)
            {
                _boundInventory.SelectionChanged +=
                    HandlePlayerContextChanged;

                _boundInventory.InventoryChanged +=
                    HandlePlayerContextChanged;
            }
        }

        if (_boundEquipment !=
            playerEquipment)
        {
            if (_boundEquipment != null)
            {
                _boundEquipment.EquipmentChanged -=
                    HandlePlayerContextChanged;
            }

            _boundEquipment =
                playerEquipment;

            if (_boundEquipment != null)
            {
                _boundEquipment.EquipmentChanged +=
                    HandlePlayerContextChanged;
            }
        }
    }

    private void UnbindPlayerEvents()
    {
        if (_boundInventory != null)
        {
            _boundInventory.SelectionChanged -=
                HandlePlayerContextChanged;

            _boundInventory.InventoryChanged -=
                HandlePlayerContextChanged;
        }

        if (_boundEquipment != null)
        {
            _boundEquipment.EquipmentChanged -=
                HandlePlayerContextChanged;
        }

        _boundInventory = null;
        _boundEquipment = null;

        if (_hardpoints != null)
        {
            for (int i = 0;
                 i < _hardpoints.Length;
                 i++)
            {
                Hardpoint hardpoint =
                    _hardpoints[i];

                if (hardpoint != null)
                {
                    hardpoint.StateChanged -=
                        HandleHardpointStateChanged;
                }
            }
        }
    }

    private void HandlePlayerContextChanged()
    {
        Refresh();
    }

    private void ShowMarker(
        Hardpoint hardpoint,
        SpriteRenderer marker,
        Color baseColor)
    {
        if (hardpoint == null ||
            marker == null)
        {
            return;
        }

        _activeMarkerBaseColors[hardpoint] =
            baseColor;

        ApplyActiveSorting(
            marker);

        marker.enabled = true;
        marker.color = baseColor;
    }

    private void HideMarker(
        Hardpoint hardpoint,
        SpriteRenderer marker,
        Color defaultColor)
    {
        if (hardpoint == null ||
            marker == null)
        {
            return;
        }

        _activeMarkerBaseColors.Remove(
            hardpoint);

        marker.color =
            defaultColor;

        RestoreDefaultSorting(
            hardpoint,
            marker);

        marker.enabled = false;
    }

    private void ApplyActiveSorting(
        SpriteRenderer marker)
    {
        if (marker == null)
            return;

        if (overrideActiveSortingLayer &&
            !string.IsNullOrWhiteSpace(
                activeSortingLayerName))
        {
            int layerId =
                SortingLayer.NameToID(
                    activeSortingLayerName);

            if (SortingLayer.IsValid(
                    layerId))
            {
                marker.sortingLayerID =
                    layerId;
            }
        }

        marker.sortingOrder =
            Mathf.Clamp(
                activeSortingOrder,
                -32768,
                32767);
    }

    private void RestoreDefaultSorting(
        Hardpoint hardpoint,
        SpriteRenderer marker)
    {
        if (hardpoint == null ||
            marker == null)
        {
            return;
        }

        if (_defaultMarkerSortingLayerIds.TryGetValue(
                hardpoint,
                out int sortingLayerId))
        {
            marker.sortingLayerID =
                sortingLayerId;
        }

        if (_defaultMarkerSortingOrders.TryGetValue(
                hardpoint,
                out int sortingOrder))
        {
            marker.sortingOrder =
                sortingOrder;
        }
    }

    private void UpdateVisibleMarkerPulse()
    {
        if (_hardpoints == null ||
            _hardpoints.Length == 0)
        {
            return;
        }

        float pulse01 =
            pulseVisibleMarkers &&
            pulseCyclesPerSecond > 0.0001f
                ? 0.5f +
                  0.5f *
                  Mathf.Sin(
                      Time.unscaledTime *
                      pulseCyclesPerSecond *
                      Mathf.PI *
                      2f)
                : 1f;

        float alphaMultiplier =
            pulseVisibleMarkers
                ? Mathf.Lerp(
                    pulseMinAlpha,
                    pulseMaxAlpha,
                    pulse01)
                : 1f;

        for (int i = 0;
             i < _hardpoints.Length;
             i++)
        {
            Hardpoint hardpoint =
                _hardpoints[i];

            if (hardpoint == null)
                continue;

            SpriteRenderer marker =
                hardpoint.PlacementMarkerRenderer;

            if (marker == null ||
                !marker.enabled)
            {
                continue;
            }

            if (!_activeMarkerBaseColors.TryGetValue(
                    hardpoint,
                    out Color baseColor))
            {
                baseColor =
                    GetDefaultColor(
                        hardpoint,
                        marker);

                _activeMarkerBaseColors[hardpoint] =
                    baseColor;
            }

            Color pulsed =
                baseColor;

            pulsed.a =
                baseColor.a *
                alphaMultiplier;

            marker.color =
                pulsed;

            // Reassert priority in case another visual system touched the
            // renderer since the last contextual refresh.
            ApplyActiveSorting(
                marker);
        }
    }

    private Color GetDefaultColor(
        Hardpoint hardpoint,
        SpriteRenderer marker)
    {
        if (hardpoint != null &&
            _defaultMarkerColors.TryGetValue(
                hardpoint,
                out Color color))
        {
            return color;
        }

        Color fallback =
            marker != null
                ? marker.color
                : Color.white;

        if (hardpoint != null &&
            !_defaultMarkerColors.ContainsKey(
                hardpoint))
        {
            _defaultMarkerColors.Add(
                hardpoint,
                fallback);
        }

        return fallback;
    }

    private void ShowAllNeutral()
    {
        if (_hardpoints == null ||
            _hardpoints.Length == 0)
        {
            CacheHardpoints();
        }

        if (_hardpoints == null)
            return;

        for (int i = 0;
             i < _hardpoints.Length;
             i++)
        {
            Hardpoint hardpoint =
                _hardpoints[i];

            if (hardpoint == null)
                continue;

            SpriteRenderer marker =
                hardpoint.PlacementMarkerRenderer;

            if (marker == null)
                continue;

            _activeMarkerBaseColors.Remove(
                hardpoint);

            marker.enabled = true;
            marker.color =
                GetDefaultColor(
                    hardpoint,
                    marker);

            RestoreDefaultSorting(
                hardpoint,
                marker);
        }
    }
}