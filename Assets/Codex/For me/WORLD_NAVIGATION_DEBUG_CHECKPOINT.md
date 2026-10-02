# World sailing preparation — checkpoint 1

Date: 2026-10-02. Implemented navigation diagnostics and session-only geographic warp; awaiting live setup/playtest. No Inspector, scene, prefab, material, or item-definition settings were edited by Bosun. Infinite fuel remains user-authored.

## Inspector tasks

1. Add **WorldNavigationDebugOverlay** once to the existing persistent **ServiceRoot** object/prefab through the Inspector. Use the same persistent root as the TimeOfDayDebugOverlay. Do not add one to each scene if the root persists between them.
2. Leave Toggle Key = **F4**, Start Open = false. Window Rect and Window Id defaults can remain. The component resolves the active BoatScene projection after scene changes; there is no spawned boat reference to assign.
3. In **both NodeScene and BoatScene**, find **CelestialObservationOverlayRunner** and uncheck **Enable Debug Hotkey** (or assign a different Debug Open Key). Both scenes currently serialize Debug Open Key = F4. Physical charting instrument interaction remains available; fragment-preview binding is independent.
4. Do not increase the travel configuration's Max Route Length. Its current asset value is 200, but that legacy field and the debug maximum overrides no longer enforce a cap.

## Controls and readouts

F4 toggles a draggable window. Escape closes it through EscapeCloseRegistry. The window can remain visible during normal sailing, telescope observation, or charting. Distance measurements keep running while it is closed.

Readouts: authoritative world X/Y, local authority/client status, geographic heading (north = 0°, clockwise), world speed in map units per game second, physical forward speed in physical units per game second, Rigidbody BoatScene X/Y, physical-to-map scale, accumulated physical travel, accumulated world travel, and measurement time. Reset clears the measurements. World travel includes projected lateral motion; physical travel counts absolute motion along the boat simulation's travel axis. They need not have identical ratios under lateral disturbances. Measurements are scoped to the current resolved BoatScene bridge and reset when that scene/bridge changes.

Enter numeric world X/Y using a decimal point. **Copy current coordinates** fills the fields. **Warp geography** changes the world projection's runtime origin. It leaves local piloting coordinates, Rigidbody position/velocity, boat contents, players, anchors, bell, and local generated ground untouched. Subsequent sailing continues from the new geographic location. Repeated warps replace the offset rather than accumulating a placement error. Warps are excluded from distance measurements.

Warp is available only for one unambiguous active BoatScene bridge on simulation authority. Docked NodeScene still displays available world truth, but cannot warp. Invalid/non-finite coordinates are rejected. No persistent boat lookup or nearest-boat behavior was added.

## Scale audit

BoatSceneController currently defines nominal scene trip distance as baseTravelDistance × distanceScale; the source default base is 200. BoatSceneWorldPositionBridge divides the source/destination world-map separation by that nominal distance. Therefore the physical-to-world scale depends on the selected route. A 400-map-unit route across a nominal 200-unit scene gives a scale of 2; a 2,000-map-unit route across the same nominal scene gives a scale of 10.

This checkpoint exposes the existing conversion; it does not choose a replacement or change boat speed. Camera and dynamic-terrain work should use an explicit agreed physical/geographic conversion after this audit is playtested.

## Distance restriction changes

MaxRouteLengthRestriction is retained as a source-compatible no-op. RouteAccessPolicy no longer rejects routes by length. WorldMapHoverController no longer produces a distance blocker. WorldMapTravelDebugController reports an unlimited maximum. Existing serialized knobs are retained for compatibility; WorldMapTravelRulesConfig labels its value as legacy.

Boarding, graph adjacency, route knowledge/unlock, and optional pre-launch outcome validation remain separate existing gates. Their reconciliation with unrestricted geographic sailing belongs to the subsequent navigation/topology passes.

## Current limitations

- Warp offset is developer session state: it survives projection rebinding for the same voyage, but resets on a different travel payload or BoatScene recreation. It is not saved.
- Finite ground, authored docks, local route guidance, and geographic land collision are not rebuilt by warp. Streaming and land integration remain future checkpoints.
- The current world still has ordinary finite bounds. Wrapping and polar rules are not implemented in this checkpoint; coordinates outside current generated content can query empty/clamped content.
- Existing mid-voyage navigation persistence requires an audit in the terrain pass. This checkpoint makes no new save/resume guarantee for boat heading, local navigation progress, or a geographic debug warp.

## Live checkpoint

1. Set up the component and remove both old F4 bindings. Start a voyage; F4 should open only this window. Drag, close/reopen, and Escape it.
2. Sail normally, reset measurements, sail forward then reverse. Check physical distance increases in both directions and world position responds to steering.
3. Copy current coordinates, change X/Y to another known ocean region, warp, then keep sailing. World X/Y should stay at the relocated region and advance from there; the physical boat/crew/tethers should not jump. Check telescope/charting queries respond to that region.
4. Repeat warp. Check measurements exclude both relocations. Invalid text, NaN, and Infinity should not relocate anything.
5. Inspect/select a known direct route longer than 200 map units. It should have no “Route too long” message or launch denial. Separate locked-route/boarding/adjacency rules may still block it.
6. Compare the scale readout on two different route lengths. Record the values and whether the observed travel pace feels sensible.
7. Return to NodeScene and start another voyage. Verify the persistent window resolves the new scene and no old debug offset leaks into the new voyage.

Commit after this checkpoint passes live testing, before the camera-safety pass.

## Automated validation

Full production runtime compilation including the new overlay succeeds. Isolated Unity checks pass **22,392 assertions**, including **33 new navigation/debug assertions**. The new checks cover route-relative scale and geographic heading; immediate, repeated, and next-tick warp; preserving physics/local navigation/velocity; continued travel after relocation; projection rebinding; invalid/client/disabled/missing-voyage rejection; clearing offsets on a different voyage; distance measurements excluding warp; reset and Escape; and uncapped long routes preserving the route-knowledge gate.

Isolated checks exercise unchanged production bridge, piloting state, overlay, and route restriction/policy sources; surrounding simulation/GameState/navigation service hosts are adapters. Actual scene wiring, on-screen layout, input interaction, and physical terrain integration still require the live checklist above. Diff whitespace validation passes. No commit was created.
