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
        bool hasWorld = WorldNavigationService.TryGetTrueWorldPosition(out Vector2 world);
        GUILayout.Label(hasWorld ? $"True world X/Y: {world.x:0.000}, {world.y:0.000}" : "True world X/Y: unavailable");
        GUILayout.Label($"Authority: {(GameplayAuthority.IsAuthoritative ? "local authority" : "client / read only")}");
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
        if (GUILayout.Button("Warp geography")) Warp();
        GUI.enabled = previousEnabled;
        if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
        GUILayout.Label("Warp is session-only. Local terrain stays unchanged until streaming lands.");
        GUILayout.Label($"{toggleKey}: toggle   |   Escape: close");
        GUILayout.EndArea();
    }

    private static void DrawCoordinate(string label, ref string text)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(20f));
        text = GUILayout.TextField(text);
        GUILayout.EndHorizontal();
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
