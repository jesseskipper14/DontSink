using MiniGames;
using UnityEngine;

public enum MapTablePage
{
    WorldMap = 0,
    StarChart = 1,
    Charts = 2
}

/// <summary>
/// Parent cartridge for the physical map table. It owns shared chrome, tab switching,
/// requester/session identity, and the single shared viewport transform. Child cartridges
/// remain responsible for their own domain UI and gameplay.
/// </summary>
public sealed class MapTableCartridge : IMiniGameCartridge, IOverlayRenderable
{
    private readonly WorldMapCartridge _worldMap;
    private readonly CelestialChartTableCartridge _starChart;
    private readonly MapTableViewportState _viewport;
    private readonly GameObject _requester;
    private readonly float _tableBorderWorldUnits;

    private MapTablePhysicalPieceEditHandle _pieceEditHandle;
    private string _selectedPieceId;
    private Vector2 _piecePreviewWorld;
    private Vector2 _pieceDragOffsetWorld;

    private MiniGameContext _context;
    private bool _requestedClose;
    private MapTablePage _activePage;

    private static Texture2D _white;

    public MapTablePage ActivePage => _activePage;
    public MapTableViewportState Viewport => _viewport;
    public GameObject Requester => _requester;
    public WorldMapOverlayRunner Runner { get; }
    private readonly CartographicChartFolio _chartFolio = new();
    public bool TryGetCartographicContext(out WorldMapKnowledgeSource source, out WorldMapTopographyField field) =>
        _worldMap.TryGetCartographicContext(out source, out field);
    public void BeginCartographicReveal(WorldMapKnowledgeSaveSnapshot previous) =>
        _worldMap.BeginCartographicReveal(previous, Runner != null ? Runner.CartographicRevealSeconds : 2f);

    public void SetActivePage(MapTablePage page)
    {
        SwitchPage(page);
    }

    public MapTableCartridge(
        GameObject requester,
        WorldMapCartridge worldMap,
        CelestialChartTableCartridge starChart,
        MapTableViewportState viewport,
        MapTablePage initialPage = MapTablePage.WorldMap,
        float tableBorderWorldUnits = 18f,
        WorldMapOverlayRunner runner = null)
    {
        _requester = requester;
        Runner = runner;
        _worldMap = worldMap;
        _starChart = starChart;
        _viewport = viewport ?? new MapTableViewportState();
        _activePage = initialPage;
        _tableBorderWorldUnits = Mathf.Max(0f, tableBorderWorldUnits);
    }

    public void Begin(MiniGameContext context)
    {
        _context = context ?? new MiniGameContext();
        _requestedClose = false;

        // Both children begin once and persist their UI/session state across tab switches.
        // Only the active child is ticked and rendered.
        _worldMap?.Begin(_context);
        _starChart?.Begin(_context);
    }

    public MiniGameResult Tick(float dt, MiniGameInput input)
    {
        if (_requestedClose)
        {
            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Cancelled,
                quality01 = 1f,
                note = "Map table closed.",
                hasMeaningfulProgress = false
            };
        }

        IMiniGameCartridge active = GetActiveCartridge();
        MiniGameResult child = active != null ? active.Tick(dt, input) : null;

        // Embedded child pages are not expected to close the parent themselves, but preserve
        // meaningful child results if one ever does request completion/interruption later.
        if (child != null && child.outcome != MiniGameOutcome.None)
            return child;

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
            note = "Map table cancelled.",
            hasMeaningfulProgress = false
        };
    }

    public MiniGameResult Interrupt(string reason)
    {
        return new MiniGameResult
        {
            outcome = MiniGameOutcome.Cancelled,
            quality01 = 1f,
            note = $"Map table interrupted: {reason}",
            hasMeaningfulProgress = false
        };
    }

    public void End()
    {
        if (_pieceEditHandle.IsValid)
            MapTablePhysicalPieceAuthority.CancelEdit(_requester, _pieceEditHandle);
        MapTablePhysicalPieceAuthority.ReleaseAllEditsForRequester(_requester);

        _worldMap?.End();
        _starChart?.End();
        _context = null;
    }

    public void DrawOverlayGUI(Rect panel)
    {
        EnsureWhiteTexture();
        DrawBackground(panel);

        const float pad = 10f;
        const float topH = 40f;
        const float tabW = 126f;
        const float gap = 6f;

        Rect top = new Rect(
            panel.x + pad,
            panel.y + 6f,
            panel.width - pad * 2f,
            topH);

        GUI.Label(
            new Rect(top.x + 4f, top.y + 6f, 150f, 24f),
            "MAP TABLE");

        float tabsX = top.x + 158f;
        DrawTab(
            new Rect(tabsX, top.y + 4f, tabW, 28f),
            MapTablePage.WorldMap,
            "WORLD MAP");

        DrawTab(
            new Rect(tabsX + tabW + gap, top.y + 4f, tabW, 28f),
            MapTablePage.StarChart,
            "STAR CHART");

        DrawTab(new Rect(tabsX + (tabW + gap) * 2f, top.y + 4f, tabW, 28f), MapTablePage.Charts, "CHARTS");

        if (GUI.Button(new Rect(top.xMax - 32f, top.y + 3f, 30f, 28f), "X"))
            _requestedClose = true;

        Rect pageRect = new Rect(
            panel.x + 4f,
            panel.y + topH + 8f,
            panel.width - 8f,
            panel.height - topH - 12f);

        MapTablePageLayout layout = MapTablePageLayout.Compute(pageRect);
        if (_activePage == MapTablePage.Charts)
        {
            _chartFolio.Draw(pageRect, this);
            return;
        }
        HandlePhysicalPieceInput(layout.Viewport);

        IOverlayRenderable renderable = GetActiveCartridge() as IOverlayRenderable;
        renderable?.DrawOverlayGUI(pageRect);

        // The physical table is anchored to the SAME shared world-space plane as both pages.
        DrawWorldAnchoredTableSurface(layout.Viewport);
        DrawPhysicalPieces(layout.Viewport);
    }

    private void HandlePhysicalPieceInput(Rect viewport)
    {
        if (_viewport == null || !_viewport.IsInitialized || Event.current == null)
            return;

        CelestialChartStateSnapshot state = ResolveTableState();
        if (state == null || state.tablePieces == null)
            return;

        Event e = Event.current;
        Vector2 mouse = e.mousePosition;

        if (_pieceEditHandle.IsValid)
        {
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                _piecePreviewWorld = ClampToPhysicalTable(
                    _viewport.ScreenToWorld(mouse, viewport) + _pieceDragOffsetWorld);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (MapTablePhysicalPieceAuthority.TryCommitEdit(
                        _requester,
                        _pieceEditHandle,
                        _piecePreviewWorld,
                        0f,
                        out MapTablePhysicalPieceSnapshot piece,
                        out _))
                {
                    _selectedPieceId = piece.pieceId;
                }
                else
                {
                    MapTablePhysicalPieceAuthority.CancelEdit(_requester, _pieceEditHandle);
                }

                _pieceEditHandle = default;
                e.Use();
                return;
            }
        }

        if (e.type != EventType.MouseDown || e.button != 0 || !viewport.Contains(mouse))
            return;

        MapTablePhysicalPieceSnapshot hit = HitTestPhysicalPiece(state, viewport, mouse);
        if (hit == null)
            return;

        _selectedPieceId = hit.pieceId;
        if (MapTablePhysicalPieceAuthority.TryBeginEdit(
                _requester,
                hit.pieceId,
                out _pieceEditHandle,
                out MapTablePhysicalPieceSnapshot authoritativePiece,
                out _))
        {
            _piecePreviewWorld = authoritativePiece.boardWorldPosition;
            _pieceDragOffsetWorld = authoritativePiece.boardWorldPosition - _viewport.ScreenToWorld(mouse, viewport);
        }

        e.Use();
    }

    private void DrawPhysicalPieces(Rect viewport)
    {
        if (_viewport == null || !_viewport.IsInitialized)
            return;

        CelestialChartStateSnapshot state = ResolveTableState();
        if (state == null || state.tablePieces == null)
            return;

        GUI.BeginGroup(viewport);
        Rect localViewport = new Rect(0f, 0f, viewport.width, viewport.height);

        for (int i = 0; i < state.tablePieces.Count; i++)
        {
            MapTablePhysicalPieceSnapshot piece = state.tablePieces[i];
            if (piece == null)
                continue;

            Vector2 world = _pieceEditHandle.IsValid && _pieceEditHandle.pieceId == piece.pieceId
                ? _piecePreviewWorld
                : piece.boardWorldPosition;

            Vector2 center = _viewport.WorldToLocal(world, localViewport);
            float sizePx = Mathf.Clamp(_viewport.PixelsPerWorldUnit * 5.5f, 13f, 34f);
            if (!new Rect(-sizePx, -sizePx, localViewport.width + sizePx * 2f, localViewport.height + sizePx * 2f).Contains(center))
                continue;

            DrawPhysicalPiece(piece, center, sizePx, piece.pieceId == _selectedPieceId);
        }

        GUI.EndGroup();
    }

    private static void DrawPhysicalPiece(
        MapTablePhysicalPieceSnapshot piece,
        Vector2 center,
        float sizePx,
        bool selected)
    {
        float radius = sizePx * 0.52f;
        Color token = ResolvePhysicalPieceColor(piece.color);
        Color normalEdge = piece.color == MapTablePhysicalPieceColor.Black
            ? new Color(0.72f, 0.72f, 0.72f, 0.95f)
            : new Color(0.10f, 0.10f, 0.10f, 0.92f);
        Color edge = selected
            ? new Color(0.30f, 0.95f, 1f, 1f)
            : normalEdge;

        Rect block = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
        GUI.color = token;
        GUI.DrawTexture(block, _white);
        GUI.color = edge;
        DrawRectOutline(block, selected ? 2f : 1f);
        GUI.color = Color.white;

        if (piece.kind == MapTablePhysicalPieceKind.PlayerBoat)
        {
            Color boatInk = piece.color == MapTablePhysicalPieceColor.Black
                ? new Color(0.92f, 0.92f, 0.92f, 1f)
                : new Color(0.03f, 0.08f, 0.12f, 1f);
            DrawPaperBoatMarker(center, sizePx * 0.34f, boatInk, 1.4f);
        }
    }

    private static Color ResolvePhysicalPieceColor(MapTablePhysicalPieceColor color)
    {
        switch (color)
        {
            case MapTablePhysicalPieceColor.Green:
                return new Color(0.20f, 0.62f, 0.28f, 0.98f);
            case MapTablePhysicalPieceColor.Red:
                return new Color(0.78f, 0.20f, 0.18f, 0.98f);
            case MapTablePhysicalPieceColor.Yellow:
                return new Color(0.92f, 0.76f, 0.16f, 0.98f);
            case MapTablePhysicalPieceColor.White:
                return new Color(0.92f, 0.92f, 0.88f, 0.98f);
            case MapTablePhysicalPieceColor.Black:
                return new Color(0.035f, 0.035f, 0.04f, 0.98f);
            default:
                return new Color(0.18f, 0.46f, 0.84f, 0.98f);
        }
    }

    private static void DrawPaperBoatMarker(Vector2 center, float size, Color lineColor, float lineThickness)
    {
        float s = Mathf.Max(3f, size);
        Vector2 leftDeck = center + new Vector2(-s * 0.92f, s * 0.10f);
        Vector2 rightDeck = center + new Vector2(s * 0.92f, s * 0.10f);
        Vector2 hullLeft = center + new Vector2(-s * 0.55f, s * 0.78f);
        Vector2 hullRight = center + new Vector2(s * 0.55f, s * 0.78f);
        Vector2 mastTop = center + new Vector2(0f, -s * 0.92f);
        Vector2 sailBase = center + new Vector2(-s * 0.20f, s * 0.10f);

        Color old = GUI.color;
        GUI.color = lineColor;
        DrawThinLine(leftDeck, rightDeck, lineThickness);
        DrawThinLine(leftDeck, hullLeft, lineThickness);
        DrawThinLine(hullLeft, hullRight, lineThickness);
        DrawThinLine(hullRight, rightDeck, lineThickness);
        DrawThinLine(sailBase, mastTop, lineThickness);
        DrawThinLine(mastTop, rightDeck, lineThickness);
        GUI.color = old;
    }

    private MapTablePhysicalPieceSnapshot HitTestPhysicalPiece(
        CelestialChartStateSnapshot state,
        Rect viewport,
        Vector2 mouse)
    {
        MapTablePhysicalPieceSnapshot best = null;
        int bestLayer = int.MinValue;
        float radius = Mathf.Clamp(_viewport.PixelsPerWorldUnit * 5.5f, 13f, 34f) * 0.65f;

        for (int i = 0; i < state.tablePieces.Count; i++)
        {
            MapTablePhysicalPieceSnapshot piece = state.tablePieces[i];
            if (piece == null)
                continue;

            Vector2 center = _viewport.WorldToScreen(piece.boardWorldPosition, viewport);
            if (Vector2.Distance(mouse, center) <= radius && piece.layerOrder >= bestLayer)
            {
                best = piece;
                bestLayer = piece.layerOrder;
            }
        }

        return best;
    }

    private Vector2 ClampToPhysicalTable(Vector2 world)
    {
        if (!TryGetTableWorldBounds(out Rect tableBounds))
            return world;

        return new Vector2(
            Mathf.Clamp(world.x, tableBounds.xMin, tableBounds.xMax),
            Mathf.Clamp(world.y, tableBounds.yMin, tableBounds.yMax));
    }

    private bool TryGetTableWorldBounds(out Rect tableWorldBounds)
    {
        tableWorldBounds = default;
        if (_worldMap == null || !_worldMap.TryGetMapTableContentBounds(out Rect contentWorldBounds))
            return false;

        float margin = Mathf.Max(0f, _tableBorderWorldUnits);
        tableWorldBounds = Rect.MinMaxRect(
            contentWorldBounds.xMin - margin,
            contentWorldBounds.yMin - margin,
            contentWorldBounds.xMax + margin,
            contentWorldBounds.yMax + margin);
        return true;
    }

    private static CelestialChartStateSnapshot ResolveTableState()
    {
        if (GameState.I == null)
            return null;

        GameState.I.EnsureCelestialChartDefaults();
        return GameState.I.celestialCharts;
    }

    private IMiniGameCartridge GetActiveCartridge()
    {
        if (_activePage == MapTablePage.Charts) return null;
        return _activePage == MapTablePage.StarChart
            ? _starChart
            : _worldMap;
    }

    private void SwitchPage(MapTablePage page)
    {
        if (_activePage == page)
            return;

        if (_pieceEditHandle.IsValid)
        {
            MapTablePhysicalPieceAuthority.CancelEdit(_requester, _pieceEditHandle);
            _pieceEditHandle = default;
        }

        if (_activePage == MapTablePage.StarChart)
            _starChart?.SuspendInteractions();
        else
            _worldMap?.SuspendEmbeddedInteraction();

        _activePage = page;
    }

    private void DrawTab(Rect rect, MapTablePage page, string label)
    {
        bool active = _activePage == page;
        Color old = GUI.color;
        GUI.color = active
            ? new Color(0.28f, 0.75f, 0.92f, 1f)
            : new Color(0.62f, 0.68f, 0.74f, 1f);

        if (GUI.Button(rect, active ? $"[{label}]" : label))
            SwitchPage(page);

        GUI.color = old;
    }

    private void DrawWorldAnchoredTableSurface(Rect viewport)
    {
        if (_worldMap == null || _viewport == null || !_viewport.IsInitialized)
            return;

        if (!_worldMap.TryGetMapTableContentBounds(out Rect contentWorldBounds))
            return;

        float margin = Mathf.Max(0f, _tableBorderWorldUnits);
        Rect tableWorldBounds = Rect.MinMaxRect(
            contentWorldBounds.xMin - margin,
            contentWorldBounds.yMin - margin,
            contentWorldBounds.xMax + margin,
            contentWorldBounds.yMax + margin);

        GUI.BeginGroup(viewport);
        Rect localViewport = new Rect(0f, 0f, viewport.width, viewport.height);
        Rect contentLocal = WorldRectToLocal(contentWorldBounds, localViewport);
        Rect tableLocal = WorldRectToLocal(tableWorldBounds, localViewport);

        Color old = GUI.color;

        // Outside the finite physical table is the dark surrounding UI/room.
        GUI.color = new Color(0.012f, 0.014f, 0.018f, 0.96f);
        DrawOutsideRect(localViewport, tableLocal);

        // Actual wood surface between the map/chart working rectangle and outer table edge.
        GUI.color = new Color(0.19f, 0.12f, 0.065f, 1f);
        DrawRectDifference(tableLocal, contentLocal);

        DrawWorldAnchoredWoodGrain(localViewport, tableWorldBounds, contentWorldBounds);

        GUI.color = new Color(0.52f, 0.34f, 0.16f, 0.78f);
        DrawRectOutline(tableLocal, 1.5f);
        GUI.color = new Color(0.42f, 0.28f, 0.14f, 0.72f);
        DrawRectOutline(contentLocal, 1f);

        GUI.color = old;
        GUI.EndGroup();
    }

    private void DrawWorldAnchoredWoodGrain(
        Rect localViewport,
        Rect tableWorldBounds,
        Rect contentWorldBounds)
    {
        if (_tableBorderWorldUnits <= 0.01f)
            return;

        float spacingWorld = Mathf.Max(6f, _tableBorderWorldUnits * 0.55f);
        Color old = GUI.color;
        GUI.color = new Color(0.40f, 0.25f, 0.11f, 0.38f);

        float firstX = Mathf.Floor(tableWorldBounds.xMin / spacingWorld) * spacingWorld;
        for (float x = firstX; x <= tableWorldBounds.xMax + spacingWorld; x += spacingWorld)
        {
            Vector2 top = _viewport.WorldToLocal(new Vector2(x, tableWorldBounds.yMax), localViewport);
            Vector2 innerTop = _viewport.WorldToLocal(new Vector2(x, contentWorldBounds.yMax), localViewport);
            Vector2 innerBottom = _viewport.WorldToLocal(new Vector2(x, contentWorldBounds.yMin), localViewport);
            Vector2 bottom = _viewport.WorldToLocal(new Vector2(x, tableWorldBounds.yMin), localViewport);

            DrawThinLine(top, innerTop, 1f);
            DrawThinLine(innerBottom, bottom, 1f);
        }

        float firstY = Mathf.Floor(tableWorldBounds.yMin / spacingWorld) * spacingWorld;
        for (float y = firstY; y <= tableWorldBounds.yMax + spacingWorld; y += spacingWorld)
        {
            Vector2 left = _viewport.WorldToLocal(new Vector2(tableWorldBounds.xMin, y), localViewport);
            Vector2 innerLeft = _viewport.WorldToLocal(new Vector2(contentWorldBounds.xMin, y), localViewport);
            Vector2 innerRight = _viewport.WorldToLocal(new Vector2(contentWorldBounds.xMax, y), localViewport);
            Vector2 right = _viewport.WorldToLocal(new Vector2(tableWorldBounds.xMax, y), localViewport);

            DrawThinLine(left, innerLeft, 1f);
            DrawThinLine(innerRight, right, 1f);
        }

        GUI.color = old;
    }

    private Rect WorldRectToLocal(Rect worldRect, Rect localViewport)
    {
        Vector2 topLeft = _viewport.WorldToLocal(new Vector2(worldRect.xMin, worldRect.yMax), localViewport);
        Vector2 bottomRight = _viewport.WorldToLocal(new Vector2(worldRect.xMax, worldRect.yMin), localViewport);

        return Rect.MinMaxRect(
            Mathf.Min(topLeft.x, bottomRight.x),
            Mathf.Min(topLeft.y, bottomRight.y),
            Mathf.Max(topLeft.x, bottomRight.x),
            Mathf.Max(topLeft.y, bottomRight.y));
    }

    private static void DrawOutsideRect(Rect viewport, Rect inner)
    {
        Rect clipped = IntersectRect(viewport, inner);

        if (clipped.yMin > viewport.yMin)
            GUI.DrawTexture(new Rect(viewport.xMin, viewport.yMin, viewport.width, clipped.yMin - viewport.yMin), _white);
        if (clipped.yMax < viewport.yMax)
            GUI.DrawTexture(new Rect(viewport.xMin, clipped.yMax, viewport.width, viewport.yMax - clipped.yMax), _white);
        if (clipped.xMin > viewport.xMin)
            GUI.DrawTexture(new Rect(viewport.xMin, clipped.yMin, clipped.xMin - viewport.xMin, clipped.height), _white);
        if (clipped.xMax < viewport.xMax)
            GUI.DrawTexture(new Rect(clipped.xMax, clipped.yMin, viewport.xMax - clipped.xMax, clipped.height), _white);
    }

    private static void DrawRectDifference(Rect outer, Rect inner)
    {
        Rect clippedInner = IntersectRect(outer, inner);

        GUI.DrawTexture(new Rect(outer.xMin, outer.yMin, outer.width, Mathf.Max(0f, clippedInner.yMin - outer.yMin)), _white);
        GUI.DrawTexture(new Rect(outer.xMin, clippedInner.yMax, outer.width, Mathf.Max(0f, outer.yMax - clippedInner.yMax)), _white);
        GUI.DrawTexture(new Rect(outer.xMin, clippedInner.yMin, Mathf.Max(0f, clippedInner.xMin - outer.xMin), clippedInner.height), _white);
        GUI.DrawTexture(new Rect(clippedInner.xMax, clippedInner.yMin, Mathf.Max(0f, outer.xMax - clippedInner.xMax), clippedInner.height), _white);
    }

    private static Rect IntersectRect(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);

        if (xMax <= xMin || yMax <= yMin)
            return new Rect(a.xMin, a.yMin, 0f, 0f);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static void DrawRectOutline(Rect rect, float thickness)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _white);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _white);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _white);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _white);
    }

    private static void DrawThinLine(Vector2 a, Vector2 b, float thickness)
    {
        Matrix4x4 oldMatrix = GUI.matrix;
        Vector2 d = b - a;
        float length = d.magnitude;
        if (length <= 0.01f)
            return;

        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, length, thickness), _white);
        GUI.matrix = oldMatrix;
    }

    private static void EnsureWhiteTexture()
    {
        if (_white == null)
            _white = Texture2D.whiteTexture;
    }

    private static void DrawBackground(Rect panel)
    {
        Color old = GUI.color;
        GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.985f);
        GUI.DrawTexture(panel, _white);

        GUI.color = new Color(0.22f, 0.32f, 0.40f, 1f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), _white);
        GUI.DrawTexture(new Rect(panel.x, panel.yMax - 2f, panel.width, 2f), _white);
        GUI.DrawTexture(new Rect(panel.x, panel.y, 2f, panel.height), _white);
        GUI.DrawTexture(new Rect(panel.xMax - 2f, panel.y, 2f, panel.height), _white);
        GUI.color = old;
    }
}
