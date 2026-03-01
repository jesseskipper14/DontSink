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

    public bool IsGrounded { get; private set; }
    public float TimeSinceGrounded { get; private set; }
    public float TimeSinceJumpPressed { get; private set; }

    public void TickTimers(float dt, bool jumpPressed)
    {
        TimeSinceGrounded += dt;
        TimeSinceJumpPressed += dt;

        if (jumpPressed)
            TimeSinceJumpPressed = 0f;
    }

    public void UpdateGrounded()
    {
        Vector2 pos = (Vector2)transform.position + groundCheckLocalOffset;

        var col = Physics2D.OverlapCircle(pos, groundCheckRadius, groundMask);
        IsGrounded = col != null && !col.isTrigger;

        if (IsGrounded)
            TimeSinceGrounded = 0f;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 pos = (Vector2)transform.position + groundCheckLocalOffset;
        Gizmos.DrawWireSphere(pos, groundCheckRadius);
    }
#endif
}
