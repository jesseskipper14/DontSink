using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Phase 7 presentation. Fragment evidence and Phase 6 editing remain in the original partial.</summary>
public sealed partial class CelestialChartTableCartridge
{
    private readonly CelestialFieldSource _fieldSource;
    private string _selectedSubjectId;
    private CelestialSubjectKind _selectedSubjectKind;
    private string _selectedOccurrence;
    private string _nameDraft = "";
    private string _noteDraft = "";
    private int _annotationDraftRevision;
    private bool _annotationDirty;
    private bool _annotationTextFocused;
    private bool _showKnownConstellations = true;
    private readonly HashSet<string> _expandedOccurrences = new(StringComparer.Ordinal);
    private readonly List<BoardOccurrence> _occurrences = new();
    private readonly List<BoardBranch> _branches = new();
    private string _pendingMarkDragFragmentId;
    private Vector2 _pendingMarkDragMouse;

    private sealed class BoardOccurrence
    {
        public string id, fragmentId;
        public CelestialSubjectKind kind;
        public Vector2 point;
        public string Key => fragmentId + ":" + id;
    }
    private readonly struct BoardBranch
    {
        public readonly string id;
        public readonly Vector2 a, b;
        public BoardBranch(string id, Vector2 a, Vector2 b) { this.id = id; this.a = a; this.b = b; }
    }

    private CelestialField KnowledgeField => _fieldSource != null ? _fieldSource.Field : null;
    private bool AnnotationHasTextFocus() => _annotationTextFocused;

    private void BuildKnowledgeGeometry(Rect board, CelestialChartStateSnapshot state)
    {
        _occurrences.Clear(); _branches.Clear();
        CelestialField field = KnowledgeField;
        if (field == null) return;
        BuildSortedPlacements(state);
        foreach (var placement in _sortedPlacements)
        {
            if (!TryGetFragment(state, placement.fragmentId, out var fragment) ||
                !CelestialKnowledgeQueries.MatchesField(fragment, field)) continue;
            var visual = _renderCache.Get(fragment);
            if (visual == null) continue;
            foreach (var mark in fragment.marks)
            {
                if (mark == null || mark.kind == CelestialObjectKind.AmbientStar || string.IsNullOrWhiteSpace(mark.celestialObjectStableId)) continue;
                Vector2 local = GetMarkFragmentLocalPosition(fragment, visual, mark);
                Vector2 point = _viewport.WorldToScreen(GetRenderCenter(placement), board) +
                    RotateScreenVector(new Vector2(local.x, -local.y) * _viewport.PixelsPerWorldUnit, GetRenderRotation(placement));
                _occurrences.Add(new BoardOccurrence { id = mark.celestialObjectStableId,
                    fragmentId = fragment.fragmentId, kind = (CelestialSubjectKind)mark.kind, point = point });
            }
        }
        foreach (var constellation in field.Constellations.All)
        {
            bool known = CelestialKnowledgeQueries.IsConstellationKnown(state, constellation.StableId);
            if (!(known && _showKnownConstellations) && !_fieldSource.ShowAllConstellationsForField) continue;
            foreach (var edge in constellation.Edges)
            {
                bool drewSameScrap = false;
                foreach (var a in _occurrences)
                {
                    if (a.id != edge.fromStarStableId) continue;
                    foreach (var b in _occurrences)
                        if (b.id == edge.toStarStableId && b.fragmentId == a.fragmentId)
                        { _branches.Add(new BoardBranch(constellation.StableId, a.point, b.point)); drewSameScrap = true; }
                }
                if (drewSameScrap) continue;
                BoardOccurrence bestA = null, bestB = null;
                float distance = float.PositiveInfinity;
                foreach (var a in _occurrences)
                    if (a.id == edge.fromStarStableId)
                        foreach (var b in _occurrences)
                            if (b.id == edge.toStarStableId && (a.point - b.point).sqrMagnitude < distance)
                            { bestA = a; bestB = b; distance = (a.point - b.point).sqrMagnitude; }
                if (bestA != null) _branches.Add(new BoardBranch(constellation.StableId, bestA.point, bestB.point));
                else if (_fieldSource.ShowAllConstellationsForField &&
                    field.TryResolveObject(edge.fromStarStableId, out var truthA) && field.TryResolveObject(edge.toStarStableId, out var truthB))
                    _branches.Add(new BoardBranch(constellation.StableId, _viewport.WorldToScreen(truthA.WorldPosition, board),
                        _viewport.WorldToScreen(truthB.WorldPosition, board)));
            }
        }
    }

    private bool TrySelectCelestialSubject(Rect board, CelestialChartStateSnapshot state, Vector2 mouse)
    {
        BuildKnowledgeGeometry(board, state);
        BoardOccurrence hit = null;
        float nearest = 10f * 10f;
        // Later occurrences are topmost; direct marks always take priority over relationships.
        for (int i = _occurrences.Count - 1; i >= 0; i--)
        {
            var occurrence = _occurrences[i];
            float d = (mouse - occurrence.point).sqrMagnitude;
            if (d < nearest) { nearest = d; hit = occurrence; }
        }
        if (hit != null)
        {
            _selectedFragmentId = hit.fragmentId;
            _pendingMarkDragFragmentId = hit.fragmentId;
            _pendingMarkDragMouse = mouse;
            SelectSubject(state, hit.id, hit.kind, hit.Key);
            return true;
        }
        for (int i = _occurrences.Count - 1; i >= 0; i--)
        {
            var occurrence = _occurrences[i];
            if (_expandedOccurrences.Contains(occurrence.Key) &&
                new Rect(occurrence.point.x + 16f, occurrence.point.y - 28f, 220f, 24f).Contains(mouse))
            { SelectSubject(state, occurrence.id, occurrence.kind, occurrence.Key); return true; }
        }
        foreach (var constellation in KnowledgeField?.Constellations.All ?? Array.Empty<CelestialConstellation>())
            if (TryGetBranchCenter(constellation.StableId, out Vector2 center) &&
                ((mouse - center).sqrMagnitude <= 100f ||
                 (_expandedOccurrences.Contains("const:" + constellation.StableId) &&
                  new Rect(center.x + 16f, center.y - 28f, 220f, 24f).Contains(mouse))))
            { SelectSubject(state, constellation.StableId, CelestialSubjectKind.Constellation, "const:" + constellation.StableId); return true; }
        foreach (var branch in _branches)
            if (DistanceToSegment(mouse, branch.a, branch.b) <= 5f)
            { SelectSubject(state, branch.id, CelestialSubjectKind.Constellation, "const:" + branch.id); return true; }
        return false;
    }

    private void SelectSubject(CelestialChartStateSnapshot state, string id, CelestialSubjectKind kind, string occurrence)
    {
        _selectedSubjectId = id; _selectedSubjectKind = kind; _selectedOccurrence = occurrence;
        if (kind == CelestialSubjectKind.Constellation && _fieldSource != null) _fieldSource.debugSelectedConstellationId = id;
        ReloadAnnotation(state);
        GUI.FocusControl(null);
    }

    private void ClearSubjectSelection()
    {
        _selectedSubjectId = null; _selectedOccurrence = null; _annotationDirty = false;
        GUI.FocusControl(null);
    }

    private void BeginPendingMarkDrag(Rect rect, CelestialChartStateSnapshot state, Event e)
    {
        if (string.IsNullOrWhiteSpace(_pendingMarkDragFragmentId)) return;
        if (e.type == EventType.MouseUp) { _pendingMarkDragFragmentId = null; return; }
        if (e.type != EventType.MouseDrag || e.button != 0 || (e.mousePosition - _pendingMarkDragMouse).sqrMagnitude < 25f) return;
        if (state.TryGetPlacement(_pendingMarkDragFragmentId, out var placement) && !placement.pinned)
            BeginEdit(placement, EditMode.Move, rect, _pendingMarkDragMouse);
        _pendingMarkDragFragmentId = null;
    }

    private void ReloadAnnotation(CelestialChartStateSnapshot state)
    {
        var annotation = CelestialKnowledgeQueries.FindAnnotation(state, _selectedSubjectId, _selectedSubjectKind);
        _nameDraft = annotation?.playerName ?? ""; _noteDraft = annotation?.note ?? "";
        _annotationDraftRevision = annotation?.revision ?? 0; _annotationDirty = false;
    }

    private void DrawCelestialKnowledge(Rect board, CelestialChartStateSnapshot state)
    {
        BuildKnowledgeGeometry(board, state);
        var lineColor = new Color(0.38f, 0.72f, 0.85f, 0.65f);
        foreach (var branch in _branches)
            DrawClippedLine(branch.a, branch.b, board, branch.id == _selectedSubjectId ? Color.yellow : lineColor, 1.5f);
        var textStyle = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = false };
        foreach (var occurrence in _occurrences)
        {
            if (!board.Contains(occurrence.point)) continue;
            if (occurrence.Key == _selectedOccurrence) DrawRing(occurrence.point, 9f, Color.yellow, 1.5f);
            var annotation = CelestialKnowledgeQueries.FindAnnotation(state, occurrence.id, occurrence.kind);
            DrawSubjectLabel(occurrence.point, occurrence.Key, annotation, textStyle);
        }
        if (KnowledgeField == null) return;
        foreach (var constellation in KnowledgeField.Constellations.All)
        {
            if (!TryGetBranchCenter(constellation.StableId, out Vector2 center) || !board.Contains(center)) continue;
            if (constellation.StableId == _selectedSubjectId) DrawRing(center, 9f, Color.yellow, 1.5f);
            if (CelestialKnowledgeQueries.IsConstellationKnown(state, constellation.StableId))
                DrawSubjectLabel(center, "const:" + constellation.StableId,
                    CelestialKnowledgeQueries.FindAnnotation(state, constellation.StableId, CelestialSubjectKind.Constellation), textStyle);
        }
        if (_fieldSource.ShowAllConstellationsForField)
            GUI.Label(new Rect(8f, board.height - 24f, board.width - 16f, 22f), "DEBUG: all constellation truth visible (does not grant knowledge)");
    }

    private void DrawSubjectLabel(Vector2 point, string key, CelestialSubjectAnnotationSnapshot annotation, GUIStyle style)
    {
        if (annotation == null || !annotation.HasData) return;
        DrawDisc(point + new Vector2(8f, -8f), 2.5f, new Color(1f, 0.80f, 0.28f));
        if (!_expandedOccurrences.Contains(key)) return;
        string text = !string.IsNullOrWhiteSpace(annotation.playerName) ? annotation.playerName : "[Note]";
        DrawLine(point + new Vector2(8f, -8f), point + new Vector2(15f, -16f), Color.gray, 1f);
        GUI.Label(new Rect(point.x + 16f, point.y - 28f, 220f, 24f), text, style);
    }

    private bool DrawCelestialSubjectDetails(Rect rect, CelestialChartStateSnapshot state)
    {
        if (string.IsNullOrWhiteSpace(_selectedSubjectId)) return false;
        DrawPanelBox(rect);
        float x = rect.x + 10f, y = rect.y + 10f, w = rect.width - 20f;
        var style = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = true };
        GUI.Label(new Rect(x, y, w, 24f), _selectedSubjectKind == CelestialSubjectKind.Constellation ? "CONSTELLATION" : "CELESTIAL SUBJECT", style); y += 28f;
        if (GUI.Button(new Rect(x, y, w, 26f), "BACK TO FRAGMENT")) { ClearSubjectSelection(); return true; } y += 32f;
        _showKnownConstellations = GUI.Toggle(new Rect(x, y, w, 24f), _showKnownConstellations, "Show Known Constellations"); y += 30f;
        bool allowed = CelestialKnowledgeQueries.CanAnnotateSubject(state, KnowledgeField, _selectedSubjectId, _selectedSubjectKind);
        if (_selectedSubjectKind == CelestialSubjectKind.Constellation)
        {
            var progress = CelestialKnowledgeQueries.GetConstellationChartProgress(state, KnowledgeField, _selectedSubjectId);
            GUI.Label(new Rect(x, y, w, 42f), (allowed ? "KNOWN" : "HIDDEN") + "\n" + progress, style); y += 46f;
            if (GUI.Button(new Rect(x, y, w, 26f), "DEBUG VALIDATE SELECTED"))
                CelestialKnowledgeAuthority.TryValidateConstellation(_requester, KnowledgeField, _selectedSubjectId, out _statusLine);
            y += 30f;
            if (GUI.Button(new Rect(x, y, w, 26f), "DEBUG RESET TO HIDDEN"))
                CelestialKnowledgeAuthority.TryResetConstellationForDebug(_requester, KnowledgeField, _selectedSubjectId, out _statusLine);
            y += 32f;
        }
        if (!allowed) { GUI.Label(new Rect(x, y, w, 60f), "Name and notes require legitimate evidence and Known constellation status.", style); return true; }
        int liveRevision = CelestialKnowledgeQueries.GetAnnotationRevision(state, _selectedSubjectId, _selectedSubjectKind);
        if (!_annotationDirty && liveRevision != _annotationDraftRevision) ReloadAnnotation(state);
        GUI.Label(new Rect(x, y, w, 20f), "Name"); y += 22f;
        GUI.SetNextControlName("CelestialName");
        string name = GUI.TextField(new Rect(x, y, w, 26f), _nameDraft, 128); y += 32f;
        GUI.Label(new Rect(x, y, w, 20f), "Notes"); y += 22f;
        GUI.SetNextControlName("CelestialNote");
        string note = GUI.TextArea(new Rect(x, y, w, 90f), _noteDraft, 2048); y += 96f;
        if (name != _nameDraft || note != _noteDraft) _annotationDirty = true;
        _nameDraft = name; _noteDraft = note;
        if (GUI.Button(new Rect(x, y, w, 26f), "SAVE NAME / NOTES"))
            if (CelestialKnowledgeAuthority.TrySetAnnotation(_requester, KnowledgeField, _selectedSubjectId, _selectedSubjectKind,
                _annotationDraftRevision, _nameDraft, _noteDraft, out _statusLine)) ReloadAnnotation(state);
        y += 30f;
        if (GUI.Button(new Rect(x, y, w * 0.5f - 2f, 26f), "CLEAR NAME"))
            if (CelestialKnowledgeAuthority.TryClearSubjectName(_requester, KnowledgeField, _selectedSubjectId, _selectedSubjectKind,
                _annotationDraftRevision, out _statusLine)) ReloadAnnotation(state);
        if (GUI.Button(new Rect(x + w * 0.5f + 2f, y, w * 0.5f - 2f, 26f), "CLEAR NOTE"))
            if (CelestialKnowledgeAuthority.TryClearSubjectNote(_requester, KnowledgeField, _selectedSubjectId, _selectedSubjectKind,
                _annotationDraftRevision, out _statusLine)) ReloadAnnotation(state);
        y += 32f;
        if (GUI.Button(new Rect(x, y, w, 26f), "RELOAD SHARED ANNOTATION")) ReloadAnnotation(state); y += 32f;
        bool expanded = _expandedOccurrences.Contains(_selectedOccurrence);
        if (GUI.Button(new Rect(x, y, w, 26f), expanded ? "COLLAPSE LABEL" : "EXPAND THIS LABEL"))
        { if (expanded) _expandedOccurrences.Remove(_selectedOccurrence); else _expandedOccurrences.Add(_selectedOccurrence); }
        return true;
    }

    private bool TryGetBranchCenter(string id, out Vector2 center)
    {
        center = Vector2.zero; int count = 0;
        foreach (var branch in _branches) if (branch.id == id) { center += branch.a + branch.b; count += 2; }
        if (count == 0) return false;
        center /= count; return true;
    }
    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        float t = d.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + d * t);
    }
}
