# Ambient-star transition into the void

Implemented 2026-10-02, following the user's clarification that areas outside the world map are void areas, with a short ambient-only transition to emptiness.

## Behavior

Known-world generation is untouched. After the scene renderer queries the existing field, it appends deterministic ambient stars outside the known bounds within a finite fade band. Outside that band there are no generated stars. No new landmark stars, nebulae, deep-sky objects or constellations are generated in the void.

Density uses the known world's frozen ambient settings, cell size, seed and generation salt, with separate deterministic random channels. A star's retention probability is `1 - smoothstep(distance / fadeDistance)`, where distance is the shortest Euclidean distance to the world rectangle. The renderer multiplies its opacity by the same factor. At the boundary the factor is 1; halfway it is 0.5; at/beyond the full distance it is 0. Corners use the same distance rule rather than extending a square of full-density stars.

Full edge cells cover the partial-cell remainder beyond the world's right/top bounds; signed cells cover the left/bottom. Only exterior points survive. Overlapping queries produce the same stable stars. Supplemental IDs use a separate `voidAmbient:1:...` namespace and are not navigational/knowledge subjects.

This continuation is requested by the **scene sky renderer only**. Ordinary CelestialField queries, world-map truth, chart/instrument coverage, generation identity/config hashes, saved star IDs and constellation catalogs remain unchanged. Existing saves get the ambient transition without regenerating or replacing their known-world truth. It uses the ordinary scene sprites, twinkle, day/night/weather visibility and existing offscreen coverage.

The prior proposed cursor/camera clamps were not implemented. Far enough into the void, empty sky is now intentional. This change softens the world-boundary discontinuity; it does not make stars extend infinitely or change the telescope's finite viewport reserve.

## Every Inspector change

Edited only `Assets/Defs/WorldMap/Celestial/CelestialSkyProjectionSettings.asset`:

- Added **Void Ambient Fade Distance World = 32** under **Void Ambient Transition**.
- This is measured in world-map units, not local scene/camera units. With the current cell size of 16 it spans two generation cells.
- Set a smaller value for a quicker transition, a larger value for a wider transition, or 0 to disable it.

No other Inspector values were changed in this refinement. Existing extension fields, telescope layer choices, camera settings, scenes and generation-settings asset were left as found.

## Exact files

- Modified `Assets/Scripts/WorldMap/Celestial/CelestialFieldGenerator.cs`: declaration becomes partial; existing known-world generation logic is unchanged.
- Added `Assets/Scripts/WorldMap/Celestial/CelestialFieldGenerator.VoidAmbient.cs`: supplemental deterministic generator and shared fade weight.
- Modified `Assets/Scripts/WorldMap/Celestial/CelestialSkyProjectionSettings.cs`: the fade-distance setting, default 32 and minimum 0.
- Modified `Assets/Defs/WorldMap/Celestial/CelestialSkyProjectionSettings.asset`: the explicit value 32.
- Modified `Assets/Scripts/WorldMap/Celestial/CelestialSkyRenderer.cs`: append void stars to scene query results, fade their opacity, update the field-boundary diagnostic.
- Added `Assets/Codex/CELESTIAL_VOID_AMBIENT_TRANSITION.md`: this report.

## Verification

Full production runtime compilation and compilation including the prior sorting-layer Editor drawer pass. The isolated Unity harness passes **22,300 assertions** including the previous celestial/telescope regressions and new void checks across every generated sample. Checks verify deterministic repeated/overlapping queries, unique IDs, ambient-only exterior points, all four edges, tapering density, correct opacity weights, rounded corner distances, deep-void emptiness, disabling with zero, non-cell-aligned right/top boundaries and unchanged known-world objects and generation identity.

This is native headless Editor verification; actual multi-monitor input and live rendered appearance have not been exercised. The ordinary star shader multiplies visible light contribution by renderer alpha, so the void opacity factor affects the actual additive rendering path.

## Your final live checks / Inspector task

1. In CelestialSkyProjectionSettings, review **Void Ambient Fade Distance World = 32**. Tune it if the visual transition is too short/long; no new component/reference setup is required.
2. At a world edge, enter the telescope and pan outward. Ambient points should grow sparser and dimmer, then stop. Check the left edge and compare the right/top/bottom.
3. Verify that landmarks/constellations remain inside known bounds, ordinary stars inside the map look unchanged, and loading an existing save preserves its charts/names.
4. Commit once that smoke check passes. 🍌
