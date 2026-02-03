using UnityEngine;

public class UprightStabilizationForce : MonoBehaviour, IForceProvider
{
    [Header("Stabilization")]
    public float uprightTorque = 30f;
    public float airborneMultiplier = 0.25f;
    public float maxAngle = 90f;
    public float stabilizationStrength = 10.0f;

    [Header("Settings")]
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 140;

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
            Debug.LogError("UprightStabilizationForce2D requires Rigidbody2D, IForceBody, and GroundingSensor2D.");
            enabled = false;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;

        Vector2 supportUp =
            grounding.IsGrounded ? grounding.GroundNormal : Vector2.up;

        // Angle between supportUp and world up (how much body should lean)
        float desiredLean = Vector2.SignedAngle(supportUp, Vector2.up);

        // Angle between supportUp and current body "up"
        float currentLean = Vector2.SignedAngle(supportUp, transform.up);

        float leanError = currentLean - desiredLean;

        // Normalize lean error within -180 to 180
        leanError = Mathf.DeltaAngle(currentLean, desiredLean);

        // Scale torque strength based on maxAngle - torque reduces as angle approaches maxAngle
        float angleFactor = Mathf.Clamp01(1f - Mathf.Abs(leanError) / maxAngle);

        // Compute raw torque
        float torque = -leanError * uprightTorque * stabilizationStrength * angleFactor;

        // Optional: clamp torque to avoid explosion
        float maxTorque = 50f; // tune as needed
        torque = Mathf.Clamp(torque, -maxTorque, maxTorque);

        // Scale down torque if airborne
        float groundingFactor = grounding.IsGrounded ? 1f : airborneMultiplier;
        torque *= groundingFactor;

        //Debug.Log($"GroundFactor={groundingFactor}, torque={torque}");

        body.AddTorque(torque);
    }
}
