using UnityEngine;

public class ThrottleForce : MonoBehaviour, IOrderedForceProvider, IThrottleReceiver
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 250;

    //public float throttleForce = 1f;
    //public float throttleInput;

    [SerializeField] private float maxForce = 25f;
    private float throttle01; // actually [-1..1]

    public void SetThrottle(float value)
    {
        throttle01 = Mathf.Clamp(value, -1f, 1f);
    }

    public void ApplyForces(IForceBody body)
    {
        // removed dt so it would compile

        // Replace this with your existing direction logic.
        // Example: push along boat's local right:
        Vector2 dir = (Vector2)transform.right;

        Vector2 f = dir * (throttle01 * maxForce);

        // Replace this with however your force system applies it:
        body.AddForce(f);
    }
}
