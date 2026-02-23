using UnityEngine;

/// <summary>
/// Consumes intent and applies forces/impulses via IForceBody.
/// MP-safe: intent can come from local input or network.
/// </summary>
[RequireComponent(typeof(CharacterMotor2D))]
public class CharacterMoveForce : MonoBehaviour, IOrderedForceProvider
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;

    [SerializeField] private int priority = 100;

    private CharacterMotor2D motor;
    private ICharacterIntentSource intentSource;
    private IForceBody body;

    void Awake()
    {
        motor = GetComponent<CharacterMotor2D>();
        body = GetComponent<IForceBody>();
        intentSource = GetComponent<ICharacterIntentSource>();

        if (body == null)
        {
            Debug.LogError("CharacterMoveForce requires IForceBody (use ForceBody2D).");
            enabled = false;
            return;
        }
        if (intentSource == null)
        {
            Debug.LogError("CharacterMoveForce requires an ICharacterIntentSource (e.g., LocalCharacterIntentSource).");
            enabled = false;
            return;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;

        float dt = Time.fixedDeltaTime;

        // Update motor timers & grounded state
        var intent = intentSource.Current;
        motor.TickTimers(dt, intent.JumpPressed);
        motor.UpdateGrounded();

        // --- Horizontal movement (force-based, capped speed) ---
        float targetX = intent.MoveX;

        float vx = body.rb.linearVelocity.x;
        bool underSpeedLimit = Mathf.Abs(vx) < motor.maxSpeed || Mathf.Sign(targetX) != Mathf.Sign(vx);

        if (Mathf.Abs(targetX) > 0.01f && underSpeedLimit)
        {
            body.AddForce(Vector2.right * (targetX * motor.moveForce));
        }

        // --- Jump (buffer + coyote) ---
        bool canJump = motor.TimeSinceGrounded <= motor.coyoteTime;
        bool buffered = motor.TimeSinceJumpPressed <= motor.jumpBuffer;

        if (canJump && buffered)
        {
            // Consume buffer so we don't multi-jump
            motor.TickTimers(0f, jumpPressed: false);
            // Reset vertical velocity for consistent jump
            var v = body.rb.linearVelocity;
            if (v.y < 0f) v.y = 0f;
            body.rb.linearVelocity = v;

            // Impulse-style jump: use AddForce with ForceMode2D.Impulse? Your IForceBody doesn't expose it.
            // So we approximate using Force over a single fixed step:
            body.AddForce(Vector2.up * (motor.jumpImpulse * body.Mass / dt));
        }
    }
}
