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

    private readonly Dictionary<PlayerBoardingState, List<BoatVisibilityZone>> _activeZonesByPlayer = new();

    private Renderer[] _exteriorRenderers;
    private Renderer[] _interiorRenderers;
    private Renderer[] _deckRenderers;
    private Renderer[] _hullRenderers;
    private Renderer[] _alwaysRenderers;

    private BoatVisibilityMode _currentMode;

    [Header("Debug")]
    [SerializeField] private bool logVisibility = false;

    private void Awake()
    {
        AutoAssignRootsIfMissing();
        CacheRenderers();
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
            ApplyMode(unboardedMode);
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
            ApplyMode(unboardedMode);
            return;
        }

        if (!IsPlayerOnThisBoat(player))
        {
            _activeZonesByPlayer.Remove(player);
            ApplyMode(unboardedMode);
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
            ApplyMode(unboardedMode);
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

        if (bestZone != null)
        {
            ApplyMode(bestZone.Mode);
            return;
        }

        ApplyMode(defaultBoardedMode);
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

        // After visibility group toggles, restore stateful hatch sprite presentation.
        RefreshHatchesInRoot(exteriorRoot, exteriorVisible);
        RefreshHatchesInRoot(exteriorDeckRoot, deckVisible);
        RefreshHatchesInRoot(interiorRoot, interiorVisible);
        RefreshHatchesInRoot(hullRoot, hullVisible);
        RefreshHatchesInRoot(alwaysVisibleRoot, alwaysVisible);

        LogRendererCounts();
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
            $"Always={(_alwaysRenderers != null ? _alwaysRenderers.Length : -1)}",
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
    }

    [ContextMenu("Cache Renderers")]
    public void CacheRenderers()
    {
        _exteriorRenderers = GetRenderers(exteriorRoot);
        _interiorRenderers = GetRenderers(interiorRoot);
        _deckRenderers = GetRenderers(exteriorDeckRoot);
        _hullRenderers = GetRenderers(hullRoot);
        _alwaysRenderers = GetRenderers(alwaysVisibleRoot);
    }

    [ContextMenu("Preview Unboarded Exterior")]
    private void PreviewUnboardedExterior()
    {
        AutoAssignRootsIfMissing();
        CacheRenderers();
        ApplyMode(BoatVisibilityMode.UnboardedExterior);
    }

    [ContextMenu("Preview Boarded Exterior Deck")]
    private void PreviewBoardedExteriorDeck()
    {
        AutoAssignRootsIfMissing();
        CacheRenderers();
        ApplyMode(BoatVisibilityMode.BoardedExteriorDeck);
    }

    [ContextMenu("Preview Boarded Interior")]
    private void PreviewBoardedInterior()
    {
        AutoAssignRootsIfMissing();
        CacheRenderers();
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

            // If showing the group, do NOT blindly enable renderers controlled by HatchRuntime.
            // HatchRuntime will decide open vs closed sprite state.
            HatchRuntime hatch = r.GetComponentInParent<HatchRuntime>();
            if (hatch != null)
                continue;

            r.enabled = true;
        }
    }

    private static void RefreshHatchesInRoot(Transform root, bool rootVisible)
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
    }
}