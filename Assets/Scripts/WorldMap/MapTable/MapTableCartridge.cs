using MiniGames;
using UnityEngine;

public enum MapTablePage
{
    WorldMap = 0,
    StarChart = 1
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

    private MiniGameContext _context;
    private bool _requestedClose;
    private MapTablePage _activePage;

    private static Texture2D _white;

    public MapTablePage ActivePage => _activePage;
    public MapTableViewportState Viewport => _viewport;
    public GameObject Requester => _requester;

    public void SetActivePage(MapTablePage page)
    {
        SwitchPage(page);
    }

    public MapTableCartridge(
        GameObject requester,
        WorldMapCartridge worldMap,
        CelestialChartTableCartridge starChart,
        MapTableViewportState viewport,
        MapTablePage initialPage = MapTablePage.WorldMap)
    {
        _requester = requester;
        _worldMap = worldMap;
        _starChart = starChart;
        _viewport = viewport ?? new MapTableViewportState();
        _activePage = initialPage;
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

        GUI.Label(
            new Rect(tabsX + (tabW + gap) * 2f + 12f, top.y + 7f, 360f, 22f),
            "Same center + scale on both pages");

        if (GUI.Button(new Rect(top.xMax - 32f, top.y + 3f, 30f, 28f), "X"))
            _requestedClose = true;

        Rect pageRect = new Rect(
            panel.x + 4f,
            panel.y + topH + 8f,
            panel.width - 8f,
            panel.height - topH - 12f);

        IOverlayRenderable renderable = GetActiveCartridge() as IOverlayRenderable;
        renderable?.DrawOverlayGUI(pageRect);
    }

    private IMiniGameCartridge GetActiveCartridge()
    {
        return _activePage == MapTablePage.StarChart
            ? _starChart
            : _worldMap;
    }

    private void SwitchPage(MapTablePage page)
    {
        if (_activePage == page)
            return;

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
