# MP.3 — Shared interaction vertical slice

## Scope

MP.2's movement/smoothing checkpoint was validated by the user. MP.3 extends that reversible MainMenu development deck with the existing BasicGenerator_Installed, PilotChair_Metal and CrateOfWood assets. It runs the existing GeneratorModule/BoatPowerState simulation, SeatController2D occupancy, WorldItem pickup through ItemAcquisitionResolver/PlayerEquipment, and WorldItemDropUtility release. It does not replace those domain implementations with networking-specific fuel, equipment or seat rules.

This is a contained shared-interaction checkpoint. The fixture remains a static deck, not a sailing boat or a generated world. Authored BoatScene/NodeScene loading, boat physics/pose replication, ordinary HUD binding and generalized interaction dispatch remain open integration work. MP.3 does not mark the overall multiplayer architecture handoff DONE.

## Authoritative request path

`NetworkSharedInteractionWorld` is a server-owned NetworkObject registered before connection. Clients may invoke its reliable SubmitRequestRpc because it is a shared object; transport sender identity is then resolved through SessionParticipantRegistry and the session's spawned actor registry. The claimed character identity must match both the participant and that actor. Clients do not submit an actor transform, drop coordinate, inventory instance, power value or fuel amount.

Requests contain a monotonically sequenced request ID, world incarnation, per-target revision and an allowlisted command. The implicit RPC NetworkObject target identifies the world, and commands select one of its explicitly bound module, station or cargo targets. These are not scene name/hierarchy searches. Per-target revisions keep unrelated station/cargo/module actions from invalidating each other. Wrong incarnation or revision is rejected before mutation. Live fuel/on-state changes also advance the module revision; continuously changing power and cargo pose do not invalidate ordinary commands.

`NetworkInteractionRequestGate` maintains a bounded token bucket per connected sender: eight immediate requests, replenished at ten per second. Repeating the most recent request returns its recorded result; an older request is rejected. Both success and failure are recorded. Reliable ordered delivery plus sequence rejection prevents an old duplicate from executing after later requests. Request state is removed when a participant disconnects.

The host checks authoritative Rigidbody2D position against the target's interaction radius. Cargo additionally runs WorldItem.CanPickup with the explicit requesting actor's InteractContext, including the existing item's tighter pickup range and acquisition rules. The development fixture is accessible to connected participants; station exit and cargo drop require ownership of the claim. Normal boat boarding/container access policies require the later real-scene integration and must not be inferred from this fixture.

## Domain behavior and replication

- Generator commands use SetOn and TryConsumeFuelCharges. Its existing Update generates power and burns fuel only on the host. The extra **Burn one test fuel charge** button exercises exactly-once resource mutation without waiting for the authored 0.05 charge/second burn interval; it is a development control.
- GeneratorModule.BindPowerSource allows the standalone test/dock generator to use an explicit BoatPowerState. An ordinary installed generator still resolves its parent boat. No fake Boat is constructed to satisfy the test.
- The chair delegates occupancy, pinning and ejection to SeatController2D. Two claims cannot succeed. Non-owners cannot release it. Server physics is restored by the existing seat release operation. Disabled-body player snapshots use the constrained Transform pose so clients see the seated actor at the actual seat.
- Cargo acquisition moves the exact host ItemInstance into the requesting actor's existing hands slot. The WorldItem is retired by its existing pickup operation; the claim/revision immediately prevents a second request from acquiring it. Drop uses the existing drop utility at a host-calculated nearby position and removes that hands item after a world object is created successfully. The item's identity survives pickup/drop.
- The host publishes module state/power, station ownership and cargo ownership/pose. The typed snapshot includes exact cargo, fuel-container and fuel item identities. Clients reconstruct this fixture's known single-fuel-container layout through the existing ItemInstanceSnapshot restore API. Late join gets current state and the host's item identities. This representation is deliberately limited to the fixture's plain crate and one fuel slot; general nested/chart/container replication remains later work.
- Clients do not simulate cargo or player Rigidbody2D bodies. Loose cargo uses the same buffered pose interpolation as players. Clients apply explicit power/fuel restore operations and mirror the shared held cargo into the appropriate replica's hands; existing PlayerHeldItemVisual shows it. Actor/world arrival order is handled by deferring replica application until item/state data and the holder actor are available.
- Replica fuel restore only occurs when charges or on/off state change, rather than rebuilding item/container objects on every pose update. No JSON or managed-reference graphs are transmitted by this fixed-layout snapshot.
- Only the fixture's intended components run. Legacy pilot overlays, local cargo/interactor commands and unrelated module behaviors remain disabled, preventing a second local mutation path.

## Lifecycle

The host releases station/cargo claims when a participant despawns or disconnects. If NGO has already removed the avatar, the world still owns the exact cargo instance and releases it at the last authoritative position. Disconnecting an unrelated observer must not release another player's claims. Seat auto-ejection clears the network claim as well.

Leave shuts down network objects and destroys fixture props/materials, then restores the original menu presentation and single-player authority. These runtime fixture states are not written into gameplay saves. Full disconnected-body/locker/sacred-item persistence and reconnect authentication belong to the lifecycle checkpoint, not this test.

## Assets and code

- Assets/Resources/Prefabs/Multiplayer/SharedInteractionPlaytest.prefab: explicit authored asset references; no changes to the original generator, chair or crate prefabs.
- Assets/Scripts/Multiplayer/NetworkSharedInteractionWorld.cs: authenticated request dispatch, domain adapters, snapshots, replicas and development controls.
- Assets/Scripts/Multiplayer/NetworkInteractionRequestGate.cs: ordered replay rejection and request budget.
- DontSinkNetworkSession: register the fixture prefab, start/bind it, expose the explicit actor registry and release claims during cleanup. Protocol is bumped to `DontSink/shared-interactions/3`; both processes require matching fresh builds.
- NetworkPlayerActor: movement uses the modern owner-restricted Rpc API; disabled-body capture supports seat pinning. Ordinary single-player remains dormant as before.
- GeneratorModule: optional explicit power source; installed boat resolution remains the default.

## Manual acceptance

1. Import/compile and make a fresh standalone build. Host in the editor's MainMenu development panel; join from the standalone.
2. On the host, click **Start MP.3 interaction playtest**. It starts the movement fixture if needed. The shared-interaction controls appear at the upper left. Generator is left of spawn, crate near the center and chair to the right.
3. Walk near the generator. Start/stop it from either peer. Both should see the same on/off, power and fuel readouts. Burn one test fuel charge and verify one decrement on both peers.
4. Walk both players to the chair. Compete for **Use station**. Exactly one player sits; the other receives a rejection. Only the seated player can leave. Movement should resume after leaving.
5. Compete for **Pick up cargo**. Exactly one player carries the crate, visible on both peers. Drop it; the same crate should reappear once and become available to either player again. The item's own 1.5-unit pickup range still applies even when the development button is enabled within the wider station radius.
6. Join another process while the generator runs and a player owns the station/cargo. It should see current resources, occupancy, held visual and player poses without duplicate cargo.
7. Leave the participant holding cargo/occupying the station. The station should become available and one cargo item should remain. Leaving an unrelated observer must leave the original claims intact.
8. Leave the host. The original menu presentation and ordinary single-player path should return.

## Verification

Editor-symbol and player-symbol production source compilation passes. An isolated Unity standalone build from the production runtime source and referenced authored assets passes, exercising NGO RPC generation.

Three actual Windows player processes pass the final acceptance run with zero runtime errors. Checks cover the same Request/result path used by the development buttons, live generator power, client-side mutation rejection, repeated/stale fuel commands producing one decrement, forged character identity, wrong incarnation/revision, out-of-range commands, conflicting seat claims/non-owner release, seat pose replication and physics restoration, conflicting cargo pickup, exact item identity across repeated drop, late-join resources/occupancy/held equipment, unrelated observer disconnect, owner disconnect release with exactly one cargo object, and Leave cleanup. Request-gate checks cover replay results, stale/zero IDs, rate limits, replenishment and sequence wraparound.

The existing two-process MP.2 regression also passes with the modern owner-only movement RPC: distinct identities, local camera/input, server-only physics, independent movement, two grounded jumps, buffered presentation, cleanup and ordinary single-player prefab behavior. The final production-source hashes match the copies built in the validation project.

Logs: `Temp/CodexNetworking/mp3-build-acceptance.log`, `mp3-host-acceptance.log`, `mp3-client-acceptance.log`, `mp3-observer-acceptance.log`, `mp3-movement-host.log` and `mp3-movement-client.log`. The harnesses inject inputs and reposition actors on the host for repeatable range/conflict scenarios. The user validated MP.3 in the actual project on 2026-10-11 and authorized MP.4. Original authored gameplay scenes are not validated as connected scenes by these fixture tests.
