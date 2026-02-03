using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ForceBody2D : MonoBehaviour, IForceBody
{
    public Rigidbody2D rb { get; private set; }

    private Vector2 accumulatedForce;
    private float accumulatedTorque;

    [Header("Geometry")]
    [SerializeField] private float width = 1f;
    [SerializeField] private float height = 1f;

    public Vector2 Position => rb.position;
    public float Mass => rb.mass;
    public float MomentOfInertia => rb.inertia;

    public float Width => width;
    public float Height => height;
    public float Volume => width * height;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void AddForce(Vector2 force)
    {
        accumulatedForce += force;
    }

    public void AddTorque(float torque)
    {
        accumulatedTorque += torque;
    }

    void FixedUpdate()
    {
        rb.AddForce(accumulatedForce, ForceMode2D.Force);
        rb.AddTorque(accumulatedTorque, ForceMode2D.Force);

        accumulatedForce = Vector2.zero;
        accumulatedTorque = 0f;
    }
}
