using UnityEngine;

/// <summary>
/// Minimal carry controller for CargoCrate.
/// - Disables physics while carried (no moving-platform bounce nonsense).
/// - Parents to carryAnchor for visuals.
/// - Applies movement multipliers while carrying.
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

    private CargoCrate _carried;

    [Header("Input")]
    public KeyCode dropKey = KeyCode.E;

    private float _baseMaxSpeed;
    private float _baseMoveForce;

    private void Reset()
    {
        carryAnchor = transform;
        motor = GetComponentInParent<CharacterMotor2D>();
    }

    private void Awake()
    {
        if (motor == null) motor = GetComponentInParent<CharacterMotor2D>();
        if (carryAnchor == null) carryAnchor = transform;

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

        if (motor != null)
        {
            bool carrying = _carried != null;
            motor.maxSpeed = carrying ? _baseMaxSpeed * maxSpeedMultiplier : _baseMaxSpeed;
            motor.moveForce = carrying ? _baseMoveForce * moveForceMultiplier : _baseMoveForce;
        }
    }

    public void ToggleCarry(CargoCrate crate)
    {
        if (crate == null) return;

        if (_carried == crate)
        {
            Drop();
            return;
        }

        if (_carried != null)
        {
            // Already carrying something; ignore for now (no juggling).
            return;
        }

        PickUp(crate);
    }

    private void PickUp(CargoCrate crate)
    {
        _carried = crate;
        crate.ApplyHeldSorting();
        crate.IsCarried = true;

        var rb = crate.GetRigidbody();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        crate.transform.SetParent(carryAnchor, true);
        crate.transform.localPosition = Vector3.zero;
        crate.transform.localRotation = Quaternion.identity;
    }

    private void Drop()
    {
        if (_carried == null) return;

        var crate = _carried;
        _carried = null;
        crate.ApplyGroundSorting();

        crate.IsCarried = false;
        crate.transform.SetParent(null, true);

        Vector3 dropPos = transform.TransformPoint((Vector3)dropLocalOffset);
        crate.transform.position = dropPos;

        var rb = crate.GetRigidbody();
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}
