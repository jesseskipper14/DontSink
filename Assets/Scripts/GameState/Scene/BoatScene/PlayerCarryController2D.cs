using UnityEngine;

/// <summary>
/// Minimal carry controller for CargoCrate.
/// - Disables physics while carried.
/// - Parents to carryAnchor for visuals.
/// - Applies movement multipliers while carrying.
/// - Exposes a safe release API for storage racks / cargo systems.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCarryController2D : MonoBehaviour
{
    [Header("Refs")]
    public Transform carryAnchor;
    public CharacterMotor2D motor;

    [Header("Movement While Carrying")]
    [Range(0.2f, 1f)] public float maxSpeedMultiplier = 0.65f;
    [Range(0.2f, 1f)] public float moveForceMultiplier = 0.75f;

    [Header("Drop")]
    public Vector2 dropLocalOffset = new Vector2(0.45f, 0.05f);

    [Header("Input")]
    public KeyCode dropKey = KeyCode.E;

    private CargoCrate _carried;

    private float _baseMaxSpeed;
    private float _baseMoveForce;

    public bool IsCarrying => _carried != null;
    public CargoCrate CarriedCargo => _carried;

    private void Reset()
    {
        carryAnchor = transform;
        motor = GetComponentInParent<CharacterMotor2D>();
    }

    private void Awake()
    {
        if (motor == null)
            motor = GetComponentInParent<CharacterMotor2D>();

        if (carryAnchor == null)
            carryAnchor = transform;

        if (motor != null)
        {
            _baseMaxSpeed = motor.maxSpeed;
            _baseMoveForce = motor.moveForce;
        }
    }

    private void Update()
    {
        if (_carried != null && Input.GetKeyDown(dropKey))
            Drop();
    }

    private void LateUpdate()
    {
        if (_carried != null && carryAnchor != null)
        {
            _carried.transform.position = carryAnchor.position;
            _carried.transform.rotation = carryAnchor.rotation;
        }

        ApplyMovementModifiers();
    }

    public void ToggleCarry(CargoCrate crate)
    {
        if (crate == null)
            return;

        if (_carried == crate)
        {
            Drop();
            return;
        }

        if (_carried != null)
        {
            // Already carrying something; ignore for now.
            return;
        }

        PickUp(crate);
    }

    public bool TryGetCarriedCargo(out CargoCrate crate)
    {
        crate = _carried;
        return crate != null;
    }

    /// <summary>
    /// Releases the carried cargo without dropping it into the world.
    /// Use this when another system, like a storage rack, is taking ownership.
    /// </summary>
    public bool TryReleaseCarriedCargoForStorage(CargoCrate expected, out CargoCrate released)
    {
        released = null;

        if (_carried == null)
            return false;

        if (expected != null && _carried != expected)
            return false;

        CargoCrate crate = _carried;
        _carried = null;

        crate.IsCarried = false;
        crate.transform.SetParent(null, true);

        Rigidbody2D rb = crate.GetRigidbody();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        crate.ApplyGroundSorting();

        ApplyMovementModifiers();

        released = crate;
        return true;
    }

    public void Drop()
    {
        if (_carried == null)
            return;

        CargoCrate crate = _carried;
        _carried = null;

        crate.ApplyGroundSorting();
        crate.IsCarried = false;
        crate.transform.SetParent(null, true);

        Vector3 dropPos = transform.TransformPoint((Vector3)dropLocalOffset);
        crate.transform.position = dropPos;

        Rigidbody2D rb = crate.GetRigidbody();
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        ApplyMovementModifiers();
    }

    private void PickUp(CargoCrate crate)
    {
        _carried = crate;

        crate.ApplyHeldSorting();
        crate.IsCarried = true;

        Rigidbody2D rb = crate.GetRigidbody();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        crate.transform.SetParent(carryAnchor, true);
        crate.transform.localPosition = Vector3.zero;
        crate.transform.localRotation = Quaternion.identity;

        ApplyMovementModifiers();
    }

    private void ApplyMovementModifiers()
    {
        if (motor == null)
            return;

        bool carrying = _carried != null;
        motor.maxSpeed = carrying ? _baseMaxSpeed * maxSpeedMultiplier : _baseMaxSpeed;
        motor.moveForce = carrying ? _baseMoveForce * moveForceMultiplier : _baseMoveForce;
    }
}