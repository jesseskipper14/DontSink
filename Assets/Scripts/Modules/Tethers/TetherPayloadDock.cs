using System;
using UnityEngine;

public enum TetherDockState
{
    Free = 0,
    Capturing = 1,
    Docked = 2
}

/// <summary>
/// Generic dock/lock for a live tether payload.
///
/// The payload GameObject remains physically present while stowed so it can
/// provide real sprites, colliders, boarding spaces, ledges, and other gameplay.
///
/// Docking is a short kinematic capture rather than an instantaneous teleport:
/// - TryDock claims the payload and enters Capturing;
/// - the authoritative dock moves the kinematic Rigidbody2D toward DockPoint;
/// - only after the payload reaches the final pose does state become Docked;
/// - on release, its authored/free Rigidbody2D state is restored.
///
/// The same GameObject is never destroyed merely because it deploys/retrieves.
/// </summary>
[DisallowMultipleComponent]
public sealed class TetherPayloadDock : MonoBehaviour
{
    [Header("Gameplay Authority")]
    [Tooltip(
        "Only authoritative peers advance Capturing motion. Replicated/client presentation may " +
        "still hold dock state without independently moving the shared payload.")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("Dock")]
    [Tooltip(
        "Optional exact capture pose. If blank, TetherDeploymentModule supplies " +
        "its PayloadHangPoint as the fallback.")]
    [SerializeField] private Transform dockPoint;

    [Tooltip(
        "When released, inherit the boat Rigidbody2D linear/angular velocity so " +
        "the payload does not lose the boat's motion instantaneously.")]
    [SerializeField] private bool inheritBoatVelocityOnRelease = true;

    [Header("Capture Motion")]
    [Tooltip("World-space translation speed used while Capturing.")]
    [SerializeField, Min(0.01f)] private float captureMoveSpeed = 2.5f;

    [Tooltip("Rotation speed in degrees/second used while Capturing.")]
    [SerializeField, Min(1f)] private float captureRotateSpeed = 180f;

    [Tooltip("Distance from DockPoint considered close enough to finalize the dock.")]
    [SerializeField, Min(0.0001f)] private float capturePositionTolerance = 0.01f;

    [Tooltip("Angular difference considered close enough to finalize the dock.")]
    [SerializeField, Min(0.01f)] private float captureRotationTolerance = 0.5f;

    [Header("Runtime Debug")]
    [SerializeField] private TetherPayload dockedPayload;
    [SerializeField] private Rigidbody2D dockedBody;
    [SerializeField] private TetherDockState dockState = TetherDockState.Free;
    [SerializeField] private Transform activeDockPoint;

    private RigidbodyType2D _freeBodyType = RigidbodyType2D.Dynamic;
    private RigidbodyConstraints2D _freeConstraints = RigidbodyConstraints2D.None;
    private bool _freeSimulated = true;
    private bool _savedFreeBodyState;
    private Transform _originalParent;

    public event Action<TetherPayloadDock, TetherPayload, TetherDockState> DockStateChanged;

    /// <summary>
    /// Payload currently claimed by this dock. This is non-null during both
    /// Capturing and Docked states.
    /// </summary>
    public TetherPayload DockedPayload => dockedPayload;

    public bool HasDockedPayload => dockedPayload != null;
    public TetherDockState State => dockState;
    public bool HasGameplayAuthority =>
        GameplayAuthority.CanRun(gameplayAuthorityMode);

    public Transform DockPoint =>
        activeDockPoint != null
            ? activeDockPoint
            : dockPoint != null
                ? dockPoint
                : transform;

    private void FixedUpdate()
    {
        if (!HasGameplayAuthority)
            return;

        if (dockState != TetherDockState.Capturing)
            return;

        TickCapture();
    }

    public TetherDockState GetDockState(TetherPayload payload)
    {
        if (payload == null || !ReferenceEquals(dockedPayload, payload))
            return TetherDockState.Free;

        return dockState;
    }

    public bool IsCapturing(TetherPayload payload)
    {
        return
            payload != null &&
            ReferenceEquals(dockedPayload, payload) &&
            dockState == TetherDockState.Capturing;
    }

    public bool IsDocked(TetherPayload payload)
    {
        return
            payload != null &&
            ReferenceEquals(dockedPayload, payload) &&
            dockState == TetherDockState.Docked;
    }

    public bool TryDock(
        TetherPayload payload,
        Transform fallbackDockPoint = null)
    {
        if (payload == null || payload.Rigidbody == null)
            return false;

        if (dockedPayload != null && !ReferenceEquals(dockedPayload, payload))
            return false;

        // A payload may be claimed by only one dock at a time. This is payload-
        // local rather than global, so multiple boats/bells can capture in
        // parallel without stealing authority from one another.
        if (payload.ActiveDock != null &&
            !ReferenceEquals(payload.ActiveDock, this))
        {
            return false;
        }

        Transform point =
            dockPoint != null
                ? dockPoint
                : fallbackDockPoint != null
                    ? fallbackDockPoint
                    : transform;

        if (point == null)
            return false;

        // Idempotent repeat request from the same payload.
        if (ReferenceEquals(dockedPayload, payload))
        {
            activeDockPoint = point;
            return true;
        }

        Rigidbody2D rb = payload.Rigidbody;

        SaveFreeBodyStateIfNeeded(payload, rb);

        dockedPayload = payload;
        dockedBody = rb;
        activeDockPoint = point;

        payload.SetActiveDock(this);

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Kinematic;

        SetDockState(TetherDockState.Capturing);

        // Do NOT parent or snap here. The Rigidbody remains in world space while
        // FixedUpdate brings it continuously toward the moving dock point.
        return true;
    }

    public bool TryRelease(TetherPayload payload)
    {
        if (payload == null ||
            !ReferenceEquals(dockedPayload, payload) ||
            dockedBody == null)
        {
            return false;
        }

        Rigidbody2D boatBody = ResolveBoatBody();

        Vector2 inheritedLinearVelocity =
            boatBody != null
                ? boatBody.linearVelocity
                : Vector2.zero;

        float inheritedAngularVelocity =
            boatBody != null
                ? boatBody.angularVelocity
                : 0f;

        Transform releaseParent = _originalParent;

        payload.transform.SetParent(
            releaseParent,
            worldPositionStays: true);

        dockedBody.bodyType =
            _savedFreeBodyState
                ? _freeBodyType
                : RigidbodyType2D.Dynamic;

        dockedBody.constraints =
            _savedFreeBodyState
                ? _freeConstraints
                : RigidbodyConstraints2D.None;

        dockedBody.simulated =
            _savedFreeBodyState
                ? _freeSimulated
                : true;

        if (inheritBoatVelocityOnRelease &&
            boatBody != null &&
            dockedBody.simulated)
        {
            dockedBody.linearVelocity = inheritedLinearVelocity;
            dockedBody.angularVelocity = inheritedAngularVelocity;
        }
        else
        {
            dockedBody.linearVelocity = Vector2.zero;
            dockedBody.angularVelocity = 0f;
        }

        if (dockedBody.simulated)
            dockedBody.WakeUp();

        payload.ClearActiveDock(this);

        dockedPayload = null;
        dockedBody = null;
        activeDockPoint = null;

        ClearSavedFreeBodyState();
        SetDockState(TetherDockState.Free);

        return true;
    }

    private void TickCapture()
    {
        if (dockedPayload == null || dockedBody == null || activeDockPoint == null)
        {
            AbortBrokenCapture();
            return;
        }

        Vector2 targetPosition = activeDockPoint.position;
        float targetRotation = activeDockPoint.eulerAngles.z;

        float moveStep = captureMoveSpeed * Time.fixedDeltaTime;
        float rotateStep = captureRotateSpeed * Time.fixedDeltaTime;

        Vector2 nextPosition =
            Vector2.MoveTowards(
                dockedBody.position,
                targetPosition,
                moveStep);

        float nextRotation =
            Mathf.MoveTowardsAngle(
                dockedBody.rotation,
                targetRotation,
                rotateStep);

        dockedBody.MovePosition(nextPosition);
        dockedBody.MoveRotation(nextRotation);

        bool positionReady =
            Vector2.Distance(nextPosition, targetPosition) <=
            capturePositionTolerance;

        bool rotationReady =
            Mathf.Abs(Mathf.DeltaAngle(nextRotation, targetRotation)) <=
            captureRotationTolerance;

        if (positionReady && rotationReady)
            FinalizeDock();
    }

    private void FinalizeDock()
    {
        if (dockedPayload == null || dockedBody == null || activeDockPoint == null)
        {
            AbortBrokenCapture();
            return;
        }

        // The only exact correction is now tolerance-sized rather than a full
        // deployment-distance teleport.
        dockedBody.position = activeDockPoint.position;
        dockedBody.rotation = activeDockPoint.eulerAngles.z;
        dockedBody.linearVelocity = Vector2.zero;
        dockedBody.angularVelocity = 0f;

        dockedPayload.transform.SetParent(
            activeDockPoint,
            worldPositionStays: true);

        dockedPayload.transform.localPosition = Vector3.zero;
        dockedPayload.transform.localRotation = Quaternion.identity;

        Physics2D.SyncTransforms();

        SetDockState(TetherDockState.Docked);
    }

    private void SaveFreeBodyStateIfNeeded(
        TetherPayload payload,
        Rigidbody2D rb)
    {
        if (_savedFreeBodyState)
            return;

        _freeBodyType = rb.bodyType;
        _freeConstraints = rb.constraints;
        _freeSimulated = rb.simulated;
        _originalParent = payload.transform.parent;
        _savedFreeBodyState = true;
    }

    private void AbortBrokenCapture()
    {
        TetherPayload previous = dockedPayload;

        if (previous != null)
            previous.ClearActiveDock(this);

        dockedPayload = null;
        dockedBody = null;
        activeDockPoint = null;

        ClearSavedFreeBodyState();
        SetDockState(TetherDockState.Free);
    }

    private void ClearSavedFreeBodyState()
    {
        _freeBodyType = RigidbodyType2D.Dynamic;
        _freeConstraints = RigidbodyConstraints2D.None;
        _freeSimulated = true;
        _savedFreeBodyState = false;
        _originalParent = null;
    }

    private void SetDockState(TetherDockState next)
    {
        if (dockState == next)
            return;

        dockState = next;
        DockStateChanged?.Invoke(this, dockedPayload, dockState);
    }

    private Rigidbody2D ResolveBoatBody()
    {
        Boat boat = GetComponentInParent<Boat>();

        return
            boat != null
                ? boat.GetComponent<Rigidbody2D>()
                : null;
    }
}
