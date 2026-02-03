using UnityEngine;

public class AntiSlideForce : MonoBehaviour, IForceProvider
{
    [Header("Traction")]
    public float maxTractionForce = 200f;
    public float damping = 1.0f;

    [Header("Settings")]
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 150;

    private Rigidbody2D rb;
    private IForceBody body;
    private GroundingSensor2D grounding;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<IForceBody>();
        grounding = GetComponent<GroundingSensor2D>();

        if (rb == null || body == null || grounding == null)
        {
            Debug.LogError("AntiSlideForce2D requires Rigidbody2D, IForceBody, and GroundingSensor2D.");
            enabled = false;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;
        if (!grounding.IsGrounded) return;

        Vector2 normal = grounding.GroundNormal;
        Vector2 tangent = new Vector2(normal.y, -normal.x); // 90° rotate

        Vector2 relativeVelocity = rb.linearVelocity - grounding.GroundVelocity;
        float tangentialSpeed = Vector2.Dot(relativeVelocity, tangent);

        // Desired force to cancel tangential motion
        float desiredForce = -tangentialSpeed * damping * body.Mass / Time.fixedDeltaTime;

        // Scale by grounding strength
        desiredForce *= grounding.GroundedStrength;

        // Clamp so traction can fail
        float clampedForce = Mathf.Clamp(
            desiredForce,
            -maxTractionForce,
            maxTractionForce
        );

        body.AddForce(tangent * clampedForce);
    }
}
