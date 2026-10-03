# Streamed seabed — Inspector tasks

2026-10-03. **These changes have not been applied.** Code is opt-in: BoatScene will continue using the old terrain until this setup is done. Perform the setup outside Play mode, then save the scene. NodeScene needs no changes for this checkpoint.

## 1. Create the streaming profile

In the Project window, create **World → Boat Streamed Terrain Profile**. Suggested name/location: `Assets/Defs/WorldMap/SeaBiomeGenType/BoatTerrainProfile_Streaming.asset`.

Start with the defaults:

| Field | Value |
| --- | --- |
| Chunk Width | 128 |
| Sample Spacing | 1 |
| Water Level Y | 0 |
| Base Depth | 120 |
| Maximum Depth | 500 |
| Rolling Amplitude | 4 |
| Rolling Wavelength | 80 |
| Maximum Slope Degrees | 20 |
| Fill Depth | 25 |
| Interest Radius | 256 |
| Unload Buffer | 128 |

This checkpoint uses gently rolling floor at a fixed broad depth. Topography-driven depth and island encounters come later. Profile settings are copied into a frozen voyage plan; runtime edits take effect on a new voyage/scene, not by morphing existing floor.

## 2. Opt BoatScene into streaming

Open **BoatScene**, find the existing **SeaFloor** root. Keep that GameObject active; the existing sampler remains the single scene query entry point.

1. Add **BoatTerrainStreamer2D** to the SeaFloor root.
2. Assign its **Profile** to the new asset.
3. Assign **Ground Material** to a terrain material using `DontSink/NodeGroundTerrain`. The existing `GroundMaterial_Style_1` is usable as a reference for this test; duplicating it into a BoatScene-specific material lets you retune independently later. Do not use `GroundMaterial_TempGround` here: its current shader is the water shader.
4. Leave Ground Layer Name = **Ground**, Sorting Layer Name = **Default**, Sorting Order = **0**, matching the audited mesh fill defaults. Ensure SeaFloor's transform is unrotated and scale is 1,1,1 for this horizontal side-view floor.
5. On the existing **GeneratedGroundSampler2D**, assign **Streamed Source** to this same BoatTerrainStreamer2D. The old Edge reference can remain serialized; the streamed provider takes precedence.

## 3. Disable the legacy BoatScene terrain output

On BoatScene only:

1. Disable **BoatSeaFloorGenerator2D**.
2. Disable the original **EdgeCollider2D** on SeaFloor. It must not coexist with the generated chunk surfaces.
3. Disable the old **GroundFillMeshRenderer2D** component and its corresponding **MeshRenderer**. Disabling the fill script alone does not necessarily hide an already generated mesh.
4. If SeaFloor has an old **SpriteRenderer** showing the finite terrain artwork, disable that renderer too. Leave unrelated water/resource renderers alone.
5. If legacy **BoundaryWall_Left / BoundaryWall_Right** children already exist, disable those GameObjects. The new streamer does not create end walls. Disabling the generator does not remove walls previously generated in the editor.

Do not disable the entire SeaFloor root or its sampler. Do not modify NodeGroundGenerator2D, NodeScene's collider/walls/fill, or the BoatSceneController nominal distance/dock settings.

The streamer rejects startup if its profile/material/sampler wiring is incomplete, or if the original root generator/edge remains enabled. That failure intentionally stops boat restore before restoring dynamic gear over an undefined floor.

## 4. Connect water depth to streamed fill

On BoatScene's existing **NodeWaterBottomBinder**, set **Ground Source** to BoatTerrainStreamer2D, leaving its Water reference pointed at the intended WaterMeshRenderer. Extra Depth can remain 0.

BoatScene has three WaterMeshRenderer components. For any other water layer that needs full-depth coverage, add a **NodeWaterBottomBinder** on that water renderer's GameObject, assign Ground Source to the same streamer, and Water to that renderer. Keep existing foreground/background renderer ownership and sorting. Default streamed fill bottom is -525; bound layers will extend to that depth.

NodeScene's separately recorded binder-reference issue is outside this setup; do not change it as part of switching BoatScene terrain.

## 5. Preserve resource setup

Keep the existing UnderwaterResourceSceneSpawner catalog, budgets, and Ground Sampler reference. Its sampler must be the same SeaFloor sampler with Streamed Source assigned. The existing notifier reference may remain on the now-disabled legacy generator.

The resource script waits for initial streamed-floor readiness, spawns its existing one-time population, and ignores legacy regeneration notifications in streaming mode. The streamer does not emit the old whole-ground OnGenerated event. There is no new resource generation while sailing yet.

## Initial Play-mode checks

1. Embark, open F4. **Streamed floor: ready** should show loaded/committed/interest counts. Several `Seabed_<index>` runtime children should appear under SeaFloor, including negative indices.
2. Confirm rolling floor is visible, water covers its depth, and no finite legacy fill or end wall remains in the streamed region.
3. Sail forward/reverse across several 128-unit boundaries. Loaded count should stay near the active interest regions while committed count grows with voyage history. Returning reloads the same floor.
4. Swim away from the boat, deploy the bell/anchor/sounding weight, and check floor remains beneath each relevant actor/gear location. Check bell exit safety and anchor holding across a chunk seam.
5. Leave an ordinary unowned loose item far behind. It should enter stasis when its ground retires, then resume as the same item when you return. Boat-owned/deployed/persistent items retain their terrain instead.
6. Check resources do not reset or multiply at chunk boundaries. The current one-time population stays near the initial region; empty new regions are expected until the resource pass.
7. Revisit NodeScene and verify its island/dock terrain, walls, resources, water, and grounding remain as before. Test ordinary embark/docking again.

The existing source/target physical docks remain as compatibility objects for this checkpoint; they are not evidence that terrain should terminate there. Geographic destination arrival is a later pass. Geographic-only F4 warp still does not move the boat or replace committed terrain.

Commit code + your reviewed scene/profile/material setup after these checks. Full source/verification details are in `STREAMED_SEABED_CHECKPOINT.md`.
