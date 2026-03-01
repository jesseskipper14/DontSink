using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSwimOrientationForce : MonoBehaviour, IOrderedForceProvider
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 55; // after buoyancy, before drowning

    [Header("Refs")]
    [SerializeField] private PlayerSubmersionState sub;
    [SerializeField] private ICharacterIntentSource intentSource;
    [SerializeField] private IForceBody body;

    [Header("Analog Swim Angle")]
    [Tooltip("Max tilt when fully sideways (input y=0). 70 means sideways becomes 70° instead of 90°.")]
    [Range(0f, 90f)] public float sidewaysTiltDeg = 75f;

    [Tooltip("Max tilt when fully downward (input y=-1, x=0). Usually 180.")]
    [Range(90f, 180f)] public float downTiltDeg = 180f;

    [Tooltip("Deadzone for input magnitude.")]
    [Range(0f, 1f)] public float inputDeadzone = 0.15f;

    [Header("Torque")]
    [Min(0f)] public float rotateTorque = 60f;
    [Min(0f)] public float angularDamping = 4f;

    void Awake()
    {
        if (!sub) sub = GetComponent<PlayerSubmersionState>();
        if (intentSource == null) intentSource = GetComponent<ICharacterIntentSource>();
        if (body == null) body = GetComponent<IForceBody>();
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag || sub == null || intentSource == null) return;
        if (!sub.SubmergedEnoughToSwim) return;

        var intent = intentSource.Current;

        Vector2 input = new Vector2(intent.MoveX,
            intent.SwimUpHeld ? 1f :
            intent.DiveHeld ? -1f : 0f);

        if (input.sqrMagnitude < 0.01f)
            return; // no rotation change if idle

        float x = intent.MoveX;
        float y = (intent.SwimUpHeld ? 1f : 0f) + (intent.DiveHeld ? -1f : 0f);

        float targetAngle = ComputeTargetAngleAnalog(x, y);

        float current = body.rb.rotation;
        float delta = Mathf.DeltaAngle(current, targetAngle);

        // PD-style rotation control
        float torque = delta * rotateTorque - body.rb.angularVelocity * angularDamping;
        body.rb.AddTorque(torque);
    }

    private float ComputeTargetAngleAnalog(float x, float y)
    {
        Vector2 v = new Vector2(x, y);

        if (v.magnitude < inputDeadzone)
            return 0f; // idle: upright

        v.Normalize();

        // We want 0 = up, 90 = sideways, 180 = down.
        // atan2(abs(x), y) achieves exactly that.
        float baseDeg = Mathf.Atan2(Mathf.Abs(v.x), v.y) * Mathf.Rad2Deg; // 0..180

        // Now shape it so sideways (90) becomes sidewaysTiltDeg (like 70),
        // while down (180) can remain 180 (or be tuned).
        float shapedDeg;
        if (baseDeg <= 90f)
        {
            float t = baseDeg / 90f; // 0..1
            shapedDeg = Mathf.Lerp(0f, sidewaysTiltDeg, t);
        }
        else
        {
            float t = (baseDeg - 90f) / 90f; // 0..1
            shapedDeg = Mathf.Lerp(sidewaysTiltDeg, downTiltDeg, t);
        }

        // Mirror left/right: right is negative rotation, left is positive (Unity 2D default)
        float sign = (v.x >= 0f) ? -1f : 1f;

        return shapedDeg * sign;
    }
}