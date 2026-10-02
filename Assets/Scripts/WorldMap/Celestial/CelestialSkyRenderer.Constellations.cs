using System.Collections.Generic;
using UnityEngine;

public sealed partial class CelestialSkyRenderer
{
    [Header("Constellation Look (local presentation)")]
    [Tooltip("Exact local player's ICharacterIntentSource. Reads FocusHeld, never a hardwired mouse button.")]
    [SerializeField] private MonoBehaviour lookIntentSource;
    [SerializeField] private bool showCrewConstellationNames = true;
    private readonly Dictionary<string, Vector2> _constellationScreenPoints = new();

    private void OnGUI()
    {
        if (!_ready || !isActiveAndEnabled || fieldSource == null || !fieldSource.HasField ||
            targetCamera == null || _starVisibility01 <= 0.001f || !WorldNavigationService.TryGetTrueWorldPosition(out _)) return;
        bool debug = fieldSource.debugShowAllConstellations;
        if (!debug && !IsLookHeld()) return;
        var state = GameState.I != null ? GameState.I.celestialCharts : null;
        _constellationScreenPoints.Clear();
        foreach (var slot in _slots)
        {
            if (slot.celestialObject == null || !slot.gameObject.activeInHierarchy || slot.renderer.color.a <= 0.001f) continue;
            Vector3 screen = targetCamera.WorldToScreenPoint(slot.transform.position);
            _constellationScreenPoints[slot.celestialObject.StableId] = new Vector2(screen.x, Screen.height - screen.y);
        }
        Rect pixel = targetCamera.pixelRect;
        Rect clip = new Rect(pixel.x, Screen.height - pixel.yMax, pixel.width, pixel.height);
        GUI.BeginGroup(clip);
        var labelStyle = new GUIStyle(GUI.skin.label) { richText = false };
        foreach (var constellation in fieldSource.Field.Constellations.All)
        {
            bool known = CelestialKnowledgeQueries.IsConstellationKnown(state, constellation.StableId);
            if (!debug && !known) continue;
            Vector2 center = Vector2.zero; int count = 0;
            foreach (var edge in constellation.Edges)
            {
                if (!_constellationScreenPoints.TryGetValue(edge.fromStarStableId, out Vector2 a) ||
                    !_constellationScreenPoints.TryGetValue(edge.toStarStableId, out Vector2 b)) continue;
                DrawConstellationSkyLine(a - clip.position, b - clip.position, _starVisibility01 * 0.65f);
                center += a + b; count += 2;
            }
            if (count > 0 && known && showCrewConstellationNames && CelestialKnowledgeQueries.TryGetCrewCelestialName(
                state, fieldSource.Field, constellation.StableId, CelestialSubjectKind.Constellation, out string name))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, _starVisibility01);
                Vector2 p = center / count - clip.position;
                GUI.Label(new Rect(p.x + 8f, p.y - 20f, 230f, 24f), name, labelStyle);
                GUI.color = old;
            }
        }
        GUI.EndGroup();
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

    private static void DrawConstellationSkyLine(Vector2 a, Vector2 b, float alpha)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude <= 0.0001f) return;
        Matrix4x4 matrix = GUI.matrix;
        Color color = GUI.color;
        GUI.color = new Color(0.42f, 0.78f, 0.95f, alpha);
        GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        GUI.DrawTexture(new Rect(a.x, a.y - 0.75f, d.magnitude, 1.5f), Texture2D.whiteTexture);
        GUI.matrix = matrix; GUI.color = color;
    }
}
