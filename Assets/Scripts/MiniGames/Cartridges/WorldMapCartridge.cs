using System;
using System.Collections.Generic;
using MiniGames;
using UnityEngine;
using WorldMap.Player.StarMap;

public sealed class WorldMapCartridge : IMiniGameCartridge, IOverlayRenderable
{
    #region Dependencies and State
    private readonly WorldMapGraphGenerator _generator;
    private readonly WorldMapRuntimeBinder _runtimeBinder;
    private readonly WorldMapPlayerRef _player;
    private readonly WorldMapTravelRulesConfig _travelRules;
    private readonly WorldMapTravelDebugController _travelDebug;
    private NodeTravelController _travelLauncher;
    private readonly WorldMapEffectCatalog _effectCatalog;

    private MiniGameContext _ctx;
    private bool _requestedClose;

    private readonly WorldMapEventManager _eventManager;
    private readonly List<WorldMapEventInstance> _tmpEvents = new();

    private readonly WorldMapTopographyDebugSource _topographyDebugSource;
    private bool _showTopographyDebug = true;
    private bool _showTopographyContours = true;
    private bool _showTopographyClassification;
    private bool _showTopographyBiomes;

    private WorldMapPOISource _poiSource;
    private WorldMapKnowledgeSource _knowledgeSource;

    private bool _showNodes = true;
    private bool _showPOIs = true;
    private bool _showSurfaceShroud = true;
    private bool _showUnderwaterSurveyShroud = true;
    private bool _hideUnsurveyedPOIs = true;

    private bool _heatmapPickerOpen;

    private bool _showInjectionTools;
    private bool _injectionToolsRectInitialized;
    private Rect _injectionToolsRect;

    private const int InjectionToolsWindowId = 918273;

    private bool _eventPickerOpen;
    private bool _outcomePickerOpen;
    private bool _buffPickerOpen;

    private int _selectedEventIndex;
    private int _selectedOutcomeIndex;
    private int _selectedBuffIndex;

    private float _debugBuffDurationHours = 12f;
    private int _debugBuffStacks = 1;

    private int _debugInjectionSeedCounter;

    private int _selectedNodeIndex = -1;
    private int _lockedNodeIndex = -1;
    private string _lockedStableId;

    private Vector2 _viewCenterGraph;
    private float _zoomPxPerGraphUnit = 32f;
    private bool _hasFitView;

    private bool _dragging;
    private Vector2 _lastMousePosition;
    private Vector2 _dragStartMousePosition;

    private string _statusLine;

    private static Texture2D _whiteTex;

    private const float MinZoom = 1f;
    private const float MaxZoom = 240f;
    private const float NodeRadiusPx = 7f;
    private const float NodeHitRadiusPx = 12f;

    #endregion

    #region Construction

    public WorldMapCartridge(
        WorldMapGraphGenerator generator,
        WorldMapRuntimeBinder runtimeBinder,
        WorldMapPlayerRef player,
        WorldMapTravelRulesConfig travelRules,
        WorldMapTravelDebugController travelDebug,
        NodeTravelController travelLauncher,
        WorldMapEventManager eventManager,
        WorldMapEffectCatalog effectCatalog,
        WorldMapTopographyDebugSource topographyDebugSource,
        WorldMapPOISource poiSource = null)
    {
        _generator = generator;
        _runtimeBinder = runtimeBinder;
        _player = player;
        _travelRules = travelRules;
        _travelDebug = travelDebug;
        _travelLauncher = travelLauncher;
        _eventManager = eventManager;
        _effectCatalog = effectCatalog;
        _topographyDebugSource = topographyDebugSource;
        _poiSource = poiSource;
        AutoWirePOISource();
        AutoWireKnowledgeSource();
    }

    #endregion

    #region IMiniGameCartridge

    public void Begin(MiniGameContext context)
    {
        _ctx = context ?? new MiniGameContext();
        _requestedClose = false;
        _statusLine = null;
        _hasFitView = false;

        SyncLockFromPlayerState();
        SelectCurrentNodeOrDefault();

        ClampInjectionIndices();

        AutoWirePOISource();
        if (_poiSource != null)
            _poiSource.EnsureGenerated();

        AutoWireKnowledgeSource();
        if (_knowledgeSource != null)
        {
            _knowledgeSource.EnsureInitialized();
            _knowledgeSource.RevealSurfaceAroundCurrentNode();
        }

        _debugBuffDurationHours = 12f;
        _debugBuffStacks = 1;

        _eventPickerOpen = false;
        _outcomePickerOpen = false;
        _buffPickerOpen = false;
    }

    public MiniGameResult Tick(float dt, MiniGameInput input)
    {
        if (_requestedClose)
        {
            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Cancelled,
                quality01 = 1f,
                note = "World map closed.",
                hasMeaningfulProgress = false
            };
        }

        return new MiniGameResult
        {
            outcome = MiniGameOutcome.None,
            quality01 = 1f,
            note = null,
            hasMeaningfulProgress = false
        };
    }

    public MiniGameResult Cancel()
    {
        return new MiniGameResult
        {
            outcome = MiniGameOutcome.Cancelled,
            quality01 = 1f,
            note = "World map cancelled.",
            hasMeaningfulProgress = false
        };
    }

    public MiniGameResult Interrupt(string reason)
    {
        return new MiniGameResult
        {
            outcome = MiniGameOutcome.Cancelled,
            quality01 = 1f,
            note = $"World map interrupted: {reason}",
            hasMeaningfulProgress = false
        };
    }

    public void End()
    {
        _ctx = null;
        _dragging = false;
    }

    #endregion

    #region IOverlayRenderable / Main Layout

    public void DrawOverlayGUI(Rect panel)
    {
        EnsureWhiteTexture();

        DrawWindowBackground(panel);

        float pad = 14f;
        float headerH = 34f;
        float footerH = 24f;

        float leftW = 240f;
        float rightW = 275f;

        Rect header = new Rect(
            panel.x + pad,
            panel.y + 8f,
            panel.width - pad * 2f,
            headerH
        );

        Rect content = new Rect(
            panel.x + pad,
            panel.y + headerH + 14f,
            panel.width - pad * 2f,
            panel.height - headerH - footerH - 28f
        );

        Rect leftRect = new Rect(
            content.x,
            content.y,
            leftW,
            content.height
        );

        Rect mapRect = new Rect(
            leftRect.xMax + pad,
            content.y,
            content.width - leftW - rightW - pad * 2f,
            content.height
        );

        Rect rightRect = new Rect(
            mapRect.xMax + pad,
            content.y,
            rightW,
            content.height
        );

        Rect footer = new Rect(
            panel.x + pad,
            panel.yMax - footerH - 6f,
            panel.width - pad * 2f,
            footerH
        );

        DrawHeader(header);
        DrawHeatmapPanel(leftRect);
        DrawMapViewport(mapRect);
        DrawNodeDetailsPanel(rightRect);
        DrawFooter(footer);

        if (_showInjectionTools)
            DrawInjectionToolsWindow(panel);
    }

    private void DrawWindowBackground(Rect panel)
    {
        Color old = GUI.color;

        GUI.color = new Color(0.04f, 0.055f, 0.075f, 0.96f);
        GUI.DrawTexture(panel, _whiteTex);

        GUI.color = new Color(0.22f, 0.28f, 0.34f, 1f);
        DrawRectOutline(panel, 2f);

        GUI.color = old;
    }

    private void DrawHeader(Rect rect)
    {
        GUI.Label(new Rect(rect.x, rect.y + 4f, rect.width - 44f, 24f), "World Map");

        if (GUI.Button(new Rect(rect.xMax - 34f, rect.y, 30f, 26f), "X"))
            _requestedClose = true;
    }

    private void DrawMapViewport(Rect rect)
    {
        if (_generator == null || _generator.graph == null || _generator.graph.nodes == null || _generator.graph.nodes.Count == 0)
        {
            GUI.Box(rect, "No world map graph.");
            return;
        }

        if (!_hasFitView)
            FitViewToGraph(rect);

        HandleViewportInput(rect);

        Color old = GUI.color;

        GUI.color = new Color(0.02f, 0.10f, 0.18f, 1f);
        GUI.DrawTexture(rect, _whiteTex);

        GUI.color = new Color(0.16f, 0.28f, 0.38f, 1f);
        DrawRectOutline(rect, 1f);

        GUI.BeginGroup(rect);
        Rect localRect = new Rect(0f, 0f, rect.width, rect.height);

        DrawTopographyDebugLayer(localRect);
        DrawViewportGrid(localRect);
        DrawUnderwaterSurveyShroud(localRect);
        DrawRoutes(localRect);
        DrawActiveTravelRoute(localRect);
        DrawPOIs(localRect);
        DrawNodes(localRect);
        DrawSurfaceShroud(localRect);

        GUI.EndGroup();

        GUI.color = old;
    }

    private void DrawNodeDetailsPanel(Rect rect)
    {
        DrawPanelBox(rect);

        float x = rect.x + 10f;
        float y = rect.y + 10f;
        float w = rect.width - 20f;

        GUI.Label(new Rect(x, y, w, 22f), "Selected Node");
        y += 28f;

        if (!TryGetSelectedRuntime(out var selectedRt))
        {
            GUI.Label(new Rect(x, y, w, 40f), "No node selected.");
            return;
        }

        var state = selectedRt.State;
        string kind = SafeNodeKind(selectedRt.NodeIndex);

        GUI.Label(new Rect(x, y, w, 22f), selectedRt.DisplayName);
        y += 22f;

        GUI.Label(new Rect(x, y, w, 22f), $"Node: #{selectedRt.NodeIndex}   Cluster: {selectedRt.ClusterId}");
        y += 22f;

        GUI.Label(new Rect(x, y, w, 22f), $"Kind: {kind}");
        y += 22f;

        GUI.Label(new Rect(x, y, w, 22f), $"Affinity: {selectedRt.ClusterAffinityId}");
        y += 22f;

        GUI.Label(new Rect(x, y, w, 22f), $"Archetype: {selectedRt.NodeArchetypeId}");
        y += 30f;

        DrawSelectedHeatmapValues(ref y, x, w, selectedRt);

        y += 10f;
        DrawSelectedBuffs(ref y, x, w, selectedRt);

        y += 10f;
        DrawSelectedEvents(ref y, x, w, selectedRt);

        y += 12f;

        bool canLock = CanLockSelected(out string lockReason);
        string lockText = IsSelectedLocked()
            ? "Unlock Destination"
            : "Lock Destination";

        GUI.enabled = canLock || IsSelectedLocked();

        if (GUI.Button(new Rect(x, y, w, 30f), lockText))
            ToggleLockSelected();

        GUI.enabled = true;
        y += 38f;

        bool canTravel = CanTravelNow(out string travelReason);

        GUI.enabled = canTravel;
        if (GUI.Button(new Rect(x, y, w, 30f), "Start Travel"))
            StartTravel();

        GUI.enabled = true;
        y += 40f;

        string routeText = BuildRouteInfoText(lockReason, travelReason);
        GUI.Label(new Rect(x, y, w, rect.yMax - y - 10f), routeText);
    }

    #endregion

    #region Selected Node Details

    private void DrawSelectedBuffs(ref float y, float x, float w, MapNodeRuntime rt)
    {
        GUI.Label(new Rect(x, y, w, 22f), "Buffs");
        y += 24f;

        if (rt == null || rt.State == null || rt.State.ActiveBuffs == null || rt.State.ActiveBuffs.Count == 0)
        {
            GUI.Label(new Rect(x, y, w, 20f), "(none)");
            y += 22f;
            return;
        }

        var buffs = rt.State.ActiveBuffs;
        int max = Mathf.Min(buffs.Count, 5);

        for (int i = 0; i < max; i++)
        {
            var inst = buffs[i];
            string name = inst.buff != null && !string.IsNullOrWhiteSpace(inst.buff.displayName)
                ? inst.buff.displayName
                : "(unknown buff)";

            float accel = inst.GetAccelThisTick();
            string sign = accel >= 0f ? "+" : "";

            string target = inst.buff != null
                ? FormatNodeValueTarget(inst.buff.target)
                : "Unknown";

            string stacks = inst.stacks > 1 ? $" x{inst.stacks}" : "";

            GUI.Label(
                new Rect(x, y, w, 38f),
                $"{name}{stacks}\n{target}: {sign}{accel:0.###}/h   {inst.RemainingHours:0.0}h left"
            );

            y += 40f;
        }

        if (buffs.Count > max)
        {
            GUI.Label(new Rect(x, y, w, 20f), $"+ {buffs.Count - max} more...");
            y += 22f;
        }
    }

    private void DrawSelectedEvents(ref float y, float x, float w, MapNodeRuntime rt)
    {
        GUI.Label(new Rect(x, y, w, 22f), "Events");
        y += 24f;

        if (_eventManager == null)
        {
            GUI.Label(new Rect(x, y, w, 20f), "(no event manager)");
            y += 22f;
            return;
        }

        if (rt == null)
        {
            GUI.Label(new Rect(x, y, w, 20f), "(no selected node)");
            y += 22f;
            return;
        }

        _eventManager.GetEventsAtNode(rt.NodeIndex, _tmpEvents);

        if (_tmpEvents.Count == 0)
        {
            GUI.Label(new Rect(x, y, w, 20f), "(none)");
            y += 22f;
            return;
        }

        int max = Mathf.Min(_tmpEvents.Count, 5);

        for (int i = 0; i < max; i++)
        {
            var ev = _tmpEvents[i];

            string name = ev.def != null && !string.IsNullOrWhiteSpace(ev.def.displayName)
                ? ev.def.displayName
                : "(unknown event)";

            string visibility = ev.def != null && !ev.def.isVisibleToPlayer
                ? "hidden"
                : "visible";

            GUI.Label(
                new Rect(x, y, w, 34f),
                $"{name}\n{ev.RemainingHours:0.0}h left   {visibility}"
            );

            y += 36f;
        }

        if (_tmpEvents.Count > max)
        {
            GUI.Label(new Rect(x, y, w, 20f), $"+ {_tmpEvents.Count - max} more...");
            y += 22f;
        }
    }

    private static string FormatNodeValueTarget(NodeValueTarget target)
    {
        switch (target.kind)
        {
            case NodeValueTargetKind.Stat:
                return target.statId.ToString();

            case NodeValueTargetKind.DockRating:
                return "Dock";

            case NodeValueTargetKind.TradeRating:
                return "Trade";

            case NodeValueTargetKind.OptionalBuildingRating:
                return target.buildingId.ToString();

            default:
                return "Unknown";
        }
    }

    #endregion

    #region Heatmap and Injection Panels

    private void DrawHeatmapPanel(Rect rect)
    {
        DrawPanelBox(rect);

        float x = rect.x + 10f;
        float y = rect.y + 10f;
        float w = rect.width - 20f;

        if (_topographyDebugSource != null && _topographyDebugSource.HasBaseTexture)
        {
            _showTopographyDebug = GUI.Toggle(
                new Rect(x, y, w, 22f),
                _showTopographyDebug,
                "Show Topography"
            );

            y += 14f;

            GUI.enabled = _showTopographyDebug && _topographyDebugSource.HasClassificationTexture;

            _showTopographyClassification = GUI.Toggle(
                new Rect(x, y, w, 22f),
                _showTopographyClassification,
                "Show Classes"
            );

            y += 14f;

            GUI.enabled = _showTopographyDebug && _topographyDebugSource.HasBiomeTexture;

            _showTopographyBiomes = GUI.Toggle(
                new Rect(x, y, w, 22f),
                _showTopographyBiomes,
                "Show Biomes 🍌"
            );

            GUI.enabled = true;
            y += 14f;

            GUI.enabled = _showTopographyDebug && _topographyDebugSource.HasContourTexture;

            _showTopographyContours = GUI.Toggle(
                new Rect(x, y, w, 22f),
                _showTopographyContours,
                "Show Contours"
            );

            GUI.enabled = true;
            y += 14f;

            WorldMapTopographyStats stats = _topographyDebugSource.Stats;

            GUI.Label(
                new Rect(x, y, w, 68f),
                $"Sea: {_topographyDebugSource.EffectiveSeaLevel01:0.000}\n" +
                $"Water: {stats.Water01:P0} Land: {stats.Land01:P0}\n" +
                $"Deep: {stats.DeepOcean01:P0} Open: {stats.OpenOcean01:P0}\n" +
                $"Shelf: {stats.ShelfWater01:P0} Shallow: {stats.ShallowWater01:P0}"
            );

            y += 64f;

            if (GUI.Button(new Rect(x, y, w, 24f), "Regenerate Topography"))
                _topographyDebugSource.Generate();

            y += 30f;
        }
        else
        {
            GUI.Label(new Rect(x, y, w, 34f), "Topography:\n(no source)");
            y += 30f;
        }

        GUI.Label(new Rect(x, y, w, 22f), "Layers");
        y += 24f;

        _showNodes = GUI.Toggle(
            new Rect(x, y, w, 22f),
            _showNodes,
            "Show Nodes"
        );
        y += 22f;

        AutoWirePOISource();

        GUI.enabled = _poiSource != null && _poiSource.HasLayer;
        _showPOIs = GUI.Toggle(
            new Rect(x, y, w, 22f),
            _showPOIs,
            "Show POIs"
        );
        GUI.enabled = true;
        y += 22f;

        if (_poiSource != null)
        {
            int poiCount = _poiSource.Layer != null && _poiSource.Layer.pois != null
                ? _poiSource.Layer.pois.Count
                : 0;

            GUI.Label(new Rect(x, y, w, 22f), $"POIs: {poiCount}");
            y += 22f;

            if (GUI.Button(new Rect(x, y, w, 24f), "Regenerate POIs"))
                _poiSource.Generate();

            y += 30f;
        }
        else
        {
            GUI.Label(new Rect(x, y, w, 22f), "POIs: no source");
            y += 26f;
        }

        AutoWireKnowledgeSource();

        GUI.Label(new Rect(x, y, w, 22f), "Map Knowledge");
        y += 22f;

        GUI.enabled = _knowledgeSource != null && _knowledgeSource.HasState;

        _showSurfaceShroud = GUI.Toggle(
            new Rect(x, y, w, 22f),
            _showSurfaceShroud,
            "Show Surface Shroud"
        );
        y += 20f;

        _showUnderwaterSurveyShroud = GUI.Toggle(
            new Rect(x, y, w, 22f),
            _showUnderwaterSurveyShroud,
            "Show Survey Shroud"
        );
        y += 20f;

        _hideUnsurveyedPOIs = GUI.Toggle(
            new Rect(x, y, w, 22f),
            _hideUnsurveyedPOIs,
            "Hide Unsurveyed POIs"
        );
        y += 20f;

        if (_knowledgeSource != null)
        {
            GUI.Label(
                new Rect(x, y, w, 38f),
                $"Surface: {_knowledgeSource.SurfaceReveal01:P0}\nUnderwater: {_knowledgeSource.UnderwaterSurvey01:P0}"
            );
            y += 40f;

            float halfW = (w - 6f) * 0.5f;

            if (GUI.Button(new Rect(x, y, halfW, 22f), "Reveal Node"))
                _knowledgeSource.RevealSurfaceAroundCurrentNode();

            if (GUI.Button(new Rect(x + halfW + 6f, y, halfW, 22f), "Survey Node"))
                _knowledgeSource.SurveyUnderwaterAroundCurrentNode();

            y += 26f;

            if (GUI.Button(new Rect(x, y, halfW, 22f), "Reveal All"))
                _knowledgeSource.RevealAllSurface();

            if (GUI.Button(new Rect(x + halfW + 6f, y, halfW, 22f), "Survey All"))
                _knowledgeSource.RevealAllUnderwater();

            y += 26f;

            if (GUI.Button(new Rect(x, y, halfW, 22f), "Clear Fog"))
                _knowledgeSource.ClearAll();

            GUI.enabled = _travelDebug != null;
            if (GUI.Button(new Rect(x + halfW + 6f, y, halfW, 22f), "Finish Travel"))
            {
                _travelDebug.DebugInstantCompleteTravel();
                _knowledgeSource.RevealSurfaceAroundCurrentNode();
            }
            GUI.enabled = true;

            y += 30f;
        }
        else
        {
            GUI.Label(new Rect(x, y, w, 22f), "Knowledge: no source");
            y += 26f;
        }

        GUI.enabled = true;

        DrawHeatmapDropdown(ref y, x, w);
        y += 10f;

        GUI.Label(new Rect(x, y, w, 22f), "Debug");
        y += 24f;

        string injectionButtonText = _showInjectionTools
            ? "Close Injection Tools"
            : "Open Injection Tools";

        if (GUI.Button(new Rect(x, y, w, 28f), injectionButtonText))
        {
            _showInjectionTools = !_showInjectionTools;
            _eventPickerOpen = false;
            _outcomePickerOpen = false;
            _buffPickerOpen = false;
        }

        y += 34f;

        if (_effectCatalog == null)
        {
            GUI.Label(new Rect(x, y, w, 34f), "Effects catalog:\n(not assigned)");
        }
        else
        {
            GUI.Label(
                new Rect(x, y, w, 52f),
                $"Effects catalog:\nE:{_effectCatalog.Events.Count}  O:{_effectCatalog.Outcomes.Count}  B:{_effectCatalog.Buffs.Count}"
            );
        }
    }

    private void DrawHeatmapDropdown(ref float y, float x, float w)
    {
        GUI.Label(new Rect(x, y, w, 22f), "Heatmap");
        y += 22f;

        if (GUI.Button(new Rect(x, y, w, 24f), GetHeatmapLabel(_heatmapMode)))
            _heatmapPickerOpen = !_heatmapPickerOpen;

        y += 26f;

        if (!_heatmapPickerOpen)
            return;

        DrawHeatmapOption(ref y, x, w, HeatmapMode.None);
        DrawHeatmapOption(ref y, x, w, HeatmapMode.Prosperity);
        DrawHeatmapOption(ref y, x, w, HeatmapMode.Stability);
        DrawHeatmapOption(ref y, x, w, HeatmapMode.Security);
        DrawHeatmapOption(ref y, x, w, HeatmapMode.DockRating);
        DrawHeatmapOption(ref y, x, w, HeatmapMode.TradeRating);
        DrawHeatmapOption(ref y, x, w, HeatmapMode.Population);
        DrawHeatmapOption(ref y, x, w, HeatmapMode.FoodBalance);
    }

    private void DrawHeatmapOption(ref float y, float x, float w, HeatmapMode mode)
    {
        string label = mode == _heatmapMode
            ? $"✓ {GetHeatmapLabel(mode)}"
            : GetHeatmapLabel(mode);

        if (GUI.Button(new Rect(x + 8f, y, w - 8f, 22f), label))
        {
            _heatmapMode = mode;
            _heatmapPickerOpen = false;
        }

        y += 23f;
    }

    private static string GetHeatmapLabel(HeatmapMode mode)
    {
        switch (mode)
        {
            case HeatmapMode.Prosperity: return "Prosperity";
            case HeatmapMode.Stability: return "Stability";
            case HeatmapMode.Security: return "Security";
            case HeatmapMode.DockRating: return "Dock Rating";
            case HeatmapMode.TradeRating: return "Trade Rating";
            case HeatmapMode.Population: return "Population";
            case HeatmapMode.FoodBalance: return "Food Balance";
            default: return "None";
        }
    }

    private void DrawInjectionToolsWindow(Rect parentPanel)
    {
        if (!_injectionToolsRectInitialized)
        {
            float w = 390f;
            float h = 500f;

            _injectionToolsRect = new Rect(
                parentPanel.center.x - w * 0.5f,
                parentPanel.center.y - h * 0.5f,
                w,
                h
            );

            _injectionToolsRectInitialized = true;
        }

        _injectionToolsRect = ClampRectToParent(_injectionToolsRect, parentPanel);

        Color old = GUI.color;

        _injectionToolsRect = GUI.Window(
            InjectionToolsWindowId,
            _injectionToolsRect,
            DrawInjectionToolsWindowContents,
            "Injection Tools"
        );

        GUI.color = old;
    }

    private void DrawInjectionToolsWindowContents(int windowId)
    {
        Rect closeRect = new Rect(_injectionToolsRect.width - 34f, 4f, 28f, 22f);
        if (GUI.Button(closeRect, "X"))
        {
            _showInjectionTools = false;
            _eventPickerOpen = false;
            _outcomePickerOpen = false;
            _buffPickerOpen = false;
            return;
        }

        Rect content = new Rect(
            12f,
            30f,
            _injectionToolsRect.width - 24f,
            _injectionToolsRect.height - 42f
        );

        DrawInjectionToolsContent(content);

        GUI.DragWindow(new Rect(0f, 0f, _injectionToolsRect.width - 40f, 26f));
    }

    private void DrawInjectionToolsContent(Rect rect)
    {
        if (_effectCatalog == null)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 40f), "No WorldMapEffectCatalog assigned.");
            return;
        }

        ClampInjectionIndices();

        float y = rect.y;

        GUI.Label(
            new Rect(rect.x, y, rect.width, 40f),
            $"Target:\n{GetSelectedInjectionTargetLabel()}"
        );

        y += 46f;

        DrawEventInjectionSection(ref y, rect.x, rect.width);
        y += 10f;

        DrawOutcomeInjectionSection(ref y, rect.x, rect.width);
        y += 10f;

        DrawBuffInjectionSection(ref y, rect.x, rect.width);
    }

    private static Rect ClampRectToParent(Rect child, Rect parent)
    {
        float margin = 8f;

        if (child.width > parent.width - margin * 2f)
            child.width = Mathf.Max(120f, parent.width - margin * 2f);

        if (child.height > parent.height - margin * 2f)
            child.height = Mathf.Max(120f, parent.height - margin * 2f);

        child.x = Mathf.Clamp(child.x, parent.xMin + margin, parent.xMax - child.width - margin);
        child.y = Mathf.Clamp(child.y, parent.yMin + margin, parent.yMax - child.height - margin);

        return child;
    }

    private void DrawEventInjectionSection(ref float y, float x, float w)
    {
        GUI.Label(new Rect(x, y, w, 20f), "Event");
        y += 21f;

        int count = _effectCatalog.Events?.Count ?? 0;

        if (count <= 0)
        {
            GUI.Label(new Rect(x, y, w, 20f), "(no events)");
            y += 22f;
            return;
        }

        if (GUI.Button(new Rect(x, y, w, 24f), _effectCatalog.GetEventLabel(_selectedEventIndex)))
        {
            _eventPickerOpen = !_eventPickerOpen;
            _outcomePickerOpen = false;
            _buffPickerOpen = false;
        }

        y += 26f;

        if (_eventPickerOpen)
            DrawCatalogPicker(ref y, x, w, count, _effectCatalog.GetEventLabel, i =>
            {
                _selectedEventIndex = i;
                _eventPickerOpen = false;
            });

        float half = (w - 6f) * 0.5f;

        GUI.enabled = CanInjectIntoSelectedNode();

        if (GUI.Button(new Rect(x, y, half, 24f), "Inject"))
            InjectSelectedEvent();

        if (GUI.Button(new Rect(x + half + 6f, y, half, 24f), "Remove"))
            RemoveSelectedEvent();

        GUI.enabled = true;

        y += 28f;
    }

    private void DrawOutcomeInjectionSection(ref float y, float x, float w)
    {
        GUI.Label(new Rect(x, y, w, 20f), "Outcome");
        y += 21f;

        int count = _effectCatalog.Outcomes?.Count ?? 0;

        if (count <= 0)
        {
            GUI.Label(new Rect(x, y, w, 20f), "(no outcomes)");
            y += 22f;
            return;
        }

        if (GUI.Button(new Rect(x, y, w, 24f), _effectCatalog.GetOutcomeLabel(_selectedOutcomeIndex)))
        {
            _outcomePickerOpen = !_outcomePickerOpen;
            _eventPickerOpen = false;
            _buffPickerOpen = false;
        }

        y += 26f;

        if (_outcomePickerOpen)
            DrawCatalogPicker(ref y, x, w, count, _effectCatalog.GetOutcomeLabel, i =>
            {
                _selectedOutcomeIndex = i;
                _outcomePickerOpen = false;
            });

        GUI.enabled = CanInjectIntoSelectedNode();

        if (GUI.Button(new Rect(x, y, w, 24f), "Apply Outcome"))
            ApplySelectedOutcome();

        GUI.enabled = true;

        y += 28f;
    }

    private void DrawBuffInjectionSection(ref float y, float x, float w)
    {
        GUI.Label(new Rect(x, y, w, 20f), "Buff");
        y += 21f;

        int count = _effectCatalog.Buffs?.Count ?? 0;

        if (count <= 0)
        {
            GUI.Label(new Rect(x, y, w, 20f), "(no buffs)");
            y += 22f;
            return;
        }

        if (GUI.Button(new Rect(x, y, w, 24f), _effectCatalog.GetBuffLabel(_selectedBuffIndex)))
        {
            _buffPickerOpen = !_buffPickerOpen;
            _eventPickerOpen = false;
            _outcomePickerOpen = false;
        }

        y += 26f;

        if (_buffPickerOpen)
            DrawCatalogPicker(ref y, x, w, count, _effectCatalog.GetBuffLabel, i =>
            {
                _selectedBuffIndex = i;
                _buffPickerOpen = false;
            });

        DrawBuffInjectionTuning(ref y, x, w);

        float half = (w - 6f) * 0.5f;

        GUI.enabled = CanInjectIntoSelectedNode();

        if (GUI.Button(new Rect(x, y, half, 24f), "Inject"))
            InjectSelectedBuff();

        if (GUI.Button(new Rect(x + half + 6f, y, half, 24f), "Remove"))
            RemoveSelectedBuff();

        GUI.enabled = true;

        y += 28f;
    }

    private static void DrawCatalogPicker(
    ref float y,
    float x,
    float w,
    int count,
    Func<int, string> getLabel,
    Action<int> select)
    {
        int maxShown = Mathf.Min(count, 6);

        for (int i = 0; i < maxShown; i++)
        {
            if (GUI.Button(new Rect(x + 8f, y, w - 8f, 22f), getLabel(i)))
                select(i);

            y += 23f;
        }

        if (count > maxShown)
        {
            GUI.Label(new Rect(x + 8f, y, w - 8f, 20f), $"+ {count - maxShown} more...");
            y += 22f;
        }
    }

    private void DrawBuffInjectionTuning(ref float y, float x, float w)
    {
        GUI.Label(new Rect(x, y, w, 20f), $"Duration: {_debugBuffDurationHours:0.0}h");
        y += 20f;

        float third = (w - 8f) / 3f;

        if (GUI.Button(new Rect(x, y, third, 22f), "-1h"))
            _debugBuffDurationHours = Mathf.Max(0.1f, _debugBuffDurationHours - 1f);

        if (GUI.Button(new Rect(x + third + 4f, y, third, 22f), "+1h"))
            _debugBuffDurationHours += 1f;

        if (GUI.Button(new Rect(x + (third + 4f) * 2f, y, third, 22f), "12h"))
            _debugBuffDurationHours = 12f;

        y += 26f;

        GUI.Label(new Rect(x, y, w, 20f), $"Stacks: {_debugBuffStacks}");
        y += 20f;

        if (GUI.Button(new Rect(x, y, third, 22f), "-"))
            _debugBuffStacks = Mathf.Max(1, _debugBuffStacks - 1);

        if (GUI.Button(new Rect(x + third + 4f, y, third, 22f), "+"))
            _debugBuffStacks = Mathf.Max(1, _debugBuffStacks + 1);

        if (GUI.Button(new Rect(x + (third + 4f) * 2f, y, third, 22f), "1"))
            _debugBuffStacks = 1;

        y += 28f;
    }

    #endregion

    #region Injection Actions

    private void InjectSelectedEvent()
    {
        if (!TryGetSelectedNodeIndex(out int nodeIndex))
        {
            _statusLine = "Event injection failed: no selected node.";
            return;
        }

        WorldMapEventDefinition def = GetSelectedEventDef();
        if (def == null)
        {
            _statusLine = "Event injection failed: no event selected.";
            return;
        }

        if (_eventManager == null)
        {
            _statusLine = "Event injection failed: no event manager.";
            return;
        }

        int seed = MakeDebugInjectionSeed(nodeIndex);
        _eventManager.AddEvent(def, nodeIndex, seed);

        _statusLine = $"Injected event '{GetSafeEventName(def)}' into node #{nodeIndex}.";
    }

    private void RemoveSelectedEvent()
    {
        if (!TryGetSelectedNodeIndex(out int nodeIndex))
        {
            _statusLine = "Remove event failed: no selected node.";
            return;
        }

        if (_eventManager == null)
        {
            _statusLine = "Remove event failed: no event manager.";
            return;
        }

        WorldMapEventDefinition selected = GetSelectedEventDef();

        for (int i = _eventManager.active.Count - 1; i >= 0; i--)
        {
            var ev = _eventManager.active[i];

            if (ev.isResolved)
                continue;

            if (ev.sourceNodeId != nodeIndex)
                continue;

            if (selected != null && ev.def != selected)
                continue;

            string name = ev.def != null ? GetSafeEventName(ev.def) : "(unknown event)";
            _eventManager.active.RemoveAt(i);

            _statusLine = $"Removed event '{name}' from node #{nodeIndex}.";
            return;
        }

        _statusLine = "No matching event found on selected node.";
    }

    private void ApplySelectedOutcome()
    {
        if (!TryGetSelectedNodeIndex(out int nodeIndex))
        {
            _statusLine = "Outcome injection failed: no selected node.";
            return;
        }

        EventOutcome outcome = GetSelectedOutcomeDef();
        if (outcome == null)
        {
            _statusLine = "Outcome injection failed: no outcome selected.";
            return;
        }

        if (_eventManager == null)
        {
            _statusLine = "Outcome injection failed: no event manager.";
            return;
        }

        bool ok = _eventManager.TryApplyOutcomeToNode(nodeIndex, outcome);

        _statusLine = ok
            ? $"Applied outcome '{GetSafeOutcomeName(outcome)}' to node #{nodeIndex}."
            : "Outcome injection failed.";
    }

    private void InjectSelectedBuff()
    {
        if (!TryGetSelectedNodeIndex(out int nodeIndex))
        {
            _statusLine = "Buff injection failed: no selected node.";
            return;
        }

        NodeBuff buff = GetSelectedBuffDef();
        if (buff == null)
        {
            _statusLine = "Buff injection failed: no buff selected.";
            return;
        }

        if (_eventManager == null)
        {
            _statusLine = "Buff injection failed: no event manager.";
            return;
        }

        bool ok = _eventManager.TryInjectBuffToNode(
            nodeIndex,
            buff,
            _debugBuffDurationHours,
            _debugBuffStacks
        );

        _statusLine = ok
            ? $"Injected buff '{GetSafeBuffName(buff)}' into node #{nodeIndex}."
            : "Buff injection failed.";
    }

    private void RemoveSelectedBuff()
    {
        if (!TryGetSelectedRuntime(out var rt) || rt == null || rt.State == null)
        {
            _statusLine = "Remove buff failed: no selected runtime node.";
            return;
        }

        NodeBuff buff = GetSelectedBuffDef();
        if (buff == null)
        {
            _statusLine = "Remove buff failed: no buff selected.";
            return;
        }

        var list = rt.State.ActiveBuffsMutable;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].buff != buff)
                continue;

            list.RemoveAt(i);
            _statusLine = $"Removed buff '{GetSafeBuffName(buff)}' from {rt.DisplayName}.";
            return;
        }

        _statusLine = "No matching buff found on selected node.";
    }

    #endregion

    #region Injection Helpers

    private bool CanInjectIntoSelectedNode()
    {
        return TryGetSelectedNodeIndex(out _);
    }

    private bool TryGetSelectedNodeIndex(out int nodeIndex)
    {
        nodeIndex = _selectedNodeIndex;

        if (_generator == null || _generator.graph == null || _generator.graph.nodes == null)
            return false;

        return nodeIndex >= 0 && nodeIndex < _generator.graph.nodes.Count;
    }

    private string GetSelectedInjectionTargetLabel()
    {
        if (TryGetSelectedRuntime(out var rt) && rt != null)
            return $"{rt.DisplayName} #{rt.NodeIndex}";

        if (TryGetSelectedNodeIndex(out int index))
            return $"Node #{index}";

        return "(none)";
    }

    private void ClampInjectionIndices()
    {
        int eventCount = _effectCatalog?.Events?.Count ?? 0;
        int outcomeCount = _effectCatalog?.Outcomes?.Count ?? 0;
        int buffCount = _effectCatalog?.Buffs?.Count ?? 0;

        _selectedEventIndex = eventCount > 0
            ? Mathf.Clamp(_selectedEventIndex, 0, eventCount - 1)
            : 0;

        _selectedOutcomeIndex = outcomeCount > 0
            ? Mathf.Clamp(_selectedOutcomeIndex, 0, outcomeCount - 1)
            : 0;

        _selectedBuffIndex = buffCount > 0
            ? Mathf.Clamp(_selectedBuffIndex, 0, buffCount - 1)
            : 0;
    }

    private WorldMapEventDefinition GetSelectedEventDef()
    {
        var list = _effectCatalog?.Events;
        if (list == null || list.Count == 0)
            return null;

        ClampInjectionIndices();
        return list[_selectedEventIndex];
    }

    private EventOutcome GetSelectedOutcomeDef()
    {
        var list = _effectCatalog?.Outcomes;
        if (list == null || list.Count == 0)
            return null;

        ClampInjectionIndices();
        return list[_selectedOutcomeIndex];
    }

    private NodeBuff GetSelectedBuffDef()
    {
        var list = _effectCatalog?.Buffs;
        if (list == null || list.Count == 0)
            return null;

        ClampInjectionIndices();
        return list[_selectedBuffIndex];
    }

    private int MakeDebugInjectionSeed(int nodeIndex)
    {
        _debugInjectionSeedCounter++;

        return unchecked(
            (_ctx != null ? _ctx.seed : 0) ^
            (nodeIndex * 92821) ^
            (_debugInjectionSeedCounter * 15485863)
        );
    }

    private static string GetSafeEventName(WorldMapEventDefinition def)
    {
        if (def == null)
            return "(null event)";

        if (!string.IsNullOrWhiteSpace(def.displayName))
            return def.displayName;

        if (!string.IsNullOrWhiteSpace(def.eventId))
            return def.eventId;

        return def.name;
    }

    private static string GetSafeOutcomeName(EventOutcome outcome)
    {
        if (outcome == null)
            return "(null outcome)";

        if (!string.IsNullOrWhiteSpace(outcome.displayName))
            return outcome.displayName;

        if (!string.IsNullOrWhiteSpace(outcome.outcomeId))
            return outcome.outcomeId;

        return outcome.name;
    }

    private static string GetSafeBuffName(NodeBuff buff)
    {
        if (buff == null)
            return "(null buff)";

        if (!string.IsNullOrWhiteSpace(buff.displayName))
            return buff.displayName;

        if (!string.IsNullOrWhiteSpace(buff.buffId))
            return buff.buffId;

        return buff.name;
    }

    #endregion

    #region Selected Values

    private void DrawSelectedHeatmapValues(ref float y, float x, float w, MapNodeRuntime rt)
    {
        if (rt == null || rt.State == null)
        {
            GUI.Label(new Rect(x, y, w, 22f), "Stats: unavailable");
            y += 24f;
            return;
        }

        var s = rt.State;

        GUI.Label(new Rect(x, y, w, 22f), "Values");
        y += 24f;

        DrawValueLine(ref y, x, w, "Prosperity", GetStatValue(s, NodeStatId.Prosperity));
        DrawValueLine(ref y, x, w, "Stability", GetStatValue(s, NodeStatId.Stability));
        DrawValueLine(ref y, x, w, "Security", GetStatValue(s, NodeStatId.Security));
        DrawValueLine(ref y, x, w, "Dock", GetStatValue(s, NodeStatId.DockRating));
        DrawValueLine(ref y, x, w, "Trade", GetStatValue(s, NodeStatId.TradeRating));
        DrawValueLine(ref y, x, w, "Population", Mathf.Round(s.population));
        DrawValueLine(ref y, x, w, "Food Balance", GetStatValue(s, NodeStatId.FoodBalance));
    }

    private static float GetStatValue(MapNodeState state, NodeStatId id)
    {
        if (state == null)
            return 0f;

        return state.TryGetStat(id, out var stat) ? stat.value : 0f;
    }

    private static void DrawValueLine(ref float y, float x, float w, string label, float value)
    {
        GUI.Label(new Rect(x, y, w * 0.62f, 20f), label);
        GUI.Label(new Rect(x + w * 0.62f, y, w * 0.38f, 20f), value.ToString("0.00"));
        y += 20f;
    }

    private static void DrawPanelBox(Rect rect)
    {
        Color old = GUI.color;

        GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.92f);
        GUI.DrawTexture(rect, _whiteTex);

        GUI.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        DrawRectOutline(rect, 1f);

        GUI.color = old;
    }

    private void DrawHeatmapSection(Rect rect)
    {
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 22f), $"Heatmap: {_heatmapMode}");
        float y = rect.y + 26f;

        float buttonH = 24f;
        float gap = 4f;
        float halfW = (rect.width - gap) * 0.5f;

        if (GUI.Button(new Rect(rect.x, y, halfW, buttonH), "None"))
            _heatmapMode = HeatmapMode.None;

        if (GUI.Button(new Rect(rect.x + halfW + gap, y, halfW, buttonH), "Prosperity"))
            _heatmapMode = HeatmapMode.Prosperity;

        y += buttonH + gap;

        if (GUI.Button(new Rect(rect.x, y, halfW, buttonH), "Stability"))
            _heatmapMode = HeatmapMode.Stability;

        if (GUI.Button(new Rect(rect.x + halfW + gap, y, halfW, buttonH), "Security"))
            _heatmapMode = HeatmapMode.Security;

        y += buttonH + gap;

        if (GUI.Button(new Rect(rect.x, y, halfW, buttonH), "Dock"))
            _heatmapMode = HeatmapMode.DockRating;

        if (GUI.Button(new Rect(rect.x + halfW + gap, y, halfW, buttonH), "Trade"))
            _heatmapMode = HeatmapMode.TradeRating;

        y += buttonH + gap;

        if (GUI.Button(new Rect(rect.x, y, halfW, buttonH), "Population"))
            _heatmapMode = HeatmapMode.Population;

        if (GUI.Button(new Rect(rect.x + halfW + gap, y, halfW, buttonH), "Food"))
            _heatmapMode = HeatmapMode.FoodBalance;
    }

    #endregion

    #region Footer and Viewport Rendering

    private void DrawFooter(Rect rect)
    {
        string text = string.IsNullOrWhiteSpace(_statusLine)
            ? "Drag to pan. Mouse wheel to zoom. Click a node to select."
            : _statusLine;

        GUI.Label(rect, text);
    }

    private void DrawTopographyDebugLayer(Rect localRect)
    {
        if (!_showTopographyDebug)
            return;

        if (_topographyDebugSource == null)
            return;

        _topographyDebugSource.EnsureBaseTexture();

        if (!_topographyDebugSource.HasBaseTexture)
            return;

        WorldMapTopographyField field = _topographyDebugSource.Field;
        Texture2D baseTex = _topographyDebugSource.BaseTexture;
        Texture2D contourTex = _topographyDebugSource.ContourTexture;

        if (field == null || baseTex == null)
            return;

        Rect drawRect = GetTopographyDrawRect(field, localRect);

        Color old = GUI.color;

        GUI.color = Color.white;
        GUI.DrawTexture(drawRect, baseTex, ScaleMode.StretchToFill);

        if (_showTopographyClassification && _topographyDebugSource.HasClassificationTexture)
        {
            _topographyDebugSource.EnsureClassificationTexture();

            if (_topographyDebugSource.ClassificationTexture != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(drawRect, _topographyDebugSource.ClassificationTexture, ScaleMode.StretchToFill);
            }
        }

        if (_showTopographyBiomes && _topographyDebugSource.HasBiomeTexture)
        {
            _topographyDebugSource.EnsureBiomeTexture();

            if (_topographyDebugSource.BiomeTexture != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(drawRect, _topographyDebugSource.BiomeTexture, ScaleMode.StretchToFill);
            }
        }

        if (_showTopographyContours && _topographyDebugSource.HasContourTexture)
        {
            _topographyDebugSource.EnsureContourTexture();
            contourTex = _topographyDebugSource.ContourTexture;

            if (contourTex != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(drawRect, contourTex, ScaleMode.StretchToFill);
            }
        }

        GUI.color = old;
    }

    private Rect GetTopographyDrawRect(WorldMapTopographyField field, Rect localRect)
    {
        Rect bounds = field.WorldBounds;

        Vector2 topLeft = GraphToLocal(new Vector2(bounds.xMin, bounds.yMax), localRect);
        Vector2 bottomRight = GraphToLocal(new Vector2(bounds.xMax, bounds.yMin), localRect);

        return Rect.MinMaxRect(
            Mathf.Min(topLeft.x, bottomRight.x),
            Mathf.Min(topLeft.y, bottomRight.y),
            Mathf.Max(topLeft.x, bottomRight.x),
            Mathf.Max(topLeft.y, bottomRight.y)
        );
    }

    private void DrawViewportGrid(Rect localRect)
    {
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.045f);

        float spacing = Mathf.Clamp(_zoomPxPerGraphUnit, 18f, 80f);

        float offsetX = Mathf.Repeat((-_viewCenterGraph.x * _zoomPxPerGraphUnit) + localRect.width * 0.5f, spacing);
        float offsetY = Mathf.Repeat((_viewCenterGraph.y * _zoomPxPerGraphUnit) + localRect.height * 0.5f, spacing);

        for (float x = offsetX; x < localRect.width; x += spacing)
            DrawLine(new Vector2(x, 0f), new Vector2(x, localRect.height), GUI.color, 1f);

        for (float y = offsetY; y < localRect.height; y += spacing)
            DrawLine(new Vector2(0f, y), new Vector2(localRect.width, y), GUI.color, 1f);

        GUI.color = old;
    }

    private void DrawRoutes(Rect localRect)
    {
        var g = _generator.graph;
        if (g.edges == null)
            return;

        for (int i = 0; i < g.edges.Count; i++)
        {
            var e = g.edges[i];

            if (e.a < 0 || e.a >= g.nodes.Count) continue;
            if (e.b < 0 || e.b >= g.nodes.Count) continue;

            Vector2 a = GraphToLocal(g.nodes[e.a].position, localRect);
            Vector2 b = GraphToLocal(g.nodes[e.b].position, localRect);

            Color c = GetRouteColor(e.a, e.b);
            float thickness = GetRouteThickness(e.a, e.b);

            DrawLine(a, b, c, thickness);
        }
    }

    private void DrawActiveTravelRoute(Rect localRect)
    {
        if (!TryGetActiveTravelEndpointRuntimes(out var fromRt, out var toRt))
            return;

        var g = _generator != null ? _generator.graph : null;
        if (g == null || g.nodes == null)
            return;

        if (fromRt.NodeIndex < 0 || fromRt.NodeIndex >= g.nodes.Count)
            return;

        if (toRt.NodeIndex < 0 || toRt.NodeIndex >= g.nodes.Count)
            return;

        Vector2 a = GraphToLocal(g.nodes[fromRt.NodeIndex].position, localRect);
        Vector2 b = GraphToLocal(g.nodes[toRt.NodeIndex].position, localRect);

        DrawDashedLine(
            a,
            b,
            new Color(0.15f, 1f, 0.45f, 0.22f),
            width: 8f,
            dashLength: 18f,
            gapLength: 8f
        );

        DrawDashedLine(
            a,
            b,
            new Color(0.25f, 1f, 0.35f, 0.95f),
            width: 3f,
            dashLength: 18f,
            gapLength: 8f
        );
    }

    private static void DrawDashedLine(
    Vector2 a,
    Vector2 b,
    Color color,
    float width,
    float dashLength,
    float gapLength)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;

        if (length <= 0.001f)
            return;

        Vector2 dir = delta / length;

        dashLength = Mathf.Max(1f, dashLength);
        gapLength = Mathf.Max(0f, gapLength);

        float step = dashLength + gapLength;
        if (step <= 0.001f)
            step = dashLength;

        for (float d = 0f; d < length; d += step)
        {
            float end = Mathf.Min(d + dashLength, length);

            Vector2 p0 = a + dir * d;
            Vector2 p1 = a + dir * end;

            DrawLine(p0, p1, color, width);
        }
    }

    private void DrawPOIs(Rect localRect)
    {
        if (!_showPOIs)
            return;

        AutoWirePOISource();

        if (_poiSource == null || !_poiSource.HasLayer)
            return;

        var layer = _poiSource.Layer;
        if (layer == null || layer.pois == null)
            return;

        Rect padded = new Rect(
            localRect.xMin - 24f,
            localRect.yMin - 24f,
            localRect.width + 48f,
            localRect.height + 48f
        );

        for (int i = 0; i < layer.pois.Count; i++)
        {
            WorldMapPOIInstance poi = layer.pois[i];
            if (poi == null)
                continue;

            AutoWireKnowledgeSource();

            if (_knowledgeSource != null && _hideUnsurveyedPOIs && !_knowledgeSource.IsPOIVisible(poi))
                continue;

            Vector2 p = GraphToLocal(poi.position, localRect);

            if (!padded.Contains(p))
                continue;

            WorldMapPOIDef def = _poiSource != null ? _poiSource.GetDefinition(poi) : null;
            Color c = GetPOIColor(def);

            DrawPOIDiamond(p, 6f, c, 2f);

            if (poi.discovered)
                DrawNodeRing(p, 9f, new Color(1f, 1f, 1f, 0.65f), 1f);
        }
    }

    private void AutoWirePOISource()
    {
        if (_poiSource != null)
            return;

        _poiSource = UnityEngine.Object.FindAnyObjectByType<WorldMapPOISource>(FindObjectsInactive.Include);
    }

    private void AutoWireKnowledgeSource()
    {
        if (_knowledgeSource != null)
            return;

        _knowledgeSource = UnityEngine.Object.FindAnyObjectByType<WorldMapKnowledgeSource>(FindObjectsInactive.Include);
    }

    private void DrawSurfaceShroud(Rect localRect)
    {
        if (!_showSurfaceShroud)
            return;

        AutoWireKnowledgeSource();

        if (_knowledgeSource == null || !_knowledgeSource.HasState)
            return;

        DrawKnowledgeShroudLayer(
            localRect,
            WorldMapKnowledgeLayer.Surface,
            new Color(0.72f, 0.78f, 0.82f, 1f),
            onlyWhereSurfaceRevealed: false
        );
    }

    private void DrawUnderwaterSurveyShroud(Rect localRect)
    {
        if (!_showUnderwaterSurveyShroud)
            return;

        AutoWireKnowledgeSource();

        if (_knowledgeSource == null || !_knowledgeSource.HasState)
            return;

        DrawKnowledgeShroudLayer(
            localRect,
            WorldMapKnowledgeLayer.UnderwaterSurvey,
            new Color(0.02f, 0.12f, 0.24f, 0.16f),
            onlyWhereSurfaceRevealed: true
        );
    }

    private void DrawKnowledgeShroudLayer(
        Rect localRect,
        WorldMapKnowledgeLayer layer,
        Color hiddenColor,
        bool onlyWhereSurfaceRevealed)
    {
        WorldMapKnowledgeState state = _knowledgeSource.State;
        if (state == null || !state.IsValid)
            return;

        Rect bounds = state.WorldBounds;

        float invZoom = 1f / Mathf.Max(0.0001f, _zoomPxPerGraphUnit);

        float visibleMinX = _viewCenterGraph.x - localRect.width * 0.5f * invZoom;
        float visibleMaxX = _viewCenterGraph.x + localRect.width * 0.5f * invZoom;
        float visibleMinY = _viewCenterGraph.y - localRect.height * 0.5f * invZoom;
        float visibleMaxY = _viewCenterGraph.y + localRect.height * 0.5f * invZoom;

        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(bounds.xMin, bounds.xMax, visibleMinX) * state.Width), 0, state.Width - 1);
        int maxX = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(bounds.xMin, bounds.xMax, visibleMaxX) * state.Width), 0, state.Width - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(bounds.yMin, bounds.yMax, visibleMinY) * state.Height), 0, state.Height - 1);
        int maxY = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(bounds.yMin, bounds.yMax, visibleMaxY) * state.Height), 0, state.Height - 1);

        if (minX > maxX)
            (minX, maxX) = (maxX, minX);

        if (minY > maxY)
            (minY, maxY) = (maxY, minY);

        Color old = GUI.color;
        GUI.color = hiddenColor;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                bool surfaceRevealed = state.IsCellRevealed(WorldMapKnowledgeLayer.Surface, x, y);

                if (onlyWhereSurfaceRevealed && !surfaceRevealed)
                    continue;

                bool revealed = state.IsCellRevealed(layer, x, y);
                if (revealed)
                    continue;

                Rect worldCell = state.CellWorldRect(x, y);

                Vector2 topLeft = GraphToLocal(
                    new Vector2(worldCell.xMin, worldCell.yMax),
                    localRect
                );

                Vector2 bottomRight = GraphToLocal(
                    new Vector2(worldCell.xMax, worldCell.yMin),
                    localRect
                );

                Rect r = Rect.MinMaxRect(
                    Mathf.Min(topLeft.x, bottomRight.x),
                    Mathf.Min(topLeft.y, bottomRight.y),
                    Mathf.Max(topLeft.x, bottomRight.x) + 1f,
                    Mathf.Max(topLeft.y, bottomRight.y) + 1f
                );

                GUI.DrawTexture(r, _whiteTex);
            }
        }

        GUI.color = old;
    }

    private static Color GetPOIColor(WorldMapPOIDef def)
    {
        if (def != null)
            return def.mapColor;

        return new Color(1f, 1f, 1f, 0.85f);
    }

    private static void DrawPOIDiamond(Vector2 center, float radius, Color color, float width)
    {
        Vector2 top = center + new Vector2(0f, -radius);
        Vector2 right = center + new Vector2(radius, 0f);
        Vector2 bottom = center + new Vector2(0f, radius);
        Vector2 left = center + new Vector2(-radius, 0f);

        DrawLine(top, right, color, width);
        DrawLine(right, bottom, color, width);
        DrawLine(bottom, left, color, width);
        DrawLine(left, top, color, width);
    }

    private void DrawNodes(Rect localRect)
    {
        if (!_showNodes)
            return;

        var g = _generator.graph;

        for (int i = 0; i < g.nodes.Count; i++)
        {
            var n = g.nodes[i];
            Vector2 p = GraphToLocal(n.position, localRect);

            float r = n.isPrimary ? NodeRadiusPx * 1.35f : NodeRadiusPx;

            bool isSelected = i == _selectedNodeIndex;
            bool isLocked = i == _lockedNodeIndex;
            bool isCurrent = IsCurrentNodeIndex(i);

            Color c = GetNodeColor(n, isSelected, isLocked, isCurrent);

            DrawNodeDot(p, r, c);

            if (isSelected)
                DrawNodeRing(p, r + 4f, Color.yellow, 2f);

            if (isLocked)
                DrawNodeRing(p, r + 7f, Color.green, 2f);

            if (isCurrent)
                DrawNodeRing(p, r + 10f, new Color(0.3f, 1f, 1f, 1f), 2f);

            DrawNodeBadges(p, i);
        }
    }

    private void DrawNodeBadges(Vector2 nodePos, int nodeIndex)
    {
        int eventCount = GetEventCount(nodeIndex);
        int buffCount = GetBuffCount(nodeIndex);

        if (eventCount > 0)
        {
            DrawCountBadge(
                nodePos + new Vector2(12f, -18f),
                eventCount,
                new Color(0.35f, 0.55f, 1f, 0.95f)
            );
        }

        if (buffCount > 0)
        {
            DrawCountBadge(
                nodePos + new Vector2(12f, 4f),
                buffCount,
                GetBuffBadgeColor(nodeIndex)
            );
        }
    }

    private int GetEventCount(int nodeIndex)
    {
        if (_eventManager == null)
            return 0;

        return _eventManager.CountEventsAtNode(nodeIndex);
    }

    private int GetBuffCount(int nodeIndex)
    {
        if (_runtimeBinder == null || !_runtimeBinder.IsBuilt)
            return 0;

        if (!_runtimeBinder.Registry.TryGetByIndex(nodeIndex, out var rt) || rt == null || rt.State == null)
            return 0;

        var buffs = rt.State.ActiveBuffs;
        return buffs == null ? 0 : buffs.Count;
    }

    private Color GetBuffBadgeColor(int nodeIndex)
    {
        if (_runtimeBinder == null || !_runtimeBinder.IsBuilt)
            return new Color(0.8f, 0.8f, 0.8f, 0.95f);

        if (!_runtimeBinder.Registry.TryGetByIndex(nodeIndex, out var rt) || rt == null || rt.State == null)
            return new Color(0.8f, 0.8f, 0.8f, 0.95f);

        var buffs = rt.State.ActiveBuffs;
        if (buffs == null || buffs.Count == 0)
            return new Color(0.8f, 0.8f, 0.8f, 0.95f);

        bool hasPositive = false;
        bool hasNegative = false;

        for (int i = 0; i < buffs.Count; i++)
        {
            var b = buffs[i].buff;
            if (b == null) continue;

            if (b.accelPerHour >= 0f)
                hasPositive = true;
            else
                hasNegative = true;
        }

        if (hasPositive && hasNegative)
            return new Color(0.8f, 0.45f, 1f, 0.95f);

        if (hasNegative)
            return new Color(1f, 0.35f, 0.35f, 0.95f);

        return new Color(0.3f, 1f, 0.4f, 0.95f);
    }

    private static void DrawCountBadge(Vector2 center, int count, Color color)
    {
        EnsureWhiteTexture();

        string text = count > 9 ? "9+" : count.ToString();

        Rect bg = new Rect(center.x - 8f, center.y - 8f, 18f, 16f);

        Color old = GUI.color;

        GUI.color = color;
        GUI.DrawTexture(bg, _whiteTex);

        GUI.color = Color.black;
        GUI.Label(new Rect(bg.x + 2f, bg.y - 2f, bg.width, bg.height + 4f), text);

        GUI.color = old;
    }

    #endregion

    #region Viewport Input and Transforms

    private void HandleViewportInput(Rect viewport)
    {
        Event e = Event.current;
        if (e == null)
            return;

        Vector2 mouse = e.mousePosition;
        bool inside = viewport.Contains(mouse);

        if (e.type == EventType.ScrollWheel && inside)
        {
            Vector2 before = ViewportToGraph(mouse, viewport);

            float zoomFactor = e.delta.y > 0f ? 0.9f : 1.1f;
            _zoomPxPerGraphUnit = Mathf.Clamp(_zoomPxPerGraphUnit * zoomFactor, MinZoom, MaxZoom);

            Vector2 after = ViewportToGraph(mouse, viewport);
            _viewCenterGraph += before - after;

            e.Use();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && inside)
        {
            int hitNode = HitTestNode(mouse, viewport);
            if (hitNode >= 0)
            {
                _selectedNodeIndex = hitNode;
                _statusLine = $"Selected node #{hitNode}.";
                e.Use();
                return;
            }

            _dragging = true;
            _lastMousePosition = mouse;
            _dragStartMousePosition = mouse;
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDrag && _dragging)
        {
            Vector2 delta = mouse - _lastMousePosition;
            _lastMousePosition = mouse;

            float invZoom = 1f / Mathf.Max(0.0001f, _zoomPxPerGraphUnit);

            // Keep X behavior the same.
            _viewCenterGraph.x -= delta.x * invZoom;

            // Flip Y only. OnGUI mouse Y increases downward, because of course it does.
            _viewCenterGraph.y += delta.y * invZoom;

            e.Use();
            return;
        }

        if (e.type == EventType.MouseUp && _dragging)
        {
            _dragging = false;
            e.Use();
        }
    }

    private int HitTestNode(Vector2 screenMouse, Rect viewport)
    {
        var g = _generator.graph;
        if (g == null || g.nodes == null)
            return -1;

        Vector2 localMouse = screenMouse - viewport.position;
        Rect localRect = new Rect(0f, 0f, viewport.width, viewport.height);

        int best = -1;
        float bestD = NodeHitRadiusPx * NodeHitRadiusPx;

        for (int i = 0; i < g.nodes.Count; i++)
        {
            Vector2 p = GraphToLocal(g.nodes[i].position, localRect);
            float d = (p - localMouse).sqrMagnitude;

            if (d <= bestD)
            {
                bestD = d;
                best = i;
            }
        }

        return best;
    }

    private Vector2 GraphToLocal(Vector2 graphPos, Rect localRect)
    {
        return new Vector2(
            localRect.width * 0.5f + (graphPos.x - _viewCenterGraph.x) * _zoomPxPerGraphUnit,
            localRect.height * 0.5f - (graphPos.y - _viewCenterGraph.y) * _zoomPxPerGraphUnit
        );
    }

    private Vector2 ViewportToGraph(Vector2 screenPos, Rect viewport)
    {
        Vector2 local = screenPos - viewport.position;

        return new Vector2(
            _viewCenterGraph.x + (local.x - viewport.width * 0.5f) / Mathf.Max(0.0001f, _zoomPxPerGraphUnit),
            _viewCenterGraph.y - (local.y - viewport.height * 0.5f) / Mathf.Max(0.0001f, _zoomPxPerGraphUnit)
        );
    }

    private void FitViewToGraph(Rect viewport)
    {
        var g = _generator.graph;

        Vector2 min = g.nodes[0].position;
        Vector2 max = g.nodes[0].position;

        for (int i = 1; i < g.nodes.Count; i++)
        {
            var p = g.nodes[i].position;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        Vector2 size = max - min;
        if (size.x < 0.001f) size.x = 1f;
        if (size.y < 0.001f) size.y = 1f;

        _viewCenterGraph = (min + max) * 0.5f;

        float zoomX = viewport.width / size.x;
        float zoomY = viewport.height / size.y;

        _zoomPxPerGraphUnit = Mathf.Clamp(Mathf.Min(zoomX, zoomY) * 0.82f, MinZoom, MaxZoom);
        _hasFitView = true;
    }

    #endregion

    #region Selection, Locking, and Travel

    private void SelectCurrentNodeOrDefault()
    {
        _selectedNodeIndex = 0;

        if (_player == null || _player.State == null)
            return;

        string current = _player.State.currentNodeId;
        if (string.IsNullOrEmpty(current))
            return;

        if (_runtimeBinder != null &&
            _runtimeBinder.IsBuilt &&
            _runtimeBinder.Registry.TryGetByStableId(current, out var rt) &&
            rt != null)
        {
            _selectedNodeIndex = rt.NodeIndex;
        }
    }

    private void SyncLockFromPlayerState()
    {
        _lockedStableId = null;
        _lockedNodeIndex = -1;

        if (_player == null || _player.State == null)
            return;

        _lockedStableId = _player.State.lockedDestinationNodeId;

        if (string.IsNullOrEmpty(_lockedStableId))
            return;

        if (_runtimeBinder != null &&
            _runtimeBinder.IsBuilt &&
            _runtimeBinder.Registry.TryGetByStableId(_lockedStableId, out var rt) &&
            rt != null)
        {
            _lockedNodeIndex = rt.NodeIndex;
        }
    }

    private bool CanLockSelected(out string reason)
    {
        reason = string.Empty;

        if (_runtimeBinder == null || !_runtimeBinder.IsBuilt)
        {
            reason = "Runtime map is not built.";
            return false;
        }

        if (_player == null || _player.State == null)
        {
            reason = "Player map state missing.";
            return false;
        }

        if (_generator == null || _generator.graph == null)
        {
            reason = "Graph missing.";
            return false;
        }

        if (_selectedNodeIndex < 0 || _selectedNodeIndex >= _generator.graph.nodes.Count)
        {
            reason = "No valid selected node.";
            return false;
        }

        if (string.IsNullOrEmpty(_player.State.currentNodeId))
        {
            reason = "Current node missing.";
            return false;
        }

        if (!_runtimeBinder.Registry.TryGetByStableId(_player.State.currentNodeId, out var fromRt) || fromRt == null)
        {
            reason = "Could not resolve current node.";
            return false;
        }

        if (!_runtimeBinder.Registry.TryGetByIndex(_selectedNodeIndex, out var toRt) || toRt == null)
        {
            reason = "Could not resolve selected node.";
            return false;
        }

        if (fromRt.NodeIndex == toRt.NodeIndex)
        {
            reason = "Already at this node.";
            return false;
        }

        if (!_generator.graph.HasEdge(fromRt.NodeIndex, toRt.NodeIndex))
        {
            reason = "No direct route edge.";
            return false;
        }

        return true;
    }

    private bool CanTravelNow(out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrEmpty(_lockedStableId))
        {
            reason = "No locked destination.";
            return false;
        }

        var gs = GameState.I;
        if (gs == null)
        {
            reason = "GameState missing.";
            return false;
        }

        if (gs.boatRegistry == null)
        {
            reason = "Boat registry missing.";
            return false;
        }

        string boatId = gs.boat != null ? gs.boat.boatInstanceId : null;
        if (string.IsNullOrEmpty(boatId))
        {
            reason = "Boat instance ID missing.";
            return false;
        }

        if (!gs.boatRegistry.TryGetById(boatId, out var boat) || boat == null)
        {
            reason = "Player boat not registered.";
            return false;
        }

        var boarding = UnityEngine.Object.FindAnyObjectByType<PlayerBoardingState>();
        if (boarding == null)
        {
            reason = "Player boarding state missing.";
            return false;
        }

        if (!boarding.IsBoarded)
        {
            reason = "Player is not boarded.";
            return false;
        }

        if (boarding.CurrentBoatRoot != boat.transform)
        {
            reason = "Player is not boarded to the active boat.";
            return false;
        }

        return true;
    }

    private void ToggleLockSelected()
    {
        if (_player == null || _player.State == null)
            return;

        if (IsSelectedLocked())
        {
            _lockedNodeIndex = -1;
            _lockedStableId = null;

            _player.State.lockedSourceNodeId = null;
            _player.State.lockedDestinationNodeId = null;

            _statusLine = "Destination unlocked.";
            return;
        }

        if (!CanLockSelected(out string reason))
        {
            _statusLine = $"Cannot lock: {reason}";
            return;
        }

        if (!_runtimeBinder.Registry.TryGetByIndex(_selectedNodeIndex, out var rt) || rt == null)
        {
            _statusLine = "Cannot lock: selected runtime missing.";
            return;
        }

        _lockedNodeIndex = rt.NodeIndex;
        _lockedStableId = rt.StableId;

        _player.State.lockedSourceNodeId = _player.State.currentNodeId;
        _player.State.lockedDestinationNodeId = _lockedStableId;

        _statusLine = $"Locked destination: {rt.DisplayName}.";
    }

    private void StartTravel()
    {
        if (_travelLauncher == null)
            _travelLauncher = UnityEngine.Object.FindAnyObjectByType<NodeTravelController>();

        if (_travelLauncher == null)
        {
            _statusLine = "Cannot travel: NodeTravelController missing.";
            return;
        }

        _travelLauncher.TryStartTravel();
        _statusLine = "Travel requested.";
    }

    private bool TryGetSelectedRuntime(out MapNodeRuntime rt)
    {
        rt = null;

        if (_runtimeBinder == null || !_runtimeBinder.IsBuilt)
            return false;

        if (_selectedNodeIndex < 0)
            return false;

        return _runtimeBinder.Registry.TryGetByIndex(_selectedNodeIndex, out rt) && rt != null;
    }

    private bool IsSelectedLocked()
    {
        return _selectedNodeIndex >= 0 &&
               _selectedNodeIndex == _lockedNodeIndex &&
               !string.IsNullOrEmpty(_lockedStableId);
    }

    private bool IsCurrentNodeIndex(int nodeIndex)
    {
        if (_player == null || _player.State == null)
            return false;

        string cur = _player.State.currentNodeId;
        if (string.IsNullOrEmpty(cur))
            return false;

        return _runtimeBinder != null &&
               _runtimeBinder.IsBuilt &&
               _runtimeBinder.Registry.TryGetByStableId(cur, out var rt) &&
               rt != null &&
               rt.NodeIndex == nodeIndex;
    }

    #endregion

    #region Visual Policy

    private Color GetRouteColor(int a, int b)
    {
        if (_runtimeBinder == null || !_runtimeBinder.IsBuilt)
            return new Color(1f, 1f, 1f, 0.18f);

        if (!_runtimeBinder.Registry.TryGetByIndex(a, out var aRt) || aRt == null)
            return new Color(1f, 1f, 1f, 0.18f);

        if (!_runtimeBinder.Registry.TryGetByIndex(b, out var bRt) || bRt == null)
            return new Color(1f, 1f, 1f, 0.18f);

        var playerState = _player != null ? _player.State : null;

        RouteKnowledgeState k = StarMapVisualQuery.GetVisualState(
            playerState,
            aRt.StableId,
            aRt.ClusterId,
            bRt.StableId,
            bRt.ClusterId
        );

        bool locked =
            (a == _lockedNodeIndex && IsCurrentNodeIndex(b)) ||
            (b == _lockedNodeIndex && IsCurrentNodeIndex(a));

        if (locked)
            return new Color(0.2f, 1f, 0.2f, 0.95f);

        return k switch
        {
            RouteKnowledgeState.Known => new Color(1f, 1f, 1f, 0.48f),
            RouteKnowledgeState.Partial => new Color(1f, 0.85f, 0.2f, 0.44f),
            RouteKnowledgeState.Rumored => new Color(0.2f, 0.9f, 1f, 0.32f),
            _ => new Color(1f, 0.25f, 0.25f, 0.16f)
        };
    }

    private float GetRouteThickness(int a, int b)
    {
        bool locked =
            (a == _lockedNodeIndex && IsCurrentNodeIndex(b)) ||
            (b == _lockedNodeIndex && IsCurrentNodeIndex(a));

        return locked ? 3.5f : 1.5f;
    }

    private Color GetNodeColor(MapNode n, bool selected, bool locked, bool current)
    {
        if (_heatmapMode != HeatmapMode.None &&
            TryEvaluateHeatmapColor(n.id, out Color heatColor))
        {
            return heatColor;
        }

        if (current)
            return new Color(0.25f, 1f, 1f, 1f);

        if (locked)
            return new Color(0.25f, 1f, 0.25f, 1f);

        if (selected)
            return new Color(1f, 0.95f, 0.25f, 1f);

        if (n.kind == NodeKind.StartDock)
            return new Color(0.25f, 1f, 0.35f, 1f);

        if (n.kind == NodeKind.Destination)
            return new Color(1f, 0.45f, 0.25f, 1f);

        if (n.isPrimary)
            return new Color(1f, 0.72f, 0.3f, 1f);

        return new Color(0.92f, 0.92f, 0.92f, 1f);
    }

    private bool TryEvaluateHeatmapColor(int nodeIndex, out Color color)
    {
        color = HeatUnknownColor;

        if (_runtimeBinder == null || !_runtimeBinder.IsBuilt)
            return false;

        if (!_runtimeBinder.Registry.TryGetByIndex(nodeIndex, out var rt) || rt == null || rt.State == null)
            return true;

        if (!TryGetHeatmapValue(rt, out float value))
            return true;

        color = EvaluateHeatmapColor(_heatmapMode, value);
        return true;
    }

    private bool TryGetHeatmapValue(MapNodeRuntime rt, out float value)
    {
        value = 0f;

        var s = rt.State;
        if (s == null)
            return false;

        switch (_heatmapMode)
        {
            case HeatmapMode.DockRating:
                value = s.GetStat(NodeStatId.DockRating).value;
                return true;

            case HeatmapMode.TradeRating:
                value = s.GetStat(NodeStatId.TradeRating).value;
                return true;

            case HeatmapMode.Prosperity:
                value = s.GetStat(NodeStatId.Prosperity).value;
                return true;

            case HeatmapMode.Stability:
                value = s.GetStat(NodeStatId.Stability).value;
                return true;

            case HeatmapMode.Security:
                value = s.GetStat(NodeStatId.Security).value;
                return true;

            case HeatmapMode.Population:
                value = s.population;
                return true;

            case HeatmapMode.FoodBalance:
                value = s.GetStat(NodeStatId.FoodBalance).value;
                return true;

            default:
                return false;
        }
    }

    private static Color EvaluateHeatmapColor(HeatmapMode mode, float value)
    {
        switch (mode)
        {
            case HeatmapMode.Population:
                return Color.Lerp(
                    PopulationLow,
                    PopulationHigh,
                    Mathf.InverseLerp(PopulationMin, PopulationMax, value)
                );

            case HeatmapMode.FoodBalance:
                if (value < 0f)
                {
                    return Color.Lerp(
                        FoodNegative,
                        FoodNeutral,
                        Mathf.InverseLerp(FoodMin, 0f, value)
                    );
                }

                return Color.Lerp(
                    FoodNeutral,
                    FoodPositive,
                    Mathf.InverseLerp(0f, FoodMax, value)
                );

            case HeatmapMode.DockRating:
            case HeatmapMode.TradeRating:
            case HeatmapMode.Prosperity:
            case HeatmapMode.Stability:
            case HeatmapMode.Security:
                return Color.Lerp(
                    HeatLowColor,
                    HeatHighColor,
                    Mathf.InverseLerp(HeatMinValue, HeatMaxValue, value)
                );

            default:
                return HeatUnknownColor;
        }
    }

    #endregion

    #region Formatting

    private string SafeNodeKind(int nodeIndex)
    {
        var g = _generator != null ? _generator.graph : null;
        if (g == null || nodeIndex < 0 || nodeIndex >= g.nodes.Count)
            return "(unknown)";

        return g.nodes[nodeIndex].kind.ToString();
    }

    private string BuildRouteInfoText(string lockReason, string travelReason)
    {
        if (_player == null || _player.State == null)
            return "Player map state missing.";

        if (TryGetActiveTravelEndpointRuntimes(out var activeFromRt, out var activeToRt))
        {
            return
                "Active travel:\n" +
                $"{activeFromRt.DisplayName} → {activeToRt.DisplayName}";
        }

        if (!TryGetSelectedRuntime(out var selectedRt))
            return "No selected route.";

        if (!_runtimeBinder.Registry.TryGetByStableId(_player.State.currentNodeId, out var currentRt) || currentRt == null)
            return "Current node unresolved.";

        if (selectedRt.NodeIndex == currentRt.NodeIndex)
            return "This is your current node.";

        if (_generator == null || _generator.graph == null)
            return "Graph missing.";

        bool hasEdge = _generator.graph.HasEdge(currentRt.NodeIndex, selectedRt.NodeIndex);
        if (!hasEdge)
            return "No direct edge to selected node.";

        float routeLen = Vector2.Distance(
            _generator.graph.nodes[currentRt.NodeIndex].position,
            _generator.graph.nodes[selectedRt.NodeIndex].position
        );

        float maxLen = _travelDebug != null
            ? _travelDebug.MaxRouteLength
            : _travelRules != null
                ? _travelRules.maxRouteLength
                : float.NaN;

        bool blocked = RouteAccessPolicy.TryGetBlockReason(
            _player.State,
            currentRt.StableId,
            selectedRt.StableId,
            currentRt.ClusterId,
            selectedRt.ClusterId,
            routeLen,
            maxLen,
            out string policyReason
        );

        if (blocked)
            return $"Route length: {routeLen:0.00}\nBlocked: {policyReason}";

        if (!string.IsNullOrWhiteSpace(travelReason) && IsSelectedLocked())
            return $"Route length: {routeLen:0.00}\nTravel unavailable: {travelReason}";

        if (!string.IsNullOrWhiteSpace(lockReason) && !IsSelectedLocked())
            return $"Route length: {routeLen:0.00}\nLock unavailable: {lockReason}";

        return $"Route length: {routeLen:0.00}\nRoute available.";
    }

    private bool TryGetActiveTravelEndpointRuntimes(
    out MapNodeRuntime fromRt,
    out MapNodeRuntime toRt)
    {
        fromRt = null;
        toRt = null;

        TravelPayload travel = GameState.I != null ? GameState.I.activeTravel : null;
        if (travel == null)
            return false;

        if (string.IsNullOrWhiteSpace(travel.fromNodeStableId))
            return false;

        if (string.IsNullOrWhiteSpace(travel.toNodeStableId))
            return false;

        if (_runtimeBinder == null || !_runtimeBinder.IsBuilt)
            return false;

        bool hasFrom = _runtimeBinder.Registry.TryGetByStableId(travel.fromNodeStableId, out fromRt);
        bool hasTo = _runtimeBinder.Registry.TryGetByStableId(travel.toNodeStableId, out toRt);

        return hasFrom && fromRt != null && hasTo && toRt != null;
    }

    #endregion

    #region Primitive Drawing

    private static void DrawNodeDot(Vector2 center, float radius, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(
            new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f),
            _whiteTex
        );

        GUI.color = old;
    }

    private static void DrawNodeRing(Vector2 center, float radius, Color color, float thickness)
    {
        DrawLine(new Vector2(center.x - radius, center.y - radius), new Vector2(center.x + radius, center.y - radius), color, thickness);
        DrawLine(new Vector2(center.x + radius, center.y - radius), new Vector2(center.x + radius, center.y + radius), color, thickness);
        DrawLine(new Vector2(center.x + radius, center.y + radius), new Vector2(center.x - radius, center.y + radius), color, thickness);
        DrawLine(new Vector2(center.x - radius, center.y + radius), new Vector2(center.x - radius, center.y - radius), color, thickness);
    }

    private static void DrawRectOutline(Rect rect, float thickness)
    {
        DrawLine(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), GUI.color, thickness);
        DrawLine(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), GUI.color, thickness);
        DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), GUI.color, thickness);
        DrawLine(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), GUI.color, thickness);
    }

    private static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
    {
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;

        Vector2 d = b - a;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float length = d.magnitude;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, length, width), _whiteTex);

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private static void EnsureWhiteTexture()
    {
        if (_whiteTex != null)
            return;

        _whiteTex = Texture2D.whiteTexture;
    }

    #endregion

    #region Heatmap Types and Colors

    private enum HeatmapMode
    {
        None,
        Prosperity,
        Stability,
        Security,
        DockRating,
        TradeRating,
        Population,
        FoodBalance
    }

    private HeatmapMode _heatmapMode = HeatmapMode.None;

    private static readonly Color HeatLowColor = new Color(1f, 0.25f, 0.25f, 1f);
    private static readonly Color HeatHighColor = new Color(0.25f, 1f, 0.35f, 1f);
    private static readonly Color HeatUnknownColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    private static readonly Color PopulationLow = new Color(0.15f, 0.15f, 0.35f, 1f);
    private static readonly Color PopulationHigh = new Color(0.55f, 0.75f, 1f, 1f);

    private static readonly Color FoodNegative = new Color(1f, 0.25f, 0.25f, 1f);
    private static readonly Color FoodNeutral = new Color(0.75f, 0.75f, 0.75f, 1f);
    private static readonly Color FoodPositive = new Color(0.25f, 1f, 0.35f, 1f);

    private const float HeatMinValue = 0f;
    private const float HeatMaxValue = 4f;
    private const float PopulationMin = 0f;
    private const float PopulationMax = 5000f;
    private const float FoodMin = -4f;
    private const float FoodMax = 4f;
    #endregion

}