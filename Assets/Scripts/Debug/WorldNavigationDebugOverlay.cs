using System.Globalization;
using UnityEngine;

/// <summary>
/// Developer-only world navigation diagnostics. Attach once to a persistent
/// service object; active BoatScene references are resolved after scene changes.
/// No camera, player knowledge, fuel, or physics state is owned by this window.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class WorldNavigationDebugOverlay : MonoBehaviour, IEscapeClosable
{
    [SerializeField] private KeyCode toggleKey = KeyCode.F4;
    [SerializeField] private bool startOpen;
    [SerializeField] private Rect windowRect = new Rect(20f, 80f, 490f, 560f);
    [SerializeField] private int windowId = 92752;

    private bool _open;
    private BoatSceneWorldPositionBridge _bridge;
    private BoatPilotingSimulation _simulation;
    private EscapeCloseRegistry _escape;
    private float _nextResolveTime;
    private string _targetX = "0", _targetY = "0", _status = string.Empty;
    private Vector2 _previousLocalPosition;
    private bool _hasSample;
    private int _warpRevision;
    private double _physicalDistance, _worldDistance, _elapsed;
    private Vector2 _scroll;

    public bool IsEscapeOpen => _open && isActiveAndEnabled;
    public int EscapePriority => 200;
    public bool CloseFromEscape() { _open = false; return true; }

    private void Awake() { _open = startOpen; }

    private void OnDisable()
    {
        if (_escape != null) _escape.Unregister(this);
        _escape = null;
        _hasSample = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) _open = !_open;
        if (Time.unscaledTime >= _nextResolveTime)
        {
            _nextResolveTime = Time.unscaledTime + 1f;
            ResolveSceneReferences();
            EscapeCloseRegistry registry = EscapeCloseRegistry.TryGetOrFind();
            if (_escape != registry)
            {
                if (_escape != null) _escape.Unregister(this);
                _escape = registry;
                if (_escape != null) _escape.Register(this);
            }
        }
    }

    private void ResolveSceneReferences()
    {
        // Fail closed if more than one active bridge exists; never guess a boat.
        BoatSceneWorldPositionBridge selected = null;
        foreach (var bridge in FindObjectsByType<BoatSceneWorldPositionBridge>(FindObjectsSortMode.None))
        {
            if (!bridge.isActiveAndEnabled) continue;
            if (selected != null) { selected = null; break; }
            selected = bridge;
        }
        if (_bridge != selected)
        {
            _bridge = selected;
            ResetMeasurement();
            _status = string.Empty;
        }
        _simulation = _bridge != null && _bridge.PilotingState != null
            ? _bridge.PilotingState.GetComponent<BoatPilotingSimulation>() : null;
    }

    private void FixedUpdate()
    {
        if (_bridge == null || !_bridge.isActiveAndEnabled || !_bridge.ProjectionReady ||
            _bridge.PilotingState == null || _simulation == null || !_simulation.isActiveAndEnabled)
        { _hasSample = false; return; }
        Vector2 position = _bridge.PilotingState.NavigationPosition;
        if (_hasSample && _warpRevision == _bridge.DebugWarpRevision)
        {
            // Project displacement, not absolute world coordinates, to avoid
            // precision loss and to exclude geographic debug relocation.
            _worldDistance += _bridge.ProjectNavigationVector(position - _previousLocalPosition).magnitude;
            _physicalDistance += Mathf.Abs(_simulation.PhysicalTravelDelta);
            _elapsed += Time.fixedDeltaTime;
        }
        _previousLocalPosition = position;
        _warpRevision = _bridge.DebugWarpRevision;
        _hasSample = true;
    }

    private void ResetMeasurement()
    {
        _physicalDistance = _worldDistance = _elapsed = 0;
        _hasSample = false;
    }

    private void OnGUI()
    {
        if (_open)
            windowRect = RuntimeDebugOverlayGUI.DrawWindow(windowId, windowRect,
                "World Navigation Debug", DrawContents);
    }

    private void DrawContents()
    {
        GUILayout.BeginArea(new Rect(12f, 28f, windowRect.width - 24f, windowRect.height - 38f));
        _scroll = GUILayout.BeginScrollView(_scroll);
        bool hasWorld = WorldNavigationService.TryGetTrueWorldPosition(out Vector2 world);
        GUILayout.Label(hasWorld ? $"True world X/Y: {world.x:0.000}, {world.y:0.000}" : "True world X/Y: unavailable");
        var topology = WorldTopologyService.Current;
        if (topology.IsValid)
        {
            GUILayout.Label($"X wraps [{topology.Bounds.xMin:0.##}, {topology.Bounds.xMax:0.##}); Y finite [{topology.Bounds.yMin:0.##}, {topology.Bounds.yMax:0.##}]");
            Vector2 west = new Vector2(topology.Bounds.xMin + 1f, 0f), east = new Vector2(topology.Bounds.xMax - 1f, 0f);
            GUILayout.Label($"Seam probe: raw {Vector2.Distance(west, east):0.##} / wrapped {topology.Distance(west, east):0.##}");
            if (_bridge != null && _bridge.PilotingState != null && _bridge.ProjectionReady)
            {
                Vector2 raw = _bridge.ProjectNavigationPosition(_bridge.PilotingState.NavigationPosition);
                GUILayout.Label($"Unwrapped projection: {raw.x:0.###}, {raw.y:0.###}");
            }
        }
        GUILayout.Label($"Authority: {(GameplayAuthority.IsAuthoritative ? "local authority" : "client / read only")}");
        if (WorldBoundaryService.TryGetCurrent(out var boundary))
        {
            GUILayout.Label($"Polar boundary: {boundary.Band}; nearest pole {boundary.Pole}; edge distance {boundary.SignedDistanceToEdge:0.00} map units");
            GUILayout.Label($"Polar severity {boundary.Severity:P0}; warning width {boundary.SoftWidth:0.00}; core width {boundary.HardWidth:0.00}");
            if (boundary.Band != WorldBoundaryBand.Normal)
                GUILayout.Label($"{(boundary.Band == WorldBoundaryBand.Hard ? "DANGEROUS POLAR CORE" : "POLAR APPROACH — TURN BACK")}: return {(boundary.Pole == WorldBoundaryPole.North ? "south" : "north")}. Effects not connected yet.");
        }
        else GUILayout.Label("Polar boundary: unavailable (navigation or world bounds missing)");
        bool ready = _bridge != null && _bridge.isActiveAndEnabled && _bridge.ProjectionReady && _bridge.PilotingState != null;
        if (ready)
        {
            BoatPilotingState state = _bridge.PilotingState;
            Vector2 velocity = _bridge.ProjectNavigationVector(state.NavigationVelocity);
            GUILayout.Label($"Heading: {_bridge.GeographicHeadingDegrees:0.0}° (world north = 0°, clockwise)");
            GUILayout.Label($"World speed: {velocity.magnitude:0.000} map units / game second");
            GUILayout.Label($"Scale: 1 physical unit = {_bridge.WorldUnitsPerLocalUnit:0.000000} map units");
            if (_simulation != null)
            {
                GUILayout.Label($"Physical forward speed: {_simulation.PhysicalForwardSpeed:0.000} units / game second");
                BoatVoyageStripState strip = _simulation.VoyageStrip;
                if (strip.IsActive)
                {
                    GUILayout.Label($"Voyage strip: {strip.Position:0.000} signed physical units; last delta {strip.LastTravelDelta:0.000000}");
                    GUILayout.Label($"Strip local origin: {strip.LocalOriginCoordinate:0.000}; axis {strip.SceneAxis.x:0.###}, {strip.SceneAxis.y:0.###}");
                    GUILayout.Label($"Voyage revision {strip.Revision}; seed {strip.Seed}; samples {strip.SampleCount}; rebases {strip.RebaseCount}");
                }
                else GUILayout.Label("Voyage strip: inactive (no authoritative voyage sample)");
                if (BoatTerrainStreamer2D.TryGet(_simulation.gameObject.scene, out var terrain))
                {
                    GUILayout.Label($"Streamed floor: {(terrain.IsReady ? "ready" : "waiting for setup")}; loaded {terrain.LoadedCount}, committed {terrain.CommittedCount}, interests {terrain.InterestCount}");
                    GUILayout.Label($"Ground fill bottom: {terrain.LastUsedBottomY:0.0}");
                    GUILayout.Label($"Geographic depth: {(terrain.GeographicDepthActive ? "active" : "fallback")}; target {terrain.GeographicTargetDepth:0.0}");
                    GUILayout.Label($"Physical coasts: {(terrain.CoastalTerrainActive ? "active; mesh = collision; negative target depth = land" : "off")}");
                    if (terrain.CoastalTerrainActive)
                        GUILayout.Label($"Coastal floor guard: {terrain.CoastalClearanceStatus}");
                    GUILayout.Label($"Terrain feature directives: {terrain.FeatureCount}");
                    BoatLandEncounter land = terrain.LandEncounter;
                    GUILayout.Label($"Geographic surface: {(land.HasGeography ? (land.IsOnLand ? "LAND" : "water") : "unavailable")}");
                    if (land.HasLandmass)
                    {
                        GUILayout.Label($"Dominant landmass #{land.LandmassId}: ~{land.Distance:0.00} map units away; range {land.VisibilityRange:0.00}");
                        GUILayout.Label($"Land sample: ({land.NearestLandSample.x:0.00}, {land.NearestLandSample.y:0.00}); area ~{land.ApproximateArea:0.0}; diameter ~{land.EquivalentDiameter:0.00}");
                        if (land.OffsetToLand.sqrMagnitude > .00000001f)
                        {
                            float bearing = Mathf.Repeat(Mathf.Atan2(land.OffsetToLand.x, land.OffsetToLand.y) * Mathf.Rad2Deg, 360f);
                            string compass = (Mathf.RoundToInt(bearing / 45f) % 8) switch
                            {
                                0 => "N", 1 => "NE", 2 => "E", 3 => "SE",
                                4 => "S", 5 => "SW", 6 => "W", _ => "NW"
                            };
                            float relative = Mathf.DeltaAngle(_bridge.GeographicHeadingDegrees, bearing);
                            GUILayout.Label($"To nearest land sample: {compass}, bearing {bearing:0.0}° (N=0°, E=90°)");
                            GUILayout.Label($"Relative to heading: {relative:+0.0;-0.0;0.0}° (+right / -left)");
                        }
                        else GUILayout.Label("Nearest land sample is here; no direction.");
                        if (land.IsOnLand) GUILayout.Label("On land: sample direction is not an escape direction to water.");
                    }
                    else if (land.HasGeography) GUILayout.Label("Dominant landmass: none in range");
                    if (land.HasGeography) GUILayout.Label($"Land identity sampling: {terrain.LandQuerySampleSpacing:0.00} map units (approximate coast distance)");
                    if (terrain.TrySampleGround(_simulation.transform.position.x, out float seabedY, out _))
                        GUILayout.Label($"Seabed under boat: Y {seabedY:0.0}");
                    if (BoatGeographicObstruction2D.TryGet(_simulation.gameObject.scene, out var obstruction))
                    {
                        GUILayout.Label($"Geographic obstruction: {obstruction.Status}; physical barrier {(obstruction.BarrierActive ? "ON" : "off")}");
                        if (obstruction.LastResult.HasGeography)
                            GUILayout.Label($"Land penetration ~{obstruction.LastResult.InitialPenetration:0.000} map units; allowed probe {obstruction.LastResult.AllowedFraction:P0}");
                        GUILayout.Label($"Last geographic travel/drift permitted: {obstruction.LastNavigationFraction:P0}");
                    }
                }
                Boat boat = _simulation.Boat;
                if (boat != null && boat.rb != null)
                    GUILayout.Label($"BoatScene X/Y: {boat.rb.position.x:0.000}, {boat.rb.position.y:0.000}");
            }
        }
        else GUILayout.Label("BoatScene projection unavailable (docked, loading, or ambiguous).");

        GUILayout.Space(6f);
        GUILayout.Label($"Measured: {_physicalDistance:0.000} physical / {_worldDistance:0.000} map units");
        GUILayout.Label($"Measurement time: {_elapsed:0.0} game seconds; warps excluded");
        if (GUILayout.Button("Reset distance measurement")) ResetMeasurement();

        GUILayout.Space(8f);
        GUILayout.Label("Warp to geographic coordinates (BoatScene only)");
        DrawCoordinate("X", ref _targetX);
        DrawCoordinate("Y", ref _targetY);
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && hasWorld;
        if (GUILayout.Button("Copy current coordinates"))
        {
            _targetX = world.x.ToString("R", CultureInfo.InvariantCulture);
            _targetY = world.y.ToString("R", CultureInfo.InvariantCulture);
        }
        GUI.enabled = previousEnabled && ready && GameplayAuthority.IsAuthoritative;
        if (topology.IsValid && hasWorld)
        {
            GUILayout.Label("Polar test presets: fill coordinates, then Warp geography");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill north warning")) FillPolarTarget(topology, world.x, true, false);
            if (GUILayout.Button("Fill north core")) FillPolarTarget(topology, world.x, true, true);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill south warning")) FillPolarTarget(topology, world.x, false, false);
            if (GUILayout.Button("Fill south core")) FillPolarTarget(topology, world.x, false, true);
            GUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Warp geography")) Warp();
        GUI.enabled = previousEnabled;
        if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
        GUILayout.Label("Warp is session-only. Committed seabed stays fixed; new chunks follow the new geography.");
        GUILayout.Label($"{toggleKey}: toggle   |   Escape: close");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static void DrawCoordinate(string label, ref string text)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(20f));
        text = GUILayout.TextField(text);
        GUILayout.EndHorizontal();
    }

    private void FillPolarTarget(WorldTopology topology, float x, bool north, bool hard)
    {
        float fraction = hard ? WorldBoundaryQuery.DefaultHardFraction * .5f :
            (WorldBoundaryQuery.DefaultSoftFraction + WorldBoundaryQuery.DefaultHardFraction) * .5f;
        float offset = topology.Bounds.height * fraction;
        float y = north ? topology.Bounds.yMax - offset : topology.Bounds.yMin + offset;
        _targetX = topology.NormalizeX(x).ToString("R", CultureInfo.InvariantCulture);
        _targetY = y.ToString("R", CultureInfo.InvariantCulture);
        _status = "Polar test coordinates filled; press Warp geography to apply.";
    }

    private void Warp()
    {
        if (!float.TryParse(_targetX, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(_targetY, NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
        { _status = "Enter numeric X and Y coordinates (decimal point: .)."; return; }
        if (_bridge == null) { _status = "BoatScene projection unavailable."; return; }
        if (!_bridge.TryDebugWarp(new Vector2(x, y), out string reason))
        { _status = reason; return; }
        // Rebase the measurement immediately so ordinary travel after the warp
        // is counted, but the warp displacement itself is never counted.
        _previousLocalPosition = _bridge.PilotingState.NavigationPosition;
        _warpRevision = _bridge.DebugWarpRevision;
        _status = $"Warped to {x:0.000}, {y:0.000}.";
    }
}
