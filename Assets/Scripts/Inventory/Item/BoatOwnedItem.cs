using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatOwnedItem : MonoBehaviour, IMassContribution
{
    public event Action<BoatOwnedItem> OwnershipChanged;

    [SerializeField] private string owningBoatInstanceId;
    [SerializeField] private bool registered;

    private BoatItemRegistry _registry;
    private Boat _owningBoat;
    private Boat _massContributionBoat;
    private Rigidbody2D _rb;

    public bool IsRegistered => registered;
    public string OwningBoatInstanceId => owningBoatInstanceId;
    public bool IsOwnedByBoat => !string.IsNullOrWhiteSpace(owningBoatInstanceId);
    public Boat OwningBoat => _owningBoat;
    public bool IsContributingMassToBoat => _massContributionBoat != null;

    /// <summary>
    /// Boat load contribution for any physical boat-owned world item.
    ///
    /// Collision is handled by GhostCollisionProxy; weight is handled explicitly
    /// here. That keeps "how heavy is this thing?" separate from whatever contact
    /// impulses Box2D happens to generate.
    /// </summary>
    public float MassContribution
    {
        get
        {
            ResolveRigidbody();

            return IsOwnedByBoat &&
                   _owningBoat != null &&
                   _rb != null
                ? Mathf.Max(0f, _rb.mass)
                : 0f;
        }
    }

    public Vector2 WorldCenterOfMass
    {
        get
        {
            ResolveRigidbody();

            return _rb != null
                ? _rb.worldCenterOfMass
                : (Vector2)transform.position;
        }
    }

    private void Awake()
    {
        ResolveRigidbody();
    }

    public void AssignToBoat(Boat boat)
    {
        if (boat == null || string.IsNullOrWhiteSpace(boat.BoatInstanceId))
        {
            ClearOwnership();
            return;
        }

        if (_registry != null)
            _registry.Unregister(this);

        UnregisterMassContribution();

        owningBoatInstanceId = boat.BoatInstanceId;
        _owningBoat = boat;

        RegisterMassContribution(boat);

        // Any physical boat-owned world item needs containment tracking.
        // Adding it here covers drops, restored items, and any future assignment
        // path that correctly comes through BoatOwnedItem.AssignToBoat().
        if (GetComponent<BoatOwnedItemEscapeTracker>() == null)
            gameObject.AddComponent<BoatOwnedItemEscapeTracker>();

        _registry = boat.GetComponent<BoatItemRegistry>();
        if (_registry != null)
        {
            _registry.Register(this);
            registered = true;
        }
        else
        {
            registered = false;
            Debug.LogWarning($"[BoatOwnedItem:{name}] Assigned to boat '{boat.name}', but boat has no BoatItemRegistry.", this);
        }

        NotifyOwnershipChanged();
    }

    public void RestoreOwnership(Boat boat, string restoredBoatInstanceId)
    {
        if (boat == null)
        {
            UnregisterMassContribution();

            owningBoatInstanceId = restoredBoatInstanceId;
            _owningBoat = null;
            registered = false;
            NotifyOwnershipChanged();
            return;
        }

        AssignToBoat(boat);
    }

    public void ClearOwnership()
    {
        if (_registry != null)
            _registry.Unregister(this);

        _registry = null;

        UnregisterMassContribution();

        _owningBoat = null;
        owningBoatInstanceId = null;
        registered = false;

        NotifyOwnershipChanged();
    }

    private void ResolveRigidbody()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Controls only the item's physical mass/COM contribution to its owning Boat.
    /// Boat ownership itself is deliberately left unchanged.
    ///
    /// This lets a loose item stop loading the Boat the instant it leaves the
    /// containment zone while preserving the existing ownership grace period for
    /// persistence/layers/recovery behavior.
    /// </summary>
    public void SetPhysicallyContainedByOwningBoat(bool contained)
    {
        if (!contained)
        {
            UnregisterMassContribution();
            return;
        }

        if (!IsOwnedByBoat || _owningBoat == null)
            return;

        RegisterMassContribution(_owningBoat);
    }

    private void RegisterMassContribution(Boat boat)
    {
        if (boat == null)
            return;

        ResolveRigidbody();

        // Non-physical BoatOwnedItems are allowed, but contribute no mass.
        if (_rb == null)
            return;

        if (ReferenceEquals(_massContributionBoat, boat))
            return;

        UnregisterMassContribution();

        _massContributionBoat = boat;

        if (!_massContributionBoat.massContributions.Contains(this))
            _massContributionBoat.RegisterMassContribution(this);

        _massContributionBoat.RecomputeMassAndCOM();
    }

    private void UnregisterMassContribution()
    {
        if (_massContributionBoat == null)
            return;

        Boat oldBoat = _massContributionBoat;
        _massContributionBoat = null;

        oldBoat.UnregisterMassContribution(this);
        oldBoat.RecomputeMassAndCOM();
    }

    private void NotifyOwnershipChanged()
    {
        OwnershipChanged?.Invoke(this);

        // Direct apply as a safety net, because events and Unity lifecycle
        // enjoy turning deterministic code into folk horror.
        BoatOwnedItemLayerPolicy layerPolicy = GetComponent<BoatOwnedItemLayerPolicy>();
        if (layerPolicy != null)
            layerPolicy.ApplyNow();

        BoatOwnedItemVisualPolicy visualPolicy = GetComponent<BoatOwnedItemVisualPolicy>();
        if (visualPolicy != null)
            visualPolicy.ApplyNow();
    }

    private void OnDestroy()
    {
        if (_registry != null)
            _registry.Unregister(this);

        UnregisterMassContribution();

        OwnershipChanged = null;
    }
}