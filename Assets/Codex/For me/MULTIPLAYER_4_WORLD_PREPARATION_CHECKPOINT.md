# MP.4C1 — Host world preparation handshake

## Status

The user accepted MP.4B on 2026-10-11. The next integration dependency is implemented and native-tested: transferring and validating one host-selected world before gameplay scene entry. This is a foundation checkpoint, not completed NodeScene/BoatScene integration. No additional Inspector work or visual acceptance is required for the transfer component itself.

## Implemented

- DontSinkNetworkSession owns NetworkWorldBootstrap for the active transport lifetime, not a second singleton or network manager. Connected late joiners receive the cached host snapshot; disconnect and Leave release their transfer/readiness state and buffers.
- Prepare captures a detached WorldMapSaveSnapshot and host-selected NodeScene/BoatScene descriptor. Existing graph/node identities, runtime/POI/effects/knowledge/survey data and packed topography travel through the existing snapshot format. The generator and save DTO versions are unchanged. Mutating the original live snapshot after preparation cannot alter the transmitted packet.
- The current WorldMapSaveBuilder already produces portable RawHeightData with packed ushort samples. Bootstrap requires this data. Baked-local-asset-only and seed-only snapshots are rejected rather than allowing a client to silently generate another terrain field.
- A versioned session/scene descriptor, increasing preparation epoch, compressed byte length, expanded byte length and SHA-256 checksum precede the stream. All messages use the same reliable fragmented transport delivery. Chunks are 16 KiB, with a total eight-chunk send budget per frame across participants. No world data is added to the pose NetworkVariables.
- Client readers accept world packets only from the server connection, bound lengths before allocating, require sequential offsets/current epoch, verify the complete checksum and bounded decompression, validate snapshot/schema/terrain/node identity data, and acknowledge the exact epoch/session/checksum. No client API uploads a world, scene name, seed or topography to the host.
- The host resolves acknowledgements against connected participant identities. Repreparing clears old acknowledgements; late join blocks the new roster's readiness until that participant acknowledges. Disconnected reservations do not veto the active roster. Transfer/acknowledgement timeout leaves readiness blocked and reports a failure; the host must reprepare to recover.
- Before the participant welcome arrives, a client can buffer the host packet but cannot finish validation/acknowledgement against an unassigned session identity.
- Staging never creates or overwrites GameState, loads a scene, restores a save or starts a second generation. Downstream scene/entity integration must explicitly consume the prepared snapshot and readiness barrier.
- Idle sessions with no prepared world return without allocating transfer lists. Completed staging also avoids those allocations while no peers are awaiting transfer/acknowledgement.

## API and limits

Host integration calls `session.WorldBootstrap.Prepare(worldSnapshot, sceneName, out reason)`, then checks `AllConnectedReady`/`IsReady(connectionId)`. `session.PrepareCurrentWorld(sceneName)` stages an existing usable GameState world snapshot; it does not generate one. There is no new development button pretending to start a playable world.

Current limits: 16 MiB compressed, 64 MiB expanded, terrain dimensions up to 4096 per axis, 20,000 nodes, and a 90-second transfer/ack deadline. Oversized or unsupported data fails explicitly. These are protocol budgets, not a change to existing world resolution or the deferred WorldGen V2 architecture. Network protocol is now `DontSink/world-bootstrap/4`, requiring matching fresh builds.

WorldMapSaveSnapshot is the generated-world preparation payload, not a complete SaveGamePayload/client save mirror. Boat manifests, participant loadouts/context, shared navigation/time restoration and durable save ownership still require their own coordinated bootstrap/lifecycle work. SHA-256 detects packet mismatch; it is not authentication beyond the existing server transport connection or proof that a modified client obeys gameplay rules.

## Verification

Editor and player production source compilation pass. An isolated Unity 6000.0.65f1 standalone build passes NGO postprocessing. Three real Windows player processes pass with zero runtime errors:

- Initial preparation/acknowledgement barrier with a constructed 512×512 packed terrain snapshot using the production save DTO/height codec.
- Exact reconstructed packed terrain samples, host-selected node IDs/scene descriptor/seed, and a detached host snapshot unaffected by post-preparation mutation.
- Late join receiving the current prepared world and acknowledging against the new connected roster.
- Replacing preparation with a different seed and BoatScene descriptor, clearing old readiness and accepting the new epoch only.
- Seed-only regeneration and duplicate node IDs rejected.
- Clients remain in MainMenu and do not create or overwrite GameState during staging.
- Disconnect no longer vetoes connected-roster readiness; Leave releases the bootstrap service/buffers.

The MP.4B three-process inventory/late-join regression and MP.2 two-process movement regression also pass against this build. Final tested source copies match production NetworkWorldBootstrap and DontSinkNetworkSession. The harness resides under ignored Temp and does not modify the user's active scene. This is not a live generated NodeScene/BoatScene acceptance test.

Logs: `Temp/CodexNetworking/mp4-world-build-final.log`, `mp4-world-host.log`, `mp4-world-client.log`, `mp4-world-observer.log`, `mp4-world-regression-items-*.log`, and `mp4-world-regression-movement-*.log`.

## Next integration work

1. Feed host generation/capture into preparation before clients initialize the gameplay scene.
2. Apply the prepared world before client generator Awake, coordinate actual scene readiness, and suppress duplicate local generation/restoration.
3. Replace the authored local actor with participant actors and explicitly bind owner camera/HUD/restore paths.
4. Attach shared replication to the normal BoatSpawner-selected boat and its restored domain state, preserving authored waves/materials and existing navigation bridges.
5. Align subsequent Node/Boat transitions with MP.5 crew/lifecycle/save requirements, then expand remaining MP.4 domain adapters.

At MP.4C1, MainMenu multiplayer New/Load remained gated. MP.4C2 subsequently connects fresh hosted New Game to NodeScene; see MULTIPLAYER_4_NODE_SCENE_ENTRY_CHECKPOINT.md. Live-session Load and broader travel/save/interaction work remain gated/pending. The original multiplayer handoff and MP.4 are not DONE.
