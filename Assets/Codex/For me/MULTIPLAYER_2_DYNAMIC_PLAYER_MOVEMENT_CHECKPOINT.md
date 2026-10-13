# MP.2 — Dynamic player and authoritative movement checkpoint

2026-10-10. Uses the user's `Assets/Resources/Prefabs/Agents/CharacterPlayer.prefab`. The full multiplayer handoff remains open.

## Boundary

This checkpoint provides a reversible development movement playtest launched from MainMenu. It exercises the actual production player prefab and existing locomotion on a simple static deck. It does not yet start a multiplayer New Game or migrate the authored BoatScene/NodeScene players. Those scenes remain functional in single-player. This deliberately separates validating actor ownership/physics from generating and synchronizing a world.

Shared interactions, ladders, tools, inventory transfers, survival/death, saves, body/locker disconnect behavior, real boat replication and scene transitions are not enabled in this fixture. Do not infer that two connected actors make these systems multiplayer-ready.

## Player audit and existing systems reused

| Owner | Current behavior / MP.2 treatment |
| --- | --- |
| CharacterPlayer prefab | Existing root Rigidbody2D, colliders, body/force providers, inventory/equipment children and authored visuals reused. Added NetworkObject and dormant NetworkPlayerActor; no dimensions, snap offsets, item configuration or other authored references changed. |
| LocalCharacterIntentSource | Existing physical bindings and jump-edge latch retained. A simulation-source seam lets all cached ICharacterIntentSource consumers read host-validated input while the owning process separately samples local input. |
| CharacterMoveForce / CharacterSwimForce / CharacterMotor2D | Existing locomotion and jump decisions run on the host. No second controller, client movement impulse or client-authored position. |
| ForceSystem / ForceBody2D | Host runs existing force providers and Rigidbody2D integration. Client actors are non-simulated kinematic replicas; role gating alone is insufficient to stop Box2D. |
| PlayerBoardingState / PlayerLoadState | Existing host collision masks and actor mass model retained. Real boat context is not assigned in the static fixture. |
| PlayerLoadoutPersistence | Host-assigned participant persistence key applied in OnNetworkSpawn before startup restore paths can run. Each actor retains its own inventory/equipment components. No local-save fallback used to hydrate network participants. |
| CameraManager / ILocalPlayerAuthority | One process-local camera explicitly binds to the owner actor. Remote input does not receive a camera binding. Camera ownership never grants simulation authority. |
| SpriteFacingController | Existing facing determines the host's root facing. Replicas receive that facing with pose updates, rather than deciding from the host keyboard. |
| PlayerLadderClimber | Still coupled to local character intent and additional ladder state. Disabled in this fixture; network ladder claims/state must join the interaction pass. |
| PlayerSceneContextRestorer / PlayerLoadoutRestoreOnSceneStart | Existing local-scene/bootstrap assumptions must not hydrate every participant from the local player record. Disabled for this fixture. Authored single-player behavior remains available outside active sessions. |
| Interactor2D / inventory input / placement / sounding / turret / prompt controllers | Disabled in this fixture, including on the host. These local mutation paths need authenticated shared commands and synchronized outcomes before enabling them. |
| Menu canvas / scene cameras / listeners | Temporarily suspended for the fixture and restored on Leave; no authored scene or menu canvas changes. |

The full handoff's scene-player audit also identifies scene-owned HUD/drag-controller references, global-player fallbacks, boat spawn/restoration ordering, and world-generation/observer binding. Converting every scene object to a prefab is not required for MP.2 and is not bundled into this checkpoint. Authoring persistent fixtures and spawning identity-bearing gameplay entities remain distinct concerns.

## Spawn and input flow

1. Register CharacterPlayer as an NGO network prefab before starting the connection.
2. Approve transport connections through SessionParticipantRegistry. No automatic NGO player object during menu connection testing.
3. Host starts the movement fixture; connected participants each receive a distinct SpawnAsPlayerObject with host-assigned persistence and character identities. Late join receives existing actors and its own actor.
4. NetworkPlayerActor freezes actor components/physics in Awake during active sessions. OnNetworkSpawn binds identity, model, simulation and owner input; it enables the checkpoint's allowed components only. This prevents Start-based save restoration and local input from running during the pre-spawn interval.
5. Owner samples its existing LocalCharacterIntentSource. Movement/buttons are sent at 30 Hz through an ownership-required reliable RPC; no actor ID or world position is accepted from the client. Keeping the jump edge in the ordered reliable stream avoids losing it or rejecting a separately delayed edge as stale.
6. Host resolves transport sender to its connected participant and registered actor, verifies character identity, validates input, and feeds existing locomotion. Jump edges remain latched until motor consumption. Inputs expire to neutral after 0.3 seconds without valid updates.
7. Host publishes pose/facing at 20 Hz. Clients retain up to 32 snapshots and interpolate the samples surrounding a 100 ms delayed presentation time. A two-snapshot history is insufficient for this delay and caused the initial stepping. Host physics uses Rigidbody2D interpolation; client replicas use snapshot interpolation. Teleport-sized changes snap, stale samples are ignored and a stalled stream holds its last pose. No client prediction in this checkpoint.
8. Leave shuts down actors, disposes the fixture and restores menu presentation and standalone authority. Disconnected participant reservations remain in the host session. Actor despawning here is a test-stage behavior, not the final BoatScene body / NodeScene locker lifecycle.

Input validation rejects non-finite/out-of-range axes, unknown button bits, replayed/out-of-order sequences and excessive rates. Sequence comparison supports uint wraparound. The host's own input passes through the same participant/actor validation. Scene hierarchy names are diagnostic only; NGO ownership/object IDs and participant identities govern routing.

## Shared-boat replica policy and next migration

Existing Boat.AddForce/AddTorque, ForceSystem, power/fuel modules and water ticks have authority seams. Keep them. For a real shared boat, host owns Rigidbody2D simulation, helm/navigation, cargo/mass, flooding and structural state. Clients need a non-simulated kinematic boat replica with synchronized pose and explicit restore/state snapshots. A camera target or NetworkObject owner must never enable client boat simulation. Ambient sky/water cosmetics remain local.

No real boat prefab is networked by this fixture. Before enabling BoatScene/NodeScene connected play, add host world/scene bootstrap and boat spawning/replication, replace the permanent scene player with participant spawns, bind the authored scene HUD to the local actor, and route the first exclusive interaction through an authenticated dispatcher. BoatPilotingSimulation's tick split and generated-world initialization remain prerequisites, not consequences of attaching NetworkObject.

## Acceptance check

1. Let Unity import/compile the new components; make a fresh standalone build.
2. Enter MainMenu in the editor and Host through Multiplayer — Development.
3. Launch the standalone at MainMenu and Join 127.0.0.1:7777.
4. On the host, click **Start MP.2 movement playtest**. Both processes should enter the simple deck view and show two spawned players.
5. Use A/D or arrows, Space and Shift. Each window controls its own player; observe the other player's motion and facing. Jumping should use the existing grounded jump behavior.
6. Check each process follows only its owner player. Remote actors must not respond to that process's keyboard or show interaction/tool UI.
7. Repeat by starting the fixture on the host before joining the client. The late join should see the existing actor and spawn independently.
8. Leave the client: its test actor disappears; host keeps the participant reservation. Leave the host: the original menu camera/canvas returns and ordinary single-player New Game is available.

## Verification

- Player-symbol source compilation passes with editor symbols and editor-only references removed.
- 18 headless input-gate checks pass, covering invalid values/buttons, duplicate/out-of-order input, timeouts, rate limits and sequence wraparound.
- Full current project editor-symbol source compilation also passes.
- An isolated Unity 6000.0.65f1 standalone build succeeds from the production runtime scripts, CharacterPlayer prefab and required support assets. NGO's RPC generation is exercised by the actual Unity build.
- Two actual Windows game processes pass the normal-connect and host-already-playing late-join cases. Checks cover distinct identities, one owner camera/input binding per process, remote input disabled, host-only Rigidbody2D simulation, interactions disabled, independent movement, two successive grounded jumps, replicated remote movement, actor cleanup/participant reservation, Leave cleanup and standalone prefab input/physics behavior.
- The final runs report zero runtime errors. Logs: `Temp/CodexNetworking/mp2-host-final.log`, `mp2-client-final.log`, `mp2-host-late-final.log`, `mp2-client-late-final.log`. Tests inject input into the existing local adapter for headless operation; visual presentation and human-controlled feel still require the user's real-scene playtest.
- Smoothing follow-up: editor/player-symbol compilation and the isolated Unity standalone build pass. Deterministic playback checks pass for 20 Hz snapshots rendered at 144 Hz with uneven delivery, history rollover, stale updates, teleports and rotation wraparound. Two rebuilt host/client processes also pass movement, repeated jumps, ownership, replication and cleanup with zero runtime errors. Logs: `Temp/CodexNetworking/mp2-smoothing-build.log`, `mp2-smoothing-host.log`, `mp2-smoothing-client.log`.

The movement adapter deliberately does not add a competing ICharacterIntentSource to the player GameObject. Existing motors continue to resolve LocalCharacterIntentSource in both networked and standalone prefabs; its private network simulation adapter forwards jump consumption to the host input latch. This is covered by the repeated-jump and standalone input checks.

Validation projects, fixtures, harnesses, executable builds and logs are under ignored `Temp/CodexNetworking`. The shipped fixture code is reachable only through the development session panel; it does not replace authored gameplay scenes. The user confirmed the MP.2 real-project movement test and subsequently confirmed MP.2 works after the snapshot-history refinement. The development movement checkpoint is validated; real BoatScene/NodeScene integration remains pending.

## Required follow-up: owner movement responsiveness — 2026-10-12

The user reports a perceptible local-client movement delay even on localhost and expects unacceptable feel under real latency. Owner movement currently waits for host simulation and then uses the same 100 ms delayed pose interpolation as observers. Treat owner prediction/reconciliation as required functional-quality work before multiplayer closure, rather than optional polish.

Preserve host authority over boarding, seats, ladders/drop-through, collisions, impacts, health and shared objects. Prefer immediate prediction of the local owner's locomotion with acknowledged-input replay/reconciliation; retain buffered interpolation for remote actors. Audit the moving-boat/ghost reference frame and transition handling first. Cover jumping, ladders, ledges, seating, unboarding, collision corrections and latency/jitter/loss tests. Simply allowing unvalidated client transforms or enabling a second independent shared physics simulation is not the planned solution. No movement authority changes are implemented in the piloting UI slice.
