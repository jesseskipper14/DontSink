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

    public bool IsAttached =>
        _attached &&
        _payload != null &&
        _runtimeJoint != null &&
        _runtimeJoint.enabled;

    public float DeployedLength => _deployedLength;
    public float CurrentDistance => _currentDistance;
    public float Slack => _slack;

    // Retained because WinchModule / future line-failure work already consume it.
    // In the current Unity joint setup this has been observed to report zero.
    public float CurrentTension => _currentTension;
    public float AppliedTension => _currentTension;

    public bool IsOverWorkingLoad =>
        _workingLoad > 0f &&
        _currentTension > _workingLoad;

    public bool IsOverBreakingLoad =>
        _breakingLoad > 0f &&
        _currentTension > _breakingLoad;

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

        UpdateJointAnchors();

        _runtimeJoint.distance = _deployedLength;
        _attached = true;

        UpdateRuntimeMeasurements();
        SetRendererVisible(true);
    }

    public void Detach()
    {
        _attached = false;

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
    }

    private void FixedUpdate()
    {
        if (!_attached ||
            _startAnchor == null ||
            _payload == null ||
            _payload.Rigidbody == null ||
            _runtimeJoint == null ||
            !_runtimeJoint.enabled)
        {
            if (_attached)
                Detach();

            return;
        }

        UpdateJointAnchors();

        if (!Mathf.Approximately(_runtimeJoint.distance, _deployedLength))
            _runtimeJoint.distance = _deployedLength;

        UpdateRuntimeMeasurements();
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

    private void UpdateRuntimeMeasurements()
    {
        if (_startAnchor == null || _payload == null)
        {
            ResetMeasurements();
            return;
        }

        Vector2 start = _startAnchor.position;
        Vector2 end = _payload.TetherAnchor.position;

        _currentDistance = Vector2.Distance(start, end);
        _slack = Mathf.Max(0f, _deployedLength - _currentDistance);

        if (_runtimeJoint != null && _runtimeJoint.enabled)
        {
            _currentTension =
                _runtimeJoint.GetReactionForce(Time.fixedDeltaTime).magnitude;
        }
        else
        {
            _currentTension = 0f;
        }
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
