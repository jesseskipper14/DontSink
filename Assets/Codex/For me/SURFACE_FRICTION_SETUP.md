# Surface friction and player ground grip

2026-10-06 — Bosun.

Prepared WalkableSurface.physicsMaterial2D under Assets/Resources/Materials/Physics Materials: friction 0.35, bounciness 0. User approved and applied the settings below. No prefab settings changed.

Originally quay and dock collider materials were unassigned. Assigning moderate physics friction alone did not resolve the reported sliding. The player grip correction previously only accepted a contacted moving Rigidbody, rejecting collider-only quay/dock and explicit static Rigidbodies. The existing AntiBounce_AntiFriction material has friction 20 and bounciness 20 but no saved prefab/scene references were found, so changing it would not address these surfaces.

Applied Inspector pass: WalkableSurface assigned to NodeScene Dock, Quay/Stairs, Quay/EdgeCollider and Quay/QuayTop BoxCollider2D material slots. CharacterMoveForce Moving Floor Grip Acceleration changed from 60 to 20 on both NodeScene and BoatScene players. Retain existing collider shapes, layers, one-way effectors and moving-support velocity inheritance. Do not modify hull grounding friction or items globally.

Check walking, stopping and jumping on quay/dock, and standing/walking on the boat at full speed. Tune surface friction or grip upward only if stopping/carry becomes too loose. The first targets are quay/dock; this is not a global override of all physics materials.

Follow-up code fix: CharacterMotor2D now resolves real supporting contacts on both static and moving ground. CharacterMoveForce applies static-floor stopping grip (new default 20 acceleration units) to those contacts, while retaining the approved moving-floor grip 20 and boat point-velocity carry. Static ground has zero support velocity; no Rigidbody must be added to quay/dock. Jumping, air, ladders and swimming remain excluded from this correction. The previous moving-support API remains available.

No additional scene/Inspector edits made in this follow-up. Production runtime compilation and seven isolated Unity Play Mode contact assertions passed (collider-only ground, explicit static body, moving body velocity, jump separation and no airborne support). Actual feel still requires live testing: release movement on quay/dock, then compare boat stopping at speed. From 7 units/second, a grip cap of 20 alone takes roughly 0.35 seconds to stop on level ground, before other forces.
