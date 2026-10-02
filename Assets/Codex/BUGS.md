# Persistence bug backlog

Keep reports focused: trigger, evidence, likely effect, proposed pass, and verification. Code inspection is not a live reproduction. Resolved entries remain as history.

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
