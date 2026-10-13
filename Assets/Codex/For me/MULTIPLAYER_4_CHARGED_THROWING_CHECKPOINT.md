# MP.4 charged cargo throwing checkpoint

Implemented 2026-10-12. Manual acceptance pending; MP.4 remains in progress.

The existing Q hold/aim/release preview now sends a Begin/Pulse/Cancel/Release command stream. The host times charge, requires the same canonical Hands item and selected Hands slot, validates finite direction and world-item capacity, and clamps charge using the actor's authored threshold/duration. Missing, cancelled, expired, changed-item, seated and unsupported releases preserve the item. Charge leases expire after 400 ms without a heartbeat or 10 seconds total; disconnect clears them. Ordinary UI input allows one pending release and cancels charging on lost focus, blocked input, selection change or disable.

The existing PlayerInventory.TryThrowHeld and ThrownCargoPhysics apply the actual release: carrier point velocity is inherited once, charge adds the authored throw impulse, player collision inclusion is temporary, thrower-only overlap grace is bounded, and ordinary collision overrides return after sustained rest. Only the host simulates projectile/cargo collisions; other peers interpolate replicated loose-item poses. Replayed release commands cannot create another projectile.

This extends supported ordinary Hands cargo. Live tether deployment, bells, treasury, save/reconnect and owner movement prediction remain outside this checkpoint. Bowling and moving-boat feel need user presentation acceptance; native tests verify networking and collision-policy restoration rather than replacing that play test.

Implementation: NetworkSceneBoat.Throwing.cs, PlayerInventoryInput, loose-item replication. Protocol 14 requires matching builds.

Native validation evidence is recorded in MULTIPLAYER_4_SIX_PASS_ACCEPTANCE.md.
