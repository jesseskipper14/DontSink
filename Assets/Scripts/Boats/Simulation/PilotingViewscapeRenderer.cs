using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

[Serializable]
public sealed class PilotingViewscapeSettings
{
    public bool enabled = true;
    [HideInInspector] public float circumference = 10; // Retained serialized legacy setting; no longer the range control.
    [Min(.01f)] public float visibleRadiusNauticalMiles = 3;
    [Tooltip("1852 assumes one physical scene unit equals one metre. Adjust if the physical unit convention changes.")]
    [Min(1)] public float physicalUnitsPerNauticalMile = 1852;
    [Min(0)] public float observerHeightMultiplier = 1;
    [Min(64)] public float panelSize = 180;
    [Range(64, 256)] public int textureResolution = 128;
    [Range(5, 30)] public float refreshHz = 15;
    [Range(0, .99f)] public float fadeStartFraction = .7f;
    [Min(.01f)] public float translationSmoothingSeconds = .2f;
    [Min(.01f)] public float visibilitySmoothingSeconds = .5f;
    [Min(.01f)] public float compassSmoothingSeconds = .35f;
    [Range(0, 1)] public float weatherVisibilityMultiplier = 1;
    [Range(0, 1)] public float waveMarks = .25f;
    public Color waterColor = new Color(.08f, .20f, .25f, .95f);
    public Color landColor = new Color(.55f, .59f, .42f, 1);
}

/// <summary>Circular boat-up observation inset; deterministic local geometry, never a position fix.</summary>
public sealed class PilotingViewscapeRenderer
{
    private static readonly ProfilerMarker BuildMarker = new("Piloting.Viewscape.Build");
    public PilotingViewscapeSettings Settings = new();
    private Texture2D _texture;
    private readonly PilotingLandGpuRenderer _land = new();
    private Color[] _pixels;
    private float _nextBuild, _nextEnvironment, _visibility = 1;
    private bool _ready;
    private Vector2 _observer;
    private int _warpRevision;
    private FogManager _fog;
    private WeatherManager _weather;
    private WorldMapTopographyField _field;
    private float _sea;
    private float _radius;
    private readonly List<HarborVisualObservation> _harbors = new();
    private readonly Vector2[] _corners = new Vector2[4];

    public void Tick(BoatSceneWorldPositionBridge bridge, BoatHarborPresentation harbor, float heading, float dt)
    {
        var cache = WorldMapRuntimeCache.I;
        if (!Settings.enabled || bridge == null || cache == null || !cache.HasTopography ||
            !WorldNavigationService.TryGetTrueWorldPosition(out var world))
        { _ready = false; return; }
        _radius = PilotingLocalVisibility.PhysicalRadius(Settings.visibleRadiusNauticalMiles,
            Settings.physicalUnitsPerNauticalMile, bridge.WorldUnitsPerLocalUnit, Settings.observerHeightMultiplier);
        if (_radius <= .0001f) { _ready = false; return; }
        if (!_ready || _field != cache.Field || _warpRevision != bridge.DebugWarpRevision)
        { _observer = world; _nextBuild = 0; }
        else _observer += WorldTopologyService.Delta(_observer, world) * HarborPresentationMath.SmoothFactor(Settings.translationSmoothingSeconds, dt);
        _observer = WorldTopologyService.Current.Normalize(_observer);
        _field = cache.Field; _sea = cache.EffectiveSeaLevel01;
        _warpRevision = bridge.DebugWarpRevision;
        if (Time.unscaledTime >= _nextEnvironment)
        {
            _nextEnvironment = Time.unscaledTime + .5f;
            _fog = UnityEngine.Object.FindAnyObjectByType<FogManager>();
            _weather = UnityEngine.Object.FindAnyObjectByType<WeatherManager>();
        }
        float target = HarborPresentationMath.Visibility(_fog != null ? _fog.FogIntensity : _weather != null ? _weather.FogIntensity : 0,
            _weather != null ? _weather.RainDropDensity : 0) * Settings.weatherVisibilityMultiplier;
        _visibility = !_ready ? target : Mathf.Lerp(_visibility, target, HarborPresentationMath.SmoothFactor(Settings.visibilitySmoothingSeconds, dt));
        _ready = true;
        if (Time.unscaledTime < _nextBuild) return;
        _nextBuild = Time.unscaledTime + 1 / Mathf.Max(1, Settings.refreshHz);
        Build(harbor, heading);
    }

    private void Build(BoatHarborPresentation harbor, float heading)
    {
        using var buildSample = BuildMarker.Auto();
        int resolution = Mathf.Clamp(Settings.textureResolution, 64, 256);
        if (_texture == null || _texture.width != resolution)
        {
            Release();
            _texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            { name = "Local Viewscape_Runtime", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _pixels = new Color[resolution * resolution];
        }
        float radius = _radius;
        float effective = radius * _visibility;
        System.Array.Clear(_pixels,0,_pixels.Length);
        HarborVisualObservation.Collect(_observer, effective, _harbors);
        foreach (var observation in _harbors)
        {
            Vector2 relative = HarborPresentationMath.BoatRelative(WorldTopologyService.Delta(_observer, observation.Harbor.Position), heading);
            Color color = new Color(.85f, .75f, .45f, HarborPresentationMath.RangeFade(relative.magnitude, effective, Settings.fadeStartFraction));
            Vector2 pixel = Pixel(relative, radius, resolution);
            // A tiny tower and roof shape rather than a map node icon/name.
            for (int row = 0; row < 5; row++) for (int column = -1; column <= 1; column++)
                Paint(Mathf.RoundToInt(pixel.x) + column, Mathf.RoundToInt(pixel.y) + row, color, resolution);
            Paint(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y) + 5, color, resolution);
        }
        if (harbor != null && harbor.Controller != null && !string.IsNullOrEmpty(harbor.Controller.CurrentHarborNodeId))
        {
            var berth = harbor.Controller.CurrentHarborBerth;
            HarborPresentationMath.BerthCorners(berth, _corners);
            for (int i = 0; i < 4; i++)
                Segment(_corners[i], _corners[(i + 1) % 4], heading, radius, effective, resolution);
        }
        // Fixed forward-facing boat at center. No position or compass ticks.
        int center = resolution / 2;
        for (int row = -3; row <= 4; row++)
        {
            int half = row > 1 ? 0 : 1;
            for (int x = -half; x <= half; x++) Paint(center + x, center + row, new Color(1, .85f, .4f, 1), resolution);
        }
        _texture.SetPixels(_pixels); _texture.Apply(false, false);
        Vector2 axisX = PilotingLocalVisibility.WorldOffset(new Vector2(radius*2,0),heading);
        Vector2 axisY = PilotingLocalVisibility.WorldOffset(new Vector2(0,radius*2),heading);
        _land.Render(_field,_observer-(axisX+axisY)*.5f,axisX,axisY,_sea,_visibility,resolution,resolution,Settings,_texture);
    }

    private static Vector2 Pixel(Vector2 relative, float radius, int resolution) =>
        (relative / Mathf.Max(.001f, radius) + Vector2.one) * ((resolution - 1) * .5f);
    private void Segment(Vector2 from, Vector2 to, float heading, float radius, float effective, int resolution)
    {
        Vector2 a = HarborPresentationMath.BoatRelative(WorldTopologyService.Delta(_observer, from), heading);
        Vector2 delta = HarborPresentationMath.BoatRelative(WorldTopologyService.Delta(from, to), heading);
        int steps = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / Mathf.Max(.001f, radius) * resolution), 1, resolution * 4);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 relative = a + delta * (i / (float)steps);
            Color color = new Color(.3f, .95f, .85f, HarborPresentationMath.RangeFade(relative.magnitude, effective, Settings.fadeStartFraction));
            Vector2 p = Pixel(relative, radius, resolution);
            Paint(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), color, resolution);
        }
    }
    private void Paint(int x, int y, Color color, int resolution)
    {
        if (x < 0 || y < 0 || x >= resolution || y >= resolution || color.a <= 0) return;
        _pixels[y * resolution + x] = color;
    }
    public void Draw(Rect rect)
    { if (_ready && _land.Texture != null) GUI.DrawTexture(rect, _land.Texture); }
    public void Reset() { _ready = false; _nextBuild = _nextEnvironment = 0; _harbors.Clear(); Release(); }
    private void Release()
    { _land.Reset(); if (_texture != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(_texture); else UnityEngine.Object.DestroyImmediate(_texture); } _texture = null; _pixels = null; }
}
