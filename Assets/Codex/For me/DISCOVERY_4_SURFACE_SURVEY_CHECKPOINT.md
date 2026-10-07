# DISC.4 — Surface survey contracts checkpoint

Implemented 2026-10-06. Surveyor NPC playtest accepted before this pass. Its building and later deterministic building-relative NPC placement remain user authoring / FEATURE_TODOS scope.

## Implemented loop

Surveyor → Survey Work → inspect job/directions/sky references → accept → sail → deployed Telescope observation view → Survey → align/click three timed horizon circles → Correct/Incorrect Survey Reading → collect all three geographic readings in any order → return to an eligible Surveyor → receive processed physical chart → deliberately integrate at the Mapping Table.

Reading areas and the surface reveal swath are fixed before acceptance. Movement never paints coverage. Readings and turn-in never integrate coverage themselves. Rewards contain surface geography only; coastline inside the swath is included, without implicit node, POI or seafloor knowledge.

## Contracts and navigation

- Offers inspect current surface coverage/topography and require unknown water at each reading-area center. Known water and land centers are excluded. Nearby candidates have stronger weights; maximum reach bounds all three targets. Up to three separated offers are generated from a bounded deterministic grid scan. Masks are generated for a bounded candidate subset, with empty masks rejected. No generation runs per-frame or through passive travel updates.
- Default tuning on WorldMapKnowledgeSource: maximum reach 100 map units; preferred distance 25; reading separation 8; reading radius 3; swath radius 28; 64×64 candidate scan; three offers and three active jobs; eight-second observations. Invalid settings produce no jobs/readings. No Inspector edits were made.
- Navigation shows headings (north = 0°, clockwise) and approximate distances from the originating node, not live target bearings or coordinates. Each reading area has a north-up, shape-preserving sky card containing actual celestial star samples and distinctive landmark stars. The current gameplay celestial field and the sky renderer's existing projection settings must be present/matching.
- Cards are saved as point/color data and drawn procedurally. References crop the central half of the registered projection at 2× zoom, preserving north-up/east-right orientation and clipping glyphs to the card. New cards filter to that crop before the 64-star limit; existing saved cards receive the same presentation zoom. They grant no celestial evidence, automatic match, geographic coverage or position fix. This is separate from the POI clue-image / held reference-chart TODO.
- A second turn-in endpoint is selected only from already-known node markers within reach of the work area. Local jobs with no eligible known neighbor turn in at their origin. Named turn-in endpoints appear in the accepted-job list.
- Offers are transient host-generated previews. Acceptance resolves the host's cached offer by ID, rejects duplicate/stale/now-charted offers and copies a fixed assignment into shared state. Accepted jobs survive later chart integrations unchanged. Refresh offered work after a save/scene capture invalidates an old offer cache.

## Telescope action

The existing BoatObservationPresentationController has an additive partial UI; there is no new instrument, prefab or camera implementation. It appears while observing through the deployed Telescope whenever an unprocessed crew job exists. Select a job, optionally display its directions/sky card, and press Survey.

The contextual action is available anywhere for unfinished active jobs. Three transient horizon targets use mouse reticle alignment/click confirmation, with a host-clock minimum observation duration. Geographic validation occurs only after the third target. Feedback is exactly Correct Survey Reading or Incorrect Survey Reading, also posted to the existing message service. Incorrect readings reveal no direction, distance, target marker or coordinate. Repeating an already completed area cannot fabricate additional progress.

Jobs with all readings complete remain visible with instructions to return for processing. Survey is disabled for that completed job. E/Escape exits observation and cancels the transient attempt. Leaving valid telescope use, unpinning, blocked sky clearance, a disabled instrument, changed world registration or replaced save/book state invalidates an attempt; completed readings remain saved.

## Authority and rewards

Accepted progress is shared crew/world state. Host-only acceptance resolves cached offers; host-only observation tickets bind the exact requester, telescope, accepted contract and current book; the host supplies elapsed time and true navigation position. No UI-supplied position or completion list is trusted. Each target confirmation rechecks the observer and registered world. Client replication/request transport is still future multiplayer work; replica mutations are disabled.

Turn-in requires completed readings, a matching registered world and an eligible endpoint. The existing Surveyor menu additionally validates station range/node/boarding for each action. The reward goes to the exact requester's inventory as the existing physical cartographic chart item. Full inventory leaves it claimable. Reward state/reentrancy protection precedes inventory notifications; an observer exception after insertion cannot permit another payout. Repeat turn-in is rejected.

## Persistence

SurfaceSurveyBook is an additive WorldMapSaveSnapshot field. Existing EnsureDefaults creates an empty book for old saves. WorldMapSaveBuilder carries a detached copy through capture and scene transitions; existing SaveGameService already serializes/restores this snapshot. New game replaces the world snapshot and therefore starts with an empty book.

Contract identity, endpoints, zones, independent reading flags, celestial registration/card data, frozen chart payload and issued-reward flag persist. Host offer caches and in-progress telescope tickets are transient. No schema bump, separate save file or redundant map-reveal boolean was added.

## Fast playtest

1. Talk to your Surveyor, choose Survey Work, inspect a job, and accept it. Confirm the map does not reveal the job's swath.
2. For a short test in the editor, inspect the accepted job and enable DEBUG: survey target coordinates. Record each point's X/Y. This is an editor-only opt-in debug readout, absent from normal gameplay. Use the existing F4 geographic warp fields after embarking.
3. Before warping into a reading area, enter the deployed Telescope view, choose the job, press Survey, wait as indicated and click each circle with the reticle. Expect Incorrect, with no hints. No paper is consumed by these survey readings.
4. Warp to Point 3 first. Repeat the telescope action and expect Correct / 1 of 3. Exit observation and check another area: the previous reading stays complete. Point 1 and Point 2 can be done in either order.
5. Return/dock at the originating node (or the named eligible other endpoint), open Survey Work and process observations. Confirm one physical chart appears, no direct map reveal happens, and repeat processing is unavailable. Full inventory should let you retry after making room.
6. Integrate that chart at the Mapping Table. Expect its predefined surface swath to appear through the existing reveal fade. No new bathymetry or POI markers should appear.
7. Save/load from NodeScene partway through a job and verify completed reading flags and reference cards survive. Try Local Chart/Fix Position and ordinary Telescope observation for regression.

## Verification and files

Final result: **66 isolated Unity checks passed** (`Library/CodexQuayChecks/surface-survey.log`). The logged inventory-observer exception is deliberately injected by the payout regression test; the test confirms the committed reward cannot be issued twice.

Production runtime/editor assemblies compile. Isolated Unity checks exercise the actual generator, state, coverage masks, celestial projection, chart state and survey authority, with adapted navigation/scene/celestial-field/observer/inventory hosts. They cover deterministic unknown-water offers; surface-only rewards; duplicate/replica/stale acceptance; accepted-job stability; minimum timing/requester checks; incorrect feedback; independent readings; JSON/save-copy persistence; interrupted/replayed tickets; endpoint/full-inventory/reentrant/observer-failure reward paths; horizontal wrapping and finite latitude. Full scene UI/camera playtesting remains the steps above.

New files, with metadata: SurfaceSurveyState.cs, SurfaceSurveyContractGenerator.cs, SurfaceSurveyAuthority.cs, SurfaceSurveyUI.cs, WorldMapKnowledgeSource.SurfaceSurveys.cs, SurveyorCartridge.SurfaceSurveys.cs, BoatObservationPresentationController.SurfaceSurveys.cs; this checkpoint.

Existing classes inspected and modified: WorldMapSaveSnapshot (additive book/defaults), WorldMapSaveBuilder (detached book capture), CelestialSkyRenderer (read-only projection-settings accessor), SurveyorCartridge (partial work-menu hook), BoatObservationPresentationController (partial class and attempt cancellation hooks). Existing agent composition, save schema, scenes, materials, prefabs and Inspector settings are unchanged. Nothing committed.

## Remaining checkpoints

DISC.5: small unknown-region surface-only cleanup and broader shared-state/save/fix regression. DISC.6: sounding evidence processing into physical bathymetry charts. Charts for Sale remains an empty Surveyor seam; POI reference clue images/held viewing remain a separate TODO.

## Accepted playtest and reference tuning — 2026-10-06

Functional loop accepted by the user; broader UI polish deferred. Star references now use a 2× central crop matching the requested example. Default chart corridor radius increases from 7 to 13 map units: approximately 2.5× coverage area for the unchanged three-point path (reading separation 8). New offers receive the larger payload; accepted contracts and already issued charts preserve their frozen rewards. Refresh Survey Work for newly tuned offers. Reading positions, acceptance, progress, turn-in and explicit integration mechanics are unchanged. No scene/Inspector edits.

Validation: production runtime/editor compilation passes; 70 isolated Unity checks pass, including crop center/orientation/bounds and a measured 2.508× default surface-mask area. Existing acceptance, reading, persistence, reward and authority checks still pass. Visual playtest remains with the user.

## Larger reward and survey center reference — 2026-10-06

User requested more coverage again after seeing the integrated chart. New offers now default to radius 28 map units (previous tuning: 13); reading targets and acceptance radius remain unchanged. Accepted jobs and existing charts retain their saved payloads; refresh offers to test the enlarged reward.

Reference cards now draw a cyan open cross at their exact center. Starting a Telescope Survey draws the same glyph at the current observer's sky projection origin. The scene marker uses CelestialSkyRenderer's own SkyViewportToWorld observation-frame helper and authored SkyViewportCenterY, followed by the viewing camera's WorldToScreenPoint: it moves with stars under telescope pan/zoom rather than sticking to screen center. No target geography or live bearing is used to move the marker. At the correct reading position, the target star pattern and live pattern have the same origin and north-up/east-right orientation (their display scales can differ). The marker is shown only during an active survey and clipped out when its sky origin is outside the camera viewport. No scene/Inspector changes.

Validation: runtime/editor compilation passes; 73 isolated Unity checks pass. New checks verify matching chart/scene projection origins at the correct position and pattern displacement at a wrong position. The sample corridor measures 3.54× the previous radius-13 reward area. Visual pan/zoom and chart playtest remain with the user.

2026-10-07: User accepted the larger reveal and matching sky/reference center marks. DISC.4 is accepted. Next checkpoint is DISC.5 tiny unknown-region cleanup and shared-authority/persistence regression.
