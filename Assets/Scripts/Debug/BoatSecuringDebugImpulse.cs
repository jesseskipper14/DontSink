using UnityEngine;

public sealed class BoatSecuringDebugImpulse : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private bool listenForKey = true;
    [SerializeField] private KeyCode triggerKey = KeyCode.I;

    [Header("Physics Impulse")]
    [SerializeField] private Rigidbody2D targetRigidbody;
    [SerializeField] private bool applyPhysicsImpulse = true;
    [SerializeField] private Vector2 impulse = new Vector2(0f, 8f);

    [Header("Cargo Securing Impact")]
    [SerializeField] private bool affectSecuredCargo = true;

    [Range(0f, 1f)]
    [SerializeField] private float securingImpactSeverity01 = 0.25f;

    [Tooltip("Impulse applied to cargo only if the securing fails and it breaks loose.")]
    [SerializeField] private Vector2 breakLooseCargoImpulse = new Vector2(0f, 2f);

    [Tooltip("If true, only affects secured cargo owned by this boat.")]
    [SerializeField] private bool onlyAffectThisBoat = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private Boat _boat;

    private void Reset()
    {
        targetRigidbody = GetComponentInParent<Rigidbody2D>();
        _boat = GetComponentInParent<Boat>();
    }

    private void Awake()
    {
        CacheRefs();
    }

    private void Update()
    {
        if (!listenForKey)
            return;

        if (triggerKey == KeyCode.None)
            return;

        if (Input.GetKeyDown(triggerKey))
            FireDebugImpulse();
    }

    [ContextMenu("Fire Debug Impulse")]
    public void FireDebugImpulse()
    {
        CacheRefs();

        if (applyPhysicsImpulse && targetRigidbody != null)
        {
            targetRigidbody.AddForce(impulse, ForceMode2D.Impulse);
            Log($"Applied physics impulse {impulse} to '{targetRigidbody.name}'.");
        }

        if (!affectSecuredCargo)
            return;

        int affected = ApplySecuringImpactToCargo();

        Log(
            $"Applied securing impact severity={securingImpactSeverity01:0.00} " +
            $"to {affected} secured cargo item(s).");
    }

    private int ApplySecuringImpactToCargo()
    {
        BoatSecuredItem[] securedItems = FindObjectsByType<BoatSecuredItem>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        int affected = 0;

        for (int i = 0; i < securedItems.Length; i++)
        {
            BoatSecuredItem item = securedItems[i];

            if (item == null || !item.IsSecured)
                continue;

            if (onlyAffectThisBoat && !BelongsToThisBoat(item))
                continue;

            if (item.ApplySecuringImpact(securingImpactSeverity01, breakLooseCargoImpulse))
                affected++;
        }

        return affected;
    }

    private bool BelongsToThisBoat(BoatSecuredItem item)
    {
        if (item == null)
            return false;

        if (_boat == null)
            return item.transform == transform || item.transform.IsChildOf(transform);

        BoatOwnedItem owned = item.GetComponent<BoatOwnedItem>();
        if (owned != null && owned.IsOwnedByBoat)
            return owned.OwningBoatInstanceId == _boat.BoatInstanceId;

        return item.transform == _boat.transform || item.transform.IsChildOf(_boat.transform);
    }

    private void CacheRefs()
    {
        if (_boat == null)
            _boat = GetComponentInParent<Boat>();

        if (targetRigidbody == null)
            targetRigidbody = GetComponentInParent<Rigidbody2D>();
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatSecuringDebugImpulse:{name}] {msg}", this);
    }
}