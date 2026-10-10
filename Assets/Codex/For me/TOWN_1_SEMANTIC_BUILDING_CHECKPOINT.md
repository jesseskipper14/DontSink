# TOWN.1 — Town Center semantic building

Implemented 2026-10-09. Ready for layout playtest; TOWN.2–TOWN.7 remain pending.

## Exact-current audit

- `NodeSettlementPlanner` generates a deterministic permanent manifest using a stable string hash and System.Random. `NodeSettlementManifest` stores terraces, connections, plots, sockets and development/damage history. Existing `Ensure` migrates versions 1/2 to the version-3 visual layout without replacing identities.
- The existing ground-level `EventReserve` is the civic placement opportunity. It has no stacked child and lies on the permanent ground street. Converting that plot avoids adding a crowded new building or reflowing the accepted town spacing.
- `NodeSettlementScene` reconstructs local presentation from the manifest and visit snapshot under NodeContext / NodeView / NodeTown. `SettlementSemanticAnchor`, `TryGetAnchor`, and `TryGetSocket` supply existing service-binding seams. Nature/prop safety already reserves fronts of non-residential buildings, so the civic entrance and future service sockets stay open.
- `AgentController` composes `AgentDefinition` with brain, movement and interaction definitions. `AgentSpawnPoint` supplies authored placement, stable ID, node assignment and home bounds; `AgentRegistry` indexes current instances by stable ID. `NpcBase` is used for ambient town actors; `SurveyorNpc` extends the same agent architecture with SurveyorServiceDefinition / SurveyorAgentServiceHandler and the existing overlay.
- The passive town brain itself has no movement behavior; wandering/stationary behavior comes from the composed movement definition. The leader must reuse that composition in TOWN.2 rather than adding an independent AI loop.
- Stable runtime NPC IDs exist, but no general persistent political/leader-state subsystem currently exists. Survey jobs live in their own authoritative save model. TOWN.2 must bind the semantic leader identity to node state; later civic quest/intelligence state belongs to the node, not transient scene actors.
- WorldMapSaveBuilder / WorldMapSaveRestorer already copy the settlement manifest. Local node values are live MapNodeState: population is an absolute value, other ratings generally use 0–4 and FoodBalance uses -4–4. Later status boards must display those actual values rather than inventing 0–100 statistics from the handoff examples.

## Changes

1. Append `SettlementRole.TownCenter` (existing enum values preserved) and advance manifest to version 4.
2. Authority upgrades a validated version-3 manifest by changing its existing ground-level EventReserve role/family. All plot/socket IDs, coordinates, dimensions and histories are retained. No layout reflow or new RNG calls. No safe reserve means an explicit diagnostic, never regeneration.
3. Validate exactly one civic center for version 4, ground-level placement, no stacked child, existing footprint/terrace access rules. New generation performs the same civic assignment after the existing layout step.
4. Include civic plots in the existing required-service development path, so a poor or empty settlement does not omit its civic building merely because population or prosperity is low. Existing damage rules remain unchanged.
5. Add `Resources/Prefabs/Node/TownCenter.prefab`: a small grey/blue placeholder civic facade with an entrance and nameplate, authored `LeaderSocket`, and authored `StatusBoardSocket` with a blank exterior board. Shared simple sprite art is in `TownCenterPlaceholder.png` beside it.
6. `TownCenterBuilding` validates the socket contract and exposes stable building/node/plot association. Future leader ID is `<NodeStableId>/civic/node_leader`, independent of position or display title.
7. Settlement reconstruction instantiates the prefab and publishes `<PlotId>/leader` and `<PlotId>/status_board` through its existing socket lookup. Old ambient socket identities remain addressable but do not populate actors/props in front of civic service areas.

The building is populated by future building/service composition, not random world-coordinate NPC placement. No leader spawned in TOWN.1; no quest/intel/menu/stat-board functionality yet. No scene edits, no Surveyor replacement, no map reveal or navigation changes, no commit.

## Validation

Runtime and production editor compilation pass (two existing unused-field warnings in WorldMapTravelDebugController). All 490 isolated Unity assertions pass: 160 deterministic layouts across eight archetypes; one civic center and reachable valid geometry; authored socket validity and movement with the building; stable future leader ID; version-3 migration identity/position/socket retention, idempotence and saved JSON roundtrip; absent-reserve refusal without mutation. Results: ignored Library/CodexThrowableChecks/town-center-results.txt.

Prefab Unity GUIDs and source-script reference are retained when exporting into Resources. The placeholder uses the existing URP Sprite-Lit-Default material. Existing scene authoring and other concurrent edits were preserved.

## Playtest

Load/re-enter NodeScene. Look for the blue-grey **TOWN CENTER** on the ground street in the former event-reserve space, generally between the residential side and the generated Market/Surveyor services. Check scale, approach, clear entrance and spacing. The blank board marks the future status-board location; the leader socket intentionally has no actor yet. You can author the prefab facade/sockets directly and their positions follow the building.

The handoff explicitly says **STOP FOR PLAYTEST** after TOWN.1. After acceptance, TOWN.2 adds the persistent direct-interaction leader through the existing AgentDefinition / behavior / AgentInteractable / service-handler architecture. TOWN.3 then populates the world-space board; later checkpoints add local information, civic work, cached neighboring intelligence and the trade refresh seam.
