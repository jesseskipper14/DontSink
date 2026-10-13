# MP.4A — Shared sailing-boat integration

## Status and checkpoint ordering

MP.3 and the MP.4A sailing-boat slice were validated by the user on 2026-10-11. MP.4 is **in progress**, not DONE. The audit identified a gap between MP.2/MP.3's stationary development fixtures and the shared moving boat required by the architecture. This first MP.4 slice addresses that gap before general inventory, vendor, harvest, ladder, AI and damage adapters are expanded.

This runs from MainMenu's reversible development panel. It does not enable multiplayer New Game/Load or load the authored BoatScene/NodeScene. Coordinated scene loading, real world generation distribution, ordinary scene HUD binding and durable session saves remain pending. No changes were made to those scenes or the original boat prefab for this slice.

## Implemented path

- SharedBoatPlaytest is a registered server-owned network prefab referencing the existing TestBoat_10_Compartment_Flooding boat. Its deck attachment point is Inspector configurable.
- Both peers instantiate that authored boat once under the network root. Only the host simulates the boat Rigidbody, force providers, propulsion, piloting and compartment equalization. Client root and mounted Rigidbody bodies are not simulated.
- The existing Boat/ForceSystem/BuoyancyPolygonForce/ThrottleForce/EngineModule/BoatPilotingSimulation implementations run the physical boat. A small isolated WaveField/WaveManager supplies calm water. The development boat starts with 1000 power; this is test provisioning, not a production save/load rule.
- NetworkBoatPlaytest publishes 20 Hz typed boat poses, velocities, throttle/rudder/navigation state, power, engine running bits, door state and bounded compartment water/air records. Client boat poses use the same 100 ms delayed server clock as player and cargo presentation. Compartment and door replicas use explicit domain restore methods.
- NetworkSharedInteractionWorld attaches its existing generator, chair and cargo path to the boat. The generator uses the actual boat's power state. Loose cargo has boat ownership; existing pickup/drop and exact held-item identity paths remain in use.
- Participants receive explicit boarding context, owner-local camera/viewer binding and host-calculated unoccupied deck spawns. Spawn columns derive from boat width, rather than participant IDs or fixed four-player slots.
- The existing exclusive chair claim grants helm authority. Only that connected actor may start/stop engines or issue throttle/rudder presets. Accepted targets ramp through the ordinary simulation and continue after the actor leaves the station. Disconnect releases the helm claim without resetting the boat's accepted orders.
- Door commands resolve a prefab-local bounded index and validate host actor position/range before calling DoorRuntime.SetOpen. Domain obstruction rules remain in charge of closing. Duplicate requests do not toggle twice. Client presentation uses RestoreOpenState, not a second obstruction/physics decision.
- Commands validate transport sender, connected participant, exact character identity, actor ownership, boat incarnation, per-target revisions, finite/bounded values and request replay/rate limits. Acknowledgements include current revisions so a second order can follow the first reply without waiting for the next pose snapshot.
- NetworkPlayerActor.AuthoritativePosition uses actual physics position or the known seat constraint. A seated player's disabled Rigidbody does not provide a stale range/drop coordinate as the boat sails away. This also feeds actor and held-cargo snapshots.
- BoatPilotingSimulation now gates live authority and its entire simulation tick on GameplayAuthority. EngineModule accepts an explicit water binding for this isolated scene; normal boat resolution remains the default.

## Asset and replica limits

MP.4A initially used protocol `DontSink/shared-boat/4`; the subsequent shared-item slice uses `DontSink/shared-items/4`. every peer needs the same fresh build and authored assets. Door/engine indices refer to identical prefab hierarchy order. Each collection is capped at 64 and rejects larger boats rather than silently truncating. Structural edits/module installs are not replicated by this schema.

This boat has no functional rudder contributor. Rudder controls remain disabled until the authored boat supplies steering; the network does not invent steering authority.

Normal scene restore/save, debug tools, unrelated modules and unreplicated interaction adapters remain disabled on this cloned boat. Its map-table adapter is removed before Awake because it requires a normal scene map overlay; its table sprite and source prefab are retained. Original single-player assets and lifecycle are unchanged.

Engine on/off and power are mirrored. General engine fuel-container inventories, arbitrary cargo/item snapshots, securing, containers/markets, hatches/ladders, NPC/AI decisions, damage/death and complete world state are **not** covered by this slice. Compartment fluid state is covered; it is not a complete damage model. Ambient sky, cosmetic water and stars are not networked.

## Manual acceptance

1. Leave the previous MP.2/MP.3 test session. Import/compile and make a fresh standalone build.
2. Host from MainMenu's Multiplayer development panel and join from the other instance.
3. On the host choose **Start MP.4 sailing boat playtest**. It replaces the static test ground with the authored boat. Both players should stand on the boat, with separate local cameras.
4. Walk to the station on the right and use it. Compete for it from both peers; only one should sit and gain helm buttons.
5. Start engines, then choose half/full ahead. Both peers should see the same ramping throttle and physical boat motion. Walk/jump on the moving boat and watch the other actor's motion.
6. Order Full stop and immediately leave the station. Throttle should continue ramping to zero. Another player should then be able to claim the station.
7. Use the generator and pick up/drop the crate while moving. Verify shared resources and one cargo item, with drops placed near the actor's current position.
8. Approach a door and toggle it. Both peers should see the state. Closing across a physical obstruction should still be rejected by the domain rule.
9. Join another process while sailing/occupied. Check current boat, helm, door, power, cargo and participant state. Disconnecting that observer must not release another player's helm/cargo.
10. Leave the session. The menu presentation and single-player authority should return.

## Verification

Editor-symbol and player-symbol production source compilation pass. An isolated Unity 6000.0.65f1 standalone build passes, including NGO RPC postprocessing. The final production source hashes match the copies compiled into that player.

Three actual Windows player processes pass with zero runtime errors: server-only boat/actor simulation, replica body policy, boarding context, exclusive remote helm, engine start, ramping throttle and real physical propulsion, non-owner helm rejection, the moving chair's actual seat point, exactly-once door toggle and out-of-range rejection, current boat/controls/occupancy on late join, unrelated observer disconnect, cargo pickup/drop while seated on the moving boat with exact item identity and current-frame placement, Full stop continuing after station release, owner disconnect releasing control, and clean Leave restoring single-player authority.

The two-process MP.2 movement regression and three-process MP.3 interaction regression also pass, including interpolation history, camera/input ownership, jumps, static interaction conflicts/replay, exact cargo identity, late join and disconnect cleanup. The last additional cargo-policy change only executes for the sailing carrier path and is covered by the final three-process boat test.

Logs: `Temp/CodexNetworking/mp4-build-complete.log`, `mp4-host-complete.log`, `mp4-client-complete.log`, `mp4-observer-complete.log`, `mp4-movement-host.log`, `mp4-movement-client.log`, `mp4-mp3-host.log`, `mp4-mp3-client.log` and `mp4-mp3-observer.log`.

The harness uses production source/assets in an isolated Unity project under ignored Temp; it does not replace the open project scene. It injects/repositions host actors/cargo for deterministic assertions. The user accepted human-controlled presentation and moving-deck traversal in the authored MainMenu on 2026-10-11. Full connected BoatScene/NodeScene/world integration is not established by these results.

## Remaining MP.4 sequence

1. Moving-boat manual acceptance completed on 2026-10-11.
2. Attach the shared entity/actor policy to the normal scene bootstrap with host-selected assets/world state, rather than duplicate authored local actors/boats. Keep scene protocol/lifecycle work aligned with MP.5.
3. Expand authenticated domain adapters for inventory/container/market/harvest and securing; replicate complete item data, not just the fixture's known crate/fuel layout.
4. Add hatches/ladders, AI and damage/world simulation state with explicit authority and target registration.
5. Repeat conflicts, movement, late join and disconnect checks for those systems before marking MP.4 complete.
