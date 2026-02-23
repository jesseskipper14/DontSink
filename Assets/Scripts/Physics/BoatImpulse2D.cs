using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatImpulse2D : MonoBehaviour
{
    [Header("Force")]
    [Tooltip("How strongly boat acceleration is applied to unsecured cargo (pseudo force).")]
    [SerializeField] private float pseudoForceScale = 1.0f;

    [Tooltip("Clamp the acceleration magnitude (world units/s^2).")]
    [SerializeField] private float maxAccel = 50f;

    [Header("Eligibility")]
    [Tooltip("Only affect cargo that is NOT parented under the boat root.")]
    [SerializeField] private bool onlyUnparentedCargo = true;

    [Tooltip("Optional: only affect cargo currently inside boarded volume.")]
    [SerializeField] private BoatBoardedVolume boardedVolume; // optional

    private Rigidbody2D _boatRb;
    private Vector2 _prevVel;

    private void Awake()
    {
        _boatRb = GetComponentInParent<Rigidbody2D>();
        if (_boatRb != null) _prevVel = _boatRb.linearVelocity;

        if (boardedVolume == null)
            boardedVolume = GetComponentInChildren<BoatBoardedVolume>(true);
    }

    private void FixedUpdate()
    {
        if (_boatRb == null) return;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        Vector2 vel = _boatRb.linearVelocity;
        Vector2 accel = (vel - _prevVel) / dt;
        _prevVel = vel;

        float mag = accel.magnitude;
        if (mag <= 0.001f) return;

        if (mag > maxAccel)
            accel = accel * (maxAccel / mag);

        // In accelerating boat frame, loose cargo experiences -accel.
        Vector2 pseudo = -accel * pseudoForceScale;

        ApplyToLooseCargo(pseudo);
    }

    private void ApplyToLooseCargo(Vector2 pseudoAccel)
    {
        // Find all cargo items in the scene. If this becomes expensive later, we can cache/register.
        var cargos = FindObjectsByType<CargoItemIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (cargos == null || cargos.Length == 0) return;

        Collider2D volume = boardedVolume != null ? boardedVolume.GetComponent<Collider2D>() : null;

        for (int i = 0; i < cargos.Length; i++)
        {
            var id = cargos[i];
            if (id == null) continue;

            // Must be relevant to THIS boat: either inside volume, or near enough.
            if (volume != null)
            {
                var col = id.GetComponentInChildren<Collider2D>();
                if (col == null) continue;

                // cheap containment check: bounds overlap. (good enough for “on boat” filtering)
                if (!volume.bounds.Intersects(col.bounds))
                    continue;
            }

            var tr = id.transform;

            if (onlyUnparentedCargo && tr.IsChildOf(transform.root))
                continue;

            var rb = id.GetComponent<Rigidbody2D>();
            if (rb == null) continue;
            if (rb.bodyType != RigidbodyType2D.Dynamic) continue;

            rb.AddForce(pseudoAccel * rb.mass, ForceMode2D.Force);
        }
    }
}