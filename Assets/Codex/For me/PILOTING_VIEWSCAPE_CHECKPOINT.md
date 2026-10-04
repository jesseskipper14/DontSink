# Piloting reconciliation — circular viewscape checkpoint

2026-10-04 — Bosun 🍌

## Implemented

The existing main piloting view remains. A separate circular inset sits next to the analog compass, and the existing close harbor approach sketch is below that instrument row when there is room. The lighthouse remains in BoatScene; its physical representation and actual berth footprint can also appear in the inset.

The inset is boat-up: the boat marker stays centered and points upward. Local coastlines rotate around it with smoothed geographic heading and move with smoothed true geographic position. It samples the actual topography/sea threshold, with wrapped X and finite Y. It does not sample beyond the polar limit by clamping/repeating edge terrain.

Range refinement: the original 10-map-unit circumference was too small for physical observation miles around the boat. Coverage now defaults to **3 nautical miles radius**, converted through the current navigation bridge scale. Physical Units Per Nautical Mile defaults to 1852, assuming one physical scene unit equals one metre. Fog/rain and the observer-height multiplier still affect visible range. The old circumference field is retained hidden for serialized compatibility and is no longer the range control.

The circle shows simple filled coastline shapes and restrained ambient water marks. It has no grid, range rings, map labels, coordinates, digital headings or route-error readouts. Fog and rain reduce effective physical range, with smooth visibility changes and soft outer fades. Physical settlement rendering ignores route selection/map knowledge, and rendering never writes discovery, coverage or believed position. The observer-height multiplier is an explicit future lookout seam, not automatic crow's-nest mechanics.

Harbors use existing HarborDefinition observations. The currently relevant actual berth rectangle is drawn in teal when its footprint is within effective view range. This uses the same corners as docking. A small tower shape depicts a physically visible settlement; it is not a node label/icon. The larger close-approach sketch remains a separate guide inside piloting, moved below the inset/compass row.

The legacy generated route/recovery curve now draws only with **DBG** open. It is not a player-recorded navigation leg, so it should not provide an automatic recommended recovery path during normal manual navigation. Existing route simulation/diagnostics were not rewritten. The main water view, water reference visuals, boat, camera motion and throttle/rudder controls remain. Numeric navigation diagnostics remain under DBG. A future recorded cartography leg should supply its recorded values directly; no automatic heading comparison or leg completion was added.

## Inspector tasks

None required. No Inspector assets were changed. Inspect the new **PilotingOverlayRunner → Local viewscape and compass → Viewscape Settings** fields outside Play mode if you want to tune:

- Visible Radius Nautical Miles: 3.
- Physical Units Per Nautical Mile: 1852 (one physical unit = one metre assumption).
- Observer Height Multiplier: 1.
- Panel Size: 180 pixels, reduced to fit beside the compass.
- Texture Resolution: 128; Refresh Hz: 15. Texture is cached between rebuilds.
- Fade Start Fraction: 0.7.
- Translation Smoothing Seconds: 0.2.
- Visibility Smoothing Seconds: 0.5.
- Compass Smoothing Seconds: 0.35 (also drives inset rotation).
- Weather Visibility Multiplier: 1.
- Wave Marks: 0.25.
- Water/Land Color and Enabled.

The new settings are passed through an optional cartridge argument, preserving existing call sites. The compass remains a separate instrument. Very small play areas can omit the inset/full harbor sketch when they cannot fit; test your minimum supported resolution. No new camera, Canvas, texture asset or prefab wiring is required.

## Live test / commit checkpoint

1. Embark and open piloting near a coastline. Confirm the existing main view remains, with a separate circular inset next to the compass. If it shows only ocean, use the existing geographic debug warp to approach land rather than expecting a distant island inside the small default radius.
2. Turn through north and make sharp turns. The boat remains centered/upward; coastlines rotate smoothly. Sail alongside the shore: coastline position should move relative to the fixed boat.
3. Approach the known destination berth using F4 as needed, then close F4 and open piloting. The local tower/berth should appear only when physically within local visibility range. Check the larger approach guide below the instruments too. Escape out of piloting, then E to dock still uses the existing interaction.
4. Test fog/rain: the circle should stop showing distant land/harbors, with full fog leaving only ocean and the centered boat marker. No map reveal or believed-position movement should occur from looking.
5. Test different Game view sizes. Check the new instrument row against the main boat, debug panel and control bars. Toggle DBG: the old generated route is now diagnostic-only.
6. Dock, embark, save/load, close/reopen piloting and return to menu. Derived texture/heading/fades should rebuild without an old observation or leaked runtime texture.

## Files

New, plus metadata:

- `Assets/Scripts/Boats/Simulation/PilotingLocalVisibility.cs` — reusable radius, boat/world transform and physical coastline sampling seam.
- `Assets/Scripts/Boats/Simulation/PilotingViewscapeRenderer.cs` — settings, cached circular texture, weather observation, coastline/harbor/berth rendering, lifecycle cleanup.

Modified:

- `Assets/Scripts/Boats/Simulation/PilotingCompassRenderer.cs` — shared smoothed geographic heading, viewscape lifecycle and responsive instrument arrangement.
- `Assets/Scripts/MiniGames/Cartridges/PilotingCartridge.cs` — optional settings argument and diagnostic-only generated route rendering.
- `Assets/Scripts/MiniGames/Runners/PilotingOverlayRunner.cs` — serialized tuning and settings handoff.

No new consequential authority actions or saved fields. Local rendering consumes existing shared world/heading/weather truth. Actual network transport remains the separate MP design. No changes to docking rules, terrain collision or engine physics.

## Verification

Full production runtime compilation passed. Isolated Unity checks passed **379 viewscape assertions**, plus **502 presentation**, 14 bridge, 121 harbor geometry and the existing terrain/topology/physics regressions. New checks cover circumference conversion, observer-range scaling, 360-degree transform consistency, coast orientation, X wrapping, finite Y, zero visibility, real texture clipping/land/boat rendering, visible tower/actual berth drawing, navigation immutability and texture cleanup. Scene/weather providers are adapted hosts. Live appearance, performance with your complete scene and assembled UI/transition flow still need the sequence above.

Stopping new feature work at this checkpoint. After your playtest/commit, mark the piloting handoff DONE_ and move to the bug patch pass. No bug fixes or commit were made in this pass.


