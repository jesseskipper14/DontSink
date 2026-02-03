using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class DragForce : MonoBehaviour, IForceProvider
{
    public MonoBehaviour submersionSource; // must implement ISubmersionProvider
    private ISubmersionProvider submersion;

    private Rigidbody2D rb;
    private PhysicsGlobals physicsGlobals;

    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 100;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        physicsGlobals = PhysicsManager.Instance?.globals;
        if (physicsGlobals == null)
        {
            Debug.LogError("PhysicsGlobals not found!");
        }

        submersion = submersionSource as ISubmersionProvider;
        if (submersion == null)
        {
            Debug.LogError("DragForce submersionSource does not implement ISubmersionProvider");
            enabled = false;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;

        float submergedFraction = submersion.SubmergedFraction;

        if (submergedFraction <= 0f)
        {
            rb.linearDamping = 0.01f;
            rb.angularDamping = 0.01f;
        }
        else
        {
            rb.linearDamping = Mathf.Clamp(submergedFraction, 0.01f, physicsGlobals.WaterVerticalDrag);
            rb.angularDamping = Mathf.Clamp(submergedFraction, 0.01f, physicsGlobals.WaterAngularDrag);
        }
    }
}
