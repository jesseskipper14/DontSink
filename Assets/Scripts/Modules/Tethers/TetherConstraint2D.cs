using UnityEngine;

[DisallowMultipleComponent]
public sealed class TetherConstraint2D : MonoBehaviour
{
    [Header("Constraint")]
    [SerializeField, Min(1f)] private float stiffness = 1200f;
    [SerializeField, Min(0f)] private float damping = 120f;
    [SerializeField, Min(1f)] private float maxSolverForce = 250000f;

    [Header("Visual")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField, Min(0f)] private float visualSlackSagPerMeter = 0.05f;
    [SerializeField, Min(0f)] private float maxVisualSag = 2f;

    private Transform _startAnchor;
    private Rigidbody2D _startBody;
    private TetherPayloadModule _payload;
    private float _deployedLength;
    private float _workingLoad;
    private float _breakingLoad;
    private bool _attached;

    public bool IsAttached => _attached && _payload != null;
    public float DeployedLength => _deployedLength;
    public float CurrentDistance { get; private set; }
    public float Slack { get; private set; }
    public float CurrentTension { get; private set; }
    public float AppliedTension { get; private set; }
    public bool IsOverWorkingLoad => _workingLoad > 0f && CurrentTension > _workingLoad;
    public bool IsOverBreakingLoad => _breakingLoad > 0f && CurrentTension > _breakingLoad;

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        SetRendererVisible(false);
    }

    public void Bind(
        Transform startAnchor,
        Rigidbody2D startBody,
        TetherPayloadModule payload,
        float deployedLength,
        float workingLoad,
        float breakingLoad)
    {
        _startAnchor = startAnchor;
        _startBody = startBody;
        _payload = payload;
        _deployedLength = Mathf.Max(0.01f, deployedLength);
        _workingLoad = Mathf.Max(0f, workingLoad);
        _breakingLoad = Mathf.Max(_workingLoad, breakingLoad);
        _attached = _startAnchor != null && _payload != null && _payload.Rigidbody != null;

        SetRendererVisible(_attached);
    }

    public void Detach()
    {
        _attached = false;
        _payload = null;
        CurrentDistance = 0f;
        Slack = 0f;
        CurrentTension = 0f;
        AppliedTension = 0f;
        SetRendererVisible(false);
    }

    public void SetDeployedLength(float meters)
    {
        _deployedLength = Mathf.Max(0.01f, meters);
    }

    public void SetLineRatings(float workingLoad, float breakingLoad)
    {
        _workingLoad = Mathf.Max(0f, workingLoad);
        _breakingLoad = Mathf.Max(_workingLoad, breakingLoad);
    }

    private void FixedUpdate()
    {
        if (!_attached || _startAnchor == null || _payload == null || _payload.Rigidbody == null)
        {
            CurrentTension = 0f;
            AppliedTension = 0f;
            return;
        }

        Rigidbody2D payloadBody = _payload.Rigidbody;
        Vector2 start = _startAnchor.position;
        Vector2 end = _payload.TetherAnchor.position;
        Vector2 delta = end - start;

        float distance = delta.magnitude;
        CurrentDistance = distance;
        Slack = Mathf.Max(0f, _deployedLength - distance);

        if (distance <= 0.0001f || distance <= _deployedLength)
        {
            CurrentTension = 0f;
            AppliedTension = 0f;
            return;
        }

        Vector2 direction = delta / distance;
        float stretch = distance - _deployedLength;

        Vector2 startVelocity = _startBody != null ? _startBody.GetPointVelocity(start) : Vector2.zero;
        Vector2 endVelocity = payloadBody.GetPointVelocity(end);
        float separatingSpeed = Vector2.Dot(endVelocity - startVelocity, direction);

        float demandedTension = stiffness * stretch + damping * separatingSpeed;
        demandedTension = Mathf.Max(0f, demandedTension);

        CurrentTension = demandedTension;
        AppliedTension = Mathf.Min(demandedTension, maxSolverForce);

        Vector2 force = direction * AppliedTension;

        payloadBody.AddForceAtPosition(-force, end, ForceMode2D.Force);

        if (_startBody != null && _startBody.bodyType == RigidbodyType2D.Dynamic && _startBody.simulated)
            _startBody.AddForceAtPosition(force, start, ForceMode2D.Force);
    }

    private void LateUpdate()
    {
        UpdateLineVisual();
    }

    private void UpdateLineVisual()
    {
        if (lineRenderer == null || !_attached || _startAnchor == null || _payload == null)
            return;

        Vector3 start = _startAnchor.position;
        Vector3 end = _payload.TetherAnchor.position;
        float slack = Mathf.Max(0f, _deployedLength - Vector2.Distance(start, end));
        float sag = Mathf.Min(maxVisualSag, slack * visualSlackSagPerMeter);

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
