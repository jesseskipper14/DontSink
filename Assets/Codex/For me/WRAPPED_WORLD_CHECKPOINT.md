# Wrapped world topology — Bosun checkpoint

Implemented the wrapping foundation across navigation, terrain, graph generation, POIs, fog, celestial queries, and both map-table pages. MP work following this pass is analysis only. Changes are uncommitted.

2026-10-03 correction: the world wraps, but the map/table is a finite sheet. Restored the pre-pass viewport, wooden borders, single-copy map/chart rendering, paper/token placement, and hit testing. Seam-crossing routes/constellation connections split at the longitude edges. No Inspector changes. The user has successfully tested world-position wrapping using F4.

## Inspector tasks

No Inspector values, scene files, prefab files, material files, or existing asset data were edited. New code exposes `Polar Ice Band Fraction` (default 0.06) and `Polar Ice Height 01` (default 0.99) on the existing topography settings. Review these after compilation; no new component or reference is required. Do not rebake an old saved world's terrain merely to make its seam smooth.

The F4 navigation window now scrolls and shows the canonical interval, finite Y bounds, raw/wrapped seam distance, and unwrapped BoatScene projection. Its existing warp fields accept longitude beyond either edge and continue through canonical geography.

## WW.1 — Math and bounds

New `Assets/Scripts/WorldMap/Navigation/WorldTopology.cs` supplies pure bounds validation, X normalization, signed shortest delta, distance, nearest visual equivalent, finite Y validation/clamping, grid index wrapping, and polar start coordinates. The canonical interval is the existing topography rectangle's `[xMin, xMax)`; Y remains `[yMin, yMax]`. Exactly antipodal deltas retain their signed direction.

`WorldTopologyService` obtains bounds from explicitly published generation/restoration bounds, saved topography/graph metadata, or runtime topography. It does not infer circumference from camera padding or a guessed universal constant. Snapshot changes clear the previous publication. Isolated graph-only legacy worlds without any valid topography/bounds remain a compatibility limitation; they need real world bounds before wrapping can operate.

## WW.2 — Navigation and spatial consumers

Authoritative and replicated navigation validate finite input and finite Y; stored true X is canonical. BoatScene route projection uses the wrapped endpoint vector, while local navigation, Rigidbody positions, tethers, and physical travel coordinates remain unchanged. Debug relocation still changes geography only.

Graph spacing, nearest-node/cluster selection, angular sorting, spanning trees, and extra-edge distances use wrapped geometry. Cluster list index difference no longer pretends to be geographic proximity. StartDock selection is seeded; Destination is the farthest node in the wrapped metric. Old graph IDs, kind assignments, and edges restore intact.

Travel length agrees across NodeTravelController, World Map cartridge, legacy map overlay, route hover, and save diagnostics. POI spacing, restored POI positions, terrain feature-center spacing, and starter chart selection account for the seam. Candidate edge exclusion now applies to north/south rather than longitude. Economy/event/resource simulations were searched: their existing relationships are node/graph based rather than additional raw geographic radius calculations, so no speculative rewrite was added.

Chart board placements and physical pieces retain ordinary finite table coordinates; paper/group movement and rotation do not wrap around the table. Immutable observed star coordinates remain evidence; visual geometry unwraps marks around their observation datum to prevent a seam-straddling scrap becoming world-wide.

## WW.3 — Terrain, biomes, generation versions

New topography generation is version 3: the complete seeded noise/feature domain receives a smooth periodic X blend; sample endpoints agree, X neighbors wrap, and Y edge falloff remains finite. Feature-placement spacing accounts for wrapped neighbors. Permanent raised polar bands provide terrain foundation and north/south start hooks. Final ice art, named polar biome definitions, collision content, and local terrain streaming remain later content work.

Biome metrics sample through the seam; biome selection endpoints and smoothing neighbors agree across X. Landmass flood fill and nearby-landmass queries wrap their grid neighbors. Contours consume the periodic height field.

New fingerprints include topology version. Bake assets have an additive generation-version field in code, with legacy default 2; no bake asset was regenerated or edited. New captures use world snapshot version 4 and record topography/graph generation metadata. Height encoding remains compatible with the existing packed save format.

## WW.4 — Map, stars, and fog

World Map renders one finite copy of terrain, celestial textures, fog cells, nodes, and POIs. Short seam-crossing routes and constellation connections stop at one longitude edge and resume at the opposite edge, rather than drawing a long line through the map's middle. The legacy Canvas overlay also splits seam-crossing routes. All map-table wooden borders remain as before this pass.

World Map and Star Chart retain the original shared finite viewport, projection, grid, and hit testing. There are no repeated scraps/pieces, automatic seam-wrapping pans, or wrapping paper/token coordinates. All paper is drawn before celestial ink as before the pass.

Celestial queries visit each canonical cell once, reject distant cells before generating them, and return canonical objects with stable IDs. Projection finds the nearest longitude equivalent. East/west void ambient generation is removed; north/south ambient taper remains. Survey sequence keys normalize X.

New worlds use constellation topology version 2 for seam-aware grouping and centroid calculation. Worlds restored from legacy terrain retain constellation topology version 1, preserving prior constellation IDs/membership instead of silently renaming saved observations. Individual star generation/IDs are unchanged.

Fog reveals and lookups wrap X, including a reveal circle crossing the seam. Snapshot bits remain a single canonical grid.

## WW.5 — Validation and compatibility

Production code compiled successfully using the project's Unity compiler references. `git diff --check` passed for the source changes. The isolated Unity harness passed **941 wrapping assertions**, **47 camera assertions**, and **61 pinning assertions**, using production helpers/generators/controllers and adapted unrelated hosts. Coverage includes negative and multiple-lap X, shortest displacement, finite Y, authority rejection, canonical navigation storage, finite-sheet projection/pan, fog reveal/save round-trip, deterministic periodic terrain/neighbor samples, polar foundation, wrapped celestial query equivalence, stable-ID uniqueness, removal of east/west void stars, and legacy constellation version selection. The finite-view correction replaces the two former repeated-view assertions with checks that west-edge content is not repeated at the east edge and table panning does not wrap its center.

The harness is under ignored `Temp/CodexPhase7/UnityHarness`; its fresh result is `Temp/CodexPhase7/checks-result.txt`. The first sandboxed Unity attempt could not connect to package-manager IPC; the existing approved isolated-harness command then ran successfully. No new permission was required from the sleeping human.

**Live visual/gameplay validation is still required.** Automated checks do not verify map rendering, chart selection/snapping, local scene bootstrapping, or long voyages in your actual scenes. The current BoatScene ground generator still does not stream global polar terrain; this pass establishes topology and terrain hooks, not the subsequent physical streaming pass. Out-of-range Y is rejected by navigation validation until that terrain exists; it is not a physical ice collision barrier.

Old terrain payloads, node/POI IDs, old edges, and immutable observations are preserved. A legacy height field can retain a visible seam discontinuity; it was not regenerated. Derived biome presentation near that old seam can differ because neighbor sampling now wraps. A legacy save lacking topography/bounds requires explicit compatibility handling rather than an invented circumference.

## Morning test sequence / commit checkpoint

1. Start a fresh world. Open F4 during BoatScene, copy the reported bounds, then warp near `xMax - 1` at a valid middle Y. Sail across X. Canonical X should change interval while the boat, player, equipment, and tethers remain physically continuous.
2. Repeat westward and after several circumference-equivalent warps. Check the raw/wrapped probe and navigation scale.
3. At night, compare telescope and instrument stars on both sides of the seam. Survey there: scraps should stay small and star IDs should stay consistent.
4. Inspect World Map and Star Chart near both edges, switch pages, use compare mode, drag/pin/snap a group, and move a physical boat marker. The sheet and wood should remain finite, with one copy of content. Check split seam routes, fog, mouse selection, and multi-monitor width.
5. Save at a NodeScene and reload; verify IDs, chart groups/markers, and fog. Repeat with a pre-pass save and expect its original terrain seam to remain.
6. Check polar bands and finite Y readouts. Physical polar blockers await the later terrain/content pass.
7. **Commit this wrapping pass after those checks, before the next implementation pass.** Review any unrelated material changes separately.

## Exact source files changed

New: `Assets/Scripts/WorldMap/Navigation/WorldTopology.cs` and its Unity metadata.

Modified:
- `Assets/Scripts/Boats/Simulation/BoatSceneWorldPositionBridge.cs`
- `Assets/Scripts/Debug/WorldNavigationDebugOverlay.cs`
- `Assets/Scripts/GameState/GameState.cs`
- `Assets/Scripts/MiniGames/Cartridges/CelestialChartTableCartridge.cs`
- `Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs`
- `Assets/Scripts/Travel/Travel/NodeTravelController.cs`
- `Assets/Scripts/WorldMap/Biomes/WorldMapBiomeGenerator.cs`
- `Assets/Scripts/WorldMap/Biomes/WorldMapBiomeLayer.cs`
- `Assets/Scripts/WorldMap/Celestial/CelestialChartFragmentVisualBuilder.cs`
- `Assets/Scripts/WorldMap/Celestial/CelestialConstellationGenerator.cs`
- `Assets/Scripts/WorldMap/Celestial/CelestialField.cs`
- `Assets/Scripts/WorldMap/Celestial/CelestialFieldGenerator.VoidAmbient.cs`
- `Assets/Scripts/WorldMap/Celestial/CelestialFieldQuery.cs`
- `Assets/Scripts/WorldMap/Celestial/CelestialSkyProjection.cs`
- `Assets/Scripts/WorldMap/Celestial/CelestialStarterChartBootstrap.cs`
- `Assets/Scripts/WorldMap/Celestial/CelestialSurveySequenceTracker.cs`
- `Assets/Scripts/WorldMap/Data/WorldMapGraphGenerator.cs`
- `Assets/Scripts/WorldMap/Data/WorldMapSaveBuilder.cs`
- `Assets/Scripts/WorldMap/Data/WorldMapSaveRestorer.cs`
- `Assets/Scripts/WorldMap/Data/WorldMapSaveSnapshot.cs`
- `Assets/Scripts/WorldMap/Fog/WorldMapKnowledgeState.cs`
- `Assets/Scripts/WorldMap/Input/WorldMapHoverController.cs`
- `Assets/Scripts/WorldMap/Navigation/WorldNavigationService.cs`
- `Assets/Scripts/WorldMap/Navigation/WorldNavigationState.cs`
- `Assets/Scripts/WorldMap/POI/WorldMapPOIGenerator.cs`
- `Assets/Scripts/WorldMap/POI/WorldMapPOISource.cs`
- `Assets/Scripts/WorldMap/Topography/WorldMapTopographyBakeAsset.cs`
- `Assets/Scripts/WorldMap/Topography/WorldMapTopographyDebugSource.cs`
- `Assets/Scripts/WorldMap/Topography/WorldMapTopographyField.cs`
- `Assets/Scripts/WorldMap/Topography/WorldMapTopographyFingerprint.cs`
- `Assets/Scripts/WorldMap/Topography/WorldMapTopographyGenerator.cs`
- `Assets/Scripts/WorldMap/Topography/WorldMapTopographySettings.cs`
- `Assets/Scripts/WorldMap/UI/MapOverlayController.cs`
