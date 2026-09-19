using UnityEngine;

/// <summary>
/// Damping-only rotational stabilizer for a diving bell.
///
/// IMPORTANT: this does NOT apply any artificial righting torque toward world-up.
/// Ballast position / aggregate COM remains responsible for the actual restoring
/// tendency. This component only removes angular energy so the bell settles instead
/// of oscillating forever in an under-damped 2D solver.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(DivingBellOccupancy))]
public sealed class DivingBellAngularDamping : MonoBehaviour
{
    [Header("Authority")]
    [SerializeField] private bool physicsAuthority = true;

    [Header("References")]
    [SerializeField] private Rigidbody2D bellBody;
    [SerializeField] private DivingBellOccupancy occupancy;
    [SerializeField] private BuoyancyPolygonForce buoyancy;

    [Header("Damping")]
    [Tooltip(
        "First-order angular damping rate while effectively out of water. " +
        "0 disables damping. Units are approximately 1/second.")]
    [SerializeField, Min(0f)] private float airDampingPerSecond = 0.10f;

    [Tooltip(
        "First-order angular damping rate when fully submerged. " +
        "This represents rotational water resistance that the current 2D drag " +
        "model does not otherwise capture well.")]
    [SerializeField, Min(0f)] private float submergedDampingPerSecond = 1.50f;

    [Tooltip(
        "Safety clamp for the damping torque. Keep comfortably above ordinary " +
        "values; this exists to prevent pathological collision angular velocity " +
        "from producing a solver-spiking torque.")]
    [SerializeField, Min(0f)] private float maxDampingTorque = 10000f;

    [Header("Runtime Debug")]
    [SerializeField] private float currentSubmergedFraction;
    [SerializeField] private float currentDampingPerSecond;
    [SerializeField] private float lastAppliedTorque;

    public bool PhysicsAuthority => physicsAuthority;

    private void Awake()
    {
        ResolveRefs();
    }

    private void FixedUpdate()
    {
        ResolveRefs();

        lastAppliedTorque = 0f;

        if (!physicsAuthority ||
            bellBody == null ||
            occupancy == null ||
            bellBody.bodyType != RigidbodyType2D.Dynamic ||
            occupancy.IsDocked)
        {
            return;
        }

        currentSubmergedFraction =
            buoyancy != null
                ? Mathf.Clamp01(
                    buoyancy.SubmergedFraction)
                : 0f;

        currentDampingPerSecond =
            Mathf.Lerp(
                Mathf.Max(0f, airDampingPerSecond),
                Mathf.Max(0f, submergedDampingPerSecond),
                currentSubmergedFraction);

        if (currentDampingPerSecond <= 0f ||
            Mathf.Abs(bellBody.angularVelocity) <= 0.0001f)
        {
            return;
        }

        // Rigidbody2D.angularVelocity is exposed in degrees/sec. Convert to
        // radians/sec so torque = -I * k * omega gives a clean first-order
        // angular decay independent of the bell's current aggregate inertia.
        float omegaRadians =
            bellBody.angularVelocity *
            Mathf.Deg2Rad;

        float torque =
            -bellBody.inertia *
            currentDampingPerSecond *
            omegaRadians;

        float safeMaxTorque =
            Mathf.Max(0f, maxDampingTorque);

        if (safeMaxTorque > 0f)
        {
            torque =
                Mathf.Clamp(
                    torque,
                    -safeMaxTorque,
                    safeMaxTorque);
        }

        if (float.IsNaN(torque) ||
            float.IsInfinity(torque))
        {
            return;
        }

        bellBody.AddTorque(
            torque,
            ForceMode2D.Force);

        lastAppliedTorque =
            torque;
    }

    public void SetPhysicsAuthority(
        bool authoritative)
    {
        physicsAuthority = authoritative;
    }

    private void ResolveRefs()
    {
        if (bellBody == null)
            bellBody = GetComponent<Rigidbody2D>();

        if (occupancy == null)
        {
            occupancy =
                GetComponent<DivingBellOccupancy>() ??
                GetComponentInChildren<DivingBellOccupancy>(true);
        }

        if (buoyancy == null)
        {
            buoyancy =
                GetComponent<BuoyancyPolygonForce>() ??
                GetComponentInChildren<BuoyancyPolygonForce>(true);
        }
    }
}
