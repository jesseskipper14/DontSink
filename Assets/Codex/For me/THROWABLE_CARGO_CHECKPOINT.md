# Throwable cargo — 2026-10-07

Implemented prototype; leave incoming handoff unprefixed until Skip can bowl a loose cargo stack across the authored deck.

## Behavior and setup

No scene/prefab/Inspector assignments required. Existing PlayerInventoryInput components gain tunables: Q hold threshold .20s, full charge .80s after threshold, launch speeds 8–24 world units/s, .25s maximum thrower grace, 1.1s/24-point local arc. Select Hands while carrying a physical droppable item. Tap Q releases through the ordinary drop path on Q-up. Hold Q shows the arc; release throws toward the local camera's current mouse position. Charge changes launch velocity, not a fixed mass-dependent impulse. No new item category.

Other selected equipment/hotbar slots retain ordinary Q-drop on release; they are not inventory-slot throw sources. A deployed sounding line remains on its existing proxy-release path and is not throwable while deployed. Changing selection/held item, losing local input, death/spectator mode, paused play, blocked UI/cartridge context or disabling input cancels the charge/preview.

## Existing paths audited / reused

- PlayerInventoryInput: existing Q-down selection drop; now pending tap/hold and camera-local LateUpdate aiming after CameraManager follow.
- PlayerEquipment Hands: the authoritative held item is an ItemInstance, not the decorative PlayerHeldItemVisual. Dropping spawns the physical WorldItem prefab. No second held-world-object lifecycle was invented.
- PlayerInventory.TryDropSelected / WorldItemDropUtility.TryDrop: canonical equipment removal/rollback, same instance identity, world initialization/mass, boat ownership, bell containment and layer policies. An out-WorldItem overload returns the released object; a per-inventory busy guard blocks callback reentry. Ordinary drop callers keep their original signature.
- WorldItemDropUtility.InheritReleaseVelocity: existing carrier precedence is occupied bell, boarded real boat, then actor rigidbody, with angular point velocity. Shared read-only GetReleaseVelocity supplies the same carrier motion for preview. Launch adds charged velocity once after canonical inheritance.
- WorldItem: existing physical mass and ground-tunneling protection. Thrown bodies explicitly use Continuous collision detection.
- CameraManager / GameplayInputBlocker / PlayerDeathSystem: exact local input owner, current active camera and context/death gates; preview is local presentation only.
- CharacterMoveForce: actual contacted support and bounded force-based grip; it does not overwrite collision response with a walking velocity. No movement tuning changes were made for throwing.

## Collision policy

Current Physics2D matrix already permits WorldItem↔WorldItem, WorldItem↔BoatItem and BoatItem↔BoatItem. WorldItem↔Player is excluded. ThrownCargoPhysics adds a Player include-layer override only to the released item's physical colliders, leaving the global matrix/resting cargo untouched. This override restores once the item settles, or on disable/pickup/despawn. No new layer and no damage, hit callback, stun or scripted knockback.

Only non-trigger collider pairs belonging to the thrower's own Rigidbody2D receive temporary IgnoreCollision. Pre-existing ignored pairs are not changed. Other players remain hittable immediately. Grace restores when all pairs separate or the timeout expires; component/object disable also restores pairs and collider override state. The launch uses real dynamic Rigidbody2D momentum, so heavier items have the same speed but greater momentum.

Settling refinement: after all launched bodies remain below .35 units/s relative speed and 8 degrees/s relative spin for .35 seconds, the component disables and restores the original collider include masks/priorities and collision-detection modes. A contacted static surface takes precedence over old boat ownership; kinematic deck/ghost support, occupied bell and owning-boat point velocity provide moving reference frames. Dynamic cargo or players do not become a rest frame merely from a collision. Movement resets the settle timer, so a brief throw apex does not end projectile collision early. Already restored items remain ordinary cargo if bumped again; a fresh throw reapplies the temporary settings.

## Authority and limits

PlayerInventory.TryThrowHeld is the trusted host entry point. It verifies GameplayAuthority, no active release transaction, live actor, Hands selection, exact held instance ID, droppable dynamic prefab and no deployed sounding proxy. It rejects nonfinite/degenerate aim and nonfinite charge/origin; charge is clamped and speed comes from that actor's configuration. It removes nothing before validation succeeds, and rejected requests do not locally release the item. A future transport must authenticate its sender before resolving this inventory. Actual multiplayer transport/replicated physics remains a separate incomplete pass; this prototype does not pretend to implement two-client replication.

Preview is a ballistic hint using the same origin, charge and carrier motion plus Physics2D gravity/prefab gravityScale. It does not predict hull collisions, water drag/buoyancy, damping or other cargo motion.

## Changed / new files

Changed: Assets/Scripts/Inventory/PlayerInventoryInput.cs; PlayerInventory.cs; Item/WorldItemDropUtility.cs. New: Item/ThrownCargoPhysics.cs and metadata, this checkpoint and metadata. Existing scripts inspected also include WorldItem, PlayerEquipment, PlayerHeldItemVisual, ItemDefinition, BoatOwnedItemLayerPolicy, PlayerBoardingState, CharacterMoveForce, CameraManager, PhysicsFrame2D, GameplayAuthority and SoundingCartographyService. Physics2DSettings and TagManager were audited without changes. No damage or save schema changes.

## Verification / playtest

2026-10-08: Skip authorized marking this prototype pass DONE_. The incoming handoff and its unchanged Unity metadata were renamed together. The implementation includes restoration of ordinary collision settings after settling. Multiplayer transport/replicated physics remains outside this closure.

Production runtime/editor compilation passes. Isolated Unity tests use a compiled assembly of current production runtime source and minimal scene objects; no gameplay class stubs. Logs and fixtures stay under ignored Library/CodexThrowableChecks. Raw keyboard sampling is not synthesized: local tap/hold/preview tests invoke actual input decision methods with prepared pending state and a real local camera binding. The fixture does not certify the authored boat/ghost/cargo-bay assembly or two-client transport.

33 focused checks pass: exact held identity/reentry, client and malformed-request rejection, charge clamps, heavy-item mass-independent speed, inherited actor motion, canonical equipment/hotbar drop, swept detection, scoped player collision with real momentum transfer, thrower separation/timeout/disable restoration, gravity-enabled bowling through the canonical held release, local camera ownership, tap versus hold, charge trajectory extension and blocked/lost-item/application-focus/pause cancellation. At full configured speed, a 50-mass projectile thrown from 12 units away moved all nine 10-mass stacked crates by more than .75 units. No scripted hit response was used. Log: Library/CodexThrowableChecks/throwable-checks-final.log.

Settling validation: 37 total checks pass against the updated production runtime assembly. Four added checks cover temporary-apex retention, renewed-motion timer reset, ordinary settings restoration at rest and restoration while riding a contacted deck at 20 world units/s. The gravity-enabled nine-crate bowling test still passes. Log: Library/CodexThrowableChecks/throwable-rest-checks.log.

Manual acceptance: carry/select Hands, tap Q; pick up again, hold about one second, aim through a loose crate stack across the deck and release. Repeat at boat speed and in reverse. Try a heavy crate, close self-overlap, another player collider, selection changes and opening a cartridge while charging. Secured/rack-attached cargo should retain its existing securing behavior rather than being loosened by this pass. No health loss should occur.
