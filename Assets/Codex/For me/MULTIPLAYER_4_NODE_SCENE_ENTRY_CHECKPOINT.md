# MP.4C2 — Hosted New Game enters the authored NodeScene

## Status

Initial hosted NodeScene entry is implemented, native-tested and accepted by the user (2026-10-11). MP.4 and the overall multiplayer handoff remain in progress. This checkpoint connects the world-preparation foundation to a real gameplay scene; it is not completion of all gameplay interactions, travel or lifecycle/save work.

## Entry and ordering

1. Start a fresh host/client session in MainMenu. Host chooses the existing New Game button. The normal New Game reset runs on the host; a client cannot initiate it, and a running development fixture cannot be converted into a hosted game.
2. The host loads the authored NodeScene and lets its existing generator, runtime binder and BoatSpawner initialize. SceneLoaded disables the authored local character before its Start/input/physics/restoration loops run; its position/catalog remain authoring references for shared actors.
3. Capture the generated world and existing restored boat through WorldMapSaveBuilder and SceneTransitionController.SaveCurrentBoatState. The prepared packet additionally contains initial player/node, boat, time, celestial and navigation state. It remains a New Game bootstrap descriptor, not a complete SaveGamePayload/save mirror.
4. Each client validates and acknowledges the prepared host world. Only then does the host order scene entry for that participant, tied to the preparation epoch.
5. The client creates/applies GameState before LoadSceneAsync. Topography Awake explicitly restores the prepared raw host field before considering any local runtime cache/bake/generation. Graph Awake restores the host graph and seed rather than generating locally. The graph seed binding also controls subsequent deterministic town/layout derivation.
6. The normal BoatSpawner resolves the host-selected boat GUID through the authored scene catalog. A missing/mismatched definition fails entry rather than silently substituting the fallback. Client spawning occurs under an inactive staging parent with passive body/force policy applied before Awake; it is reapplied after module restoration. Client loose-item/tether physics restoration is deferred.
7. After local terrain/runtime/boat initialization, the client acknowledges scene readiness. The host then creates its owning participant actor at a host-selected unoccupied player spawn. Actors already received during late join survive the client scene load under persistent roots, with rendering hidden until the gameplay scene is locally ready.
8. Bind owner camera, inventory display and boat viewer explicitly. Leave/host disconnect shuts down networking, releases role/transfer state, restores the default local persistence key and returns to MainMenu.

NGO automatic scene management remains disabled. Named messages implement this narrow initial-entry protocol; it must be extended with the MP.5 crew transition/lifecycle rules before Node/Boat travel is enabled. This checkpoint originally used schema protocol `DontSink/node-scene/4`; the subsequent boarding/station slice advances it to `DontSink/node-boarding/4`, requiring matching fresh builds and assets.

## Shared boat and actor policy

- New registered SharedSceneBoat prefab/NetworkSceneBoat adopts the existing BoatSpawner boat on each peer. It does not instantiate another boat, replace authored water, change materials or add development power.
- Host alone simulates player and boat bodies. Client boat bodies/force providers are passive before activation; ordinary client actor bodies remain non-simulated pose replicas.
- SharedSceneBoat publishes root pose, power, navigation/control values, engine-running bits, door bits and compartment water/air. Client replicas use delayed pose history and explicit domain restore APIs. The bounded arrays retain the current 64-entry capacity limits.
- NetworkPlayerActor retains host-authenticated input, keyed loadouts and complete item snapshots. Scene catalog resolution is deferred until local scene initialization; no client inventory restoration runs against an unavailable catalog.
- Owner camera and PlayerInventoryUI bind to that participant, not the inactive authored character. The inventory display is read-only in this checkpoint; direct slot clicking and drag mutation are disabled until generic inventory commands are wired. Other personal HUD/tool/death bindings still need coverage.
- Client agent brain ticking and WorldMapDriftSimulator ticking are authority-gated. This prevents a second simulation; it does not replicate live NPC movement or changing town stats yet.

## Deliberate remaining scope

- Ordinary actor interactors/tools/ladders/boarding stations remain disabled by the existing network actor policy. The earlier shared interaction/fuel/helm development fixtures remain separate tests. Connecting all normal scene interaction targets, boarding presentation and broader inventory/domain commands is still MP.4 work.
- Live NPC/fish movement, AI/damage/health state, evolving world/town stats and complete module/item manifests are not covered by this initial-entry snapshot/boat schema.
- Node/Boat travel is gated through CanDepartCurrentScene until crew departure/transition rules are implemented. Normal save/load service entry points also reject live sessions until participant-aware saves/client mirrors are ready; single-player saves remain unchanged when offline.
- This path starts a fresh game, not a host load/reconnect/offline fork. No scene asset, authored boat/player prefab, wave shader/material or world-generation algorithm/version was edited for it.

## Verification

Editor-symbol and player-symbol production compilation pass. A Unity 6000.0.65f1 standalone validation build includes the actual authored NodeScene and its referenced assets (851 assets, plus 27 dynamically loaded town dependencies). Production runtime source hashes match the isolated copies.

Three actual Windows processes pass normal NodeScene entry with zero runtime errors: host New Game, prepared world restoration before client Awake, unique dynamic actors and inactive legacy character, explicit owner camera/input, host-only body simulation, exactly one player boat per peer, host-selected boat identity, world height data matching the existing ushort save quantization, restored graph seed, walking in the normal scene, late join, disconnect, and Leave returning to MainMenu/single-player authority.

The first harness run omitted dynamically loaded TownCenter/SettlementTownNpc assets, compared the host's original floats exactly against its packed ushort save, and injected input after the network sampling phase. Those harness/dependency defects were corrected without changing authored scene assets or physics. Passing logs retain that history separately.

Inventory, shared interaction and movement native regressions pass. The sailing regression initially hit the previously recorded fixed-time displacement instability; its harness now waits for actual forward movement within the existing bounded timeout. Production boat physics was not changed for that check. The final host/client/observer sailing run passes with zero runtime errors.

Logs are under ignored Temp/CodexNetworking: `mp4-node-build-complete.log`, `mp4-node-complete-*.log`, `mp4-node-final-*.log`, `mp4-node-regression-items-*.log`, `mp4-node-regression-interaction-*.log`, `mp4-node-regression-movement-*.log`, `mp4-boat-complete-*.log`. Earlier `mp4-node-host/client.log` and `mp4-node-regression-boat-host.log` retain the initial failures. The harness and builds did not replace the user's open scene.

## Manual acceptance

1. Leave old sessions. Make a fresh matching build with MainMenu and NodeScene included; open the usual host/client pair in MainMenu.
2. Host/Join using the development panel, then have the host press the regular New Game button. Both peers should enter the authored NodeScene automatically.
3. Walk and jump. Check distinct actors, each camera following its own player, expected land/town/water/background visuals and one visible player boat. This is a scene/presentation check; ordinary interactions/travel/save are still gated as described above.
4. Join another process while the host is already in NodeScene. It should enter that same world and see the current crew/boat. Use the development panel's Leave button and confirm MainMenu returns; disconnecting the host should return remaining clients too.

Next: normal scene target registration and authenticated boarding/station/interaction paths, then additional MP.4 gameplay domains and MP.5 crew transitions/saves/lifecycle. Do not mark the full multiplayer handoff DONE.

## Acceptance follow-up — 2026-10-11

The user confirmed normal NodeScene entry works and authorized continuation. MP.4C3 connects deck boarding and authored pilot-chair occupancy; see MULTIPLAYER_4_BOARDING_STATIONS_CHECKPOINT.md. The deliberate remaining-scope list above records the boundary at C2, before that extension. Full MP.4 and the multiplayer handoff remain pending.

