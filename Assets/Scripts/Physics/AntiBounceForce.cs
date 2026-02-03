using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PredictiveStickyPlatform : MonoBehaviour
{
    [Header("Stick Settings")]
    public float stickRayLength = 0.1f;      // How far below to check for platforms
    public LayerMask groundMask;
    public float stickGraceTime = 0.1f;      // How long to remain "effectively grounded"

    private Rigidbody2D rb;

    // Last detected platform info
    private Rigidbody2D currentPlatform;
    private Vector2 platformNormal;
    private Vector2 platformVelocity;
    private float lastGroundedTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        PredictGround();

        if (IsEffectivelyGrounded())
        {
            ApplyStickyVelocity();
        }
    }

    private void PredictGround()
    {
        // Raycast slightly below the player
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, stickRayLength, groundMask);
        if (hit.collider != null)
        {
            Rigidbody2D platformRb = hit.collider.attachedRigidbody;

            // Record platform info
            currentPlatform = platformRb;
            platformNormal = hit.normal;
            platformVelocity = platformRb != null ? platformRb.linearVelocity : Vector2.zero;
            lastGroundedTime = Time.time;
        }
        else
        {
            currentPlatform = null;
        }
    }

    private bool IsEffectivelyGrounded()
    {
        return currentPlatform != null || (Time.time - lastGroundedTime <= stickGraceTime);
    }

    private void ApplyStickyVelocity()
    {
        if (currentPlatform == null) return;

        Vector2 up = platformNormal;

        // Relative velocity along the normal
        float velAlongNormal = Vector2.Dot(rb.linearVelocity, up);
        float platformVelAlongNormal = Vector2.Dot(platformVelocity, up);

        // Only apply if separating
        //if (velAlongNormal < platformVelAlongNormal)
        //{
            // Average the velocities along the normal
            float blendedVel = (velAlongNormal + platformVelAlongNormal * platformVelAlongNormal) * 0.3333f;

            // Set the new velocity along normal while preserving tangent velocity
            Vector2 tangent = new Vector2(-up.y, up.x); // perpendicular to normal
            float tangentVel = Vector2.Dot(rb.linearVelocity, tangent);

            rb.linearVelocity = tangent * tangentVel + up * blendedVel;
        //}
    }


    // Optional: visualize raycast
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * stickRayLength);
    }
}
