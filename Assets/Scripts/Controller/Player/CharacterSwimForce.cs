using UnityEngine;

/// <summary>
/// Swim + dive controls when sufficiently submerged.
/// Designed to coexist with CharacterMoveForce by gating on SubmergedEnoughToSwim.
/// </summary>
[DisallowMultipleComponent]
public sealed class 
    CharacterSwimForce : MonoBehaviour, IOrderedForceProvider
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 40;

    [Header("Tuning")]
    [Min(0f)] public float swimMaxSpeedX = 2.2f;
    [Min(0f)] public float swimAccelX = 25f;

    [Min(0f)] public float swimUpAccel = 18f;
    [Min(0f)] public float diveAccel = 22f;

    [Header("Sprint (Swim)")]
    [SerializeField, Min(1f)] private float swimSprintSpeedMultiplier = 1.4f;
    [SerializeField, Min(1f)] private float swimSprintAccelMultiplier = 1.3f;
    [SerializeField, Min(1f)] private float swimSprintVerticalMultiplier = 1.2f;

    [Tooltip("Clamp vertical speed while swimming.")]
    [Min(0f)] public float swimMaxSpeedY = 3.0f;

    [Header("Drag While Swimming")]
    [Tooltip("When swimming, we override Rigidbody2D.drag to this value.")]
    [Min(0f)] public float swimDrag = 4.0f;

    [Tooltip("When swimming, we override Rigidbody2D.angularDrag to this value.")]
    [Min(0f)] public float swimAngularDrag = 4.0f;

    [Header("Exit Behavior")]
    [Tooltip("If true, when you stop swimming we restore original drag values.")]
    public bool restoreDragOnExit = true;

    private PlayerExertionEnergyState _ee;

    private IForceBody _body;
    private ICharacterIntentSource _intent;
    private PlayerSubmersionState _submersion;

    private float _origDrag;
    private float _origAngularDrag;
    private bool _wasSwimming;

    public void SetEnabled(bool value) => enabledFlag = value;

    void Awake()
    {
        _body = GetComponent<IForceBody>();
        _intent = GetComponent<ICharacterIntentSource>();
        _submersion = GetComponent<PlayerSubmersionState>();
        _ee = GetComponent<PlayerExertionEnergyState>();

        if (_body == null)
        {
            Debug.LogError("CharacterSwimForce requires IForceBody.");
            enabled = false;
            return;
        }
        if (_intent == null)
        {
            Debug.LogError("CharacterSwimForce requires ICharacterIntentSource.");
            enabled = false;
            return;
        }
        if (_submersion == null)
        {
            Debug.LogError("CharacterSwimForce requires PlayerSubmersionState.");
            enabled = false;
            return;
        }

        _origDrag = _body.rb.linearDamping;
        _origAngularDrag = _body.rb.angularDamping;
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;

        bool swimming = _submersion.SubmergedEnoughToSwim;

        // Handle drag transitions
        if (swimming && !_wasSwimming)
        {
            _origDrag = body.rb.linearDamping;
            _origAngularDrag = body.rb.angularDamping;
            body.rb.linearDamping = swimDrag;
            body.rb.angularDamping = swimAngularDrag;
            _wasSwimming = true;
        }
        else if (!swimming && _wasSwimming)
        {
            if (restoreDragOnExit)
            {
                body.rb.linearDamping = _origDrag;
                body.rb.angularDamping = _origAngularDrag;
            }
            _wasSwimming = false;
        }

        if (!swimming) return;

        var intent = _intent.Current;

        // --- Authority / sprint gating ---
        var ee = _ee; // cached; see Awake snippet below
        float moveAuth = (ee != null) ? ee.MoveAuthority : 1f;
        float upAuth = (ee != null) ? ee.SwimUpAuthority : 1f;

        bool sprintRequested = intent.SprintHeld;
        bool sprintAllowed = sprintRequested && (ee == null || ee.CanSprint);
        float sprintLerp = (ee != null) ? ee.SprintAuthority : 1f;

        // If sprint isn't allowed, treat as normal swim
        bool sprint = sprintAllowed;

        // Sprint scaling is LERPED by SprintAuthority (so low energy gives weak sprint),
        // but if energy hits zero, CanSprint is false so sprint becomes fully disabled.
        float speedMultX = sprint ? Mathf.Lerp(1f, swimSprintSpeedMultiplier, sprintLerp) : 1f;
        float accelMultX = sprint ? Mathf.Lerp(1f, swimSprintAccelMultiplier, sprintLerp) : 1f;
        float vertMult = sprint ? Mathf.Lerp(1f, swimSprintVerticalMultiplier, sprintLerp) : 1f;

        float maxSpeedX = swimMaxSpeedX * speedMultX;
        float accelX = swimAccelX * accelMultX;

        float upAccel = swimUpAccel * vertMult;
        float downAccel = diveAccel * vertMult;

        float maxSpeedY = swimMaxSpeedY * (sprint ? speedMultX : 1f);

        // Apply MOVE authority to horizontal capability (this is what you were missing)
        maxSpeedX *= moveAuth;
        accelX *= moveAuth;

        // Apply swim authority to vertical control (surfacing/dive sluggishness)
        upAccel *= upAuth;
        downAccel *= upAuth;
        maxSpeedY *= upAuth;

        // --- Horizontal swim ---
        float targetX = intent.MoveX;
        float vx = body.rb.linearVelocity.x;

        bool underLimit = Mathf.Abs(vx) < maxSpeedX || Mathf.Sign(targetX) != Mathf.Sign(vx);
        if (Mathf.Abs(targetX) > 0.01f && underLimit)
            body.AddForce(Vector2.right * (targetX * accelX));

        // --- Vertical swim / dive ---
        float vy = body.rb.linearVelocity.y;

        if (intent.SwimUpHeld && vy < maxSpeedY)
            body.AddForce(Vector2.up * (upAccel) * body.Mass);

        if (intent.DiveHeld && vy > -maxSpeedY)
            body.AddForce(Vector2.down * (downAccel) * body.Mass);

        // Clamp vertical speed
        var v = body.rb.linearVelocity;
        v.y = Mathf.Clamp(v.y, -maxSpeedY, maxSpeedY);
        body.rb.linearVelocity = v;
    }
}