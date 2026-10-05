# Destination-free Embark and passive reveal checkpoint

2026-10-05 — Bosun 🍌

Implemented the two requested navigation changes. The rest of legacy cleanup is on hold. No saved scene, prefab, material, layer or Inspector values were edited. Not committed.

## Behavior

- Embark works without selecting/locking a destination, without a graph edge, without route-length/cluster/star-unlock gates and without an outcome roll before departure.
- Still requires the active boat, valid identity/prefab, boarding (unless the existing debug bypass is intentionally enabled), current harbor, departure gate and safe offshore geometry. Authority and transition guards remain in force.
- Embark is accessible above selected-node details, so no node selection is needed. It is disabled while already underway; docking clears the voyage and makes another departure possible.
- New voyages use the harbor's waterward direction and one independent scale: HarborTravelSettings.departureMapUnitsPerPhysicalUnit, default 0.0135 map units per physical unit. This is the last scale reported during your testing. Old saved voyages retain their saved departure scale and endpoint basis. Legacy API callers with an actual destination retain their existing route-derived fallback.
- The departure scale is captured in the ordinary TravelPayload and retained across save/load. No dummy destination or fake edge is created. The diagnostic world-map snapshot now explicitly records destinationFree.
- Opening the map table, loading/visiting a node and debug instant travel no longer grant coverage around the current node, destination or route corridor.
- Fresh knowledge receives only the StartDock patch using the existing 28-unit radius. The old serialized revealCurrentNodeOnAwake toggle now controls this starting patch only; its tooltip explains that compatibility name.
- Existing saved coverage, an intentionally empty saved chart, explicit Reveal Node/Reveal All/Survey buttons and celestial starter evidence remain intact.
- Necessary fresh-game integration: New Game now clears its retained world-map snapshot and clears an unset starting-node override. Otherwise the previous save's coverage and settlement layouts would leak into the next game. A configured starting-node override is still honored.

## Morning test

1. Load a node scene and board the boat. Leave destination unlocked; open the map table without selecting a node and press Embark. Verify offshore spawn, waterward heading, zero starting throttle and normal sailing.
2. Check the F4 scale. A fresh departure should read 0.013500; load a pre-existing voyage and check that its saved scale is unchanged.
3. Save while sailing, reload and check geographic position, heading, speed and terrain. Dock at another harbor, then embark without choosing a destination. Repeat once more.
4. Open and close the map table at different nodes, sail, dock and reload. Coverage should not expand from those actions. Existing explicit chart sources and debug reveal tools can still add coverage.
5. Load a game with revealed coverage, return to menu, then start New Game. It should use the configured starting node or the graph's StartDock and receive only its starter patch, without old towns/history/coverage.
6. Confirm you cannot launch a second Embark while at sea or without boarding the active boat.

## Inspector tasks

None required. Optional tuning is the new departureMapUnitsPerPhysicalUnit field inside SceneTransitionController's Harbor Settings. No old voyage scale is rewritten by changing it. Existing legacy destination buttons/readouts are deliberately still present until the deferred cleanup resumes.

## Verification

Full production C# assembly compilation passed. Unity regression checks passed, including destination-free coincident endpoints, configured conversion, resumed geographic position, old-route fallback, starter-only reveal, preserved saved/empty coverage and explicit debug reveal. The checks use production logic with adapted scene/state hosts. They are not a substitute for the assembled scene/save-slot cycle above.

The initial restricted Unity launch failed at Package Manager IPC, before tests ran. The previously approved test command worked outside that restriction. No approval request is pending.

## Exact modified files

- [NodeTravelController.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Travel/Travel/NodeTravelController.cs>)
- [HarborGeometryQuery.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Travel/Travel/HarborGeometryQuery.cs>)
- [SceneTransitionController.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Scene/SceneTransitionController.cs>)
- [GameState.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/GameState.cs>)
- [BoatSceneWorldPositionBridge.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Simulation/BoatSceneWorldPositionBridge.cs>)
- [WorldMapCartridge.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs>)
- [MapOverlayController.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/UI/MapOverlayController.cs>)
- [WorldMapKnowledgeSource.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Fog/WorldMapKnowledgeSource.cs>)
- [WorldMapTravelDebugController.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Travel/Travel/WorldMapTravelDebugController.cs>)
- [MainMenuController.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Menu/MainMenuController.cs>)
- [WorldMapSaveBuilder.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapSaveBuilder.cs>)
- [WorldMapSaveSnapshot.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapSaveSnapshot.cs>)
- [SaveCompatibilityDiagnostics.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveCompatibilityDiagnostics.cs>)

No new production navigation scripts. Settlement integration also touches the shared save builder/snapshot files, documented in the separate settlement report. Test harness changes live in ignored Temp/CodexPhase7 and do not wire the real scenes.

Commit after the morning regression checks. Broader source/destination APIs, route UI deletion, economy graph links, discovery redesign and migration cleanup remain on hold.
