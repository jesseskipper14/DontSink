# Persistence bug backlog

Keep reports focused: trigger, evidence, likely effect, proposed pass, and verification. Code inspection is not a live reproduction. Resolved entries remain as history.

## Follow-up: fixed-height UI panels can overflow small game windows

- Reported: 2026-10-03. Map-table screenshot shows layer controls and node/travel controls drawing below their panel and window bounds. Other UIs may have the same pattern; they have not been audited in this pass.
- World Map fix: both sidebars now clip and scroll independently, calculate content height including expanded debug controls, and give topography toggles non-overlapping spacing. Applies to standalone World Map and the map table's World Map page. Production compilation passes; visual/input checks remain in Play Mode.
- Proposed contained follow-up: check Star Chart and other cartridge/module panels at the user's smaller Game view size; use bounded scrolling where content exceeds available height. Preserve central map/page registration.
- Verify: reach bottom controls with the wheel/scrollbar, open debug sections/dropdowns, switch nodes with differing buff/event counts, and confirm no clicks leak to UI underneath.
- Follow-up implemented: shared cartridge overlay now scrolls a minimum 1040 × 800 layout on small windows; Winch and Star Chart details have their own vertical scrolling. Production compilation passes. Play Mode input/visual checks remain, especially nested list scrolling and chart drag interactions. See `Assets/Codex/For me/CARTRIDGE_SMALL_WINDOW_LAYOUT.md`. Separate observation runner and other non-cartridge UIs have not been redesigned.

## Investigate: Unity Inspector null targets during assembly reload

- Reported: 2026-10-03. Actual Editor.log places GameObjectInspector.OnDisable NullReferenceException and MeshRenderer/GameObject/Transform Inspector SerializedObjectNotCreatableException between Begin MonoManager ReloadAssembly and the end of domain reload.
- Evidence: those exception stacks contain UnityEditor Inspector code, with no project script frame identifying a gameplay failure. A stale/destroyed Inspector target is a plausible cause, not a confirmed identification of the object.
- Recovery to try: unlock any Inspector displaying transient runtime objects, select a persistent scene object or asset, clear Console, then repeat Play Mode. If necessary close/reopen the affected Inspector tab. No automatic selection/layout manipulation or exception suppression was added.
- Verify: whether the errors recur with an unlocked Inspector targeting a stable object; collect fresh stacks and triggering action if they do. Avoid treating these editor exceptions as evidence that terrain or map gameplay failed.

## Open: water-bottom binders reference generators rather than fill-bottom providers

- Found: dynamic-terrain Phase 0 audit, 2026-10-03; serialized references and interface casts verified, no runtime reproduction.
- Evidence: BoatScene NodeWaterBottomBinder.groundSource points to BoatSeaFloorGenerator2D (fileID 1516219392); NodeScene points to NodeGroundGenerator2D (fileID 1821200605). Neither implements IGroundFillBottomSource. NodeWaterBottomBinder casts the assigned component to that interface and cannot apply its bottom-depth updates when the cast fails.
- Risk: rendered water bottom does not follow generated fill depth through this binder; separately serialized water depths can mask the issue.
- Proposed contained fix: reviewed NodeScene Inspector reassignment to the actual fill-bottom provider; BoatScene's upcoming streaming pass should instead expose aggregate loaded-chunk bottom depth. No references were changed during the audit.
- Verify: regenerate into deeper terrain and check intended foreground/background water coverage and no unwanted shallowing on chunk unload. Details: `Assets/Codex/For me/DYNAMIC_TERRAIN_PHASE0_AUDIT.md`.

## Follow-up: multiplayer authority and transaction hardening

- Found: 2026-10-02, analysis-only MP audit after wrapping. No multiplayer implementation was made.
- Full findings and proposed contained passes: `Assets/Codex/For me/MULTIPLAYER_INFRASTRUCTURE_AUDIT.md`.
- Highest priorities: Modules/Boat Power ticks and commands lack consistent host gates; module/storage UI loses or ignores requester context; inventory and station claims need host transactions; market item/offer changes precede final payment with no rollback on failure.
- Existing local risk: `TradeEffectApplier.TryApply` can return failure after items/offers/embargo state have changed if final treasury application fails, or after earlier lines changed when a later removal fails. Reproduce with a controlled failing store/treasury fixture before choosing the transaction implementation.
- Proposed pass: Modules/Boat Power first, then identity/bootstrap and a minimal two-instance integration, followed by shared transactions and lifecycle. Keep the complete evidence in the audit instead of duplicating every finding here.
- Verify: authority-negative tests, stale/repeated/concurrent request cases, two actors with distinct inventories, two-instance shared state, and disconnect/scene/save lifecycle cases specified in the audit.

## Follow-up: wrapped legacy-world bounds and live seam checks

- Found: wrapping pass, 2026-10-02; compatibility limits and live checks, not a reproduced regression.
- Evidence: wrapping needs valid bounds from published generation, saved graph/topography metadata, or runtime topography. A graph-only legacy world lacking every bounds source cannot establish a real circumference. Old saved height payloads are preserved and can retain their original terrain discontinuity.
- Proposed contained pass: inspect real legacy saves before deciding whether a bounds migration or explicit unsupported-save message is needed. Do not invent a circumference or regenerate saved geography to hide the issue.
- Verify: `Assets/Codex/For me/WRAPPED_WORLD_CHECKPOINT.md` morning checklist, including ordinary and old saves, map/chart selection, multi-monitor panning, physical sailing, and telescope surveys at both longitude edges. Physical polar blockers/local terrain streaming remain later terrain/content work.

## Follow-up: remaining inventory/build UI ownership before multiplayer

- Found: camera ownership pass, 2026-10-02; source inspection, not a multiplayer reproduction.
- Trigger: several player inventories exist on one client, or a replicated remote interaction reaches a legacy UI/cartridge entry point.
- Evidence: `ExternalContainerOverlayUI.Awake` still selects the first PlayerInventory and caches Camera.main; `HardpointSupportHoverPreview` still obtains a global inventory/camera; legacy ModuleCartridge/WorkbenchCartridge and other UI adapters contain global player searches. Camera-pass piloting/map-table/charting entry guards are local-requester scoped, but they are not a replacement for a complete inventory/UI ownership pass.
- Risk: wrong-player inventory/build previews, stale camera projection, or remote-origin interaction reaching shared local UI. No transport exists yet, so this is an integration requirement rather than a demonstrated current network bug.
- Proposed contained pass: resolve UI owners from authenticated requesters/local presentation identity, bind inventory/equipment explicitly, and scope open/close/release callbacks to the session owner. Preserve today's one-local-human-per-client UI and avoid inventing split-screen.
- Verify: two actor/inventory fixtures; remote open/close cannot change local panels or consume local items; local scene transitions and camera changes preserve the correct inventory/projection.

## Follow-up: authoritative simulation LOD relevance for multiple actors

- Found: camera ownership pass, 2026-10-02; source inspection.
- Evidence: camera-position fallback and arbitrary first-player selection were removed from `SimulationLodTargetResolver`. Its single-actor default deliberately returns null when several actors exist; FishSchoolSimulationLod also caches its assigned/default target.
- Risk: no distance updates when a multi-actor host has no explicit target, or relevance remains tied to a cached actor after another player joins. A free spectator camera must never become a simulation target.
- Proposed contained pass: host-owned relevant-actor collection/relevance policy, coordinated with dynamic terrain/resource work. Keep simulation authority separate from whichever actor the local camera views.
- Verify: two actors widely separated, join/despawn/reconnect, spectator roaming, and distant actor activity. Fish/terrain relevance should follow authoritative actor positions.

## Follow-up: cache telescope hide-layer renderer discovery

- Found: camera audit, 2026-10-02; pre-existing code, not a measured performance regression.
- Evidence: `BoatObservationPresentationController` enumerates scene actors/renderers during render suppression for configured hidden layers. Camera ownership changes add no per-frame scene searches, but this existing render-time discovery may grow expensive as boats/NPC populations increase.
- Proposed contained pass: cache relevant renderer membership and refresh on scene/spawn/ownership changes, retaining per-camera suppression/restoration and the current telescope fade.
- Verify: profile telescope rendering in a large scene, then test renderer spawn/despawn, scene transitions, sorting-layer changes, and restoration after interrupted observation.

## Implemented: pinned charting/telescope instruments destabilize boat physics

- Reported: 2026-10-02. User observed oscillation immediately on load, followed by the boat flipping onto land; unpinning both instruments stopped the behavior.
- Evidence: both instruments reuse `PlaceableBoatEquipment.RestoreDeployment`, which creates a FixedJoint2D connected to the boat Rigidbody with `enableCollision = true` and infinite break force/torque. This pinning code was not changed in the world-navigation debug checkpoint.
- User confirmed overlap: raising the pinned instruments' colliders by 0.1 units stopped the behavior.
- Change: shared deployment checks actual solid collider distances before joint creation; searches upward along boat-up in 0.01-unit steps for 0.02-unit clearance, capped at 0.25-unit lift. It restores the original pose and creates no joint if blocked. The support probe covers buffered poses. Connected boat-body collision is disabled while the joint exists; other collision relationships are retained. Manual pinning and persisted deployment restoration use the same path.
- Automated verification: full production runtime compilation and 49 isolated Unity assertions pass, using the production deployment source and actual Unity 2D physics with adapted boat/item hosts. Checks include compound colliders, two instruments pinned together, a 25-degree boat rotation, 150 solver steps per orientation without boat translation/rotation, idempotent restore, unpin/redeploy, blocked correction with pose rollback, unrelated world obstruction, trigger-only equipment, and client rejection.
- Awaiting live verification on the actual prefabs/save. Inspector and prefab data were not changed. See `Assets/Codex/For me/PINNED_INSTRUMENT_CLEARANCE.md`.
- Follow-up regressions corrected: player-adjacent deployment was blocked because the new buffer queried all layers without honoring authored obstruction exclusions; pin collision suppression also stopped containment-trigger contacts, causing five-second ownership expiry. External buffer obstructions now respect the instrument's existing sky-obstruction mask, structural surfaces still get clearance, and the escape tracker recognizes a valid live pin as physical containment with retained mass contribution. Unpin/disabled joints return to ordinary escape tracking. Expanded validation passes 61 assertions with the production ownership, registry, zone, and tracker code; live ten-second pin/save-load checks remain required.
- Verify: pin each instrument separately, then both; sail/rotate the boat and save/load deployed instruments. Compare joint anchors, collider contacts, and boat motion with instruments unpinned. Confirm unpin/pickup/redeployment and bell/anchor behavior remain intact.

## Open: old world-map snapshot survives New Game

- Found: 2026-10-02; confirmed reset/restore paths by inspection, not reproduced in play mode.
- Trigger: load a save, return to the menu, start New Game.
- Evidence: `Assets/Scripts/Menu/MainMenuController.cs` replaces `gs.worldMap` but leaves `gs.worldMapSnapshot`. `Assets/Scripts/WorldMap/Runtime/WorldMapRuntimeBinder.cs:Rebuild` restores saved node runtime state when the fresh store is empty and the snapshot is usable. `Assets/Scripts/WorldMap/Data/WorldMapSaveRestorer.cs` also restores graph identity from the snapshot.
- Risk: previous node/world state can reappear in a fresh game.
- Proposed contained pass: reset the persisted world-map snapshot; inspect matching topography/cache and world-navigation initialization before selecting the complete reset scope.
- Verify: alter node state in a saved game, load it, return to menu, start New Game, inspect fresh node state and world identity; confirm ordinary save loading still restores the original world.

## Open: old starting dock can survive New Game

- Found: 2026-10-02; conditional behavior confirmed by inspection, not reproduced in play mode.
- Trigger: previous player has a valid current node and `MainMenuController.defaultStartingNodeId` is blank.
- Evidence: the menu reuses `gs.player` and only replaces its current node when an explicit starting ID exists. `WorldMapRuntimeBinder.EnsurePlayerHasCurrentNode` preserves any ID that still resolves.
- Risk: fresh game starts at the last dock rather than StartDock.
- Proposed contained pass: initialize a fresh player state or explicitly clear the current node when no starting override exists. Coordinate with the world-map reset above.
- Verify: save at a non-start dock, return to menu, start New Game with a blank starting override; check StartDock. Also test an explicit starting override and normal save loading.

## Implemented: keyed inventory/context survives New Game

- Fixed: 2026-10-02; awaiting live menu/scene verification.
- Cause: only legacy mirrors were cleared; `GameState.EnsurePlayerPersistenceDefaults` restored the old keyed record into those mirrors.
- Change: New Game rebuilds keyed records through `SetPlayerPersistenceStates(null)` after clearing the legacy inventory/context mirrors. The existing `clearPlayerSceneContextOnNewGame` option remains respected; all previous keyed player records are discarded.
- Verify: load a save with equipment/context, return to menu, start New Game; check fresh inventory and context. Check normal loading restores the original equipment. If context clearing is deliberately disabled, verify that preference is preserved for the local player.

## Implemented: treasury records survive New Game

- Fixed: 2026-10-02; awaiting live menu/scene verification.
- Cause: treasury snapshot and live chest registrations survived in the persistent GameState service.
- Change: `MoneyChestTreasuryService.ResetForNewGame` detaches old chest change callbacks, clears registrations, and replaces treasury state. The menu resets snapshot state directly if no service exists.
- Verify: save money in an active chest, return to menu, start New Game; check no previous active chest/balance/lost records survive. Check the new starter chest registers normally and loading the old slot still restores its treasury.

## Implemented: clean New Game never offers its first money chest

- Fixed: 2026-10-02; awaiting live slot-spawn verification.
- Cause: `MoneyChestReplacementChestSpawner.SpawnWhenReady` stops when `ShouldOfferReplacementChest` is false. That query required lost-chest history, which a correctly reset new-game treasury does not have. NodeScene's replacement prefab/item references are assigned, so the clean-state gate rejects the spawn before slot lookup.
- Change: the treasury query now accepts an empty treasury with no active chest, in addition to the existing lost-chest replacement case. Existing active-chest duplicate prevention remains intact. The existing slot-first spawn/bootstrap pipeline is reused.
- Verification: full runtime compile passes; isolated Unity checks pass 6,467 assertions, including six new eligibility cases (empty, active, lost, lost history plus active, retired-only, missing GameState). Exact production treasury query/accessor methods were used with the test host. Live spawning/physics/slot placement remains untested.
- Verify: start a fresh game and confirm one pickupable chest in the secure slot; revisit/reload with an existing active chest and confirm no duplicate; lose a chest and confirm the replacement path still works.

### Reset verification

- Full runtime C# compilation passes.
- Isolated Unity harness: 6,461 assertions passed, including ten new inventory/treasury reset checks.
- The harness uses the extracted menu reset statements, treasury reset method, and GameState player persistence logic with adapted singleton/chest hosts (diagnostic logging context also adapted). Checks cover stale keyed inventory/context, removal of other crew records, clean local record creation, treasury snapshot/registration clearing, old chest callback detachment, context-retention preference, and absent-service fallback.
- Actual menu transitions, starter chest registration, and loading existing slots remain play-mode checks. Test artifacts are ignored under `Temp/CodexPhase7`.

## Open: world-map sandy shoreline does not visually match land/water truth

- Reported: 2026-10-03 during geographic land encounter testing.
- Observation: the F4 land/water boundary appears about halfway through the sandy shoreline band, rather than at the blue water edge on the map. Not yet independently reproduced or diagnosed.
- Follow-up: compare topography effective sea level and bilinear sampling with world-map class/color thresholds and texture filtering. Align the visual water boundary with gameplay truth while preserving intentional beach styling.
- Verify: sample/warp along several coastlines, including the wrapping seam; compare F4 surface classification and map coloration. Keep this separate from island presentation and collision implementation.

## Open: node spacing makes voyages excessively long

- Reported: 2026-10-03 during maximum-speed ocean testing. World size feels acceptable, but node separation feels extreme.
- Follow-up: measure nearest-neighbor/route distances against actual boat speed and current map-to-physical scale; tune node density/placement separately from world bounds. Do not silently rescale the world or regenerate existing saves.
- Verify: compare a few short, medium, and long port-to-port trips in real play time, including wrapping seam routes, before choosing new generation settings.

## Queued: ocean testing bug-fix pass (2026-10-03)

These are user-reported observations, not independently reproduced. Defer fixes until the current terrain/land feature handoff is complete. Preserve authored Inspector settings during diagnosis.

- **Regression — inventory world drop in BoatScene:** InventoryDragDropController no longer lets items be dragged out of inventory and dropped into BoatScene. Check scene drop references, camera/raycast masks, UI gating, and recent runtime visual layers; compare NodeScene. User suspects an Inspector miss.
- **High priority — hatches cannot close while moving:** inspect obstruction/nearest-collider queries against moving real/ghost geometry and physics/interpolation timing. Verify stationary versus cruising, including player/cargo near the hatch.
- **High priority — dropped objects lack carrier velocity:** at boat speed, dropped items appear to fall backward. Audit inventory drops, held-item releases, spawns and other release paths for inherited linear plus angular point velocity. Test several item types, both directions, and moving/rotating boats; avoid fixing just one prefab.
- **Submerged diving bell player motion:** player inherits strange motion in the submerged bell in BoatScene, without the same behavior in NodeScene. Compare scene wiring, bell interior/ghost support and the recent actual-contact moving-floor carry path for double carry or incorrect support velocity. Verify riding, walking, jumping and descent/ascent; Inspector mismatch is a hypothesis, not a confirmed cause.
- **Diving bell interior water ignores depth darkness:** audit the bell water material/context against the authoritative depth lighting source; compare with exterior water at the same depth and with lights enabled/disabled.
- **High priority — overall deep-water darkness too weak:** near maximum game depth, the user expects effectively pitch black without a light source, possibly a small glow around the player's head. Audit depth coordinates/normalization, scene environment wiring, shader/global attenuation, lighting and per-renderer exceptions. Compare BoatScene/NodeScene, exterior water, bell water, terrain, items and actors. Record visual test depths before changing balance.
- **Enhancement — more aggressive geographic depth transitions:** user accepts slopes **45° or steeper** when actual and target depth differ greatly. Revisit the current ordinary transition cap/profile ranges and mismatch response in a contained tuning pass, preserving support, shared edges and feature history. The recent smoothing fix removed per-knot shelves but did not raise the ordinary cap; do not misreport this request as already implemented.
- **Engine propulsion works on land (reported 2026-10-04):** grounding tests reveal that engine thrust has no water/submersion eligibility gate. Audit where propulsion is applied and define the submerged propulsion point/area; an engine mounted above water is not necessarily invalid if its propeller remains submerged. Verify afloat, shallow grounding with propeller still submerged, fully dry land, and re-entry into water. Queue for the bug-fix pass; no engine behavior changed here.
- **Grounding collision feels too slippery (requested 2026-10-04):** alongside the engine propulsion fix, add strong boat–ground friction so running aground produces a harsh impact and rapid slowdown instead of smooth coasting. Keep this contact behavior specific to boat/ground; preserve walking, dropped-item, bell and underwater interaction behavior. Verify fast impact, sustained throttle, dry-land propulsion rejection, and practical retreat/refloating. Friction addresses sliding resistance; impact severity also needs the assembled hull/buoyancy test. No material or Inspector settings changed yet.
