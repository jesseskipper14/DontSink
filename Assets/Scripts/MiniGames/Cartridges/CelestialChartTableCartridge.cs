using System;
using System.Collections.Generic;
using MiniGames;
using UnityEngine;

/// <summary>
/// Phase 6A physical star-chart board.
///
/// Immutable CelestialChartFragmentSnapshot evidence is rendered at one shared celestial/world
/// scale. Mutable placement/rotation/pinning is separate shared crew belief state. The central
/// viewport is the exact same transform used by WorldMapCartridge, allowing direct visual
/// registration without revealing the true answer.
/// </summary>
public sealed class CelestialChartTableCartridge : IMiniGameCartridge, IOverlayRenderable
{
    private enum EditMode
    {
        None,
        Move,
        Rotate
    }

    private readonly GameObject _requester;
    private readonly MapTableViewportState _viewport;
    private readonly WorldMapCartridge _worldReference;
    private readonly CelestialChartBoardRenderCache _renderCache;

    private MiniGameContext _context;
    private bool _compareWorldReference;
    private string _statusLine;
    private string _selectedFragmentId;

    private int _folioPage;
    private string _folioDragFragmentId;

    private bool _panning;
    private Vector2 _lastPanMouse;

    private EditMode _editMode;
    private CelestialChartBoardEditHandle _editHandle;
    private Vector2 _previewCenterWorld;
    private float _previewRotationDegrees;
    private Vector2 _moveOffsetWorld;
    private float _rotatePointerStartAngle;
    private float _rotatePlacementStartDegrees;

    private readonly List<CelestialChartBoardPlacementSnapshot> _sortedPlacements = new();

    private static Texture2D _white;

    public CelestialChartTableCartridge(
        GameObject requester,
        MapTableViewportState viewport,
        WorldMapCartridge worldReference,
        CelestialChartFragmentVisualSettings visualSettings = null)
    {
        _requester = requester;
        _viewport = viewport ?? new MapTableViewportState();
        _worldReference = worldReference;
        _renderCache = new CelestialChartBoardRenderCache(visualSettings);
    }

    public void Begin(MiniGameContext context)
    {
        _context = context ?? new MiniGameContext();
        _statusLine = "Drag a fragment from the folio onto the board. Left-drag scraps to move; right-drag to rotate.";
        _folioPage = 0;
        _folioDragFragmentId = null;
        CancelActiveEdit();
    }

    public MiniGameResult Tick(float dt, MiniGameInput input)
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            _compareWorldReference = !_compareWorldReference;
            _statusLine = _compareWorldReference
                ? "World reference overlay enabled. Paper is ghosted; celestial ink stays strong."
                : "World reference overlay disabled.";
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
        CancelActiveEdit();
        return new MiniGameResult
        {
            outcome = MiniGameOutcome.Cancelled,
            quality01 = 1f,
            note = "Star chart closed.",
            hasMeaningfulProgress = false
        };
    }

    public MiniGameResult Interrupt(string reason)
    {
        CancelActiveEdit();
        return new MiniGameResult
        {
            outcome = MiniGameOutcome.Cancelled,
            quality01 = 1f,
            note = $"Star chart interrupted: {reason}",
            hasMeaningfulProgress = false
        };
    }

    public void End()
    {
        SuspendInteractions();
        CelestialChartBoardAuthority.ReleaseAllEditsForRequester(_requester);
        _context = null;
        _renderCache.Dispose();
    }

    public void SuspendInteractions()
    {
        CancelActiveEdit();
        _folioDragFragmentId = null;
        _panning = false;
    }

    public void DrawOverlayGUI(Rect panel)
    {
        EnsureWhiteTexture();
        CelestialChartStateSnapshot state = ResolveState();
        if (state == null)
        {
            GUI.Box(panel, "Chart state unavailable.");
            return;
        }

        MapTablePageLayout layout = MapTablePageLayout.Compute(panel);
        EnsureViewportInitialized(layout.Viewport);

        DrawFolio(layout.Left, state, layout.Viewport);
        DrawBoard(layout.Viewport, state);
        DrawDetails(layout.Right, state, layout.Viewport);
        DrawFooter(layout.Footer, state);
    }

    private CelestialChartStateSnapshot ResolveState()
    {
        if (GameState.I == null)
            return null;

        GameState.I.EnsureCelestialChartDefaults();
        return GameState.I.celestialCharts;
    }

    private void EnsureViewportInitialized(Rect viewportRect)
    {
        if (_viewport.IsInitialized)
            return;

        if (_worldReference != null && _worldReference.TryGetWorldReferenceBounds(out Rect bounds))
        {
            _viewport.FitToBounds(viewportRect, bounds, 0.82f);
            return;
        }

        _viewport.Set(Vector2.zero, 8f);
    }

    #region Folio

    private void DrawFolio(Rect rect, CelestialChartStateSnapshot state, Rect boardRect)
    {
        DrawPanelBox(rect);

        float x = rect.x + 10f;
        float y = rect.y + 10f;
        float w = rect.width - 20f;

        int fragmentCount = state.fragments != null ? state.fragments.Count : 0;
        int placedCount = state.boardPlacements != null ? state.boardPlacements.Count : 0;

        GUI.Label(new Rect(x, y, w, 22f), "CHART FOLIO");
        y += 23f;
        GUI.Label(new Rect(x, y, w, 34f), $"Fragments: {fragmentCount}\nOn board: {placedCount}");
        y += 40f;

        float rowH = 58f;
        float controlsH = 34f;
        int rowsPerPage = Mathf.Max(1, Mathf.FloorToInt((rect.yMax - y - controlsH - 8f) / rowH));
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(fragmentCount / (float)rowsPerPage));
        _folioPage = Mathf.Clamp(_folioPage, 0, pageCount - 1);

        int start = _folioPage * rowsPerPage;
        int end = Mathf.Min(fragmentCount, start + rowsPerPage);
        Event e = Event.current;

        for (int i = start; i < end; i++)
        {
            CelestialChartFragmentSnapshot fragment = state.fragments[i];
            if (fragment == null)
                continue;

            Rect row = new Rect(x, y, w, rowH - 4f);
            bool placed = state.TryGetPlacement(fragment.fragmentId, out _);
            bool selected = fragment.fragmentId == _selectedFragmentId;

            Color old = GUI.color;
            GUI.color = selected
                ? new Color(0.20f, 0.42f, 0.52f, 0.95f)
                : new Color(0.09f, 0.12f, 0.15f, 0.95f);
            GUI.DrawTexture(row, _white);
            GUI.color = old;

            string shortId = ShortId(fragment.fragmentId);
            GUI.Label(
                new Rect(row.x + 6f, row.y + 4f, row.width - 12f, 20f),
                $"Survey {fragment.surveySequence}  •  {shortId}");
            GUI.Label(
                new Rect(row.x + 6f, row.y + 24f, row.width - 12f, 20f),
                placed
                    ? $"ON BOARD  •  marks {fragment.marks?.Count ?? 0}"
                    : $"DRAG TO BOARD  •  marks {fragment.marks?.Count ?? 0}");

            if (e != null && e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
            {
                _selectedFragmentId = fragment.fragmentId;

                if (!placed)
                {
                    _folioDragFragmentId = fragment.fragmentId;
                    _statusLine = "Drag the fragment onto the central board and release to place it.";
                }
                else
                {
                    CenterViewOnPlacement(state, fragment.fragmentId);
                    _statusLine = "Selected placed fragment.";
                }

                e.Use();
            }

            y += rowH;
        }

        float buttonY = rect.yMax - 30f;
        float half = (w - 6f) * 0.5f;
        GUI.enabled = _folioPage > 0;
        if (GUI.Button(new Rect(x, buttonY, half, 24f), "PREV"))
            _folioPage--;

        GUI.enabled = _folioPage < pageCount - 1;
        if (GUI.Button(new Rect(x + half + 6f, buttonY, half, 24f), "NEXT"))
            _folioPage++;
        GUI.enabled = true;

        // Finalize a folio drag after the page controls have had a chance to consume their events.
        if (!string.IsNullOrWhiteSpace(_folioDragFragmentId) &&
            e != null && e.type == EventType.MouseUp && e.button == 0)
        {
            if (boardRect.Contains(e.mousePosition))
            {
                Vector2 world = _viewport.ScreenToWorld(e.mousePosition, boardRect);
                if (CelestialChartBoardAuthority.TryPlaceFragment(
                        _requester,
                        _folioDragFragmentId,
                        world,
                        0f,
                        out CelestialChartBoardPlacementSnapshot placement,
                        out string reason))
                {
                    _selectedFragmentId = placement.fragmentId;
                    _statusLine = "Fragment placed. Left-drag to move, right-drag to rotate, then pin it when satisfied.";
                }
                else
                {
                    _statusLine = reason;
                }
            }
            else
            {
                _statusLine = "Fragment returned to folio.";
            }

            _folioDragFragmentId = null;
            e.Use();
        }
    }

    #endregion

    #region Board

    private void DrawBoard(Rect rect, CelestialChartStateSnapshot state)
    {
        EnsureViewportInitialized(rect);
        HandleBoardInput(rect, state);

        if (_compareWorldReference && _worldReference != null)
        {
            _worldReference.DrawKnownWorldReference(rect);
        }
        else
        {
            Color old = GUI.color;
            GUI.color = new Color(0.018f, 0.025f, 0.040f, 1f);
            GUI.DrawTexture(rect, _white);
            GUI.color = new Color(0.16f, 0.28f, 0.36f, 1f);
            DrawRectOutline(rect, 1f);
            GUI.color = old;
        }

        Vector2 globalMouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;

        // Clip all paper/ink to the central board. A rotated scrap is allowed to extend beyond
        // the visible board in world space, but it must not vandalize the folio/details panels.
        GUI.BeginGroup(rect);
        Rect localBoard = new Rect(0f, 0f, rect.width, rect.height);

        if (!_compareWorldReference)
            DrawBoardGrid(localBoard);

        BuildSortedPlacements(state);

        // All paper first. Celestial ink is rendered in a second pass so overlapping paper
        // can never erase the star evidence players need for alignment.
        for (int i = 0; i < _sortedPlacements.Count; i++)
            DrawPlacementLayer(localBoard, state, _sortedPlacements[i], paper: true);

        for (int i = 0; i < _sortedPlacements.Count; i++)
            DrawPlacementLayer(localBoard, state, _sortedPlacements[i], paper: false);

        DrawSelectionAndPins(localBoard, state);
        DrawSharedReferenceReticle(localBoard);
        DrawFolioDragGhost(localBoard, state, globalMouse - rect.position);
        GUI.EndGroup();

        if (_compareWorldReference)
        {
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f),
                "COMPARE: known World Map beneath your placed chart evidence");
        }
    }

    private void HandleBoardInput(Rect rect, CelestialChartStateSnapshot state)
    {
        Event e = Event.current;
        if (e == null || !string.IsNullOrWhiteSpace(_folioDragFragmentId))
            return;

        Vector2 mouse = e.mousePosition;
        bool inside = rect.Contains(mouse);

        if (e.type == EventType.ScrollWheel && inside)
        {
            float zoomFactor = e.delta.y > 0f ? 0.9f : 1.1f;
            _viewport.ZoomAroundScreenPoint(mouse, rect, zoomFactor);
            e.Use();
            return;
        }

        if (_editMode != EditMode.None)
        {
            if (e.type == EventType.MouseDrag)
            {
                if (_editMode == EditMode.Move)
                {
                    _previewCenterWorld = _viewport.ScreenToWorld(mouse, rect) + _moveOffsetWorld;
                }
                else if (_editMode == EditMode.Rotate)
                {
                    Vector2 centerPx = _viewport.WorldToScreen(_previewCenterWorld, rect);
                    float currentAngle = ScreenAngleDegrees(centerPx, mouse);
                    float delta = Mathf.DeltaAngle(_rotatePointerStartAngle, currentAngle);
                    _previewRotationDegrees = Mathf.DeltaAngle(0f, _rotatePlacementStartDegrees + delta);
                }

                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp)
            {
                CommitActiveEdit();
                e.Use();
                return;
            }
        }

        if (_panning)
        {
            if (e.type == EventType.MouseDrag)
            {
                Vector2 delta = mouse - _lastPanMouse;
                _lastPanMouse = mouse;
                _viewport.PanByScreenDelta(delta);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp)
            {
                _panning = false;
                e.Use();
                return;
            }
        }

        if (!inside || e.type != EventType.MouseDown)
            return;

        CelestialChartBoardPlacementSnapshot hit = HitTestTopmostPlacement(rect, state, mouse);

        if (e.button == 0)
        {
            if (hit != null)
            {
                _selectedFragmentId = hit.fragmentId;

                if (hit.pinned)
                {
                    _statusLine = "That fragment is pinned. Unpin it before moving it.";
                }
                else
                {
                    BeginEdit(hit, EditMode.Move, rect, mouse);
                }
            }
            else
            {
                _panning = true;
                _lastPanMouse = mouse;
            }

            e.Use();
            return;
        }

        if (e.button == 1 && hit != null)
        {
            _selectedFragmentId = hit.fragmentId;

            if (hit.pinned)
            {
                _statusLine = "That fragment is pinned. Unpin it before rotating it.";
            }
            else
            {
                BeginEdit(hit, EditMode.Rotate, rect, mouse);
            }

            e.Use();
        }
    }

    private void BeginEdit(
        CelestialChartBoardPlacementSnapshot placement,
        EditMode mode,
        Rect boardRect,
        Vector2 mouse)
    {
        if (!CelestialChartBoardAuthority.TryBeginEdit(
                _requester,
                placement.fragmentId,
                out _editHandle,
                out CelestialChartBoardPlacementSnapshot authoritativePlacement,
                out string reason))
        {
            _statusLine = reason;
            return;
        }

        _editMode = mode;
        _previewCenterWorld = authoritativePlacement.boardCenterWorld;
        _previewRotationDegrees = authoritativePlacement.rotationDegrees;

        if (mode == EditMode.Move)
        {
            _moveOffsetWorld =
                authoritativePlacement.boardCenterWorld -
                _viewport.ScreenToWorld(mouse, boardRect);
            _statusLine = "Moving fragment. Release to commit shared board state.";
        }
        else
        {
            Vector2 centerPx = _viewport.WorldToScreen(authoritativePlacement.boardCenterWorld, boardRect);
            _rotatePointerStartAngle = ScreenAngleDegrees(centerPx, mouse);
            _rotatePlacementStartDegrees = authoritativePlacement.rotationDegrees;
            _statusLine = "Rotating fragment. Release to commit shared board state.";
        }
    }

    private void CommitActiveEdit()
    {
        if (_editMode == EditMode.None || !_editHandle.IsValid)
        {
            ClearEditPreview();
            return;
        }

        if (CelestialChartBoardAuthority.TryCommitEdit(
                _requester,
                _editHandle,
                _previewCenterWorld,
                _previewRotationDegrees,
                bringToFront: true,
                out CelestialChartBoardPlacementSnapshot placement,
                out string reason))
        {
            _selectedFragmentId = placement.fragmentId;
            _statusLine = _editMode == EditMode.Rotate
                ? "Fragment rotation committed."
                : "Fragment position committed.";
        }
        else
        {
            _statusLine = reason;
            CelestialChartBoardAuthority.CancelEdit(_requester, _editHandle);
        }

        ClearEditPreview();
    }

    private void CancelActiveEdit()
    {
        if (_editHandle.IsValid)
            CelestialChartBoardAuthority.CancelEdit(_requester, _editHandle);

        ClearEditPreview();
    }

    private void ClearEditPreview()
    {
        _editMode = EditMode.None;
        _editHandle = default;
        _previewCenterWorld = Vector2.zero;
        _previewRotationDegrees = 0f;
        _moveOffsetWorld = Vector2.zero;
        _rotatePointerStartAngle = 0f;
        _rotatePlacementStartDegrees = 0f;
    }

    private void BuildSortedPlacements(CelestialChartStateSnapshot state)
    {
        _sortedPlacements.Clear();
        if (state.boardPlacements == null)
            return;

        for (int i = 0; i < state.boardPlacements.Count; i++)
        {
            CelestialChartBoardPlacementSnapshot p = state.boardPlacements[i];
            if (p != null)
                _sortedPlacements.Add(p);
        }

        _sortedPlacements.Sort((a, b) =>
        {
            int order = a.layerOrder.CompareTo(b.layerOrder);
            if (order != 0)
                return order;
            return string.CompareOrdinal(a.fragmentId, b.fragmentId);
        });
    }

    private CelestialChartBoardPlacementSnapshot HitTestTopmostPlacement(
        Rect boardRect,
        CelestialChartStateSnapshot state,
        Vector2 mouse)
    {
        BuildSortedPlacements(state);

        for (int i = _sortedPlacements.Count - 1; i >= 0; i--)
        {
            CelestialChartBoardPlacementSnapshot placement = _sortedPlacements[i];
            if (!TryGetFragment(state, placement.fragmentId, out CelestialChartFragmentSnapshot fragment))
                continue;

            CelestialChartFragmentVisual visual = _renderCache.Get(fragment);
            if (visual == null)
                continue;

            Vector2 center = _viewport.WorldToScreen(GetRenderCenter(placement), boardRect);
            float width = visual.RotatedCelestialBounds.width * _viewport.PixelsPerWorldUnit;
            float height = visual.RotatedCelestialBounds.height * _viewport.PixelsPerWorldUnit;
            float rotation = GetRenderRotation(placement);

            Vector2 local = RotateScreenVector(mouse - center, -rotation);
            if (Mathf.Abs(local.x) <= width * 0.5f && Mathf.Abs(local.y) <= height * 0.5f)
                return placement;
        }

        return null;
    }

    private void DrawPlacementLayer(
        Rect boardRect,
        CelestialChartStateSnapshot state,
        CelestialChartBoardPlacementSnapshot placement,
        bool paper)
    {
        if (!TryGetFragment(state, placement.fragmentId, out CelestialChartFragmentSnapshot fragment))
            return;

        CelestialChartFragmentVisual visual = _renderCache.Get(fragment);
        if (visual == null)
            return;

        Rect screenRect = GetPlacementLocalRect(boardRect, placement, visual);
        float rotation = GetRenderRotation(placement);

        Texture2D texture = paper ? visual.PaperTexture : visual.InkTexture;
        if (texture == null)
            return;

        float alpha = paper
            ? (_compareWorldReference ? 0.58f : 0.97f)
            : 1f;

        DrawRotatedTexture(screenRect, texture, rotation, alpha);
    }

    private Rect GetPlacementLocalRect(
        Rect localBoardRect,
        CelestialChartBoardPlacementSnapshot placement,
        CelestialChartFragmentVisual visual)
    {
        Vector2 center = _viewport.WorldToLocal(GetRenderCenter(placement), localBoardRect);
        float width = visual.RotatedCelestialBounds.width * _viewport.PixelsPerWorldUnit;
        float height = visual.RotatedCelestialBounds.height * _viewport.PixelsPerWorldUnit;
        return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
    }

    private Rect GetPlacementScreenRect(
        Rect boardRect,
        CelestialChartBoardPlacementSnapshot placement,
        CelestialChartFragmentVisual visual)
    {
        Vector2 center = _viewport.WorldToScreen(GetRenderCenter(placement), boardRect);
        float width = visual.RotatedCelestialBounds.width * _viewport.PixelsPerWorldUnit;
        float height = visual.RotatedCelestialBounds.height * _viewport.PixelsPerWorldUnit;
        return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
    }

    private Vector2 GetRenderCenter(CelestialChartBoardPlacementSnapshot placement)
    {
        if (_editMode != EditMode.None && _editHandle.fragmentId == placement.fragmentId)
            return _previewCenterWorld;

        return placement.boardCenterWorld;
    }

    private float GetRenderRotation(CelestialChartBoardPlacementSnapshot placement)
    {
        if (_editMode != EditMode.None && _editHandle.fragmentId == placement.fragmentId)
            return _previewRotationDegrees;

        return placement.rotationDegrees;
    }

    private void DrawSelectionAndPins(Rect boardRect, CelestialChartStateSnapshot state)
    {
        BuildSortedPlacements(state);

        for (int i = 0; i < _sortedPlacements.Count; i++)
        {
            CelestialChartBoardPlacementSnapshot placement = _sortedPlacements[i];
            if (!TryGetFragment(state, placement.fragmentId, out CelestialChartFragmentSnapshot fragment))
                continue;

            CelestialChartFragmentVisual visual = _renderCache.Get(fragment);
            if (visual == null)
                continue;

            Rect screenRect = GetPlacementLocalRect(boardRect, placement, visual);
            float rotation = GetRenderRotation(placement);

            if (placement.fragmentId == _selectedFragmentId)
            {
                DrawRotatedOutline(
                    screenRect,
                    rotation,
                    2f,
                    new Color(0.30f, 0.95f, 1f, 0.92f));
            }

            if (placement.pinned)
            {
                Vector2 center = screenRect.center;
                DrawDisc(center, 6f, new Color(0.92f, 0.32f, 0.22f, 0.95f));
                DrawDisc(center, 2f, new Color(1f, 0.86f, 0.48f, 1f));
            }
        }
    }

    private void DrawFolioDragGhost(
        Rect localBoardRect,
        CelestialChartStateSnapshot state,
        Vector2 localMouse)
    {
        if (string.IsNullOrWhiteSpace(_folioDragFragmentId))
            return;

        if (!TryGetFragment(state, _folioDragFragmentId, out CelestialChartFragmentSnapshot fragment))
            return;

        CelestialChartFragmentVisual visual = _renderCache.Get(fragment);
        if (visual == null)
            return;

        float width = visual.RotatedCelestialBounds.width * _viewport.PixelsPerWorldUnit;
        float height = visual.RotatedCelestialBounds.height * _viewport.PixelsPerWorldUnit;
        Rect r = new Rect(localMouse.x - width * 0.5f, localMouse.y - height * 0.5f, width, height);

        bool inside = localBoardRect.Contains(localMouse);
        DrawRotatedTexture(r, visual.PaperTexture, 0f, inside ? 0.62f : 0.30f);
        DrawRotatedTexture(r, visual.InkTexture, 0f, 0.85f);
    }

    private void DrawBoardGrid(Rect rect)
    {
        float zoom = Mathf.Max(0.0001f, _viewport.PixelsPerWorldUnit);
        float spacing = Mathf.Clamp(zoom, 18f, 80f);
        float offsetX = Mathf.Repeat((-_viewport.CenterWorld.x * zoom) + rect.width * 0.5f, spacing);
        float offsetY = Mathf.Repeat((_viewport.CenterWorld.y * zoom) + rect.height * 0.5f, spacing);

        Color c = new Color(1f, 1f, 1f, 0.045f);
        for (float x = rect.x + offsetX; x < rect.xMax; x += spacing)
            DrawLine(new Vector2(x, rect.y), new Vector2(x, rect.yMax), c, 1f);

        for (float y = rect.y + offsetY; y < rect.yMax; y += spacing)
            DrawLine(new Vector2(rect.x, y), new Vector2(rect.xMax, y), c, 1f);
    }

    private static void DrawSharedReferenceReticle(Rect rect)
    {
        Vector2 c = rect.center;
        Color color = new Color(0.30f, 0.95f, 1f, 0.70f);
        DrawLine(c + Vector2.left * 11f, c + Vector2.left * 3f, color, 1f);
        DrawLine(c + Vector2.right * 3f, c + Vector2.right * 11f, color, 1f);
        DrawLine(c + Vector2.up * 11f, c + Vector2.up * 3f, color, 1f);
        DrawLine(c + Vector2.down * 3f, c + Vector2.down * 11f, color, 1f);
        DrawRing(c, 4f, color, 1f);
    }

    #endregion

    #region Details / controls

    private void DrawDetails(Rect rect, CelestialChartStateSnapshot state, Rect boardRect)
    {
        DrawPanelBox(rect);

        float x = rect.x + 10f;
        float y = rect.y + 10f;
        float w = rect.width - 20f;

        GUI.Label(new Rect(x, y, w, 22f), "STAR CHART BOARD");
        y += 28f;

        string compareLabel = _compareWorldReference
            ? "COMPARE WORLD: ON"
            : "COMPARE WORLD: OFF";

        if (GUI.Button(new Rect(x, y, w, 28f), compareLabel))
        {
            _compareWorldReference = !_compareWorldReference;
            _statusLine = _compareWorldReference
                ? "Known World Map registered beneath your chart evidence."
                : "World reference overlay disabled.";
        }
        y += 34f;

        GUI.Label(
            new Rect(x, y, w, 78f),
            "World Map and Star Chart share the exact same center and scale.\n\n" +
            "In COMPARE, the small circle + center dot on a scrap is the observation datum. Wherever that mark lands is your theoretical geographic position.");
        y += 86f;

        if (string.IsNullOrWhiteSpace(_selectedFragmentId) ||
            !TryGetFragment(state, _selectedFragmentId, out CelestialChartFragmentSnapshot fragment))
        {
            GUI.Label(new Rect(x, y, w, 54f), "No fragment selected.\nDrag one from the folio or click one on the board.");
            return;
        }

        state.TryGetPlacement(fragment.fragmentId, out CelestialChartBoardPlacementSnapshot placement);

        GUI.Label(new Rect(x, y, w, 22f), $"Selected: Survey {fragment.surveySequence}");
        y += 22f;
        GUI.Label(new Rect(x, y, w, 36f), $"{ShortId(fragment.fragmentId)}\nMarks: {fragment.marks?.Count ?? 0}");
        y += 42f;

        if (placement == null)
        {
            GUI.Label(new Rect(x, y, w, 52f), "This fragment is still in the folio.\nDrag its row onto the board to place it.");
            return;
        }

        GUI.Label(
            new Rect(x, y, w, 42f),
            $"Rotation: {placement.rotationDegrees:0.0}°\n" +
            $"State: {(placement.pinned ? "PINNED" : "LOOSE")}   Rev {placement.revision}");
        y += 48f;

        if (GUI.Button(new Rect(x, y, w, 28f), placement.pinned ? "UNPIN FRAGMENT" : "PIN FRAGMENT"))
        {
            bool wasPinned = placement.pinned;
            if (CelestialChartBoardAuthority.TrySetPinned(
                    _requester,
                    placement.fragmentId,
                    placement.revision,
                    !wasPinned,
                    out _,
                    out string reason))
            {
                _statusLine = wasPinned ? "Fragment unpinned." : "Fragment pinned in place.";
            }
            else
            {
                _statusLine = reason;
            }
        }
        y += 34f;

        GUI.enabled = !placement.pinned && _editMode == EditMode.None;
        float quarter = (w - 9f) * 0.25f;
        DrawRotateButton(new Rect(x, y, quarter, 26f), "-15°", placement, -15f);
        DrawRotateButton(new Rect(x + quarter + 3f, y, quarter, 26f), "-1°", placement, -1f);
        DrawRotateButton(new Rect(x + (quarter + 3f) * 2f, y, quarter, 26f), "+1°", placement, 1f);
        DrawRotateButton(new Rect(x + (quarter + 3f) * 3f, y, quarter, 26f), "+15°", placement, 15f);
        GUI.enabled = true;
        y += 32f;

        if (GUI.Button(new Rect(x, y, w, 26f), "BRING TO FRONT"))
        {
            if (CelestialChartBoardAuthority.TryBringToFront(
                    _requester,
                    placement.fragmentId,
                    placement.revision,
                    out _,
                    out string reason))
            {
                _statusLine = "Fragment brought to front.";
            }
            else
            {
                _statusLine = reason;
            }
        }
        y += 32f;

        if (GUI.Button(new Rect(x, y, w, 26f), "CENTER VIEW ON FRAGMENT"))
        {
            _viewport.Set(placement.boardCenterWorld, _viewport.PixelsPerWorldUnit);
            _statusLine = "View centered on selected fragment.";
        }
        y += 32f;

        GUI.enabled = !placement.pinned && _editMode == EditMode.None;
        if (GUI.Button(new Rect(x, y, w, 26f), "RETURN TO FOLIO"))
        {
            if (CelestialChartBoardAuthority.TryReturnToFolio(
                    _requester,
                    placement.fragmentId,
                    placement.revision,
                    out string reason))
            {
                _statusLine = "Fragment returned to folio.";
            }
            else
            {
                _statusLine = reason;
            }
        }
        GUI.enabled = true;

        y += 38f;
        GUI.Label(
            new Rect(x, y, w, Mathf.Max(40f, rect.yMax - y - 8f)),
            "Controls\n" +
            "• Left-drag loose scrap: move\n" +
            "• Right-drag loose scrap: free rotate\n" +
            "• Left-drag empty board: pan\n" +
            "• Mouse wheel: zoom both map pages\n" +
            "• C: toggle world comparison");
    }

    private void DrawRotateButton(
        Rect rect,
        string label,
        CelestialChartBoardPlacementSnapshot placement,
        float delta)
    {
        if (!GUI.Button(rect, label))
            return;

        if (CelestialChartBoardAuthority.TryRotateBy(
                _requester,
                placement.fragmentId,
                placement.revision,
                delta,
                out _,
                out string reason))
        {
            _statusLine = $"Fragment rotated {delta:+0;-0;0}°.";
        }
        else
        {
            _statusLine = reason;
        }
    }

    private void DrawFooter(Rect rect, CelestialChartStateSnapshot state)
    {
        string status = string.IsNullOrWhiteSpace(_statusLine)
            ? "Arrange scraps by matching stars. The game does not auto-snap or correct your belief."
            : _statusLine;

        GUI.Label(
            rect,
            $"{status}   •   Board rev {state.boardRevision}");
    }

    #endregion

    #region Helpers

    private void CenterViewOnPlacement(CelestialChartStateSnapshot state, string fragmentId)
    {
        if (state.TryGetPlacement(fragmentId, out CelestialChartBoardPlacementSnapshot placement) && placement != null)
            _viewport.Set(placement.boardCenterWorld, _viewport.PixelsPerWorldUnit);
    }

    private static bool TryGetFragment(
        CelestialChartStateSnapshot state,
        string fragmentId,
        out CelestialChartFragmentSnapshot fragment)
    {
        fragment = null;
        if (state == null || state.fragments == null || string.IsNullOrWhiteSpace(fragmentId))
            return false;

        for (int i = 0; i < state.fragments.Count; i++)
        {
            CelestialChartFragmentSnapshot candidate = state.fragments[i];
            if (candidate != null && candidate.fragmentId == fragmentId)
            {
                fragment = candidate;
                return true;
            }
        }

        return false;
    }

    private static string ShortId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "(no id)";

        return id.Length <= 8 ? id : id.Substring(0, 8);
    }

    private static float ScreenAngleDegrees(Vector2 center, Vector2 point)
    {
        Vector2 d = point - center;
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }

    private static Vector2 RotateScreenVector(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r);
        float s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private static void DrawRotatedTexture(Rect rect, Texture texture, float degrees, float alpha)
    {
        if (texture == null || rect.width <= 0.1f || rect.height <= 0.1f)
            return;

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        GUIUtility.RotateAroundPivot(degrees, rect.center);
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        GUI.color = oldColor;
        GUI.matrix = oldMatrix;
    }

    private static void DrawRotatedOutline(Rect rect, float degrees, float thickness, Color color)
    {
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        GUIUtility.RotateAroundPivot(degrees, rect.center);
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _white);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _white);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _white);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _white);
        GUI.color = oldColor;
        GUI.matrix = oldMatrix;
    }

    private static void DrawPanelBox(Rect rect)
    {
        Color old = GUI.color;
        GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.92f);
        GUI.DrawTexture(rect, _white);
        GUI.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        DrawRectOutline(rect, 1f);
        GUI.color = old;
    }

    private static void DrawRectOutline(Rect r, float t)
    {
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), _white);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), _white);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), _white);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), _white);
    }

    private static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
    {
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;

        Vector2 delta = b - a;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float length = delta.magnitude;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, length, width), _white);

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private static void DrawDisc(Vector2 center, float radius, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(
            new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f),
            _white);
        GUI.color = old;
    }

    private static void DrawRing(Vector2 center, float radius, Color color, float width)
    {
        const int segments = 20;
        Vector2 prev = center + new Vector2(radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector2 next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            DrawLine(prev, next, color, width);
            prev = next;
        }
    }

    private static void EnsureWhiteTexture()
    {
        if (_white == null)
            _white = Texture2D.whiteTexture;
    }

    #endregion
}
