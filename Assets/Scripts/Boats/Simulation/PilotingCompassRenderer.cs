using UnityEngine;

/// <summary>Derived analog north reference; owns no controls, routes or saved state.</summary>
public sealed class PilotingCompassRenderer
{
    private BoatSceneWorldPositionBridge _bridge;
    private BoatHarborPresentation _harbor;
    private float _nextResolve, _heading;
    private bool _ready;
    private GUIStyle _label;
    private readonly PilotingViewscapeRenderer _viewscape = new();
    public void ConfigureViewscape(PilotingViewscapeSettings settings) => _viewscape.Settings = settings ?? new PilotingViewscapeSettings();

    public void Tick(BoatPilotingState state, float dt)
    {
        if (Time.unscaledTime >= _nextResolve || _bridge == null)
        {
            _nextResolve = Time.unscaledTime + .5f;
            BoatSceneWorldPositionBridge.TryGetForState(state, out _bridge);
            _harbor = null;
            foreach (var candidate in Object.FindObjectsByType<BoatHarborPresentation>(FindObjectsSortMode.None))
            {
                if (!candidate.isActiveAndEnabled || candidate.Controller == null || candidate.Controller.HarborBridge != _bridge) continue;
                if (_harbor != null) { _harbor = null; break; }
                _harbor = candidate;
            }
        }
        if (_bridge == null || _bridge.PilotingState != state || !_bridge.TryRefreshProjection())
        { _ready = false; _viewscape.Reset(); return; }
        if (!_ready) _heading = _bridge.GeographicHeadingDegrees;
        else _heading += Mathf.DeltaAngle(_heading, _bridge.GeographicHeadingDegrees) *
            HarborPresentationMath.SmoothFactor(_viewscape.Settings.compassSmoothingSeconds, dt);
        _ready = true;
        _viewscape.Tick(_bridge, _harbor, _heading, dt);
    }

    public static float SmoothHeading(float previous, float target, float dt) =>
        previous + Mathf.DeltaAngle(previous, target) * HarborPresentationMath.SmoothFactor(.35f, dt);

    public static Vector2 NorthPointer(float heading) => HarborPresentationMath.BoatRelative(Vector2.up, heading);

    public void Draw(Rect playArea, BoatPilotingState state)
    {
        float compassSize = Mathf.Min(96, playArea.height - 16, playArea.width - 16);
        if (compassSize < 56) return;
        Rect compass = new Rect(playArea.xMax - compassSize - 8, playArea.y + 8, compassSize, compassSize);
        float viewSize = _viewscape.Settings.enabled ? Mathf.Max(0, Mathf.Min(_viewscape.Settings.panelSize, playArea.height * .48f, playArea.width - compassSize - 24)) : 0;
        if (viewSize >= 64) _viewscape.Draw(new Rect(compass.x - viewSize - 8, compass.y, viewSize, viewSize));
        if (_harbor != null)
        {
            float rowHeight = Mathf.Max(compassSize, viewSize);
            float size = Mathf.Min(_harbor.Controller.HarborPresentation.approachPanelSize, playArea.height - rowHeight - 24, playArea.width - 16);
            _harbor.DrawPilotingApproach(new Rect(playArea.xMax - size - 8, compass.y + rowHeight + 8, size, size), state);
        }
        Color old = GUI.color;
        GUI.color = new Color(.06f, .10f, .12f, .9f);
        GUI.DrawTexture(compass, Texture2D.whiteTexture);
        GUI.BeginGroup(compass);
        Vector2 center = Vector2.one * compassSize * .5f;
        float radius = compassSize * .35f;
        GUI.color = new Color(.7f, .8f, .8f, .8f);
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2 / 48, b = (i + 1) * Mathf.PI * 2 / 48;
            Line(center + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * radius,
                center + new Vector2(Mathf.Sin(b), -Mathf.Cos(b)) * radius, 1);
        }
        _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
        GUI.Label(new Rect(0, 0, compassSize, 16), "Bow", _label);
        if (_ready)
        {
            Vector2 north = NorthPointer(_heading); north.y = -north.y;
            Vector2 tip = center + north * radius * .72f;
            GUI.color = new Color(1, .65f, .35f, 1);
            Line(center, tip, 2);
            GUI.Label(new Rect(tip.x - 8, tip.y - 8, 16, 16), "N", _label);
            GUI.color = new Color(.7f, .8f, .8f, .6f);
            Line(center, center - north * radius * .55f, 2);
        }
        else GUI.Label(new Rect(0, compassSize * .35f, compassSize, 28), "No reference", _label);
        GUI.EndGroup(); GUI.color = old;
    }
    public void Reset() { _bridge = null; _harbor = null; _ready = false; _nextResolve = 0; _viewscape.Reset(); }
    private static void Line(Vector2 from, Vector2 to, float width)
    {
        Matrix4x4 old = GUI.matrix; Vector2 delta = to - from;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
        GUI.DrawTexture(new Rect(from.x, from.y - width * .5f, delta.magnitude, width), Texture2D.whiteTexture);
        GUI.matrix = old;
    }
}
