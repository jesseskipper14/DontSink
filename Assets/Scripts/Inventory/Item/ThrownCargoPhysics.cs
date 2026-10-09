using System.Collections.Generic;
using UnityEngine;

/// <summary>Real physics only: scoped player collision and temporary thrower-only overlap grace.</summary>
[DisallowMultipleComponent]
public sealed class ThrownCargoPhysics : MonoBehaviour
{
    private struct Pair { public Collider2D item, actor; }
    private struct Override { public Collider2D collider; public LayerMask include; public int priority; }
    private struct BodyState { public Rigidbody2D body; public CollisionDetectionMode2D detection; }
    private readonly List<Pair> ignored = new();
    private readonly List<Override> overrides = new();
    private readonly List<BodyState> bodies = new();
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
    private float restoreAt;
    private float settledTime;

    public static void Launch(WorldItem item, GameObject actor, Vector2 deltaVelocity, float graceSeconds)
    {
        var protection = item.GetComponent<ThrownCargoPhysics>() ?? item.gameObject.AddComponent<ThrownCargoPhysics>();
        protection.enabled = true;
        protection.Begin(actor, graceSeconds);
        foreach (var body in item.GetComponentsInChildren<Rigidbody2D>(true))
        {
            if (body.bodyType != RigidbodyType2D.Dynamic || !body.simulated) continue;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // Canonical drop has already supplied carrier velocity exactly once.
            body.linearVelocity += deltaVelocity;
            body.WakeUp();
        }
    }

    private void Begin(GameObject actor, float graceSeconds)
    {
        RestoreGrace();
        RestoreOverrides();
        settledTime = 0f;
        foreach (var body in GetComponentsInChildren<Rigidbody2D>(true))
            if (body.bodyType == RigidbodyType2D.Dynamic && body.simulated)
                bodies.Add(new BodyState { body = body, detection = body.collisionDetectionMode });
        int playerLayer = LayerMask.NameToLayer("Player");
        var actorBody = actor != null ? actor.GetComponentInParent<Rigidbody2D>() : null;
        var actorColliders = actorBody != null ? actorBody.GetComponentsInChildren<Collider2D>(true) :
            actor != null ? actor.GetComponentsInChildren<Collider2D>(true) : new Collider2D[0];
        foreach (var collider in GetComponentsInChildren<Collider2D>(true))
        {
            if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody == null) continue;
            if (playerLayer >= 0)
            {
                overrides.Add(new Override { collider = collider, include = collider.includeLayers, priority = collider.layerOverridePriority });
                collider.includeLayers |= 1 << playerLayer;
                collider.layerOverridePriority = Mathf.Max(10, collider.layerOverridePriority);
            }
            foreach (var other in actorColliders)
            {
                if (other == null || other == collider || other.isTrigger || !other.enabled ||
                    (actorBody != null && other.attachedRigidbody != actorBody) || Physics2D.GetIgnoreCollision(collider, other)) continue;
                ignored.Add(new Pair { item = collider, actor = other });
                Physics2D.IgnoreCollision(collider, other, true);
            }
        }
        restoreAt = Time.time + Mathf.Clamp(graceSeconds, .05f, .5f);
    }

    private void FixedUpdate()
    {
        if (ignored.Count > 0)
        {
            bool separated = true;
            foreach (var pair in ignored)
                if (pair.item != null && pair.actor != null && pair.item.enabled && pair.actor.enabled &&
                    pair.item.gameObject.activeInHierarchy && pair.actor.gameObject.activeInHierarchy &&
                    pair.item.Distance(pair.actor).isOverlapped) { separated = false; break; }
            if (separated || Time.time >= restoreAt) RestoreGrace();
        }

        bool resting = bodies.Count > 0;
        foreach (var state in bodies)
        {
            var body = state.body;
            if (body == null || !body.simulated) continue;
            var carrier = ResolveRestFrame(body);
            if (carrier != null && (!carrier.simulated || !carrier.gameObject.activeInHierarchy)) carrier = null;
            Vector2 reference = carrier != null ? carrier.GetPointVelocity(body.worldCenterOfMass) : Vector2.zero;
            float spin = body.angularVelocity - (carrier != null ? carrier.angularVelocity : 0f);
            if ((body.linearVelocity - reference).sqrMagnitude > .35f * .35f || Mathf.Abs(spin) > 8f)
            { resting = false; break; }
        }
        // A sustained pause avoids restoring collision settings at a jump/throw apex.
        settledTime = resting ? settledTime + Time.fixedDeltaTime : 0f;
        if (settledTime >= .35f) enabled = false; // OnDisable restores ordinary settings.
    }

    private Rigidbody2D ResolveRestFrame(Rigidbody2D body)
    {
        int count = body.GetContacts(contacts);
        for (int i = 0; i < count; i++)
        {
            var contact = contacts[i];
            var other = contact.collider.attachedRigidbody == body ? contact.otherCollider : contact.collider;
            if (other == null || contact.normal.y < .5f) continue;
            var support = other.attachedRigidbody;
            // Static ground wins over former boat ownership; moving cargo/players
            // are not a rest frame merely because they collided with the projectile.
            if (support == null || support.bodyType == RigidbodyType2D.Static) return null;
            if (support.bodyType == RigidbodyType2D.Kinematic) return support;
        }
        var bell = GetComponent<DivingBellContainedItem>();
        if (bell != null && bell.CurrentBell != null)
            return bell.CurrentBell.GetComponent<Rigidbody2D>();
        var owned = GetComponent<BoatOwnedItem>();
        return owned != null && owned.OwningBoat != null ? owned.OwningBoat.rb : null;
    }

    private void RestoreGrace()
    {
        foreach (var pair in ignored)
            if (pair.item != null && pair.actor != null) Physics2D.IgnoreCollision(pair.item, pair.actor, false);
        ignored.Clear();
    }
    private void RestoreOverrides()
    {
        foreach (var state in overrides)
            if (state.collider != null)
            { state.collider.includeLayers = state.include; state.collider.layerOverridePriority = state.priority; }
        overrides.Clear();
        foreach (var state in bodies)
            if (state.body != null) state.body.collisionDetectionMode = state.detection;
        bodies.Clear();
    }
    private void OnDisable() { RestoreGrace(); RestoreOverrides(); }
}
