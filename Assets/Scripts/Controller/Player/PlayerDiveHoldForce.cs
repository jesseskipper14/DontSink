using UnityEngine;

/// <summary>
/// Player-only "neutral buoyancy" assist.
/// Always active when submerged enough: damps bobbing and counteracts buoyancy-ish lift.
/// Space (SwimUpHeld) reduces the assist so buoyancy can surface you.
/// DiveHeld increases downward bias to go deeper.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDiveHoldForce : MonoBehaviour, IOrderedForceProvider
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 45;

    [Header("Refs")]
    [SerializeField] private MonoBehaviour intentSourceBehaviour; // must implement ICharacterIntentSource
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private MonoBehaviour submersionProviderBehaviour; // must implement ISubmersionProvider (optional)

    private ICharacterIntentSource _intent;
    private ISubmersionProvider _submersion;
    private PhysicsGlobals _globals;

    [Header("Activation")]
    [Tooltip("Only apply when submerged fraction is at least this much.")]
    [Range(0f, 1f)]
    public float minSubmersionToApply = 0.15f;

    [Header("Force Tuning")]
    [Tooltip("How strongly we counteract upward buoyancy. 1 = strong neutral feel.")]
    [Range(0f, 2f)]
    public float neutralizeStrength = 1.0f;

    [Tooltip("Damps vertical bobbing. Higher = more stable, too high = sticky.")]
    [Range(0f, 50f)]
    public float verticalDamping = 12f;

    [Header("Input Modifiers")]
    [Tooltip("While holding Space/SwimUp, multiply neutralization by this (0 = fully release buoyancy).")]
    [Range(0f, 1f)]
    public float neutralizeWhileSwimUpMultiplier = 0.0f;

    [Tooltip("Additional downward force factor while holding Dive.")]
    [Range(0f, 2f)]
    public float extraSinkBiasWhileDive = 0.25f;

    void Awake()
    {
        _globals = PhysicsManager.Instance?.globals;
        if (_globals == null)
            Debug.LogWarning("PlayerDiveHoldForce: PhysicsGlobals not found (will fall back to Physics2D.gravity).");

        if (!rb) rb = GetComponent<Rigidbody2D>();

        if (intentSourceBehaviour == null)
            intentSourceBehaviour = GetComponent<MonoBehaviour>();
        _intent = intentSourceBehaviour as ICharacterIntentSource;
        if (_intent == null)
            _intent = GetComponent<ICharacterIntentSource>();

        if (_intent == null)
            Debug.LogError("PlayerDiveHoldForce requires an ICharacterIntentSource.");

        if (submersionProviderBehaviour != null)
            _submersion = submersionProviderBehaviour as ISubmersionProvider;

        if (_submersion == null)
            _submersion = GetComponent<ISubmersionProvider>();

        if (_submersion == null)
            Debug.LogError("PlayerDiveHoldForce requires an ISubmersionProvider on the player.");
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;
        if (_intent == null || _submersion == null || rb == null) return;

        float sub = Mathf.Clamp01(_submersion.SubmergedFraction);
        if (sub < minSubmersionToApply) return;

        var intent = _intent.Current;

        float g = (_globals != null) ? _globals.Gravity : Mathf.Abs(Physics2D.gravity.y);

        // Base neutralization scales with submersion
        float neutralizeMult = neutralizeStrength;

        // If holding SwimUp (Space), release neutralization so buoyancy can lift you.
        if (intent.SwimUpHeld)
            neutralizeMult *= neutralizeWhileSwimUpMultiplier;

        float downForce = rb.mass * g * sub * neutralizeMult;

        // Dive makes you sink more (on top of whatever your swim force does)
        if (intent.DiveHeld)
            downForce += rb.mass * g * sub * extraSinkBiasWhileDive;

        // Vertical damping to reduce bobbing. (Still applies even while SwimUpHeld; feels smoother.)
        float dampForce = -rb.mass * rb.linearVelocity.y * verticalDamping;

        rb.AddForce(Vector2.down * downForce, ForceMode2D.Force);
        rb.AddForce(Vector2.up * dampForce, ForceMode2D.Force);
    }
}