using System.Collections.Generic;
using UnityEngine;

public sealed partial class CelestialSkyRenderer
{
    [Header("Constellation Look (local presentation)")]
    [Tooltip("Exact local player's ICharacterIntentSource. Reads FocusHeld, never a hardwired mouse button.")]
    [SerializeField] private MonoBehaviour lookIntentSource;
    [SerializeField] private bool showCrewConstellationNames = true;
    [SerializeField, Min(0.01f)] private float constellationFadeInSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float constellationFadeOutSeconds = 0.45f;
    [SerializeField, Range(0f, 0.25f)] private float constellationPulseAmount = 0.06f;
    [SerializeField, Min(0.1f)] private float constellationPulsePeriodSeconds = 3f;
    private float _constellationLookFade;
    private float _constellationPulsePhase;
    private CelestialField _constellationPointField;
    private readonly Dictionary<string, Vector2> _constellationWorldPoints = new();
    private readonly List<LineRenderer> _constellationLines = new();
    private readonly List<TextMesh> _constellationLabels = new();
    private Material _constellationLineMaterial;
    private Font _constellationFont;

    private void LateUpdate()
    {
        int usedLines = 0, usedLabels = 0;
        bool ready = CanRenderConstellationLook();
        bool held = ready && IsLookHeld();
        AdvanceConstellationLook(ready, held, Time.unscaledDeltaTime);
        if (ready && _constellationLookFade > 0.001f)
            RenderConstellationLook(ref usedLines, ref usedLabels);
        for (int i = usedLines; i < _constellationLines.Count; i++)
            _constellationLines[i].gameObject.SetActive(false);
        for (int i = usedLabels; i < _constellationLabels.Count; i++)
            _constellationLabels[i].gameObject.SetActive(false);
    }

    private bool CanRenderConstellationLook() => _ready && isActiveAndEnabled &&
        fieldSource != null && fieldSource.FixedSky == null && fieldSource.HasField && targetCamera != null &&
        projectionSettings != null && projectionSettings.showLandmarkStars &&
        _starVisibility01 > 0.001f;

    private void AdvanceConstellationLook(bool ready, bool held, float deltaSeconds)
    {
        if (!ready) { _constellationLookFade = 0f; _constellationPulsePhase = 0f; return; }
        if (held && _constellationLookFade <= 0f) _constellationPulsePhase = 0f;
        float duration = held ? constellationFadeInSeconds : constellationFadeOutSeconds;
        _constellationLookFade = Mathf.MoveTowards(_constellationLookFade, held ? 1f : 0f,
            Mathf.Max(0f, deltaSeconds) / Mathf.Max(0.01f, duration));
        if (held) _constellationPulsePhase = Mathf.Repeat(_constellationPulsePhase +
            Mathf.Max(0f, deltaSeconds) * Mathf.PI * 2f / Mathf.Max(0.1f, constellationPulsePeriodSeconds), Mathf.PI * 2f);
    }

    private float ConstellationLookAlpha => Mathf.SmoothStep(0f, 1f, _constellationLookFade) *
        (1f - Mathf.Clamp(constellationPulseAmount, 0f, 0.25f) *
            (0.5f - 0.5f * Mathf.Cos(_constellationPulsePhase)));

    private void RenderConstellationLook(ref int usedLines, ref int usedLabels)
    {
        if (!WorldNavigationService.TryGetTrueWorldPosition(out Vector2 observer)) return;
        bool debug = fieldSource.ShowAllConstellationsForField;
        var state = GameState.I != null ? GameState.I.celestialCharts : null;
        if (_constellationPointField != fieldSource.Field)
        {
            _constellationPointField = fieldSource.Field;
            _constellationWorldPoints.Clear();
        }
        GetWorldUnitsPerPixel(out _, out float worldPerPixelY);
        Color lineColor = new Color(0.42f, 0.78f, 0.95f, _starVisibility01 * 0.65f * ConstellationLookAlpha);
        foreach (var constellation in fieldSource.Field.Constellations.All)
        {
            bool known = CelestialKnowledgeQueries.IsConstellationKnown(state, constellation.StableId);
            if (!debug && !known) continue;
            Vector2 center = Vector2.zero; int count = 0;
            foreach (var edge in constellation.Edges)
            {
                if (!TryGetConstellationWorldPoint(edge.fromStarStableId, out Vector2 from) ||
                    !TryGetConstellationWorldPoint(edge.toStarStableId, out Vector2 to) ||
                    !CelestialSkyProjection.TryProjectSceneSegment(fieldSource.Field.WorldBounds, observer,
                        from, to, projectionSettings, ResolveProjectionViewportAspect(),
                        out Vector2 a, out Vector2 b)) continue;
                LineRenderer line = GetConstellationLine(usedLines++);
                line.startWidth = line.endWidth = Mathf.Max(0.0001f, worldPerPixelY * 1.5f);
                line.startColor = line.endColor = lineColor;
                line.SetPosition(0, ConstellationViewportToWorld(a));
                line.SetPosition(1, ConstellationViewportToWorld(b));
                center += a + b; count += 2;
            }
            if (count > 0 && known && showCrewConstellationNames && CelestialKnowledgeQueries.TryGetCrewCelestialName(
                state, fieldSource.Field, constellation.StableId, CelestialSubjectKind.Constellation, out string name))
            {
                TextMesh label = GetConstellationLabel(usedLabels++);
                label.text = name;
                label.color = new Color(1f, 1f, 1f, _starVisibility01 * ConstellationLookAlpha);
                label.characterSize = worldPerPixelY * 18f;
                Vector2 position = center / count + new Vector2(8f / Mathf.Max(1, targetCamera.pixelWidth),
                    12f / Mathf.Max(1, targetCamera.pixelHeight));
                label.transform.position = ConstellationViewportToWorld(position);
                label.transform.rotation = targetCamera.transform.rotation;
            }
        }
    }

    private Vector3 ConstellationViewportToWorld(Vector2 point)
    {
        Vector3 world = SkyViewportToWorld(point);
        world.z = ResolveRenderWorldZ();
        return world;
    }

    private LineRenderer GetConstellationLine(int index)
    {
        if (_constellationLineMaterial == null)
        {
            Shader shader = Shader.Find("Custom/CelestialUnlitAlpha2D") ?? Shader.Find("Sprites/Default");
            _constellationLineMaterial = new Material(shader) { name = "CelestialConstellationLines_Runtime", hideFlags = HideFlags.DontSave };
        }
        while (_constellationLines.Count <= index)
        {
            var go = new GameObject("ConstellationBranch");
            go.transform.SetParent(renderRoot, false);
            go.layer = gameObject.layer;
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _constellationLineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.alignment = LineAlignment.View;
            line.sortingLayerName = "Background";
            line.sortingOrder = -99;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            _constellationLines.Add(line);
        }
        LineRenderer result = _constellationLines[index];
        result.gameObject.SetActive(true);
        return result;
    }

    private TextMesh GetConstellationLabel(int index)
    {
        if (_constellationFont == null) _constellationFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        while (_constellationLabels.Count <= index)
        {
            var go = new GameObject("ConstellationCrewName");
            go.transform.SetParent(renderRoot, false);
            go.layer = gameObject.layer;
            var label = go.AddComponent<TextMesh>();
            label.font = _constellationFont;
            label.fontSize = 32;
            label.richText = false;
            label.anchor = TextAnchor.MiddleLeft;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _constellationFont.material;
            renderer.sortingLayerName = "Background";
            renderer.sortingOrder = -99;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _constellationLabels.Add(label);
        }
        TextMesh result = _constellationLabels[index];
        result.gameObject.SetActive(true);
        return result;
    }

    private void HideConstellationLook()
    {
        _constellationLookFade = 0f;
        _constellationPulsePhase = 0f;
        foreach (var line in _constellationLines) if (line != null) line.gameObject.SetActive(false);
        foreach (var label in _constellationLabels) if (label != null) label.gameObject.SetActive(false);
    }

    private bool TryGetConstellationWorldPoint(string id, out Vector2 point)
    {
        if (_constellationWorldPoints.TryGetValue(id, out point)) return true;
        if (!_constellationPointField.TryResolveObject(id, out var obj)) return false;
        point = obj.WorldPosition;
        _constellationWorldPoints[id] = point;
        return true;
    }

    private bool IsLookHeld()
    {
        if (lookIntentSource == null)
        {
            // Compatibility for today's single-player scenes; never pick arbitrarily among players.
            var sources = FindObjectsByType<LocalCharacterIntentSource>(FindObjectsSortMode.None);
            LocalCharacterIntentSource candidate = null;
            foreach (var source in sources)
            {
                var local = source.GetComponent<ILocalPlayerAuthority>();
                if (local != null && !local.IsLocal) continue;
                if (candidate != null) return false;
                candidate = source;
            }
            lookIntentSource = candidate;
        }
        return lookIntentSource != null && lookIntentSource.isActiveAndEnabled &&
            lookIntentSource is ICharacterIntentSource intent && intent.Current.FocusHeld;
    }

}
