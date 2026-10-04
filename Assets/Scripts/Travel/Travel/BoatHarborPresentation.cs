using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class HarborPresentationSettings
{
    public bool enabled = true;
    public string sortingLayer = "WorldBackdrop";
    public int sortingOrder = 5;
    public Color silhouetteColor = new Color(.20f, .25f, .27f, .95f);
    [Min(.01f)] public float fadeSeconds = .8f;
    [Min(.01f)] public float movementSmoothingSeconds = .35f;
    [Range(0, .99f)] public float fadeStartFraction = .65f;
    [Min(1)] public float bearingSpread = 90;
    [Min(.01f)] public float farScale = .35f;
    [Min(.01f)] public float nearScale = 1.4f;
    public AnimationCurve distanceScale = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [Min(80)] public float approachPanelSize = 240;
}

/// <summary>Local derived art and close harbor sketch. No input, physics, navigation or discovery writes.</summary>
public sealed class BoatHarborPresentation : MonoBehaviour
{
    public BoatSceneController Controller;
    private sealed class Proxy
    {
        public GameObject Root;
        public Mesh Mesh;
        public MeshRenderer Renderer;
        public float Opacity;
        public bool Seen;
    }
    private readonly Dictionary<MapNode, Proxy> _proxies = new();
    private readonly List<MapNode> _retire = new();
    private readonly List<HarborVisualObservation> _observations = new();
    private readonly Vector2[] _corners = new Vector2[4];
    private Material _material;
    private MaterialPropertyBlock _properties;
    private FogManager _fog;
    private WeatherManager _weather;
    private object _voyage;
    private float _nextPoll, _heading, _guideAlpha;
    private bool _hasHeading, _ready;
    private bool _hasGuide;
    private HarborBerth _displayBerth;
    private Vector2 _world;
    private GUIStyle _label;

    private void LateUpdate()
    {
        _ready = false;
        var settings = Controller != null ? Controller.HarborPresentation : null;
        var boat = Controller != null ? Controller.HarborBoat : null;
        var bridge = Controller != null ? Controller.HarborBridge : null;
        var harborSettings = SceneTransitionController.I != null ? SceneTransitionController.I.HarborSettings : null;
        var travel = GameState.I != null ? GameState.I.activeTravel : null;
        if (!ReferenceEquals(_voyage, travel))
        { Clear(); _voyage = travel; _nextPoll = 0; _hasHeading = false; _hasGuide = false; _guideAlpha = 0; }
        if (settings == null || !settings.enabled || harborSettings == null || harborSettings.terrainProfile == null || boat == null || bridge == null ||
            !bridge.TryRefreshProjection() || travel == null || !WorldNavigationService.TryGetTrueWorldPosition(out _world))
        { Clear(); _guideAlpha = 0; return; }
        _ready = true;
        float dt = Time.deltaTime;
        float move = HarborPresentationMath.SmoothFactor(settings.movementSmoothingSeconds, dt);
        if (!_hasHeading) { _heading = bridge.GeographicHeadingDegrees; _hasHeading = true; }
        _heading += Mathf.DeltaAngle(_heading, bridge.GeographicHeadingDegrees) * move;
        if (Time.unscaledTime >= _nextPoll)
        {
            _nextPoll = Time.unscaledTime + .5f;
            _fog = FindAnyObjectByType<FogManager>();
            _weather = FindAnyObjectByType<WeatherManager>();
            HarborVisualObservation.Collect(_world, SceneTransitionController.I.HarborSettings.visibilityRange, _observations);
        }
        float visibility = HarborPresentationMath.Visibility(_fog != null ? _fog.FogIntensity : _weather != null ? _weather.FogIntensity : 0,
            _weather != null ? _weather.RainDropDensity : 0);
        float range = SceneTransitionController.I.HarborSettings.visibilityRange * visibility;
        foreach (var proxy in _proxies.Values) proxy.Seen = false;
        foreach (var observation in _observations)
        {
            float distance = WorldTopologyService.Distance(_world, observation.Harbor.Position);
            float opacity = HarborPresentationMath.RangeFade(distance, range, settings.fadeStartFraction);
            if (!_proxies.TryGetValue(observation.Node, out var proxy))
            {
                if (opacity <= 0) continue;
                proxy = CreateProxy(observation.Node); if (proxy == null) continue;
                _proxies.Add(observation.Node, proxy);
            }
            proxy.Seen = true;
            proxy.Opacity = Mathf.MoveTowards(proxy.Opacity, opacity, dt / Mathf.Max(.01f, settings.fadeSeconds));
            Vector2 relative = HarborPresentationMath.BoatRelative(WorldTopologyService.Delta(_world, observation.Harbor.Position), _heading);
            float bearing = relative.sqrMagnitude > .0001f ? relative.x / relative.magnitude : 0;
            float normalized = Mathf.Clamp01(distance / Mathf.Max(.001f, range));
            float curve = settings.distanceScale != null ? settings.distanceScale.Evaluate(normalized) : 1 - normalized;
            float scale = Mathf.Lerp(settings.farScale, settings.nearScale, Mathf.Clamp01(curve));
            Vector3 position = new Vector3(boat.transform.position.x + bearing * settings.bearingSpread,
                SceneTransitionController.I.HarborSettings.terrainProfile.waterLevelY, 0);
            if (!proxy.Root.activeSelf) { proxy.Root.transform.position = position; proxy.Root.transform.localScale = Vector3.one * scale; proxy.Root.SetActive(true); }
            proxy.Root.transform.position = Vector3.Lerp(proxy.Root.transform.position, position, move);
            proxy.Root.transform.localScale = Vector3.Lerp(proxy.Root.transform.localScale, Vector3.one * scale, move);
            Apply(proxy, settings);
        }
        _retire.Clear();
        foreach (var pair in _proxies)
        {
            if (pair.Value.Seen) continue;
            pair.Value.Opacity = Mathf.MoveTowards(pair.Value.Opacity, 0, dt / Mathf.Max(.01f, settings.fadeSeconds));
            Apply(pair.Value, settings);
            if (pair.Value.Opacity <= 0) _retire.Add(pair.Key);
        }
        foreach (var node in _retire) { DestroyProxy(_proxies[node]); _proxies.Remove(node); }
        float guide = 0;
        if (!string.IsNullOrEmpty(Controller.CurrentHarborNodeId))
        {
            _displayBerth = Controller.CurrentHarborBerth; _hasGuide = true;
            float distance = WorldTopologyService.Distance(_world, Controller.CurrentHarborBerth.Center);
            guide = HarborPresentationMath.RangeFade(distance, SceneTransitionController.I.HarborSettings.guidanceRange * visibility, settings.fadeStartFraction);
        }
        _guideAlpha = Mathf.MoveTowards(_guideAlpha, guide, dt / Mathf.Max(.01f, settings.fadeSeconds));
    }

    private void Apply(Proxy proxy, HarborPresentationSettings settings)
    {
        proxy.Renderer.sortingLayerName = settings.sortingLayer;
        proxy.Renderer.sortingOrder = settings.sortingOrder;
        Color color = settings.silhouetteColor; color.a *= proxy.Opacity;
        _properties.SetColor("_Color", color); proxy.Renderer.SetPropertyBlock(_properties);
        proxy.Renderer.enabled = proxy.Opacity > .001f;
    }

    private Proxy CreateProxy(MapNode node)
    {
        if (_material == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            _material = new Material(shader) { name = "Runtime Harbor Silhouette" };
            _properties = new MaterialPropertyBlock();
        }
        var vertices = new List<Vector3>(); var indices = new List<int>();
        void Quad(float x, float y, float width, float height)
        {
            int n = vertices.Count;
            vertices.Add(new Vector3(x, y)); vertices.Add(new Vector3(x, y + height));
            vertices.Add(new Vector3(x + width, y + height)); vertices.Add(new Vector3(x + width, y));
            indices.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }
        // Compact placeholder landmark: one tower, lantern room and pitched cap.
        Quad(-.75f, 0, 1.5f, .25f);
        Quad(-.45f, .25f, .9f, 2.5f);
        Quad(-.8f, 2.75f, 1.6f, .2f);
        Quad(-.6f, 2.95f, 1.2f, .5f);
        int roof = vertices.Count;
        vertices.Add(new Vector3(-.85f, 3.45f));
        vertices.Add(new Vector3(0, 3.95f));
        vertices.Add(new Vector3(.85f, 3.45f));
        indices.AddRange(new[] { roof, roof + 1, roof + 2 });
        var mesh = new Mesh { name = "Derived Harbor Silhouette" };
        mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
        var root = new GameObject("HarborProxy_Runtime"); root.transform.SetParent(transform, false);
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        root.SetActive(false);
        return new Proxy { Root = root, Mesh = mesh, Renderer = renderer };
    }

    private void OnGUI()
    {
        var camera = CameraManager.Instance;
        if (!_ready || !_hasGuide || _guideAlpha <= .001f || Controller == null ||
            camera == null || camera.ActiveCamera == null || camera.ActiveCamera.gameObject.scene != gameObject.scene) return;
        var viewport = camera.ActiveCamera.pixelRect;
        float size = Mathf.Min(Controller.HarborPresentation.approachPanelSize, viewport.width * .32f, viewport.height * .42f);
        if (size < 100) return;
        Rect panel = new Rect(viewport.xMax - size - 12, Screen.height - viewport.yMax + 12, size, size);
        Color old = GUI.color;
        GUI.color = new Color(.07f, .11f, .14f, _guideAlpha * .82f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.BeginGroup(panel);
        _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
        GUI.color = new Color(.8f, .9f, .9f, _guideAlpha);
        GUI.Label(new Rect(0, 2, size, 22), "Harbor approach · Ahead", _label);
        GUI.Label(new Rect(0, size - 22, size, 20), "Astern", _label);
        var berth = _displayBerth;
        float coverage = Mathf.Max(SceneTransitionController.I.HarborSettings.guidanceRange,
            WorldTopologyService.Distance(berth.Center, berth.Departure) * 1.1f, berth.HalfLength * 3);
        Vector2 center = new Vector2(size * .5f, size * .52f);
        float pixels = size * .37f / Mathf.Max(.001f, coverage);
        Vector2 Project(Vector2 point)
        {
            Vector2 relative = HarborPresentationMath.BoatRelative(WorldTopologyService.Delta(_world, point), _heading);
            return center + new Vector2(relative.x, -relative.y) * pixels;
        }
        HarborPresentationMath.BerthCorners(berth, _corners);
        GUI.color = new Color(.35f, .9f, .85f, _guideAlpha);
        for (int i = 0; i < 4; i++) Line(Project(_corners[i]), Project(_corners[(i + 1) % 4]), 2);
        Line(Project(berth.Center), Project(berth.Departure), 1);
        GUI.color = new Color(1, .85f, .45f, _guideAlpha);
        Line(center + new Vector2(0, -6), center + new Vector2(-4, 5), 2);
        Line(center + new Vector2(-4, 5), center + new Vector2(4, 5), 2);
        Line(center + new Vector2(4, 5), center + new Vector2(0, -6), 2);
        GUI.color = new Color(.8f, .9f, .9f, _guideAlpha);
        GUI.Label(new Rect(2, size * .45f, 44, 22), "Port", _label);
        GUI.Label(new Rect(size - 64, size * .45f, 62, 22), "Starboard", _label);
        bool inside = berth.Contains(_world, WorldTopologyService.Current);
        GUI.Label(new Rect(0, size - 44, size, 20), inside ? "In berth · E to dock" : "Outline: berth · Line: approach", _label);
        GUI.EndGroup(); GUI.color = old;
    }

    private static void Line(Vector2 from, Vector2 to, float width)
    {
        Matrix4x4 old = GUI.matrix;
        Vector2 delta = to - from;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
        GUI.DrawTexture(new Rect(from.x, from.y - width * .5f, delta.magnitude, width), Texture2D.whiteTexture);
        GUI.matrix = old;
    }
    private static void DestroyProxy(Proxy proxy)
    { if (proxy.Root != null) proxy.Root.SetActive(false); Release(proxy.Root); Release(proxy.Mesh); }
    private static void Release(UnityEngine.Object value)
    { if (value != null) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
    private void Clear()
    { foreach (var proxy in _proxies.Values) DestroyProxy(proxy); _proxies.Clear(); _observations.Clear(); }
    private void OnDisable() { _ready = false; _guideAlpha = 0; Clear(); }
    private void OnDestroy() { Clear(); Release(_material); }
}
