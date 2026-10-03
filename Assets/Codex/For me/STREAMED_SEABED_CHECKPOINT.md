# Streamed BoatScene seabed — checkpoint 2

2026-10-03. Runtime implementation is ready for manual Inspector setup and live testing. No scenes, prefabs, existing assets, materials, or Inspector values were changed by Bosun. Changes are uncommitted.

## Scope

Adds continuous, gently rolling chunked seabed to BoatScene as an opt-in replacement for the finite generator. NodeScene retains its single-edge generator and query path. The finite map/table presentation and geographic longitude wrapping remain unchanged. Topography-driven depth, islands, boat land obstruction, dynamic resources/POIs, networking, and floating origin are not implemented in this checkpoint.

Apply `INSPECTOR_TASKS_STREAMED_SEABED.md` before expecting different terrain in BoatScene. No new boat-prefab references are required; the existing spawner prepares the scene's registered streamer once the restored boat pose is known.

## Responsibilities and behavior

- **BoatTerrainProfile**: explicit tunable chunk size, physical sample density, fixed broad depth, rolling shape, slope cap, fill bottom, interest radius, and unload hysteresis. Created/assigned by the user through the Inspector.
- **BoatTerrainPlan**: a frozen seeded voyage plan. Terrain samples depend on logical strip coordinates and a separate terrain hash channel, not frame/load order or resource RNG. Shared endpoint samples and smooth noise derivatives avoid shape seams. Profile edits cannot morph committed floor mid-voyage.
- **BoatTerrainChunk2D**: one horizontal/full-depth column, EdgeCollider2D with adjacent endpoint samples, and a mesh filled down to the shared bottom. World-consistent UVs and white vertex colors support terrain shader alpha/pattern. Runtime meshes are released with chunk objects.
- **BoatTerrainStreamer2D**: scene registration, initial readiness, union of physical interest ranges, load/retire, committed identities, item stasis, and streamed queries. It does not consume camera position, own navigation, emit resource-wide regeneration events, or create end shores/walls.
- **GeneratedGroundSampler2D**: optional streamed-provider delegation. One stable sampler remains available to existing player guard, bell clearance, and resource code. A loaded envelope can contain gaps; sampling a missing chunk returns false instead of drawing an imaginary surface between islands of loaded terrain.

Chunk width defaults to 128 units with 1-unit sampling. Initial origin is the restored physical boat X, corresponding to the voyage baseline. This checkpoint assumes the current horizontal scene-forward +X convention and an unrotated/unscaled SeaFloor root. The strip and chunk indices support negative/reverse travel. Physical relocation/recenter remapping is not implemented; a geographic F4 warp changes no local terrain or strip coordinates.

Committed records retain chunk IDs; geometry can unload and regenerate identically from the frozen plan. GameObjects/meshes are not retained forever. This constant-depth checkpoint has a deterministic future profile; heading-sensitive hidden forecast replanning and macro-depth context will be introduced in the next planner checkpoint, while retaining already committed history.

Interest discovery is cached once per unscaled second, with transform positions consumed during physics updates. Boats, living players, undocked tether payloads (including bell, anchor, sounding gear), boat-owned physical items, and PersistentWorld items retain needed regions. Ordinary unowned WorldItems do not permanently retain chunks: on retirement, their instances enter inactive stasis and reactivate when that chunk returns. Stasis is session-local, not a new durable save format. This does not solve future corpse/NPC or arbitrary non-WorldItem lifetime policy; those need their own explicit interest/retirement integration when introduced.

Chunk-specific committed/loaded/unloaded events expose the lifecycle seam for later features. Coverage requests grant short leases so restore/new actor discovery cannot immediately retire freshly requested ground. Configurations and unbounded/non-finite coverage requests are validated. No transport/client terrain hydration exists yet; new shared generation/mutation paths are authority-gated.

## Startup, restore, and existing systems

BoatSpawner restores the boat transform, rebases its physical-motion sample, then synchronously prepares streamed coverage before restoring modules/tethers/items. Incomplete opt-in setup stops that boat restore with an error instead of proceeding over missing floor. Legacy scenes without a streamer follow the existing path.

Restored external dynamic bodies can request coverage over their footprint and be lifted above the actual sampled floor. Downward velocity is canceled after correction. Secured boat items and bell-contained child items are excluded from the general external-item pass. Tether restore corrects the payload before applying final line length; if correction moves it, line length is increased only as necessary to avoid introducing an artificial stretch impulse. Unboarded player restore waits for streamed readiness and checks ground; normal boarded restore remains unchanged.

Resources wait for the initial streamed floor and keep the existing scene population. Chunk changes never broadcast legacy whole-ground regeneration, and the spawner ignores such notifications while using a streamed sampler. New sailing regions do not get a fresh resource population in this checkpoint.

The streamer supplies IGroundFillBottomSource for water bindings. Its default consistent fill bottom is -525. Inspector setup must point the intended water-layer binders at this source; the scene's incorrect legacy generator references were not silently changed.

Legacy physical docks and the nominal geographic projection scale are retained. Terrain no longer depends on their finite span once streaming is selected. Geographic arrival is deferred.

## Verification

Production runtime compilation and source whitespace checks passed. The isolated Unity harness passed **1,342 streamed-terrain assertions**, plus **21 voyage**, **941 wrapping**, **47 camera**, and **61 pinning** assertions.

Terrain checks use the exact production planner, profile, chunk builder, streamer, and sampler with adapted unrelated actor/item/GameState hosts. They cover negative/exact chunk indices, 40 neighboring chunk seams, profile immutability, per-segment slope cap, startup coverage, missing-chunk rejection, authority-negative and invalid/unbounded requests, footprint rescue, a real Physics2D body resting on a shared collider endpoint, unload/stasis/reload identity, simultaneous separated boat/swimmer/gear regions, shader vertex alpha, and the legacy Node single-edge sampler path. The earlier pinning checks still emit their known edit-mode Destroy diagnostics; new terrain cleanup supports the edit-mode harness.

These are not full production scene/bootstrap, bell/anchor rigging, shader appearance, long-voyage performance, or resource-spawner integration tests. Inspector setup and the live checklist in the companion file are required before committing. Test artifacts remain ignored under Temp/CodexPhase7.

## Files changed

New source under `Assets/Scripts/Environment/GroundGeneration`:

- BoatTerrainProfile.cs
- BoatTerrainPlan.cs
- BoatTerrainChunk2D.cs
- BoatTerrainStreamer2D.cs
- IStreamedGroundSource2D.cs

Existing source modified:

- Environment/GroundGeneration/GeneratedGroundSampler2D.cs
- Environment/Resources/UnderwaterResourceSceneSpawner.cs
- GameState/Boat/BoatSpawner.cs
- GameState/Scene/PlayerSceneContextRestorer.cs
- Modules/Tethers/TetherDeploymentModule.Persistence.cs
- Debug/WorldNavigationDebugOverlay.cs

Next checkpoint, after setup/live checks/commit: world topography drives safe macro-depth forecasts, with a proof terrain-feature directive. Island presentation and boat-only land obstruction follow separately.
