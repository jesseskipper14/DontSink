using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class MoneyChestDynamicWeight : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MoneyChestState chest;
    [SerializeField] private Rigidbody2D rb;

    [Header("Linear Mass")]
    [Tooltip("Rigidbody mass when the chest has no money.")]
    [SerializeField, Min(0.01f)] private float emptyMass = 0.75f;

    [Tooltip("Mass added per 1 money unit in the chest. Example: 0.0018 means $500 adds 0.9 mass.")]
    [SerializeField, Min(0f)] private float massPerMoneyUnit = 0.0018f;

    [Tooltip("Maximum Rigidbody mass, so absurd rich-player money does not become a physics black hole.")]
    [SerializeField, Min(0.01f)] private float maxMass = 12f;

    [Header("Reference Preview")]
    [Tooltip("Does not control behavior directly. Used for debug preview: what mass does the chest have around this balance?")]
    [SerializeField, Min(0)] private int referenceSinkBalance = 500;

    [Header("Optional Stability")]
    [Tooltip("Optional: lower center of mass as the chest fills, making loaded chests feel heavier/stabler.")]
    [SerializeField] private bool adjustCenterOfMass = false;

    [SerializeField] private Vector2 emptyCenterOfMass = Vector2.zero;
    [SerializeField] private Vector2 loadedCenterOfMass = new Vector2(0f, -0.08f);

    [Tooltip("Balance where center of mass reaches Loaded Center Of Mass.")]
    [SerializeField, Min(1)] private int balanceForLoadedCenterOfMass = 10000;

    [Header("Debug")]
    [SerializeField] private bool logMassChanges = false;
    [SerializeField, Min(0f)] private float logMinMassDelta = 0.05f;

    private int lastAppliedBalance = int.MinValue;
    private float lastAppliedMass = -1f;

    public float EmptyMass => emptyMass;
    public float MassPerMoneyUnit => massPerMoneyUnit;
    public float MaxMass => maxMass;

    public float ReferenceSinkMass => ComputeMass(referenceSinkBalance);

    private void Reset()
    {
        ResolveRefs();
    }

    private void Awake()
    {
        ResolveRefs();
        ApplyWeightNow("Awake", true);
    }

    private void OnEnable()
    {
        ResolveRefs();

        if (chest != null)
            chest.Changed += HandleChestChanged;

        ApplyWeightNow("OnEnable", true);
    }

    private void OnDisable()
    {
        if (chest != null)
            chest.Changed -= HandleChestChanged;
    }

    private void FixedUpdate()
    {
        // Backup path. The Changed event should normally catch balance updates,
        // but this protects against restore/debug/inspector goblin paths.
        ApplyWeightNow("FixedUpdate", false);
    }

    private void OnValidate()
    {
        emptyMass = Mathf.Max(0.01f, emptyMass);
        massPerMoneyUnit = Mathf.Max(0f, massPerMoneyUnit);
        maxMass = Mathf.Max(0.01f, maxMass);

        if (maxMass < emptyMass)
            maxMass = emptyMass;

        referenceSinkBalance = Mathf.Max(0, referenceSinkBalance);
        balanceForLoadedCenterOfMass = Mathf.Max(1, balanceForLoadedCenterOfMass);
    }

    private void ResolveRefs()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (chest == null)
            chest = GetComponent<MoneyChestState>();

        if (chest == null)
            chest = GetComponentInParent<MoneyChestState>();

        if (chest == null)
            chest = GetComponentInChildren<MoneyChestState>(true);
    }

    private void HandleChestChanged(MoneyChestState changedChest)
    {
        ApplyWeightNow("ChestChanged", true);
    }

    public void ApplyWeightNow(string reason = "Manual", bool force = true)
    {
        if (rb == null || chest == null)
            ResolveRefs();

        if (rb == null)
            return;

        int balance = chest != null ? Mathf.Max(0, chest.Balance) : 0;
        float targetMass = ComputeMass(balance);

        bool balanceChanged = balance != lastAppliedBalance;
        bool massChanged = Mathf.Abs(targetMass - lastAppliedMass) >= 0.0001f;

        if (!force && !balanceChanged && !massChanged)
            return;

        rb.mass = targetMass;

        if (adjustCenterOfMass)
            rb.centerOfMass = ComputeCenterOfMass(balance);

        if (logMassChanges)
        {
            bool shouldLog =
                force ||
                lastAppliedMass < 0f ||
                Mathf.Abs(rb.mass - lastAppliedMass) >= logMinMassDelta;

            if (shouldLog)
            {
                Debug.Log(
                    $"[MoneyChestDynamicWeight:{name}] " +
                    $"balance={balance}, mass={rb.mass:F4}, " +
                    $"emptyMass={emptyMass:F4}, massPerMoneyUnit={massPerMoneyUnit:F6}, " +
                    $"referenceSinkBalance={referenceSinkBalance}, referenceSinkMass={ReferenceSinkMass:F4}, " +
                    $"reason='{reason}'",
                    this);
            }
        }

        lastAppliedBalance = balance;
        lastAppliedMass = rb.mass;
    }

    private float ComputeMass(int balance)
    {
        balance = Mathf.Max(0, balance);

        float mass = emptyMass + balance * massPerMoneyUnit;
        return Mathf.Clamp(mass, emptyMass, maxMass);
    }

    private Vector2 ComputeCenterOfMass(int balance)
    {
        float t = Mathf.InverseLerp(
            0,
            Mathf.Max(1, balanceForLoadedCenterOfMass),
            Mathf.Max(0, balance));

        t = Mathf.SmoothStep(0f, 1f, t);

        return Vector2.Lerp(emptyCenterOfMass, loadedCenterOfMass, t);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Apply Weight Now")]
    private void DebugApplyWeightNow()
    {
        ApplyWeightNow("Debug context menu", true);
    }

    [ContextMenu("Debug/Log Weight Preview")]
    private void DebugLogWeightPreview()
    {
        int[] samples =
        {
            0,
            50,
            100,
            referenceSinkBalance,
            1000,
            5000,
            10000
        };

        for (int i = 0; i < samples.Length; i++)
        {
            int balance = Mathf.Max(0, samples[i]);

            Debug.Log(
                $"[MoneyChestDynamicWeight:{name}] Preview " +
                $"balance={balance}, mass={ComputeMass(balance):F4}",
                this);
        }
    }
#endif
}