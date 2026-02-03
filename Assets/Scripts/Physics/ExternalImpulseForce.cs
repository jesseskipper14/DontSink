using UnityEngine;

public class ExternalImpulseForce : MonoBehaviour, IForceProvider
{
    private Vector2 pendingImpulse;
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 300;

    public void AddImpulse(Vector2 impulse)
    {
        pendingImpulse += impulse;
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;

        if (pendingImpulse == Vector2.zero) return;

        body.AddForce(pendingImpulse / Time.fixedDeltaTime);
        pendingImpulse = Vector2.zero;
    }
}
