using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GroundingSensor2D : MonoBehaviour
{
    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float minGroundNormalY = 0.2f;

    // Public grounding info
    public float GroundedStrength { get; private set; }   // 0..1 how strong the grounding is
    public Vector2 GroundNormal { get; private set; }     // Average normal of contacts
    public Vector2 GroundVelocity { get; private set; }   // Weighted average velocity of the ground
    public bool IsGrounded => GroundedStrength > 0.01f;

    private Rigidbody2D rb;
    private Rigidbody2D currentPlatform;
    private Vector2 currentNormal;

    // Per-step accumulators
    private Vector2 normalSum;
    private Vector2 velocitySum;
    private float weightSum;

    private Rigidbody2D lastGroundRb;
    private float lastGroundedTime;
    [SerializeField] private float groundedGraceTime = 0.1f;
    public bool IsEffectivelyGrounded => (Time.time - lastGroundedTime <= groundedGraceTime);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        // Normalize results
        if (weightSum > 0f)
        {
            GroundNormal = (normalSum / weightSum).normalized;
            GroundVelocity = velocitySum / weightSum;
            GroundedStrength = Mathf.Clamp01(weightSum);

            // Update last grounded info
            if (GroundedStrength > 0.01f)
            {
                lastGroundRb = rb; // Or collision.rigidbody from contacts
                lastGroundedTime = Time.time;
            }
        }
        else
        {
            GroundNormal = Vector2.up;
            GroundVelocity = Vector2.zero;
            GroundedStrength = 0f;
        }

        // Reset accumulators
        normalSum = Vector2.zero;
        velocitySum = Vector2.zero;
        weightSum = 0f;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // Only process collisions in the ground layer mask
        if ((groundMask.value & (1 << collision.gameObject.layer)) == 0)
            return;

        foreach (var contact in collision.contacts)
        {
            // Reject walls / ceilings
            if (contact.normal.y < minGroundNormalY)
                continue;

            float weight = contact.normal.y; // weight proportional to normal Y

            // Sum normals for averaging
            normalSum += contact.normal * weight;
            weightSum += weight;

            // Sum platform velocity
            Rigidbody2D otherRb = collision.rigidbody;
            if (otherRb != null)
            {
                // Use point velocity to account for moving/rotating platforms
                velocitySum += otherRb.GetPointVelocity(contact.point) * weight;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if ((groundMask.value & (1 << collision.gameObject.layer)) == 0)
            return;

        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y >= minGroundNormalY)
            {
                lastGroundedTime = Time.time; // Start grace period
                break;
            }
        }
    }
}
