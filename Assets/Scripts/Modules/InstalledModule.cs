using UnityEngine;

[DisallowMultipleComponent]
public sealed class InstalledModule : MonoBehaviour, IMassContribution
{
    [SerializeField] private ModuleDefinition definition;
    [SerializeField] private Hardpoint ownerHardpoint;

    private Rigidbody2D _rb;
    private Boat _registeredMassBoat;

    public ModuleDefinition Definition => definition;
    public Hardpoint OwnerHardpoint => ownerHardpoint;

    public float MassContribution
    {
        get
        {
            ResolveRigidbody();

            if (_rb != null)
                return Mathf.Max(0f, _rb.mass);

            return ComputeDefinitionAndContentsMass();
        }
    }

    public Vector2 WorldCenterOfMass
    {
        get
        {
            ResolveRigidbody();

            if (_rb != null)
            {
                // Installed module rigidbodies are intentionally Kinematic and
                // simulated=false. In that state Rigidbody2D.worldCenterOfMass
                // can remain stale after the module Transform is parented/aligned
                // to its hardpoint.
                //
                // The Transform is authoritative for installed-module placement.
                // Rigidbody2D.centerOfMass is local-space data, so transform it
                // through the module's CURRENT Transform instead of trusting the
                // disabled physics body's cached world-space COM.
                return
                    _rb.transform.TransformPoint(
                        _rb.centerOfMass);
            }

            return
                transform.position;
        }
    }

    private void Awake()
    {
        ResolveRigidbody();
    }

    private void Start()
    {
        RefreshMassFromDefinitionAndContents();

        // Normalize after all Awake calls so Hardpoint.Awake / Boat.Awake order
        // cannot leave this contribution registered twice.
        EnsureSingleMassRegistration();
    }

    public void Initialize(
        ModuleDefinition moduleDefinition,
        Hardpoint hardpoint)
    {
        if (!ReferenceEquals(ownerHardpoint, hardpoint))
            DetachMassContributionFromBoat();

        definition = moduleDefinition;
        ownerHardpoint = hardpoint;

        ResolveRigidbody();
        RefreshMassFromDefinitionAndContents();
        EnsureSingleMassRegistration();
    }

    public void DetachMassContributionFromBoat()
    {
        if (_registeredMassBoat == null)
            return;

        Boat oldBoat = _registeredMassBoat;
        _registeredMassBoat = null;

        RemoveAllRegistrations(oldBoat);
        oldBoat.RecomputeMassAndCOM();
    }

    public void ApplyDefinitionBaseMassToRigidbody()
    {
        // Compatibility shim for older callers.
        RefreshMassFromDefinitionAndContents();
    }

    public void RefreshMassFromDefinitionAndContents()
    {
        ResolveRigidbody();

        float targetMass =
            ComputeDefinitionAndContentsMass();

        if (_rb != null)
            _rb.mass = Mathf.Max(0.0001f, targetMass);

        if (_registeredMassBoat != null)
            _registeredMassBoat.RecomputeMassAndCOM();
    }

    private float ComputeDefinitionAndContentsMass()
    {
        float baseMass =
            definition != null &&
            definition.ItemDefinition != null
                ? definition.ItemDefinition.UnitMass
                : 0f;

        StorageModule storage =
            GetComponent<StorageModule>();

        float contentsMass =
            storage != null
                ? storage.ContentsMass
                : 0f;

        return Mathf.Max(
            0f,
            baseMass + contentsMass);
    }

    private void ResolveRigidbody()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
    }

    private Boat ResolveOwningBoat()
    {
        if (ownerHardpoint != null)
        {
            Boat fromHardpoint =
                ownerHardpoint.GetComponentInParent<Boat>();

            if (fromHardpoint != null)
                return fromHardpoint;
        }

        return GetComponentInParent<Boat>();
    }

    private void EnsureSingleMassRegistration()
    {
        Boat targetBoat =
            ResolveOwningBoat();

        if (targetBoat == null ||
            definition == null)
        {
            DetachMassContributionFromBoat();
            return;
        }

        if (_registeredMassBoat != null &&
            !ReferenceEquals(
                _registeredMassBoat,
                targetBoat))
        {
            DetachMassContributionFromBoat();
        }

        RemoveAllRegistrations(targetBoat);

        targetBoat.RegisterMassContribution(this);
        _registeredMassBoat = targetBoat;

        targetBoat.RecomputeMassAndCOM();
    }

    private void RemoveAllRegistrations(
        Boat boat)
    {
        if (boat == null ||
            boat.massContributions == null)
        {
            return;
        }

        while (boat.massContributions.Contains(this))
            boat.UnregisterMassContribution(this);
    }

    private void OnDestroy()
    {
        DetachMassContributionFromBoat();
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Refresh Definition + Contents Mass")]
    private void DebugReapplyDefinitionBaseMass()
    {
        RefreshMassFromDefinitionAndContents();

        if (_registeredMassBoat != null)
            _registeredMassBoat.RecomputeMassAndCOM();
    }
#endif
}
