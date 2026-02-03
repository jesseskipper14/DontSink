using UnityEngine;

public class CharacterPlayer : MonoBehaviour, IForceBody
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
    public BuoyancyPolygonForce buoyancyForce;
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
        buoyancyForce = GetComponent<BuoyancyPolygonForce>();
        if (buoyancyForce != null)
        {
            buoyancyForce.bodySource = this;
            if (wave == null)
                wave = FindFirstObjectByType<WaveField>();
            buoyancyForce.wave = wave;
        }

        // Set inertia initially
        UpdateInertia();
        rb.centerOfMass = new Vector2(0, -0.5f);
    }

    void FixedUpdate()
    {
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
    void OnDrawGizmosSelected()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) return;

        // Rigidbody2D.centerOfMass is LOCAL space
        Vector2 worldCOM = rb.transform.TransformPoint(rb.centerOfMass);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(worldCOM, 0.2f);

        // Optional: draw a short vertical reference line
        Gizmos.DrawLine(worldCOM, worldCOM + Vector2.up * 0.3f);
    }
}
