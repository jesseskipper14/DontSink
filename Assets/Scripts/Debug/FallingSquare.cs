using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FallingSquare : MonoBehaviour, IForceBody
{
    // ========================
    // Config
    // ========================
    [Header("Square Dimensions")]
    public float width = 0.5f;
    public float height = 0.5f;
    public float volume = 0.25f; // default width * height
    public bool destroyOnImpact = true;

    [Header("References")]
    public BuoyancyForce genericBuoyancy;
    public WaveField wave;

    // Rigidbody reference
    public Rigidbody2D rb { get; private set; }

    // Moment of inertia (calculated dynamically)
    private float momentOfInertia;
    public float MomentOfInertia => momentOfInertia;

    // IForceBody properties
    public Vector2 Position => rb.position;
    public float Mass => rb.mass;
    public float Width => width;
    public float Height => height;
    public float Volume => volume;

    // ========================
    // Unity lifecycle
    // ========================
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("FallingSquare requires a Rigidbody2D!");
            enabled = false;
            return;
        }

        // Setup generic buoyancy
        genericBuoyancy = GetComponent<BuoyancyForce>();
        if (genericBuoyancy != null)
        {
            genericBuoyancy.bodySource = this;
            if (wave == null)
                wave = FindFirstObjectByType<WaveField>();
            genericBuoyancy.wave = wave;
        }

        // Set inertia initially
        UpdateInertia();
    }

    void FixedUpdate()
    {
        // Apply gravity manually if needed (Unity gravity already works)
        // rb.AddForce(Vector2.down * PhysicsManager.Instance.globals.Gravity * rb.mass);

        // Buoyancy is applied automatically via the genericBuoyancy system
        UpdateInertia();
    }

    // ========================
    // IForceBody interface
    // ========================
    public void AddForce(Vector2 force)
    {
        rb.AddForce(force, ForceMode2D.Force);
    }

    public void AddTorque(float torque)
    {
        rb.AddTorque(torque, ForceMode2D.Force);
    }

    public void UpdateInertia()
    {
        // Rectangle inertia: I = m * (w^2 + h^2)/12
        momentOfInertia = rb.mass * (width * width + height * height) * 0.08333333f;
        rb.inertia = momentOfInertia;
    }
}
