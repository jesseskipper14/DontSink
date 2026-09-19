using UnityEngine;

[DisallowMultipleComponent]
public sealed class TetherConstraint2D : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField, Min(0f)] private float visualSlackSagPerMeter = 0.05f;
    [SerializeField, Min(0f)] private float maxVisualSag = 2f;

    [Header("Runtime Debug")]
    [SerializeField] private bool _attached;
    [SerializeField] private float _deployedLength;
    [SerializeField] private float _currentDistance;
    [SerializeField] private float _slack;
    [SerializeField] private float _currentTension;
    [SerializeField] private float _workingLoad;
    [SerializeField] private float _breakingLoad;

    private Transform _startAnchor;
    private Rigidbody2D _startBody;
    private TetherPayload _payload;
    private DistanceJoint2D _runtimeJoint;
    private TetherJointBreakRelay2D _breakRelay;

    private bool _breakPending;
    private float _pendingBreakTension;

    public bool IsAttached =>
        _attached &&
        _payload != null &&
        _runtimeJoint != null &&
        _runtimeJoint.enabled;

    public float DeployedLength => _deployedLength;
    public float CurrentDistance => _currentDistance;
    public float Slack => _slack;

    // Tension is sampled from Unity's solved Joint2D reaction force.
    // Sampling happens after physics as well as before the next fixed step so
    // the value is useful to UI / winch logic instead of being read only while
    // the solver is between steps.
    public float CurrentTension => _currentTension;
    public float AppliedTension => _currentTension;

    public bool IsOverWorkingLoad =>
        _workingLoad > 0f &&
        _currentTension > _workingLoad;

    public bool IsOverBreakingLoad =>
        _breakingLoad > 0f &&
        _currentTension > _breakingLoad;

    /// <summary>
    /// Consumes a solver-owned DistanceJoint2D break notification.
    /// The break is latched until the owning WinchModule has had a chance to
    /// release the payload and reconcile line inventory.
    /// </summary>
    public bool TryConsumeBreak(out float breakTension)
    {
        breakTension = 0f;

        if (!_breakPending)
            return false;

        breakTension = Mathf.Max(0f, _pendingBreakTension);
        _breakPending = false;
        _pendingBreakTension = 0f;
        return true;
    }

    private void Awake()
    {
        CacheLineRenderer();

        if (lineRenderer == null)
        {
            Debug.LogWarning(
                $"[TetherConstraint2D:{name}] No LineRenderer assigned or found in children.",
                this);
        }

        SetRendererVisible(false);
    }

    private void CacheLineRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = GetComponentInChildren<LineRenderer>(true);

        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 3;
        }
    }

    public void Bind(
        Transform startAnchor,
        Rigidbody2D startBody,
        TetherPayload payload,
        float deployedLength,
        float workingLoad,
        float breakingLoad)
    {
        Detach();

        _startAnchor = startAnchor;
        _startBody = startBody;
        _payload = payload;
        _deployedLength = Mathf.Max(0.01f, deployedLength);
        _workingLoad = Mathf.Max(0f, workingLoad);
        _breakingLoad = Mathf.Max(_workingLoad, breakingLoad);

        if (_startAnchor == null ||
            _payload == null ||
            _payload.Rigidbody == null)
        {
            ResetMeasurements();
            return;
        }

        Rigidbody2D payloadBody = _payload.Rigidbody;

        _runtimeJoint =
            payloadBody.gameObject.AddComponent<DistanceJoint2D>();

        _runtimeJoint.autoConfigureConnectedAnchor = false;
        _runtimeJoint.autoConfigureDistance = false;
        _runtimeJoint.enableCollision = false;
        _runtimeJoint.maxDistanceOnly = true;
        _runtimeJoint.connectedBody = _startBody;

        _runtimeJoint.breakAction = JointBreakAction2D.Disable;
        ApplyJointBreakSettings();

        _breakRelay =
            payloadBody.GetComponent<TetherJointBreakRelay2D>();

        if (_breakRelay == null)
        {
            _breakRelay =
                payloadBody.gameObject.AddComponent<TetherJointBreakRelay2D>();
        }

        _breakRelay.Bind(
            this,
            _runtimeJoint);

        UpdateJointAnchors();

        _runtimeJoint.distance = _deployedLength;
        _attached = true;

        UpdateGeometryMeasurements();
        SampleReactionForce();
        SetRendererVisible(true);
    }

    public void Detach()
    {
        _attached = false;
        _breakPending = false;
        _pendingBreakTension = 0f;

        if (_breakRelay != null)
            _breakRelay.Unbind(this);

        _breakRelay = null;

        if (_runtimeJoint != null)
        {
            _runtimeJoint.enabled = false;
            Destroy(_runtimeJoint);
        }

        _runtimeJoint = null;
        _startAnchor = null;
        _startBody = null;
        _payload = null;
        _deployedLength = 0f;

        ResetMeasurements();
        SetRendererVisible(false);
    }

    public void SetDeployedLength(float meters)
    {
        _deployedLength = Mathf.Max(0.01f, meters);

        if (_runtimeJoint != null)
            _runtimeJoint.distance = _deployedLength;
    }

    public void SetLineRatings(float workingLoad, float breakingLoad)
    {
        _workingLoad = Mathf.Max(0f, workingLoad);
        _breakingLoad = Mathf.Max(_workingLoad, breakingLoad);
        ApplyJointBreakSettings();
    }

    private void ApplyJointBreakSettings()
    {
        if (_runtimeJoint == null)
            return;

        _runtimeJoint.breakForce =
            _breakingLoad > 0f
                ? _breakingLoad
                : Mathf.Infinity;
    }

    private void FixedUpdate()
    {
        // With breakAction=Disable, a solver break leaves the runtime joint
        // present but disabled. The relay normally catches the callback first;
        // this is a defensive fallback in case another component interfered.
        if (_attached &&
            _runtimeJoint != null &&
            !_runtimeJoint.enabled)
        {
            NotifyRuntimeJointBroken(
                _runtimeJoint);

            return;
        }

        if (!_attached ||
            _startAnchor == null ||
            _payload == null ||
            _payload.Rigidbody == null ||
            _runtimeJoint == null)
        {
            if (_attached)
                Detach();

            return;
        }

        // Read the previous solved step BEFORE changing anchors/distance.
        // Mutating joint configuration first can discard the very reaction data
        // we are trying to observe.
        SampleReactionForce();

        UpdateJointAnchors();

        if (!Mathf.Approximately(_runtimeJoint.distance, _deployedLength))
            _runtimeJoint.distance = _deployedLength;

        UpdateGeometryMeasurements();
    }

    private void Update()
    {
        if (!IsAttached)
            return;

        // Update runs after the physics step(s) for the rendered frame, so this
        // is the important sampling point for HUD/readout purposes.
        UpdateGeometryMeasurements();
        SampleReactionForce();
    }

    private void UpdateJointAnchors()
    {
        if (_runtimeJoint == null ||
            _payload == null ||
            _payload.Rigidbody == null ||
            _startAnchor == null)
        {
            return;
        }

        Rigidbody2D payloadBody = _payload.Rigidbody;

        _runtimeJoint.anchor =
            payloadBody.transform.InverseTransformPoint(
                _payload.TetherAnchor.position);

        _runtimeJoint.connectedAnchor =
            _startBody != null
                ? _startBody.transform.InverseTransformPoint(_startAnchor.position)
                : _startAnchor.position;
    }

    private void UpdateGeometryMeasurements()
    {
        if (_startAnchor == null || _payload == null)
        {
            _currentDistance = 0f;
            _slack = 0f;
            return;
        }

        Vector2 start = _startAnchor.position;
        Vector2 end = _payload.TetherAnchor.position;

        _currentDistance = Vector2.Distance(start, end);
        _slack = Mathf.Max(0f, _deployedLength - _currentDistance);
    }

    private void SampleReactionForce()
    {
        if (_runtimeJoint == null ||
            !_runtimeJoint.enabled)
        {
            return;
        }

        float tension =
            _runtimeJoint.reactionForce.magnitude;

        // Keep the explicit timestep API as a fallback. In Unity 6 the
        // reactionForce property is the preferred solved-state read, but both
        // values represent the same joint reaction in Newtons.
        if (tension <= 0.0001f)
        {
            tension =
                _runtimeJoint.GetReactionForce(
                    Time.fixedDeltaTime).magnitude;
        }

        _currentTension =
            Mathf.Max(
                0f,
                tension);
    }

    internal void NotifyRuntimeJointBroken(
        Joint2D brokenJoint)
    {
        if (brokenJoint == null ||
            _runtimeJoint == null ||
            brokenJoint != _runtimeJoint ||
            _breakPending)
        {
            return;
        }

        float breakTension =
            brokenJoint.reactionForce.magnitude;

        if (breakTension <= 0.0001f)
        {
            breakTension =
                brokenJoint.GetReactionForce(
                    Time.fixedDeltaTime).magnitude;
        }

        _currentTension =
            Mathf.Max(
                _currentTension,
                breakTension);

        _pendingBreakTension =
            _currentTension;

        _breakPending = true;
        _attached = false;

        SetRendererVisible(false);
    }

    private void ResetMeasurements()
    {
        _currentDistance = 0f;
        _slack = 0f;
        _currentTension = 0f;
    }

    private void LateUpdate()
    {
        if (!IsAttached ||
            _startAnchor == null ||
            _payload == null)
        {
            SetRendererVisible(false);
            return;
        }

        SampleReactionForce();
        UpdateLineVisual();
        SetRendererVisible(true);
    }

    private void UpdateLineVisual()
    {
        if (lineRenderer == null)
        {
            CacheLineRenderer();

            if (lineRenderer == null)
                return;
        }

        Vector3 start = _startAnchor.position;
        Vector3 end = _payload.TetherAnchor.position;

        float slack =
            Mathf.Max(
                0f,
                _deployedLength - Vector2.Distance(start, end));

        float sag =
            Mathf.Min(
                maxVisualSag,
                slack * visualSlackSagPerMeter);

        Vector3 mid = Vector3.Lerp(start, end, 0.5f);
        mid.y -= sag;

        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, mid);
        lineRenderer.SetPosition(2, end);
    }

    private void SetRendererVisible(bool visible)
    {
        if (lineRenderer != null)
            lineRenderer.enabled = visible;
    }
}
