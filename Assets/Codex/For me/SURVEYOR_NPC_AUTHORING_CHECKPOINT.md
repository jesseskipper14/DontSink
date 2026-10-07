# Surveyor NPC authoring checkpoint — 2026-10-06

## Authoring

Place `Assets/Resources/Prefabs/Agents/SurveyorNpc.prefab` in NodeScene under your station/kiosk, optionally within NodeContext/NodeView/NodeTown. Place one. No automatic settlement Surveyor or scene edit was added.

This is an NpcBase prefab variant, inheriting the body/collider, kinematic Rigidbody2D, GroundSnapper, interaction collider and AgentInteractable. The shirt is blue. AgentController already has SurveyorNpc assigned. Author the station and appearance as with other NPCs. The prefab root is the body center, not its feet: place its feet on your chosen surface.

For the shared NodeScene, leave AgentController Node Id blank; the menu resolves the current canonical node when opened. A generator can instead initialize it with the explicit node ID. Assign a unique authored stable ID if placing directly. Alternatively use an existing AgentSpawnPoint with SurveyorNpc AgentDefinition, stable ID and ground snapping enabled. Parent that spawn anchor to the station. Its prefab reference supplies the variant automatically. Do not place an NPC and a spawn point for the same station.

## Agent architecture reviewed

- AgentDefinition supplies identity/faction/prefab/behavior/services. AgentBehaviorSetDefinition composes independent brain, movement and interaction runtimes.
- AgentController manages initialization, simulation LOD/ticking, service execution and runtime identity snapshots. AgentRegistry uses stable IDs. Save-safe spawned identity comes from AgentSpawnPoint; temporary instance IDs are unsuitable for persistent identities.
- PassiveTownNpcBrain is intentionally empty. Movement and interaction do not come from that brain. SurveyorBehavior uses the existing passive brain, Stationary movement and ServiceInteraction (default service index 0). Existing home-bound wandering can replace Stationary later.
- AgentInteractable supplies the ordinary prompt/label/ranges and boarding-context filtering. ServiceInteraction calls AgentController, which dispatches through AgentServiceDefinition and AgentServiceDispatcher. SurveyorAgentServiceHandler is on this NPC and rejects another NPC's dispatch context.
- The menu uses MiniGameOverlayHost for input blocking, Escape, cancellation and world interruptions, like existing service overlays.
- AgentSpawnPoint, AgentSpawnPointGroupDefinition and AgentSceneSpawner provide the authored-anchor/grouped/authority-gated spawn paths. GroundSnapper handles actual support including composite quays. No second movement, brain or spawning implementation was added.
- Creature brains, composite movement modules, schools/threats and diagnostics are separate from the town service composition and remain unchanged.

## Functional services

E opens Local Chart, Fix Position, Survey Work and Charts for Sale. Local Chart uses the existing physical issuance/reissue pipeline, not direct coverage grants. Fix Position is explicit and only corrects the shared believed-position marker. Starting-island charts may correctly report already integrated.

Live NPC/requester, scene, node, distance and unboarded state are checked before opening and each transaction. Leaving range, disabling the NPC or changing node invalidates the menu. The node is captured once at opening. Both mutations use existing host-only authority and source-receipt guards. Replica transactions are disabled; actual networking transport remains future work.

Survey Work and Charts for Sale show honest empty states. No contracts or purchases are invented for this checkpoint. Chart items, receipts and marker state persist in their existing systems; the menu itself is transient.

## Surface survey work: ready versus missing

Ready foundations: shared coverage/topography, canonical navigation/world registration, physical chart rewards/integration/consumption and celestial data. We can implement distance-weighted unknown-water candidates, predefined swaths/reading zones, navigation instructions, persistent accepted contracts/independent readings, eligible endpoint turn-in and processed chart rewards now.

Still to build in that next pass: the authoritative contract generator/lifecycle, additive save data, and a timed contextual Telescope Survey action with Correct/Incorrect feedback. The current telescope provides sky observation, not surface-contract readings. Existing celestial sequence tracking belongs to star charting and should not be reused as surface-contract completion state.

Star reference data can use the existing celestial system. Generated POI clue images and held reference viewing remain the separate FEATURE_TODOS entry. They do not block mechanical contracts, but reference-card visuals will remain provisional until implemented. No full dialogue/quest framework, settlement rewrite or new world generator is required.

## Changes

Modified: AgentTypes.cs (additive Surveyor enum value 50); INCOMING_HANDOFF_STATUS.md.

New, with metadata: SurveyorServiceDefinition.cs, SurveyorAgentServiceHandler.cs, SurveyorCartridge.cs; Resources/SurveyorService.asset, SurveyorBehavior.asset, SurveyorNpc.asset; Resources/Prefabs/Agents/SurveyorNpc.prefab; this checkpoint.

Existing scenes, prefabs, Inspector settings and behavior assets are unchanged. Nothing committed.

## Verification

Result: **23 isolated Unity checks passed**, logged at `Library/CodexQuayChecks/surveyor-npc.log`.

Production runtime and editor assemblies compile successfully. Isolated Unity checks use the actual NPC/definition/behavior/service/dispatcher/ground/spawn/cartridge scripts and authored assets, with adapted knowledge/navigation/boarding/overlay hosts. The checks cover prefab import, behavior composition, ordinary service dispatch, spawn-anchor prefab resolution/idempotence, distance/boarding/node/disabled-NPC invalidation and menu lifecycle. Full NodeScene placement and presentation still require the playtest below. Existing chart service tests remain in DISC.3; this checkpoint does not repeat or replace that verification.

## Playtest

1. Place the variant by your station, approach unboarded and use E.
2. At an unintegrated node, issue a local chart, check duplicate/reissue behavior and integrate it at the Mapping Table.
3. Move the believed marker, then choose Fix Position. Verify marker correction without boat movement or coverage grants.
4. Close/reopen, leave range, try from aboard the boat, and verify existing vendors still work.
5. Confirm future service buttons display empty states.
