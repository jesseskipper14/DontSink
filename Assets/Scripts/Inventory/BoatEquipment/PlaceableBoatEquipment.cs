using UnityEngine;

/// <summary>Shared physical deployment; presentation and inventory contents stay separate.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WorldItem), typeof(BoatOwnedItem), typeof(Rigidbody2D))]
public sealed class PlaceableBoatEquipment : MonoBehaviour, IWorldItemPickupParticipant
{
    private const float PinClearance = 0.02f;
    private const float MaximumPinLift = 0.25f;
    private const float PinLiftStep = 0.01f;
    [SerializeField, Min(0.1f)] private float supportWidth = 0.75f;
    [SerializeField, Min(0.01f)] private float supportDepth = 0.16f;
    [SerializeField] private float footOffset = -0.64f;
    [SerializeField] private LayerMask supportLayers = ~0;
    private readonly Collider2D[] hits = new Collider2D[64];
    private FixedJoint2D pin;
    private Boat deployedBoat;
    private float nextSupportCheck;
    public Boat OwningBoat => GetComponent<BoatOwnedItem>().OwningBoat;
    public bool IsDeployed => isActiveAndEnabled && pin != null && pin.enabled && deployedBoat != null &&
        GetComponent<Rigidbody2D>().simulated && GetComponent<WorldItem>().Instance != null &&
        deployedBoat == OwningBoat && pin.connectedBody == deployedBoat.rb;

    public bool IsActorOnBoat(GameObject actor)
    {
        PlayerBoardingState boarding = actor != null ? actor.GetComponentInParent<PlayerBoardingState>() : null;
        return boarding != null && boarding.IsBoarded && OwningBoat != null &&
            boarding.CurrentBoatRoot == OwningBoat.transform;
    }

    public bool HasValidSupport()
    {
        Boat boat = OwningBoat;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (!isActiveAndEnabled || boat == null || boat.rb == null || !body.simulated ||
            GetComponent<WorldItem>().Instance == null) return false;
        // The foot probe follows boat-up, but rejects sideways/upside-down placement.
        if (Vector2.Dot(transform.up, boat.transform.up) < 0.85f) return false;
        Physics2D.SyncTransforms();
        Vector2 center = transform.TransformPoint(new Vector3(0f, footOffset, 0f));
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(supportLayers);
        // The clearance repair can lift the body slightly above its original
        // foot probe. Keep both restored and newly buffered poses supported.
        center -= (Vector2)boat.transform.up * (MaximumPinLift * 0.5f);
        int count = Physics2D.OverlapBox(center, new Vector2(supportWidth, supportDepth + MaximumPinLift),
            boat.transform.eulerAngles.z, filter, hits);
        if (count == hits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform) ||
                hit.GetComponentInParent<WorldItem>() != null ||
                hit.GetComponentInParent<PlayerBoardingState>() != null) continue;
            if (hit.GetComponentInParent<Boat>() == boat) return true;
        }
        return false;
    }

    public bool TryDeploy(GameObject requester)
    {
        if (!GameplayAuthority.IsAuthoritative || !IsActorOnBoat(requester)) return false;
        return RestoreDeployment(OwningBoat);
    }

    // Existing host restore path calls this after item ownership and pose are restored.
    public bool RestoreDeployment(Boat boat)
    {
        if (!GameplayAuthority.IsAuthoritative || boat == null || boat != OwningBoat ||
            !HasValidSupport()) return false;
        if (IsDeployed) return true;
        ReleasePin();
        if (!TryEstablishPinClearance()) return false;
        deployedBoat = boat;
        pin = gameObject.AddComponent<FixedJoint2D>();
        pin.enabled = false;
        pin.autoConfigureConnectedAnchor = true;
        pin.connectedBody = boat.rb;
        // A rigid attachment must not compete with contact separation against
        // its connected body. Other bodies retain their ordinary collision rules.
        pin.enableCollision = false;
        pin.breakForce = Mathf.Infinity;
        pin.breakTorque = Mathf.Infinity;
        pin.enabled = true;
        return true;
    }

    private bool TryEstablishPinClearance()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>();
        Bounds bounds = default;
        bool hasSolid = false;
        foreach (Collider2D collider in ownColliders)
        {
            if (!IsOwnSolid(collider, body)) continue;
            if (!hasSolid) bounds = collider.bounds;
            else bounds.Encapsulate(collider.bounds);
            hasSolid = true;
        }
        if (!hasSolid) return false;

        Vector2 up = OwningBoat.transform.up;
        Vector2 start = body.position;
        Vector3 originalTransformPosition = transform.position;
        Vector2 sweep = up * MaximumPinLift;
        Vector2 querySize = (Vector2)bounds.size +
            new Vector2(Mathf.Abs(sweep.x), Mathf.Abs(sweep.y)) + Vector2.one * (PinClearance * 2f);
        var candidates = new Collider2D[64];
        SkyClearanceRequirement clearance = GetComponent<SkyClearanceRequirement>();
        int obstructionMask = clearance != null ? (int)clearance.ObstructionLayers : ~0;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(~0);
        int count = Physics2D.OverlapBox((Vector2)bounds.center + sweep * 0.5f, querySize,
            0f, filter, candidates);
        // A truncated query cannot certify a safe placement.
        if (count == candidates.Length) return false;

        int stepCount = Mathf.CeilToInt(MaximumPinLift / PinLiftStep);
        for (int step = 0; step <= stepCount; step++)
        {
            float lift = Mathf.Min(MaximumPinLift, step * PinLiftStep);
            Vector2 position = start + up * lift;
            body.position = position;
            transform.position = new Vector3(position.x, position.y, originalTransformPosition.z);
            Physics2D.SyncTransforms();
            if (HasPinClearance(ownColliders, body, candidates, count, OwningBoat, obstructionMask) && HasValidSupport())
                return true;
        }

        // No safe pin: never move the boat or create a conflicting constraint.
        body.position = start;
        transform.position = originalTransformPosition;
        Physics2D.SyncTransforms();
        return false;
    }

    private static bool IsOwnSolid(Collider2D collider, Rigidbody2D body) =>
        collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
        !collider.isTrigger && collider.attachedRigidbody == body;

    private static bool HasPinClearance(Collider2D[] ownColliders, Rigidbody2D body,
        Collider2D[] candidates, int count, Boat boat, int obstructionMask)
    {
        foreach (Collider2D own in ownColliders)
        {
            if (!IsOwnSolid(own, body)) continue;
            for (int i = 0; i < count; i++)
            {
                Collider2D other = candidates[i];
                if (other == null || other.attachedRigidbody == body || !other.enabled || other.isTrigger)
                    continue;
                bool boatSurface = other.GetComponentInParent<Boat>() == boat &&
                    other.GetComponentInParent<WorldItem>() == null &&
                    other.GetComponentInParent<PlayerBoardingState>() == null;
                // Boat geometry always needs a safe gap. External placement
                // obstructions follow the instrument's authored obstruction mask,
                // including an excluded PLAYER layer. Do not move/re-layer players.
                if (!boatSurface && (obstructionMask & (1 << other.gameObject.layer)) == 0)
                    continue;
                if (!boatSurface && (
                    Physics2D.GetIgnoreLayerCollision(own.gameObject.layer, other.gameObject.layer) ||
                    Physics2D.GetIgnoreCollision(own, other))) continue;
                ColliderDistance2D distance = Physics2D.Distance(own, other);
                if (!distance.isValid || distance.isOverlapped || distance.distance < PinClearance - 0.0001f)
                    return false;
            }
        }
        return true;
    }

    public bool TryUnpin(GameObject requester)
    {
        if (!GameplayAuthority.IsAuthoritative || !IsActorOnBoat(requester)) return false;
        return BreakDeployment();
    }

    /// <summary>Host seam for future impulse/unseat systems. No impulse policy here.</summary>
    public bool BreakDeployment()
    {
        if (!GameplayAuthority.IsAuthoritative) return false;
        ReleasePin();
        return true;
    }

    private void ReleasePin()
    {
        if (pin != null) { pin.enabled = false; Destroy(pin); }
        pin = null;
        deployedBoat = null;
    }

    private void Update()
    {
        if (pin != null && !IsDeployed && GameplayAuthority.IsAuthoritative) ReleasePin();
        if (IsDeployed && GameplayAuthority.IsAuthoritative && Time.unscaledTime >= nextSupportCheck)
        {
            nextSupportCheck = Time.unscaledTime + 0.25f;
            if (!HasValidSupport()) ReleasePin();
        }
    }
    private void OnDisable() { ReleasePin(); }
    public bool AllowsWorldItemPickup(in InteractContext context) => !IsDeployed;
    public void OnWorldItemPickupCommitted() { ReleasePin(); }
}
