using UnityEngine;

public class RightingTorqueForce : MonoBehaviour, IOrderedForceProvider
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 60;

    private PhysicsGlobals globals;

    void Awake()
    {
        globals = PhysicsManager.Instance.globals;
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;

        var rb = body.rb;

        float angleRad = rb.rotation * Mathf.Deg2Rad;

        float stiffness = globals.RightingStiffness * rb.mass * body.Width;
        float restoring = -angleRad * stiffness;
        float damping = -rb.angularVelocity * globals.AngularDamping;

        body.AddTorque(restoring + damping);
    }
}
