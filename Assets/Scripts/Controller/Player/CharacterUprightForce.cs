using UnityEngine;

/// <summary>
/// While holding Upright (W), applies torque to rotate the body back to 0 degrees.
/// Uses a PD controller (proportional + damping) for stable behavior.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterUprightForce : MonoBehaviour, IOrderedForceProvider
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 90; // Run before movement/jump if you want. After is fine too.

    [Header("Upright Target")]
    [Tooltip("Target world rotation in degrees. 0 = upright.")]
    [SerializeField] private float targetAngleDeg = 0f;

    [Header("Controller Tuning")]
    [Tooltip("How strongly we try to correct angle error (deg -> torque).")]
    [SerializeField] private float kP = 40f;

    [Tooltip("How strongly we damp angular velocity (deg/s -> torque).")]
    [SerializeField] private float kD = 8f;

    [Tooltip("Clamp torque to avoid insane spinning.")]
    [SerializeField] private float maxTorque = 200f;

    [Tooltip("Don't bother correcting tiny errors.")]
    [SerializeField] private float deadZoneDeg = 2f;

    private ICharacterIntentSource _intentSource;
    private IForceBody _body;

    void Awake()
    {
        _intentSource = GetComponent<ICharacterIntentSource>();
        _body = GetComponent<IForceBody>();

        if (_body == null)
        {
            Debug.LogError("CharacterUprightForce requires IForceBody (use ForceBody2D).");
            enabled = false;
            return;
        }
        if (_intentSource == null)
        {
            Debug.LogError("CharacterUprightForce requires an ICharacterIntentSource.");
            enabled = false;
            return;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;

        var intent = _intentSource.Current;
        if (!intent.UprightHeld) return;

        var rb = body.rb;
        if (rb == null) return;

        // Rigidbody2D.rotation is degrees.
        float current = rb.rotation;
        float error = Mathf.DeltaAngle(current, targetAngleDeg); // shortest signed error (-180..180)

        if (Mathf.Abs(error) <= deadZoneDeg)
            return;

        // Rigidbody2D.angularVelocity is deg/s (2D uses degrees, because sure, why not).
        float angVel = rb.angularVelocity;

        // PD torque: drive error to 0, damp velocity to prevent wobble.
        float torque = (error * kP) - (angVel * kD);

        // Clamp so you don't create a human blender.
        torque = Mathf.Clamp(torque, -maxTorque, maxTorque);

        rb.AddTorque(torque);
    }
}