using UnityEngine;

/// <summary>Shared physical deployment; presentation and inventory contents stay separate.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WorldItem), typeof(BoatOwnedItem), typeof(Rigidbody2D))]
public sealed class PlaceableBoatEquipment : MonoBehaviour, IWorldItemPickupParticipant
{
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
        int count = Physics2D.OverlapBox(center, new Vector2(supportWidth, supportDepth),
            boat.transform.eulerAngles.z, filter, hits);
        if (count == hits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform) ||
                hit.GetComponentInParent<WorldItem>() != null) continue;
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
        deployedBoat = boat;
        pin = gameObject.AddComponent<FixedJoint2D>();
        pin.autoConfigureConnectedAnchor = true;
        pin.connectedBody = boat.rb;
        pin.enableCollision = true;
        pin.breakForce = Mathf.Infinity;
        pin.breakTorque = Mathf.Infinity;
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
