# Legacy navigation/cartography cleanup — CLEAN.1 audit

2026-10-04 — Bosun 🍌. Audit checkpoint only: no gameplay changes, deletions, save migrations or Inspector edits. Newer cleanup/discovery designs supersede obsolete route-permission requirements. Findings are source evidence, not full scene reproductions.

## Main findings

1. Embark still requires player.lockedDestinationNodeId. NodeTravelController.TryStartTravel rejects a missing destination and, unless debug-bypassed, requires generator.graph.HasEdge. It can still run route restrictions/outcome rolls.
2. Removing those checks alone is unsafe: SceneTransitionController derives map scale from routeLength/nominalLocalTravelDistance; BoatSceneWorldPositionBridge retains endpoint-based projection fallback. Free departure needs an explicit stable distance scale and harbor WaterwardDirection, not a fake destination.
3. Physical harbor docking already works by actual proximity/requester validation. Legacy CompleteTravelToDestination/AbortTravelToSource and source/target dock modes remain reachable through debug/compatibility entry points.
4. Geographic knowledge is still granted around current nodes by WorldMapKnowledgeSource, which also exposes destination/corridor reveal. This conflicts with the new chart-integration design beyond the expressly retained temporary starting-island grant.
5. Graph edges are live economy data: PressureDiffusionSystem.Tick iterates graph.edges, weights intra/inter-cluster conductivity and estimates graph degree. Deleting the graph would damage simulation.

## KEEP / MIGRATE / REMOVE / DEFER inventory

| Current systems | Classification | Action/checkpoint |
| --- | --- | --- |
| WorldNavigationService, WorldTopologyService, harbor definition/berth/corridor queries | KEEP | Physical truth, wrapping and valid harbor departure remain. Never derive truth from a camera or believed marker. |
| NodeTravelController.TryStartTravel; SceneTransitionController.StartTravelToBoatScene; TravelPayload departure fields; bridge scale/projection | MIGRATE | CLEAN.2: destination-free Embark with explicit scale and valid waterward departure; preserve boat identity, captures, boarding/departure and authority/loading gates. |
| TravelRequest/TravelResult, TravelSystem, SimpleTravelResolver, RestrictionGateResolver and debug overrides | REMOVE player-travel role / inspect residual consumers | Do not delete shared types until all adapters and debug consumers are migrated. Random travel success no longer controls physical sailing. |
| MaxRouteLengthRestriction | REMOVE compatibility shell later | Already always permits movement; config/UI/debug consumers still reference the old value. Do not claim the live maximum-distance gate remains active. |
| RouteUnlockRestriction, ClusterUnlockRestriction, RouteAccessPolicy | REMOVE movement-permission role | Star/cluster knowledge must not authorize sailing. Preserve useful regional metadata. |
| BoatSceneController old source/target dock paths; SceneTransitionController CompleteTravelToDestination/AbortTravelToSource | MIGRATE/REMOVE | CLEAN.3: keep explicit actual-harbor docking; isolate any genuinely useful debug teleport/dock action from normal arrival. |
| HarborTravelService, HarborDockInteraction, BoatHarborPresentation and piloting harbor observation | KEEP | Already proximity-based; no destination dependency required for observation. |
| WorldMapCartridge, MapOverlayController, WorldMapHoverController, selection/click adapters | MIGRATE | CLEAN.4: destination locking/launch, graph-route/range/locked presentation must leave gameplay UI. Preserve explicit truth-only debug tools and manual map interaction. |
| WorldMapKnowledgeSource current-node/destination/corridor reveal; geographic discovery state/UI | MIGRATE | CLEAN.5 coordinated with discovery handoff: remove passive geographic grants; preserve known coverage and explicit cartographic sources. Keep temporary starting-island reveal until its replacement exists. |
| BoatPilotingSimulation route prototype/BoatPilotingRouteState and remaining route quality/progress consumers | REMOVE route-solver role | CLEAN.6: audit every consumer; retain physical controls, distance measurement, compass and viewscape. Do not turn manual navigation legs into validated true routes. |
| StarMapActions, StarMapDebugController, StarObservationWorldMapRunner writes to unlockedRoutes | MIGRATE | CLEAN.7: remove movement permission writes, retain celestial observations/knowledge/naming/fragment workflows. Distinguish celestial knowledge states from geographic state removal. |
| WorldMapPlayerState currentNodeId/lockedDestinationNodeId/unlockedRoutes/unlockedClusters; activeTravel; WorldMap save snapshots/builders/restorers; SaveCompatibilityDiagnostics | MIGRATE | CLEAN.8: version old fields, retain docked identity and meaningful physical voyage context, stop obsolete writes. Never manufacture a route, reveal coverage or correct believed position from migration. |
| MapGraph edges, ClusterId, graph save restoration and PressureDiffusionSystem | KEEP simulation topology | CLEAN.9: economy needs adjacency/cluster/degree. Remove player permission meaning, not regional economic connectivity. |
| New Surveyor, physical believed marker, cartography workbench, survey contracts, settlement generation | DEFER implementation | Follow their own handoffs. Cleanup preserves seams; it does not implement missing features or invent state merely to pass acceptance checks. |

## Source coverage

Read/inspected core methods: NodeTravelController, SceneTransitionController, BoatSceneController, BoatSceneWorldPositionBridge, HarborTravelService, HarborDockInteraction, RouteAccessPolicy, RouteUnlockRestriction, MaxRouteLengthRestriction, PressureDiffusionSystem. Harbor closure additionally read NodeGroundGenerator2D, BoatSpawner and PlayerSceneContextRestorer.

Searched concrete consumers/fields in WorldMapTravelDebugController, TravelSystem, SimpleTravelResolver, RestrictionGateResolver, ClusterUnlockRestriction, WorldMapCartridge, MapOverlayController, WorldMapHoverController, StarMapActions, StarMapDebugController, StarObservationWorldMapRunner, WorldMapPlayerState, WorldMapKnowledgeSource, WorldMapSaveBuilder, WorldMapSaveRestorer, WorldMapSaveSnapshot, SaveGameService, SaveGameFile and SaveCompatibilityDiagnostics. These searches identify follow-up readers; they are not a claim that every branch of those large classes was reviewed.

Event/quest search did not establish a direct graph-edge consumer in the inspected event paths. Do not infer zero dependencies: per-node events/resolvers and future contract candidate generation need a targeted consumer check before final graph/API removal. PressureMarketPolicy and its trade/pressure dependencies need regression coverage alongside PressureDiffusionSystem; direct edge iteration is verified in the latter.

## First implementation checkpoint: CLEAN.2

- Introduce destination-free Embark while preserving lifecycle/authority, boat readiness, boarding and capture safety.
- Define one explicit map-to-physical conversion independent of selected node distance. Current endpoint-derived scales vary by destination; silently selecting a new default would change speed, range and berth scaling.
- Preserve HarborTravelSettings safe corridor/boat-scaled anchor and departure resets.
- Keep enough voyage context for existing spawning/navigation/saves without pretending it is a selected graph edge.
- Retain old-save compatibility until CLEAN.8 migration; tests must cover legacy payload projection fallback.
- Tests: no destination/no edge/cross-cluster Embark; negative authority; missing boat/harbor; departure reset; physical movement and wrapping; matching geographic scale; save/load and two harbor dock/embark cycles.

The incoming document calls for CLEAN.1 review before deletions and a playtest/commit between behavioral checkpoints. This audit supplies the concrete review artifact. No cleanup implementation began, no files were deleted, no route UI removed, no fields migrated, no packages added. The existing harbor regression suite passed; it does not test destination-free Embark, which is not implemented yet.
