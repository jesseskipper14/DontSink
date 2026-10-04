# Harbor destination / departure checkpoint

2026-10-04 — Bosun 🍌

## Implemented

- Embark derives a deterministic waterward approach from node position and the current topography. It searches a continuous water corridor, then checks a boat-sized berth and a farther offshore departure patch against water and terrain depth. An unsuitable harbor blocks departure with a reason instead of launching inland.
- Departure carries a separate offshore geographic anchor. Node positions, stable IDs and saved graph positions stay unchanged. The boat begins facing waterward with navigation velocity, angular velocity, throttle and physical Rigidbody velocity reset.
- BoatScene uses geographic berth docking instead of the two fixed route dock triggers. A boarded player can press E inside a nearby node's berth, regardless of selected destination, heading or speed. Ordinary hovered E interactions retain priority; aim away from them to dock. Existing departure restrictions for tethers/diving bell remain.
- Docking snapshots the existing boat/player state, updates current node and exact node world position, clears travel and loads NodeScene. Authority and transition guards protect the new action.
- F4 reports nearby harbor/berth status and provides **Fill destination berth coordinates**, followed by the existing **Warp geography** button for quick testing.
- New travel payloads retain their offshore anchor and scale across saves. Reloading uses saved geographic position rather than repeating the departure reset. Old payloads without the added fields keep their previous projection behavior.

## Inspector tasks — do these before testing

No scenes, prefabs, materials or Inspector references were edited in this pass.

1. Open **MainMenu** outside Play mode. Add **SceneTransitionController** to a dedicated GameObject if absent. MainMenu and save loading otherwise create an unconfigured controller before NodeScene loads; that persistent instance destroys the NodeScene duplicate. Keep NodeScene's configured version for direct scene testing.
2. Expand **Harbor Settings** and assign **Terrain Profile** to `Assets/Defs/WorldMap/SeaBiomeGenType/BoatTerrainProfile.asset` — the same profile used by BoatScene's terrain streamer. It must have geographic depth enabled.
3. Confirm **Nominal Local Travel Distance = 2000**. This matches the current BoatScene controller's `Base Travel Distance 2000 × Distance Scale 1`. Keep these values matched if you tune them later; changing this setting changes map-to-physical scale on new departures.
4. Confirm **Coastal Depth Band = 0.12**, matching the terrain streamer's coastal band. Berth/departure safety uses that same depth calculation.
5. In BoatScene, confirm **BoatSceneController → Use Harbor Docking** is enabled (new source default). No HarborDockInteraction component needs manual installation: BoatSceneController creates it at runtime. Keep the existing scene-owned **BoatSceneWorldPositionBridge**; it does not need to be added to boat prefabs.

Assign the profile in the authoring scene, not just on the DontDestroyOnLoad instance during Play mode. Missing configuration intentionally blocks embark.

### Follow-up: scene-owned bridge fix

The initial spawn/docking implementation incorrectly looked for the bridge on the spawned boat. The actual BoatScene bridge is a separate scene object. BoatSpawner and geographic docking now resolve one matching enabled bridge in the boat's scene and bind it to the boat's piloting state before initializing. Missing or ambiguous bridges fail with a more specific diagnostic. No Inspector modifications were made for this fix. The underwater resource setup error was downstream of the boat spawn abort.

## Short live test

1. Embark from the node that previously placed you inland. F4 should show a water position with adequate depth, throttle should be zero, and the geographic heading should point offshore. The local physical boat spawn still uses the existing scene spawn point.
2. In F4, click **Fill destination berth coordinates**, then **Warp geography**. Wait a moment for the nearby harbor query. F4 should say **E — Dock available**.
3. Close debug/cartridge UI, stay aboard, aim away from other interactable objects and press E. You should load the destination NodeScene once, with the correct current node. Existing tether/bell restrictions can still block this action.
4. Embark again. Verify another safe offshore departure, rather than reusing the previous inland node coordinate.
5. Sail or warp elsewhere in water, save, return to menu and reload. Verify the saved geographic position is retained instead of snapping back to departure.
6. For route-independent docking, use a third node's nearby berth. E should arrive at that node even when another destination is selected. Outside the berth, E should not dock; simply entering a berth should not auto-transition.

## Compatibility and remaining scope

Existing nodes can sit inland. The solver allows a bounded initial shoreline connector to reach water, but never skips land encountered after entering the water corridor. It does not relocate old nodes or rewrite the graph. A harbor with no safe corridor for the visiting boat rejects embark.

This prevents the inland-origin problem for newly validated departures. It does not relocate boats in already-grounded saves or retrofit old in-progress travel payloads. Arbitrary debug warps can still place a boat on land.

This is the core transition checkpoint, not the entire harbor handoff: settlement proxy art, visual approach guidance, NodeScene side mirroring, and the separate NodeScene harbor/mooring/quay mini-pass remain. Physical NodeScene dock geometry was not changed. Engine submerged propulsion and harsh grounding friction remain in the bug backlog.

## Validation

Full production runtime scripts compile successfully. The isolated Unity suite passed 121 harbor geometry checks and 14 harbor bridge/reset checks, plus the existing coastal, obstruction, boundary, terrain, island and authority regressions. Harbor checks cover deterministic corridors, boat scaling, insufficient clearance, invalid configuration, peninsula/puddle rejection, X wrapping, finite Y, departure reset, geographic save restoration and old-payload compatibility, separate scene-owned bridge resolution, disabled/missing state handling and duplicate bridge rejection.

The harness uses adapted scene hosts. It does not certify the assembled NodeScene/BoatScene transition, actual E prompt, or full save/load cycle; use the live sequence above. Final source diff whitespace check passed.

## Source files

New: `HarborGeometryQuery.cs`, `HarborTravelService.cs`, `HarborDockInteraction.cs` under `Assets/Scripts/Travel/Travel`, with Unity metadata.

Modified:

- `Assets/Scripts/GameState/Scene/SceneTransitionController.cs`
- `Assets/Scripts/GameState/Scene/BoatScene/BoatSceneController.cs`
- `Assets/Scripts/GameState/Boat/BoatSpawner.cs`
- `Assets/Scripts/GameState/GameState.cs`
- `Assets/Scripts/Boats/Simulation/BoatSceneWorldPositionBridge.cs`
- `Assets/Scripts/WorldMap/Data/WorldMapGraphGenerator.cs`
- `Assets/Scripts/Controller/Interactions/Interactor2D.cs`
- `Assets/Scripts/Controller/Interactions/InteractPromptDriver.cs`
- `Assets/Scripts/Debug/WorldNavigationDebugOverlay.cs`

Commit after the Inspector wiring and live tests pass. No commit was made by Bosun.

