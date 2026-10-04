# Geographic land encounter checkpoint 🍌

## Implemented

BoatScene now exposes a read-only `BoatLandEncounter` through its existing terrain streamer. It uses authoritative world navigation coordinates and the runtime topography's effective sea level. Land/water classification uses the same bilinear height sampling as geographic depth. Out-of-bounds Y, invalid coordinates, missing geography, and inactive voyages report unavailable rather than pretending to be water. X wraps normally.

`BoatGeographicLandQuery` builds a deterministic, four-neighbor landmass index from topography cell centers. Connectivity wraps horizontally and stops at the north/south bounds. The duplicated periodic endpoint is not counted twice. IDs belong to this topography field; they are not node IDs or persistent save identities. Component area and equivalent circular diameter are approximate map-space size measures.

Nearby selection supports one dominant landmass. Detection range is base range plus diameter times size factor, capped at maximum range. Selection uses nearest sampled land distance, with a switch margin that retains the current island when another is only marginally closer. An island outside its detection range is always released. Identity does not depend on boat heading or the camera.

The streamer refreshes the encounter every 0.25 unscaled seconds on the authority, independently of the geographic-depth toggle. The index is rebuilt when the topography field or sea level changes and discarded with the voyage. Selection also clears when disabled or navigation/geography becomes unavailable. There is no discovery update, boat movement change, generated island silhouette, physical land barrier, docking interaction, or saved encounter state in this pass.

F4 now reports geographic surface (water/LAND/unavailable), dominant landmass ID, approximate distance, visibility range, nearest sample coordinates, approximate area/diameter, and sampling spacing.

Testing refinement: F4 also shows the compass direction and clockwise bearing to the nearest sampled land point (north 0°, east 90°), using the wrapped geographic offset. The relative angle uses current geographic heading: positive is right, negative is left. A coincident sample reports no direction. `Geographic surface: LAND` means the boat's world position is already land; the displayed sample distance is not shoreline clearance, and its bearing is not an escape route to water. No Inspector work is required for this addition.

## Inspector tasks

No Inspector assets, scene wiring, prefab contents, or existing profile values were edited. No new component or reference assignment is required.

On the existing BoatTerrainProfile, review the new **Geographic Land Detection (map units)** fields:

- Base Range: **10**.
- Maximum Range: **30**.
- Size Factor: **0.35**.
- Encounter Switch Margin: **1**.

These are initial script defaults, not settled balance values. You can tune them manually during Play Mode; subsequent encounter samples use the current values. Detection does not depend on **Use Geographic Depth** being enabled.

## Live checks

1. Enter BoatScene and open **F4**. In open water expect `water` and either no dominant landmass or a nearby one within its size-adjusted range. The map's coordinates/warp tools remain useful here.
2. Pick points progressively nearer an island. Expect one stable ID and falling approximate distance; moving far away clears the encounter. Larger islands should be detectable farther away than smaller ones, up to Maximum Range.
3. Test between neighboring islands. Small position changes should retain the current island while distances are close; a clearly closer island should replace it. Changing heading in place should not change the ID merely because of heading.
4. Warp to a clearly inland map point. F4 should show `LAND`. This pass deliberately permits the existing debug warp and normal navigation to cross land; enforcement is a later pass.
5. Leave BoatScene and revisit, or load a different save. Check that the readout reflects the current map/position, with no retained encounter from the previous voyage.
6. If your map has land crossing the horizontal seam, inspect it from both sides: the wrapped landmass should have the same ID and short distance. Synthetic seam tests also cover this case independently of your generated map.

## Validation and limits

Full production runtime C# compilation passed. The isolated Unity harness passed **6,645 geographic land assertions**, plus **5,656 existing terrain feature/planner assertions**. Land checks use the production query, topography field, topology struct, and Unity math. They cover periodic island connectivity/identity, finite Y and invalid inputs, empty/full land fields, legacy sampling, size-based range, switching hysteresis, stale-selection release, deterministic reconstruction, and bounded-search distances compared with an independent full-grid oracle over 2,541 positions. The runtime streamer lifecycle and F4 presentation still require the live checks above; this harness does not boot the full game.

Classification is exact relative to the existing topography sampler, not to a new coastline mesh. Landmass identity, area, and nearest-land distance use cell-center sampling. Narrow land features or diagonal-only connections may be missed/split at this resolution; distance is not an exact shoreline distance or hull clearance guarantee. Index construction visits the grid once when context changes; ordinary queries search a bounded neighborhood without per-query collections. Very large configured ranges can scan the full grid.

The later boat-obstruction pass must validate continuous/swept geographic land contact and boat clearance rather than treating this visual encounter distance as collision truth. The new harbor handoff places future harbor positions at node coordinates and departures offshore; this pass introduces no node exemption, arrival transition, or automatic departure relocation.

After live checks, commit this checkpoint. Next is the contained **stable island visual projection** pass, followed by geographic boat obstruction. Harbor transitions remain a separate implementation pass.
