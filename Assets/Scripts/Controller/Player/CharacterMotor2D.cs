using System.Text;
using UnityEngine;

/// <summary>
/// Authoritative motor state & utilities (ground check, parameters).
/// Server owns this in MP. Clients may read for visuals.
/// </summary>
[DisallowMultipleComponent]
public class CharacterMotor2D : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 4.5f;
    public float moveForce = 40f;

    [Header("Jump")]
    public float jumpImpulse = 6.5f;
    public float coyoteTime = 0.08f;
    public float jumpBuffer = 0.08f;

    [Header("Ground Check")]
    public LayerMask groundMask;
    public Vector2 groundCheckLocalOffset = new Vector2(0f, -0.55f);
    public float groundCheckRadius = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugJumpChecks = true;
    private Collider2D[] _lastGroundHitsForDebug;

    public bool IsGrounded { get; private set; }
    public float TimeSinceGrounded { get; private set; }
    public float TimeSinceJumpPressed { get; private set; }

    public Collider2D LastGroundCollider { get; private set; }
    public int LastGroundProbeHitCount { get; private set; }
    public int LastGroundSolidHitCount { get; private set; }
    public Vector2 LastGroundCheckWorldPosition { get; private set; }

    private void Awake()
    {
        //EnsureGroundHitBuffer();
    }

    public void TickTimers(float dt, bool jumpPressed)
    {
        TimeSinceGrounded += dt;
        TimeSinceJumpPressed += dt;

        if (jumpPressed)
            TimeSinceJumpPressed = 0f;
    }

    public void UpdateGrounded()
    {
        LastGroundCheckWorldPosition = (Vector2)transform.position + groundCheckLocalOffset;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            LastGroundCheckWorldPosition,
            groundCheckRadius,
            groundMask);

        LastGroundProbeHitCount = hits != null ? hits.Length : 0;
        LastGroundSolidHitCount = 0;
        LastGroundCollider = null;

        if (hits != null)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                    continue;

                if (hit.isTrigger)
                    continue;

                LastGroundSolidHitCount++;

                if (LastGroundCollider == null)
                    LastGroundCollider = hit;
            }
        }

        IsGrounded = LastGroundCollider != null;

        if (IsGrounded)
            TimeSinceGrounded = 0f;

        _lastGroundHitsForDebug = hits;
    }

    public void DebugLogJumpCheck(string source, bool jumpPressed, string decision = null)
    {
        if (!debugJumpChecks || !jumpPressed)
            return;

        PlayerBoardingState boarding = GetComponent<PlayerBoardingState>();

        Debug.Log(
            $"[JumpDebug:{name}] source={source} decision={decision ?? "n/a"} " +
            $"jumpPressed={jumpPressed} isGrounded={IsGrounded} " +
            $"timeSinceGrounded={TimeSinceGrounded:F3} timeSinceJumpPressed={TimeSinceJumpPressed:F3} " +
            $"groundMask={LayerMaskToNames(groundMask)} " +
            $"checkWorld={LastGroundCheckWorldPosition} radius={groundCheckRadius:F3} " +
            $"hits={LastGroundProbeHitCount} solidHits={LastGroundSolidHitCount} " +
            $"lastGround={(LastGroundCollider != null ? LastGroundCollider.name : "NULL")} " +
            $"boarded={(boarding != null && boarding.IsBoarded)} " +
            $"boat={(boarding != null && boarding.CurrentBoatRoot != null ? boarding.CurrentBoatRoot.name : "NULL")}",
            this);

        if (_lastGroundHitsForDebug == null)
            return;

        for (int i = 0; i < _lastGroundHitsForDebug.Length; i++)
        {
            Collider2D hit = _lastGroundHitsForDebug[i];
            if (hit == null)
                continue;

            Debug.Log(
                $"[JumpDebug:{name}] groundHit[{i}] object={hit.name} " +
                $"layer={LayerMask.LayerToName(hit.gameObject.layer)} " +
                $"isTrigger={hit.isTrigger} " +
                $"attachedRb={(hit.attachedRigidbody != null ? hit.attachedRigidbody.name : "NULL")}",
                hit);
        }
    }

    private static string LayerMaskToNames(LayerMask mask)
    {
        int value = mask.value;
        if (value == 0)
            return "Nothing";

        StringBuilder sb = new();

        for (int i = 0; i < 32; i++)
        {
            if ((value & (1 << i)) == 0)
                continue;

            if (sb.Length > 0)
                sb.Append("|");

            string layerName = LayerMask.LayerToName(i);
            sb.Append(string.IsNullOrWhiteSpace(layerName) ? $"Layer{i}" : layerName);
        }

        return sb.ToString();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector2 pos = (Vector2)transform.position + groundCheckLocalOffset;
        Gizmos.DrawWireSphere(pos, groundCheckRadius);
    }
#endif
}