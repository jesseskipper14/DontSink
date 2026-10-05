using UnityEngine;

/// <summary>Strong sliding resistance only for real hull contacts with streamed ground.</summary>
[RequireComponent(typeof(Boat))]
public sealed class BoatGroundContactResistance2D : MonoBehaviour
{
    [SerializeField, Min(0)] private float groundDeceleration = 80f;
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[64];
    private Boat boat;
    private void Awake() => boat = GetComponent<Boat>();
    private void FixedUpdate()
    {
        if (!GameplayAuthority.IsAuthoritative || boat == null || boat.rb == null || !boat.rb.simulated) return;
        int count = boat.rb.GetContacts(contacts);
        Vector2 strongestCorrection = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            var c = contacts[i];
            var ground = c.collider.attachedRigidbody == boat.rb ? c.otherCollider : c.collider;
            if (ground == null || ground.GetComponent<BoatTerrainChunk2D>() == null) continue;
            Vector2 relative = boat.rb.GetPointVelocity(c.point);
            if (ground.attachedRigidbody != null) relative -= ground.attachedRigidbody.GetPointVelocity(c.point);
            if (Vector2.Dot(relative, c.normal) > .5f) continue; // already lifting off
            Vector2 tangent = new Vector2(c.normal.y, -c.normal.x);
            Vector2 correction = -tangent * Mathf.Clamp(Vector2.Dot(relative, tangent) / Time.fixedDeltaTime,
                -groundDeceleration, groundDeceleration);
            // Multiple edges at a shared seam must not multiply resistance.
            if (correction.sqrMagnitude > strongestCorrection.sqrMagnitude) strongestCorrection = correction;
        }
        boat.rb.AddForce(strongestCorrection * boat.rb.mass);
    }
}
