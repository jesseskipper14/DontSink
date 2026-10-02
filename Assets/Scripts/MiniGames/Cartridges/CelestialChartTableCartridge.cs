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
public sealed partial class CelestialChartTableCartridge : IMiniGameCartridge, IOverlayRenderable
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
    private readonly CelestialChartRotatedTextureCache _rotatedTextureCache;
    private readonly float _snapPositionTolerancePixels;
    private readonly float _snapRotationToleranceDegrees;
    private readonly float _snapResidualTolerancePixels;
    private readonly int _snapMinimumSharedMarks;

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
    private CelestialChartBoardGroupEditHandle _groupEditHandle;
    private readonly Dictionary<string, Vector2> _groupOriginalCenters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _groupOriginalRotations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _groupPreviewCenters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _groupPreviewRotations = new(StringComparer.Ordinal);
    private Vector2 _groupAnchorOriginalCenter;
    private float _groupAnchorOriginalRotation;
    private Vector2 _previewCenterWorld;
    private float _previewRotationDegrees;
    private Vector2 _moveOffsetWorld;
    private float _rotatePointerStartAngle;
    private float _rotatePlacementStartDegrees;
    private float _snapFeedbackUntil;

    private readonly List<CelestialChartBoardPlacementSnapshot> _sortedPlacements = new();

    private static Texture2D _white;
    private static Texture2D _boardPaperTexture;

    public CelestialChartTableCartridge(
        GameObject requester,
        MapTableViewportState viewport,
        WorldMapCartridge worldReference,
        CelestialChartFragmentVisualSettings visualSettings = null,
        float snapPositionTolerancePixels = 6f,
        float snapRotationToleranceDegrees = 2f,
        float snapResidualTolerancePixels = 1.5f,
        int snapMinimumSharedMarks = 2,
        CelestialFieldSource fieldSource = null)
    {
        _requester = requester;
        _fieldSource = fieldSource;
        _viewport = viewport ?? new MapTableViewportState();
        _worldReference = worldReference;
        _renderCache = new CelestialChartBoardRenderCache(visualSettings);
        _rotatedTextureCache = new CelestialChartRotatedTextureCache();
        _snapPositionTolerancePixels = Mathf.Max(0f, snapPositionTolerancePixels);
        _snapRotationToleranceDegrees = Mathf.Max(0f, snapRotationToleranceDegrees);
        _snapResidualTolerancePixels = Mathf.Max(0f, snapResidualTolerancePixels);
        _snapMinimumSharedMarks = Mathf.Max(2, snapMinimumSharedMarks);
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
        if (Input.GetKeyDown(KeyCode.C) && !AnnotationHasTextFocus())
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
        _rotatedTextureCache.Dispose();
        _renderCache.Dispose();
    }

    public void SuspendInteractions()
    {
        _pendingMarkDragFragmentId = null;
        _annotationTextFocused = false;
        CancelActiveEdit();
        _folioDragFragmentId = null;
        _panning = false;
    }

    public void DrawOverlayGUI(Rect panel)
    {
        _fieldSource?.EnsureField();
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
        _annotationTextFocused = GUI.GetNameOfFocusedControl() == "CelestialName" ||
            GUI.GetNameOfFocusedControl() == "CelestialNote";
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

        _showKnownConstellations = GUI.Toggle(new Rect(x, y, w, 24f), _showKnownConstellations, "Show Known Constellations");
        y += 28f;
        if (GUI.Button(new Rect(x, y, w, 26f), "DEBUG VALIDATE ALL ELIGIBLE"))
            CelestialKnowledgeAuthority.ValidateAllEligibleForDebug(_requester, KnowledgeField, out _statusLine);
        y += 32f;

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
            string fragmentLabel = fragment.isStarterPatch
                ? "Starter Patch"
                : $"Survey {fragment.surveySequence}";
            GUI.Label(
                new Rect(row.x + 6f, row.y + 4f, row.width - 12f, 20f),
                $"{fragmentLabel}  •  {shortId}");
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
        {
            DrawBoardPaper(localBoard);
            DrawBoardGrid(localBoard);
        }

        BuildSortedPlacements(state);

        // All paper first. Celestial ink is rendered in a second pass so overlapping paper
        // can never erase the star evidence players need for alignment.
        for (int i = 0; i < _sortedPlacements.Count; i++)
            DrawPlacementLayer(localBoard, state, _sortedPlacements[i], paper: true);

        for (int i = 0; i < _sortedPlacements.Count; i++)
            DrawPlacementLayer(localBoard, state, _sortedPlacements[i], paper: false);

        DrawCelestialKnowledge(localBoard, state);

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
        BeginPendingMarkDrag(rect, state, e);

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

                UpdateGroupPreviewTransforms();
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

        if (e.button == 0 && TrySelectCelestialSubject(rect, state, mouse))
        {
            e.Use();
            return;
        }
        if (e.button == 0) ClearSubjectSelection();

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
        CelestialChartBoardPlacementSnapshot authoritativePlacement = null;

        if (!string.IsNullOrWhiteSpace(placement.groupId))
        {
            if (!CelestialChartBoardAuthority.TryBeginGroupEdit(
                    _requester,
                    placement.fragmentId,
                    out _groupEditHandle,
                    out List<CelestialChartBoardPlacementSnapshot> groupPlacements,
                    out string groupReason))
            {
                _statusLine = groupReason;
                return;
            }

            _groupOriginalCenters.Clear();
            _groupOriginalRotations.Clear();
            _groupPreviewCenters.Clear();
            _groupPreviewRotations.Clear();

            for (int i = 0; i < groupPlacements.Count; i++)
            {
                CelestialChartBoardPlacementSnapshot member = groupPlacements[i];
                _groupOriginalCenters[member.fragmentId] = member.boardCenterWorld;
                _groupOriginalRotations[member.fragmentId] = member.rotationDegrees;
                if (member.fragmentId == placement.fragmentId)
                    authoritativePlacement = member;
            }
        }
        else
        {
            if (!CelestialChartBoardAuthority.TryBeginEdit(
                    _requester,
                    placement.fragmentId,
                    out _editHandle,
                    out authoritativePlacement,
                    out string reason))
            {
                _statusLine = reason;
                return;
            }
        }

        if (authoritativePlacement == null)
        {
            CancelActiveEdit();
            _statusLine = "The selected chart fragment could not be resolved for editing.";
            return;
        }

        _editMode = mode;
        _previewCenterWorld = authoritativePlacement.boardCenterWorld;
        _previewRotationDegrees = authoritativePlacement.rotationDegrees;
        _groupAnchorOriginalCenter = authoritativePlacement.boardCenterWorld;
        _groupAnchorOriginalRotation = authoritativePlacement.rotationDegrees;
        UpdateGroupPreviewTransforms();

        if (mode == EditMode.Move)
        {
            _moveOffsetWorld =
                authoritativePlacement.boardCenterWorld -
                _viewport.ScreenToWorld(mouse, boardRect);
            _statusLine = _groupEditHandle.IsValid
                ? "Moving assembled chart group. Release to commit all linked scraps together."
                : "Moving fragment. Release to commit shared board state.";
        }
        else
        {
            Vector2 centerPx = _viewport.WorldToScreen(authoritativePlacement.boardCenterWorld, boardRect);
            _rotatePointerStartAngle = ScreenAngleDegrees(centerPx, mouse);
            _rotatePlacementStartDegrees = authoritativePlacement.rotationDegrees;
            _statusLine = _groupEditHandle.IsValid
                ? "Rotating assembled chart group. Release to commit all linked scraps together."
                : "Rotating fragment. Release to commit shared board state.";
        }
    }

    private void CommitActiveEdit()
    {
        if (_editMode == EditMode.None)
        {
            ClearEditPreview();
            return;
        }

        if (_groupEditHandle.IsValid)
        {
            if (CelestialChartBoardAuthority.TryCommitGroupEdit(
                    _requester,
                    _groupEditHandle,
                    _previewCenterWorld,
                    _previewRotationDegrees,
                    bringToFront: true,
                    out List<CelestialChartBoardPlacementSnapshot> groupPlacements,
                    out string groupReason))
            {
                _selectedFragmentId = _groupEditHandle.anchorFragmentId;
                _statusLine = _editMode == EditMode.Rotate
                    ? $"Assembled group rotation committed ({groupPlacements.Count} scraps)."
                    : $"Assembled group moved ({groupPlacements.Count} scraps).";
            }
            else
            {
                _statusLine = groupReason;
                CelestialChartBoardAuthority.CancelGroupEdit(_requester, _groupEditHandle);
            }

            ClearEditPreview();
            return;
        }

        if (!_editHandle.IsValid)
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

        if (_groupEditHandle.IsValid)
            CelestialChartBoardAuthority.CancelGroupEdit(_requester, _groupEditHandle);

        ClearEditPreview();
    }

    private void ClearEditPreview()
    {
        _editMode = EditMode.None;
        _editHandle = default;
        _groupEditHandle = default;
        _groupOriginalCenters.Clear();
        _groupOriginalRotations.Clear();
        _groupPreviewCenters.Clear();
        _groupPreviewRotations.Clear();
        _groupAnchorOriginalCenter = Vector2.zero;
        _groupAnchorOriginalRotation = 0f;
        _previewCenterWorld = Vector2.zero;
        _previewRotationDegrees = 0f;
        _moveOffsetWorld = Vector2.zero;
        _rotatePointerStartAngle = 0f;
        _rotatePlacementStartDegrees = 0f;
    }

    private void UpdateGroupPreviewTransforms()
    {
        if (!_groupEditHandle.IsValid)
            return;

        float deltaRotation = Mathf.DeltaAngle(_groupAnchorOriginalRotation, _previewRotationDegrees);
        foreach (KeyValuePair<string, Vector2> pair in _groupOriginalCenters)
        {
            string fragmentId = pair.Key;
            Vector2 originalCenter = pair.Value;
            float originalRotation = _groupOriginalRotations.TryGetValue(fragmentId, out float r) ? r : 0f;

            if (fragmentId == _groupEditHandle.anchorFragmentId)
            {
                _groupPreviewCenters[fragmentId] = _previewCenterWorld;
                _groupPreviewRotations[fragmentId] = _previewRotationDegrees;
                continue;
            }

            Vector2 local = originalCenter - _groupAnchorOriginalCenter;
            Vector2 rotatedLocal = RotateVector(local, -deltaRotation);
            _groupPreviewCenters[fragmentId] = _previewCenterWorld + rotatedLocal;
            _groupPreviewRotations[fragmentId] = Mathf.DeltaAngle(0f, originalRotation + deltaRotation);
        }
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

        float rotation = GetRenderRotation(placement);
        CelestialChartRotatedTextureCache.RotatedVisual rotated =
            _rotatedTextureCache.Get(
                fragment.fragmentId,
                visual,
                rotation);

        if (rotated == null)
            return;

        Texture2D texture = paper ? rotated.PaperTexture : rotated.InkTexture;
        if (texture == null)
            return;

        Vector2 center = _viewport.WorldToLocal(GetRenderCenter(placement), boardRect);
        float width = rotated.WorldSize.x * _viewport.PixelsPerWorldUnit;
        float height = rotated.WorldSize.y * _viewport.PixelsPerWorldUnit;
        Rect screenRect = new Rect(
            center.x - width * 0.5f,
            center.y - height * 0.5f,
            width,
            height);

        float alpha = paper
            ? (_compareWorldReference ? 0.58f : 0.97f)
            : 1f;

        // Rotation has already been baked into an axis-aligned texture. Normal IMGUI group
        // clipping now works even at extreme zoom, so scraps cannot escape the board viewport.
        DrawTextureClippedByBoard(screenRect, texture, alpha);
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
        if (_editMode != EditMode.None)
        {
            if (_groupEditHandle.IsValid && _groupPreviewCenters.TryGetValue(placement.fragmentId, out Vector2 groupCenter))
                return groupCenter;

            if (_editHandle.IsValid && _editHandle.fragmentId == placement.fragmentId)
                return _previewCenterWorld;
        }

        return placement.boardCenterWorld;
    }

    private float GetRenderRotation(CelestialChartBoardPlacementSnapshot placement)
    {
        if (_editMode != EditMode.None)
        {
            if (_groupEditHandle.IsValid && _groupPreviewRotations.TryGetValue(placement.fragmentId, out float groupRotation))
                return groupRotation;

            if (_editHandle.IsValid && _editHandle.fragmentId == placement.fragmentId)
                return _previewRotationDegrees;
        }

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

            Vector2 center = _viewport.WorldToLocal(GetRenderCenter(placement), boardRect);
            float width = visual.RotatedCelestialBounds.width * _viewport.PixelsPerWorldUnit;
            float height = visual.RotatedCelestialBounds.height * _viewport.PixelsPerWorldUnit;
            float rotation = GetRenderRotation(placement);

            if (placement.fragmentId == _selectedFragmentId)
            {
                DrawClippedRotatedOutline(
                    boardRect,
                    center,
                    width,
                    height,
                    rotation,
                    2f,
                    new Color(0.30f, 0.95f, 1f, 0.92f));

                if (Time.unscaledTime <= _snapFeedbackUntil)
                {
                    DrawClippedRotatedOutline(
                        boardRect,
                        center,
                        width + 5f,
                        height + 5f,
                        rotation,
                        2f,
                        new Color(1f, 0.82f, 0.28f, 0.95f));
                }
            }

            if (placement.pinned && boardRect.Contains(center))
            {
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

    private void DrawBoardPaper(Rect rect)
    {
        EnsureBoardPaperTexture();

        if (_boardPaperTexture == null)
            return;

        float zoom = Mathf.Max(0.0001f, _viewport.PixelsPerWorldUnit);
        float worldWidth = rect.width / zoom;
        float worldHeight = rect.height / zoom;
        float worldLeft = _viewport.CenterWorld.x - worldWidth * 0.5f;
        float worldBottom = _viewport.CenterWorld.y - worldHeight * 0.5f;

        const float tileWorldSize = 18f;
        Rect uv = new Rect(
            worldLeft / tileWorldSize,
            worldBottom / tileWorldSize,
            worldWidth / tileWorldSize,
            worldHeight / tileWorldSize);

        Color old = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTextureWithTexCoords(rect, _boardPaperTexture, uv, true);
        GUI.color = old;
    }

    private void DrawBoardGrid(Rect rect)
    {
        float zoom = Mathf.Max(0.0001f, _viewport.PixelsPerWorldUnit);
        float minorWorld = NiceGridStep(44f / zoom);
        float majorWorld = minorWorld * 5f;

        DrawWorldGridLines(
            rect,
            minorWorld,
            new Color(0.80f, 0.84f, 0.78f, 0.045f),
            1f);

        DrawWorldGridLines(
            rect,
            majorWorld,
            new Color(0.88f, 0.90f, 0.82f, 0.085f),
            1f);

        // Sparse registration dots give the eye something obvious to compare against while
        // panning the whole chart plane versus dragging one loose scrap.
        Vector2 worldMin = _viewport.ScreenToWorld(rect.min, rect);
        Vector2 worldMax = _viewport.ScreenToWorld(rect.max, rect);

        float minX = Mathf.Min(worldMin.x, worldMax.x);
        float maxX = Mathf.Max(worldMin.x, worldMax.x);
        float minY = Mathf.Min(worldMin.y, worldMax.y);
        float maxY = Mathf.Max(worldMin.y, worldMax.y);

        float firstX = Mathf.Floor(minX / majorWorld) * majorWorld;
        float firstY = Mathf.Floor(minY / majorWorld) * majorWorld;
        Color dotColor = new Color(0.88f, 0.90f, 0.82f, 0.12f);

        for (float x = firstX; x <= maxX + majorWorld; x += majorWorld)
        {
            for (float y = firstY; y <= maxY + majorWorld; y += majorWorld)
            {
                Vector2 local = _viewport.WorldToLocal(new Vector2(x, y), rect);
                if (rect.Contains(local))
                    DrawDisc(local, 1.5f, dotColor);
            }
        }
    }

    private void DrawWorldGridLines(
        Rect rect,
        float worldStep,
        Color color,
        float thickness)
    {
        if (worldStep <= 0.0001f)
            return;

        Vector2 worldTopLeft = _viewport.ScreenToWorld(rect.min, rect);
        Vector2 worldBottomRight = _viewport.ScreenToWorld(rect.max, rect);

        float minX = Mathf.Min(worldTopLeft.x, worldBottomRight.x);
        float maxX = Mathf.Max(worldTopLeft.x, worldBottomRight.x);
        float minY = Mathf.Min(worldTopLeft.y, worldBottomRight.y);
        float maxY = Mathf.Max(worldTopLeft.y, worldBottomRight.y);

        float firstX = Mathf.Floor(minX / worldStep) * worldStep;
        for (float x = firstX; x <= maxX + worldStep; x += worldStep)
        {
            float localX = _viewport.WorldToLocal(new Vector2(x, _viewport.CenterWorld.y), rect).x;
            DrawLine(new Vector2(localX, rect.y), new Vector2(localX, rect.yMax), color, thickness);
        }

        float firstY = Mathf.Floor(minY / worldStep) * worldStep;
        for (float y = firstY; y <= maxY + worldStep; y += worldStep)
        {
            float localY = _viewport.WorldToLocal(new Vector2(_viewport.CenterWorld.x, y), rect).y;
            DrawLine(new Vector2(rect.x, localY), new Vector2(rect.xMax, localY), color, thickness);
        }
    }

    private static float NiceGridStep(float targetWorldStep)
    {
        targetWorldStep = Mathf.Max(0.0001f, targetWorldStep);
        float power = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(targetWorldStep)));
        float scaled = targetWorldStep / power;

        float nice;
        if (scaled <= 1f)
            nice = 1f;
        else if (scaled <= 2f)
            nice = 2f;
        else if (scaled <= 5f)
            nice = 5f;
        else
            nice = 10f;

        return nice * power;
    }

    private static void DrawSharedReferenceReticle(Rect rect)
    {
        Vector2 c = rect.center;
        Color color = new Color(0.30f, 0.95f, 1f, 0.78f);
        DrawLine(c + Vector2.left * 12f, c + Vector2.left * 7f, color, 1f);
        DrawLine(c + Vector2.right * 7f, c + Vector2.right * 12f, color, 1f);
        DrawLine(c + Vector2.up * 12f, c + Vector2.up * 7f, color, 1f);
        DrawLine(c + Vector2.down * 7f, c + Vector2.down * 12f, color, 1f);
        DrawRing(c, 6f, new Color(color.r, color.g, color.b, 0.42f), 1f);
        DrawPaperBoatMarker(c, 8f, color, new Color(0.03f, 0.10f, 0.15f, 0.90f), 1.35f);
    }

    #endregion

    #region Details / controls

    private void DrawDetails(Rect rect, CelestialChartStateSnapshot state, Rect boardRect)
    {
        DrawPanelBox(rect);

        float x = rect.x + 10f;
        float y = rect.y + 10f;
        float w = rect.width - 20f;

        if (DrawCelestialSubjectDetails(rect, state)) return;

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
            "In COMPARE, the paper boat printed on a scrap is the observation datum. Wherever that mark lands is your theoretical geographic position.");
        y += 86f;

        if (string.IsNullOrWhiteSpace(_selectedFragmentId) ||
            !TryGetFragment(state, _selectedFragmentId, out CelestialChartFragmentSnapshot fragment))
        {
            GUI.Label(new Rect(x, y, w, 54f), "No fragment selected.\nDrag one from the folio or click one on the board.");
            return;
        }

        state.TryGetPlacement(fragment.fragmentId, out CelestialChartBoardPlacementSnapshot placement);

        GUI.Label(
            new Rect(x, y, w, 22f),
            fragment.isStarterPatch
                ? "Selected: Starter Patch"
                : $"Selected: Survey {fragment.surveySequence}");
        y += 22f;
        GUI.Label(new Rect(x, y, w, 36f), $"{ShortId(fragment.fragmentId)}\nMarks: {fragment.marks?.Count ?? 0}");
        y += 42f;

        if (placement == null)
        {
            GUI.Label(new Rect(x, y, w, 52f), "This fragment is still in the folio.\nDrag its row onto the board to place it.");
            return;
        }

        int groupCount = CountGroupMembers(state, placement.groupId);

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

        GUI.enabled = !placement.pinned && _editMode == EditMode.None;
        if (GUI.Button(new Rect(x, y, w, 28f), "ATTEMPT SNAP"))
            AttemptEvidenceSnap(state, placement, boardRect);
        GUI.enabled = true;
        y += 34f;

        if (groupCount > 1)
        {
            GUI.enabled = _editMode == EditMode.None;
            if (GUI.Button(new Rect(x, y, w, 26f), "DETACH FROM ASSEMBLED GROUP"))
            {
                if (CelestialChartBoardAuthority.TryDetachFromGroup(
                        _requester,
                        placement.fragmentId,
                        placement.revision,
                        out _,
                        out string detachReason))
                {
                    _statusLine = "Fragment detached from its assembled chart group.";
                }
                else
                {
                    _statusLine = detachReason;
                }
            }
            GUI.enabled = true;
            y += 32f;
        }

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

        if (!string.IsNullOrWhiteSpace(placement.groupId))
        {
            if (!CelestialChartBoardAuthority.TryBeginGroupEdit(
                    _requester,
                    placement.fragmentId,
                    out CelestialChartBoardGroupEditHandle handle,
                    out _,
                    out string beginReason))
            {
                _statusLine = beginReason;
                return;
            }

            if (CelestialChartBoardAuthority.TryCommitGroupEdit(
                    _requester,
                    handle,
                    placement.boardCenterWorld,
                    Mathf.DeltaAngle(0f, placement.rotationDegrees + delta),
                    bringToFront: true,
                    out List<CelestialChartBoardPlacementSnapshot> groupPlacements,
                    out string groupReason))
            {
                _statusLine = $"Assembled group rotated {delta:+0;-0;0}° ({groupPlacements.Count} scraps).";
            }
            else
            {
                CelestialChartBoardAuthority.CancelGroupEdit(_requester, handle);
                _statusLine = groupReason;
            }

            return;
        }

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
            ? "Arrange scraps manually. ATTEMPT SNAP only precision-seats an already-close overlap; dragging never snaps automatically."
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

    private static int CountGroupMembers(CelestialChartStateSnapshot state, string groupId)
    {
        if (state == null || state.boardPlacements == null || string.IsNullOrWhiteSpace(groupId))
            return 0;

        int count = 0;
        for (int i = 0; i < state.boardPlacements.Count; i++)
        {
            CelestialChartBoardPlacementSnapshot p = state.boardPlacements[i];
            if (p != null && p.groupId == groupId)
                count++;
        }

        return count;
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

    private void AttemptEvidenceSnap(
        CelestialChartStateSnapshot state,
        CelestialChartBoardPlacementSnapshot placement,
        Rect boardRect)
    {
        if (state == null || placement == null)
            return;

        if (placement.pinned)
        {
            _statusLine = "Unpin the fragment before attempting a snap.";
            return;
        }

        if (!TryFindEvidenceSnap(
                boardRect,
                state,
                placement,
                out Vector2 snappedCenterWorld,
                out float snappedRotationDegrees,
                out int sharedCount,
                out string targetFragmentId,
                out int targetRevision,
                out string reason))
        {
            _statusLine = reason;
            return;
        }

        int snappedRevision;
        if (!string.IsNullOrWhiteSpace(placement.groupId))
        {
            if (!CelestialChartBoardAuthority.TryBeginGroupEdit(
                    _requester,
                    placement.fragmentId,
                    out CelestialChartBoardGroupEditHandle groupHandle,
                    out _,
                    out string beginReason))
            {
                _statusLine = beginReason;
                return;
            }

            if (!CelestialChartBoardAuthority.TryCommitGroupEdit(
                    _requester,
                    groupHandle,
                    snappedCenterWorld,
                    snappedRotationDegrees,
                    bringToFront: true,
                    out List<CelestialChartBoardPlacementSnapshot> groupPlacements,
                    out string commitReason))
            {
                CelestialChartBoardAuthority.CancelGroupEdit(_requester, groupHandle);
                _statusLine = commitReason;
                return;
            }

            snappedRevision = 0;
            for (int i = 0; i < groupPlacements.Count; i++)
            {
                CelestialChartBoardPlacementSnapshot p = groupPlacements[i];
                if (p != null && p.fragmentId == placement.fragmentId)
                {
                    snappedRevision = p.revision;
                    break;
                }
            }
        }
        else
        {
            if (!CelestialChartBoardAuthority.TryBeginEdit(
                    _requester,
                    placement.fragmentId,
                    out CelestialChartBoardEditHandle handle,
                    out _,
                    out string lockReason))
            {
                _statusLine = lockReason;
                return;
            }

            if (!CelestialChartBoardAuthority.TryCommitEdit(
                    _requester,
                    handle,
                    snappedCenterWorld,
                    snappedRotationDegrees,
                    bringToFront: true,
                    out CelestialChartBoardPlacementSnapshot snappedPlacement,
                    out string commitReason))
            {
                CelestialChartBoardAuthority.CancelEdit(_requester, handle);
                _statusLine = commitReason;
                return;
            }

            snappedRevision = snappedPlacement.revision;
        }

        _selectedFragmentId = placement.fragmentId;
        _snapFeedbackUntil = Time.unscaledTime + 0.45f;

        string groupReason = null;
        if (snappedRevision > 0 &&
            CelestialChartBoardAuthority.TryMergeGroupsAfterSnap(
                _requester,
                placement.fragmentId,
                snappedRevision,
                targetFragmentId,
                targetRevision,
                out _,
                out int memberCount,
                out groupReason))
        {
            _statusLine =
                $"Attempt snap succeeded: {sharedCount} shared marks seated. " +
                $"Assembled group now has {memberCount} scraps.";
        }
        else
        {
            _statusLine = string.IsNullOrWhiteSpace(groupReason)
                ? $"Attempt snap succeeded: {sharedCount} shared marks precision-seated."
                : $"Snap seated, but grouping failed: {groupReason}";
        }
    }

    private bool TryFindEvidenceSnap(
        Rect boardRect,
        CelestialChartStateSnapshot state,
        CelestialChartBoardPlacementSnapshot movingPlacement,
        out Vector2 snappedCenterWorld,
        out float snappedRotationDegrees,
        out int bestSharedCount,
        out string targetFragmentId,
        out int targetRevision,
        out string reason)
    {
        snappedCenterWorld = movingPlacement != null ? movingPlacement.boardCenterWorld : Vector2.zero;
        snappedRotationDegrees = movingPlacement != null ? movingPlacement.rotationDegrees : 0f;
        bestSharedCount = 0;
        targetFragmentId = null;
        targetRevision = 0;
        reason = null;

        if (movingPlacement == null || state == null)
        {
            reason = "Attempt snap failed: placement state is unavailable.";
            return false;
        }

        if (!TryGetFragment(state, movingPlacement.fragmentId, out CelestialChartFragmentSnapshot movingFragment))
        {
            reason = "Attempt snap failed: selected fragment evidence is unavailable.";
            return false;
        }

        CelestialChartFragmentVisual movingVisual = _renderCache.Get(movingFragment);
        if (movingVisual == null || movingFragment.marks == null || movingFragment.marks.Count == 0)
        {
            reason = "Attempt snap failed: selected fragment has no usable marks.";
            return false;
        }

        float bestScore = float.MaxValue;
        Vector2 bestCenterScreen = _viewport.WorldToScreen(movingPlacement.boardCenterWorld, boardRect);
        float bestRotation = movingPlacement.rotationDegrees;

        BuildSortedPlacements(state);
        for (int i = 0; i < _sortedPlacements.Count; i++)
        {
            CelestialChartBoardPlacementSnapshot otherPlacement = _sortedPlacements[i];
            if (otherPlacement == null || otherPlacement.fragmentId == movingPlacement.fragmentId)
                continue;

            if (!string.IsNullOrWhiteSpace(movingPlacement.groupId) && otherPlacement.groupId == movingPlacement.groupId)
                continue;

            if (!TryGetFragment(state, otherPlacement.fragmentId, out CelestialChartFragmentSnapshot otherFragment))
                continue;

            CelestialChartFragmentVisual otherVisual = _renderCache.Get(otherFragment);
            if (otherVisual == null || otherFragment.marks == null || otherFragment.marks.Count == 0)
                continue;

            var movingLocalScreen = new List<Vector2>();
            var targetScreen = new List<Vector2>();

            for (int a = 0; a < movingFragment.marks.Count; a++)
            {
                CelestialChartFragmentMark movingMark = movingFragment.marks[a];
                if (movingMark == null || string.IsNullOrWhiteSpace(movingMark.celestialObjectStableId))
                    continue;

                CelestialChartFragmentMark otherMark = FindMarkByStableId(otherFragment, movingMark.celestialObjectStableId);
                if (otherMark == null)
                    continue;

                Vector2 movingLocalWorld = GetMarkFragmentLocalPosition(movingFragment, movingVisual, movingMark);
                movingLocalScreen.Add(new Vector2(
                    movingLocalWorld.x * _viewport.PixelsPerWorldUnit,
                    -movingLocalWorld.y * _viewport.PixelsPerWorldUnit));

                targetScreen.Add(GetMarkScreenPosition(
                    otherFragment,
                    otherVisual,
                    otherMark,
                    otherPlacement,
                    boardRect));
            }

            int sharedCount = movingLocalScreen.Count;
            if (sharedCount < _snapMinimumSharedMarks)
                continue;

            SolveRigidEvidenceFitScreen(
                movingLocalScreen,
                targetScreen,
                out Vector2 candidateCenterScreen,
                out float candidateRotation);

            Vector2 currentCenterScreen = _viewport.WorldToScreen(movingPlacement.boardCenterWorld, boardRect);
            float centerShiftPx = Vector2.Distance(candidateCenterScreen, currentCenterScreen);
            float rotationShift = Mathf.Abs(Mathf.DeltaAngle(movingPlacement.rotationDegrees, candidateRotation));

            if (centerShiftPx > _snapPositionTolerancePixels ||
                rotationShift > _snapRotationToleranceDegrees)
            {
                continue;
            }

            float residualPx = ComputeEvidenceResidualScreen(
                movingLocalScreen,
                targetScreen,
                candidateCenterScreen,
                candidateRotation);

            if (residualPx > _snapResidualTolerancePixels)
                continue;

            float score =
                centerShiftPx +
                rotationShift * Mathf.Max(1f, _snapPositionTolerancePixels * 0.5f) +
                residualPx * 4f;

            bool better =
                sharedCount > bestSharedCount ||
                (sharedCount == bestSharedCount && score < bestScore);

            if (!better)
                continue;

            bestSharedCount = sharedCount;
            bestScore = score;
            bestCenterScreen = candidateCenterScreen;
            bestRotation = candidateRotation;
            targetFragmentId = otherPlacement.fragmentId;
            targetRevision = otherPlacement.revision;
        }

        if (bestSharedCount < _snapMinimumSharedMarks)
        {
            reason =
                $"Attempt snap found no convincing fit within {_snapPositionTolerancePixels:0.#} px / " +
                $"{_snapRotationToleranceDegrees:0.#}° using at least {_snapMinimumSharedMarks} shared marks.";
            return false;
        }

        snappedCenterWorld = _viewport.ScreenToWorld(bestCenterScreen, boardRect);
        snappedRotationDegrees = Mathf.DeltaAngle(0f, bestRotation);
        return true;
    }

    private static void SolveRigidEvidenceFitScreen(
        List<Vector2> sourceLocalScreen,
        List<Vector2> targetScreen,
        out Vector2 centerScreen,
        out float rotationDegrees)
    {
        Vector2 sourceCentroid = Vector2.zero;
        Vector2 targetCentroid = Vector2.zero;
        int count = Mathf.Min(sourceLocalScreen.Count, targetScreen.Count);

        for (int i = 0; i < count; i++)
        {
            sourceCentroid += sourceLocalScreen[i];
            targetCentroid += targetScreen[i];
        }

        sourceCentroid /= Mathf.Max(1, count);
        targetCentroid /= Mathf.Max(1, count);

        float dot = 0f;
        float cross = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = sourceLocalScreen[i] - sourceCentroid;
            Vector2 b = targetScreen[i] - targetCentroid;
            dot += a.x * b.x + a.y * b.y;
            cross += a.x * b.y - a.y * b.x;
        }

        rotationDegrees = Mathf.Atan2(cross, dot) * Mathf.Rad2Deg;
        centerScreen = targetCentroid - RotateScreenVector(sourceCentroid, rotationDegrees);
    }

    private static float ComputeEvidenceResidualScreen(
        List<Vector2> sourceLocalScreen,
        List<Vector2> targetScreen,
        Vector2 centerScreen,
        float rotationDegrees)
    {
        int count = Mathf.Min(sourceLocalScreen.Count, targetScreen.Count);
        if (count <= 0)
            return float.MaxValue;

        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector2 transformed = centerScreen + RotateScreenVector(sourceLocalScreen[i], rotationDegrees);
            total += Vector2.Distance(transformed, targetScreen[i]);
        }

        return total / count;
    }

    private Vector2 GetMarkScreenPosition(
        CelestialChartFragmentSnapshot fragment,
        CelestialChartFragmentVisual visual,
        CelestialChartFragmentMark mark,
        CelestialChartBoardPlacementSnapshot placement,
        Rect boardRect)
    {
        Vector2 localWorld = GetMarkFragmentLocalPosition(fragment, visual, mark);
        Vector2 localScreen = new Vector2(
            localWorld.x * _viewport.PixelsPerWorldUnit,
            -localWorld.y * _viewport.PixelsPerWorldUnit);

        Vector2 centerScreen = _viewport.WorldToScreen(placement.boardCenterWorld, boardRect);
        return centerScreen + RotateScreenVector(localScreen, placement.rotationDegrees);
    }

    private static Vector2 GetMarkFragmentLocalPosition(
        CelestialChartFragmentSnapshot fragment,
        CelestialChartFragmentVisual visual,
        CelestialChartFragmentMark mark)
    {
        if (fragment == null || visual == null || mark == null)
            return Vector2.zero;

        // Fragment visuals bake the observation instrument rotation into the generated
        // paper/ink texture before board placement rotation is applied. Reproduce that
        // exact transform here so evidence snapping compares the same geometry the player sees.
        float recordedRotation = NormalizeSignedDegrees(fragment.recordedInstrumentRotationDegrees);
        Vector2 rotatedWorld = RotateVector(mark.celestialWorldPosition, recordedRotation);
        return rotatedWorld - visual.RotatedCelestialBounds.center;
    }

    private static CelestialChartFragmentMark FindMarkByStableId(CelestialChartFragmentSnapshot fragment, string stableId)
    {
        if (fragment == null || fragment.marks == null || string.IsNullOrWhiteSpace(stableId))
            return null;

        for (int i = 0; i < fragment.marks.Count; i++)
        {
            CelestialChartFragmentMark mark = fragment.marks[i];
            if (mark != null && mark.celestialObjectStableId == stableId)
                return mark;
        }

        return null;
    }

    private static float NormalizeSignedDegrees(float degrees)
    {
        if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            return 0f;

        return Mathf.DeltaAngle(0f, degrees);
    }

    private static Vector2 RotateVector(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r);
        float s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private static void DrawPaperBoatMarker(Vector2 center, float size, Color lineColor, Color fillColor, float lineThickness)
    {
        float s = Mathf.Max(4f, size);
        Vector2 leftDeck = center + new Vector2(-s * 0.92f, s * 0.10f);
        Vector2 rightDeck = center + new Vector2(s * 0.92f, s * 0.10f);
        Vector2 hullLeft = center + new Vector2(-s * 0.55f, s * 0.78f);
        Vector2 hullRight = center + new Vector2(s * 0.55f, s * 0.78f);
        Vector2 mastTop = center + new Vector2(0f, -s * 0.92f);
        Vector2 sailBase = center + new Vector2(-s * 0.20f, s * 0.10f);

        DrawLine(leftDeck, rightDeck, lineColor, lineThickness);
        DrawLine(leftDeck, hullLeft, lineColor, lineThickness);
        DrawLine(hullLeft, hullRight, lineColor, lineThickness);
        DrawLine(hullRight, rightDeck, lineColor, lineThickness);
        DrawLine(sailBase, mastTop, lineColor, lineThickness);
        DrawLine(mastTop, rightDeck, lineColor, lineThickness);
        DrawDisc(center + new Vector2(0f, s * 0.28f), 1.6f, fillColor);
    }

    private static void DrawTextureClippedByBoard(
        Rect rect,
        Texture texture,
        float alpha)
    {
        if (texture == null || rect.width <= 0.1f || rect.height <= 0.1f)
            return;

        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        GUI.color = old;
    }

    private static void DrawClippedRotatedOutline(
        Rect clipRect,
        Vector2 center,
        float width,
        float height,
        float degrees,
        float thickness,
        Color color)
    {
        Vector2 halfX = RotateScreenVector(new Vector2(width * 0.5f, 0f), degrees);
        Vector2 halfY = RotateScreenVector(new Vector2(0f, height * 0.5f), degrees);

        Vector2 p0 = center - halfX - halfY;
        Vector2 p1 = center + halfX - halfY;
        Vector2 p2 = center + halfX + halfY;
        Vector2 p3 = center - halfX + halfY;

        DrawClippedLine(p0, p1, clipRect, color, thickness);
        DrawClippedLine(p1, p2, clipRect, color, thickness);
        DrawClippedLine(p2, p3, clipRect, color, thickness);
        DrawClippedLine(p3, p0, clipRect, color, thickness);
    }

    private static void DrawClippedLine(
        Vector2 a,
        Vector2 b,
        Rect clipRect,
        Color color,
        float width)
    {
        if (!ClipLineToRect(ref a, ref b, clipRect))
            return;

        DrawLine(a, b, color, width);
    }

    private static bool ClipLineToRect(
        ref Vector2 a,
        ref Vector2 b,
        Rect rect)
    {
        Vector2 d = b - a;
        float t0 = 0f;
        float t1 = 1f;

        if (!ClipTest(-d.x, a.x - rect.xMin, ref t0, ref t1) ||
            !ClipTest( d.x, rect.xMax - a.x, ref t0, ref t1) ||
            !ClipTest(-d.y, a.y - rect.yMin, ref t0, ref t1) ||
            !ClipTest( d.y, rect.yMax - a.y, ref t0, ref t1))
        {
            return false;
        }

        Vector2 originalA = a;
        a = originalA + d * t0;
        b = originalA + d * t1;
        return true;
    }

    private static bool ClipTest(
        float p,
        float q,
        ref float t0,
        ref float t1)
    {
        if (Mathf.Abs(p) <= 0.000001f)
            return q >= 0f;

        float r = q / p;
        if (p < 0f)
        {
            if (r > t1)
                return false;
            if (r > t0)
                t0 = r;
        }
        else
        {
            if (r < t0)
                return false;
            if (r < t1)
                t1 = r;
        }

        return true;
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

    private static void EnsureBoardPaperTexture()
    {
        if (_boardPaperTexture != null)
            return;

        const int size = 128;
        const int seed = 0x5A17C0DE;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "StarChartDraftingPaper",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.DontSave
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                uint h = unchecked((uint)(seed ^ (x * 73856093) ^ (y * 19349663)));
                h ^= h >> 13;
                h *= 1274126177u;
                float noise = (h & 0xFFFF) / 65535f;

                float fiber =
                    Mathf.Sin((x + y * 0.31f) * 0.21f) * 0.5f +
                    Mathf.Sin((y - x * 0.17f) * 0.11f) * 0.5f;

                float v = 0.93f + (noise - 0.5f) * 0.08f + fiber * 0.018f;
                Color baseColor = new Color(0.075f, 0.078f, 0.070f, 1f);
                Color c = new Color(
                    Mathf.Clamp01(baseColor.r * v),
                    Mathf.Clamp01(baseColor.g * v),
                    Mathf.Clamp01(baseColor.b * v),
                    1f);

                pixels[y * size + x] = (Color32)c;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        _boardPaperTexture = texture;
    }

    #endregion
}
