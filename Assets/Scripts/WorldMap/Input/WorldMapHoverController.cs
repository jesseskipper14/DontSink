using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WorldMap.Player.StarMap;

public class WorldMapHoverController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private WorldMapTooltipUI tooltip;
    [SerializeField] private WorldMapRuntimeBinder runtimeBinder;
    [SerializeField] private WorldMapRouteOverlay routeOverlay;
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapPlayer player;
    [SerializeField] private WorldMapTravelDebugController travelDebug;
    [SerializeField] private WorldMapTravelRulesConfig travelRules;
    [SerializeField] private KeyCode freezeKey = KeyCode.LeftShift;
    private bool _freezeRoutes;
    private int _frozenNodeId = -1;
    private int _lastHoveredNodeId = -1;
    private bool _wasShiftHeld;

    private bool IsFreezeHeld()
    => Input.GetKey(freezeKey) || Input.GetKey(KeyCode.RightShift);

    private static bool ShiftHeld()
    => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);


    [Header("Raycast")]
    [SerializeField] private LayerMask hoverLayers = ~0;

    [SerializeField] private WorldMapEventManager eventManager;
    private readonly System.Collections.Generic.List<WorldMapEventInstance> _tmpEvents = new();

    private MapNodeView _current;

    private void Reset()
    {
        cam = Camera.main;
        tooltip = FindAnyObjectByType<WorldMapTooltipUI>();
        if (eventManager == null) eventManager = FindAnyObjectByType<WorldMapEventManager>();
        runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        routeOverlay = FindAnyObjectByType<WorldMapRouteOverlay>();
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        player = FindAnyObjectByType<WorldMapPlayer>();
        travelDebug = FindAnyObjectByType<WorldMapTravelDebugController>();
        // travelRules is a ScriptableObject; assign via inspector (no reliable scene lookup)
    }

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (tooltip == null) tooltip = FindAnyObjectByType<WorldMapTooltipUI>();
        if (eventManager == null) eventManager = FindAnyObjectByType<WorldMapEventManager>();
        if (runtimeBinder == null) runtimeBinder = FindAnyObjectByType<WorldMapRuntimeBinder>();
        if (routeOverlay == null) routeOverlay = FindAnyObjectByType<WorldMapRouteOverlay>();
        if (generator == null) generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        if (player == null) player = FindAnyObjectByType<WorldMapPlayer>();
        if (travelDebug == null)
            travelDebug = FindAnyObjectByType<WorldMapTravelDebugController>();
        // travelRules is a ScriptableObject; assign via inspector
    }

    private void Update()
    {
        if (cam == null || tooltip == null) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;

        bool shift = ShiftHeld();

        // If shift was held and now released: clear pinned overlays
        if (_wasShiftHeld && !shift)
            routeOverlay?.ClearPinnedAnchors();
        _wasShiftHeld = shift;

        // If UI is blocking pointer, don't hover nodes through it
        if (PointerOverInteractiveUI())
        {
            // Don't clear overlays while shift is held (keep the plan visible)
            if (!shift)
            {
                _current = null;
                _lastHoveredNodeId = -1;
                tooltip.Hide();
                routeOverlay?.ClearHover();
            }
            return;
        }

        Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 p = new Vector2(w.x, w.y);

        var hit = Physics2D.Raycast(p, Vector2.zero, 0f, hoverLayers);

        // Buff icon hover shows tooltip, but shouldn't disturb route overlays while shift is held
        var buffIcon = hit.collider ? hit.collider.GetComponent<BuffIconView>() : null;
        if (buffIcon != null)
        {
            tooltip.Show(buffIcon.GetTooltipText(), Input.mousePosition);
            _current = null;

            if (!shift)
            {
                _lastHoveredNodeId = -1;
                routeOverlay?.ClearHover();
            }

            return;
        }

        var view = hit.collider ? hit.collider.GetComponent<MapNodeView>() : null;

        // Nothing hovered
        if (view == null)
        {
            _current = null;
            tooltip.Hide();

            if (!shift)
            {
                _lastHoveredNodeId = -1;
                routeOverlay?.ClearHover();
            }

            return;
        }

        // Same node hovered: just update tooltip (no pin spam)
        if (view == _current)
        {
            UpdateTooltip(view);
            return;
        }

        // Node changed
        if (shift && _lastHoveredNodeId >= 0 && _lastHoveredNodeId != view.NodeId)
        {
            // Pin the previous node so its overlays remain visible
            routeOverlay?.PinAnchor(_lastHoveredNodeId);
        }

        _lastHoveredNodeId = view.NodeId;

        _current = view;
        routeOverlay?.SetHoverNode(view.NodeId);
        UpdateTooltip(_current);
    }

    private void UpdateTooltip(MapNodeView view)
    {
        int id = view.NodeId;
        if (id < 0) return;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return;
        if (!runtimeBinder.Registry.TryGetByIndex(id, out var rt)) return;

        var state = rt.State;
        if (state == null) return;

        var routeInfo = TryBuildRouteInfoFromPlayer(id);
        string routeSection = FormatRouteSection(routeInfo);

        string text =
            $"** {rt.DisplayName} **\n" +
            $"Dock: {state.GetStat(NodeStatId.DockRating).value:0.00}   Trade: {state.GetStat(NodeStatId.TradeRating).value:0.00}\n" +
            $"Population: {state.population:0}\n" +
            $"{FormatStats(state)}" +
            $"{routeSection}\n" +
            $"\n{FormatEvents(id)}" +
            $"\n{FormatBuffs(state)}";

        tooltip.Show(text, Input.mousePosition);
    }

    private static string FormatStats(MapNodeState state)
    {
        if (state == null || state.Stats == null || state.Stats.Count == 0) return "Stats: (none)";

        string s = "Stats:\n";
        foreach (var kvp in state.Stats)
            s += $"  - {kvp.Key}: {kvp.Value.value:0.00}\n";

        return s.TrimEnd();
    }

    private static string FormatBuffs(MapNodeState state)
    {
        var buffs = state.ActiveBuffs;
        if (buffs == null || buffs.Count == 0) return "Buffs: (none)";

        int n = Mathf.Min(buffs.Count, 8);
        string s = $"Buffs ({buffs.Count}):\n";
        for (int i = 0; i < n; i++)
        {
            var b = buffs[i];
            if (b.buff == null) continue;
            s += $"  - {b.buff.displayName} ({b.RemainingHours:0.0}h)\n";
        }
        if (buffs.Count > n) s += "  - ...\n";
        return s.TrimEnd();
    }

    private void ClearHover()
    {
        _current = null;
        tooltip.Hide();

        bool freezeHeld = IsFreezeHeld();
        if (!freezeHeld)
        {
            _frozenNodeId = -1;
            routeOverlay?.ClearHover();
        }
    }

    private static bool PointerOverInteractiveUI()
    {
        if (EventSystem.current == null) return false;

        var results = new List<RaycastResult>();
        var data = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        EventSystem.current.RaycastAll(data, results);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject.GetComponent<Selectable>() != null)
                return true; // Button/Slider/etc.
        }

        return false;
    }

    private string FormatEvents(int nodeId)
    {
        if (eventManager == null) return "Events: (no manager)";

        eventManager.GetEventsAtNode(nodeId, _tmpEvents);
        if (_tmpEvents.Count == 0) return "Events: (none)";

        string s = "Events:\n";
        int n = Mathf.Min(_tmpEvents.Count, 6);
        for (int i = 0; i < n; i++)
        {
            var e = _tmpEvents[i];
            string name = e.def != null ? e.def.displayName : "(unknown)";
            s += $"  - {name} ({e.RemainingHours:0.0}h)\n";
        }
        if (_tmpEvents.Count > n) s += "  - ...\n";
        return s.TrimEnd();
    }

    private RouteHoverInfo TryBuildRouteInfoFromPlayer(int hoveredNodeIndex)
    {
        if (generator?.graph == null) return null;
        if (player?.State == null) return null;
        if (runtimeBinder == null || !runtimeBinder.IsBuilt) return null;

        if (!runtimeBinder.Registry.TryGetByStableId(player.State.currentNodeId, out var currentRt) || currentRt == null)
            return null;

        int fromIndex = currentRt.NodeIndex;
        int toIndex = hoveredNodeIndex;

        if (fromIndex < 0 || toIndex < 0) return null;
        if (fromIndex >= generator.graph.nodes.Count || toIndex >= generator.graph.nodes.Count) return null;

        // Must be a direct edge for “route info”
        if (!generator.graph.HasEdge(fromIndex, toIndex))
            return null;

        var info = new RouteHoverInfo
        {
            isDirectEdge = true
        };

        // Length (graph-space deterministic)
        Vector2 a = generator.graph.nodes[fromIndex].position;
        Vector2 b = generator.graph.nodes[toIndex].position;
        info.length = Vector2.Distance(a, b);

        if (!runtimeBinder.Registry.TryGetByIndex(fromIndex, out var fromRt) || fromRt == null) return info;
        if (!runtimeBinder.Registry.TryGetByIndex(toIndex, out var toRt) || toRt == null) return info;

        // Star Map visual state (intra-cluster treated as Known).
        var kState = StarMapVisualQuery.GetVisualState(
            player.State,
            fromRt.StableId, fromRt.ClusterId,
            toRt.StableId, toRt.ClusterId);

        // Store a note line for display.
        info.notes.Add($"Star Map: {kState}");

        // Max length from config, else debug fallback.
        float maxLen = travelRules != null
            ? travelRules.maxRouteLength
            : (travelDebug != null ? travelDebug.MaxRouteLength : float.PositiveInfinity);

        // Blocker 1: distance
        if (info.length > maxLen)
            info.blockers.Add($"Route too long (max {maxLen:0.00}, got {info.length:0.00})");

        // Blocker 2: star map gate (cross-cluster only, because intra-cluster is known-by-default)
        // Here we treat ONLY Known as travelable knowledge for phase 1.
        if (fromRt.ClusterId != toRt.ClusterId && kState != RouteKnowledgeState.Known)
            info.blockers.Add("Route not fully known (requires Star Map: Known)");

        return info;
    }

    private static string FormatRouteSection(RouteHoverInfo info)
    {
        if (info == null || !info.isDirectEdge)
            return "";

        string s =
            "\n\nRoute (from current):\n" +
            $"  - Length: {info.length:0.00}\n" +
            $"  - Status: {(info.blockers != null && info.blockers.Count > 0 ? "BLOCKED" : "Available")}\n";

        if (info.notes != null && info.notes.Count > 0)
        {
            s += "Notes:\n";
            for (int i = 0; i < info.notes.Count; i++)
                s += $"  - {info.notes[i]}\n";
        }

        if (info.blockers != null && info.blockers.Count > 0)
        {
            s += "Blockers:\n";
            for (int i = 0; i < info.blockers.Count; i++)
                s += $"  - {info.blockers[i]}\n";
        }

        if (info.risks != null && info.risks.Count > 0)
        {
            s += "Risks:\n";
            for (int i = 0; i < info.risks.Count; i++)
                s += $"  - {info.risks[i]}\n";
        }

        return s.TrimEnd();
    }
}
