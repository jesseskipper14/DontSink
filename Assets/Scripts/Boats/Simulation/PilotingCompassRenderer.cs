using UnityEngine;
using Unity.Profiling;

/// <summary>Derived analog north reference; owns no controls, routes or saved state.</summary>
public sealed class PilotingCompassRenderer
{
    private static readonly ProfilerMarker TickMarker = new("Piloting.Compass.Tick");
    private static readonly ProfilerMarker ApproachMarker = new("Piloting.HarborApproach.Draw");
    private BoatSceneWorldPositionBridge _bridge;
    private BoatHarborPresentation _harbor;
    private float _nextResolve, _heading;
    private FogManager _fog;
    private WeatherManager _weather;
    private bool _ready;
    public float? GeographicHeading => _ready ? Mathf.Repeat(_heading,360) : (float?)null;
    private GUIStyle _label;
    private readonly PilotingViewscapeRenderer _viewscape = new();
    private readonly PilotingNearFieldRenderer _nearField = new();
    public void ConfigureViewscape(PilotingViewscapeSettings settings) => _viewscape.Settings = settings ?? new PilotingViewscapeSettings();

    public void Tick(BoatPilotingState state, float dt)
    {
        using var tickSample = TickMarker.Auto();
        if (Time.unscaledTime >= _nextResolve || _bridge == null)
        {
            _nextResolve = Time.unscaledTime + .5f;
            _fog = Object.FindAnyObjectByType<FogManager>();
            _weather = Object.FindAnyObjectByType<WeatherManager>();
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
        { _ready = false; _viewscape.Reset(); _nearField.Reset(); return; }
        if (!_ready) _heading = _bridge.GeographicHeadingDegrees;
        else _heading += Mathf.DeltaAngle(_heading, _bridge.GeographicHeadingDegrees) *
            HarborPresentationMath.SmoothFactor(_viewscape.Settings.compassSmoothingSeconds, dt);
        _ready = true;
        _viewscape.Tick(_bridge, _harbor, _heading, dt);
        _nearField.Configure(_bridge,_harbor,HarborPresentationMath.Visibility(_fog != null ? _fog.FogIntensity : 0,
            _weather != null ? _weather.RainDropDensity : 0));
    }

    public static float SmoothHeading(float previous, float target, float dt) =>
        previous + Mathf.DeltaAngle(previous, target) * HarborPresentationMath.SmoothFactor(.35f, dt);

    public static Vector2 NorthPointer(float heading) => HarborPresentationMath.BoatRelative(Vector2.up, heading);

    public void Draw(Rect playArea, BoatPilotingState state, Rect approachArea)
    {
        float gap=10;
        // Use the gap beside the main scope on wide cartridges. On narrow
        // windows reserve a full third panel below the other instruments.
        bool separateApproach = approachArea.width >= 160f;
        float size=Mathf.Max(0,Mathf.Min(playArea.width-16,
            (playArea.height-gap*(separateApproach ? 1 : 2))/(separateApproach ? 2 : 3)));
        if(size<56)return;
        Rect mini=new Rect(playArea.center.x-size*.5f,playArea.y,size,size);
        if(_viewscape.Settings.enabled)_viewscape.Draw(mini);
        Rect compass=new Rect(mini.x,mini.yMax+gap,size,size);
        float compassSize=size;
        if(_harbor!=null)
        {
            float approachSize = separateApproach
                ? Mathf.Min(320f, approachArea.width, approachArea.height)
                : Mathf.Min(size,playArea.yMax-compass.yMax-gap);
            Rect approach = separateApproach
                ? new Rect(approachArea.center.x-approachSize*.5f,approachArea.center.y-approachSize*.5f,approachSize,approachSize)
                : new Rect(playArea.center.x-approachSize*.5f,compass.yMax+gap,approachSize,approachSize);
            if(approachSize>=100)
                using(ApproachMarker.Auto())
                    _harbor.DrawPilotingApproach(approach,state);
        }
        Color old = GUI.color;
        GUI.color = new Color(.06f, .10f, .12f, .9f);
        GUI.DrawTexture(compass, Texture2D.whiteTexture);
        GUI.BeginGroup(compass);
        Vector2 center = Vector2.one * compassSize * .5f;
        float radius = compassSize * .43f;
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
    public void DrawNearField(PilotingViewProjection view,BoatPilotingState state) => _nearField.Draw(view,state);
    public void Reset() { _bridge = null; _harbor = null; _ready = false; _nextResolve = 0; _viewscape.Reset(); _nearField.Reset(); }
    private static void Line(Vector2 from, Vector2 to, float width)
    {
        Matrix4x4 old = GUI.matrix; Vector2 delta = to - from;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
        GUI.DrawTexture(new Rect(from.x, from.y - width * .5f, delta.magnitude, width), Texture2D.whiteTexture);
        GUI.matrix = old;
    }
}

