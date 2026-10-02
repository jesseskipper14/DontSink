# Telescope sky coverage and optional visual layers

Implemented 2026-10-02. User authorized Inspector changes for this pass, with all changes reported.

## Every saved Inspector change

Only `Assets/Defs/WorldMap/Celestial/CelestialSkyProjectionSettings.asset` was edited:

| Inspector field | Added value |
| --- | --- |
| Scene Side Extension Viewport | 0.30 |
| Scene Top Extension Viewport | 0.30 |

Side extension means **30% of the full camera width beyond each side**, in addition to the existing 0.04 cull margin. Top extension adds **30% of the full camera height above** the existing band and 0.05 vertical margin. Bottom coverage is unchanged. Existing horizontal/vertical world fractions, query padding, star size, brightness, visibility, knowledge and generated truth remain unchanged.

No telescope prefab, scene, camera Inspector, GameObject layer assignment, sorting layer assignment, material or ProjectSettings layer data was edited. Two new telescope Inspector fields have empty script defaults; no layer choices were applied to the prefab.

## Behavior

Scene sky queries and culling now retain the expanded offscreen region. The square chart/instrument projection and query remain unchanged. Constellation branch clipping uses the same expanded scene envelope as stars.

Padding alone was insufficient: scene stars previously used the camera's moving viewport frame each update. While an orthographic telescope session is active, scene sky coordinates now use a local reference frame captured from the existing camera on entry. Physical actor movement translates that frame, but RMB camera pan no longer drags the stars along. The existing modest zoom naturally magnifies the anchored field. Stars, constellation branches and labels use the same conversion. Exit blends back to the ordinary camera-relative frame over the existing return fade. No new camera, telescope-specific truth or celestial evidence is created.

Normal view retains its original camera-relative behavior. Existing RMB pan strength/range is unchanged: the 30% region is a reserve, not an increase to the maximum pan distance. Near generated field boundaries there can still be no stars where no celestial truth exists. This anchoring is currently for the project's orthographic camera; perspective cameras retain their ordinary projection.

## Additional omission controls

Open the telescope prefab, select its root, and find **Observation Telescope Interactable → Presentation**:

1. **Additional Hidden Layers** is a GameObject LayerMask picker. For NPCs, select **Agent** if their actual SpriteRenderer/renderer GameObjects use that layer. Child renderers need the intended layer as well.
2. **Additional Hidden Sorting Layers** is a list of sorting-layer dropdowns. Add an entry and select **WorldBuildings** to hide renderers using that sorting layer. Select None/remove entries to stop hiding that layer.
3. Save your choices; exit/re-enter observation to apply them. Both lists start empty, so nothing extra is hidden until you choose it.

These controls add to the existing boat hierarchy, registered onboard items and boarded-player omission. Either picker matching a renderer includes it. They affect **visuals only**, use the same fade/steady-state suppression/restoration, and apply only during the observing camera render. Physics, obstruction queries and interactions for other players are unchanged. World-space canvases honor their GameObject layers and their owning canvas's sorting layer. Screen-space HUD canvases remain visible.

GameObject layers and sorting layers are different systems: **Agent** is a GameObject layer; **WorldBuildings** is a sorting layer. Broad selections such as Default, Ground or Background can also hide terrain/sky that shares those layers; choose the specific layers you intend. Selected external renderers are collected while observation renders so newly spawned NPCs/buildings are included. Transparent sprite/line/TMP materials fade; opaque/custom shaders need alpha-blending support for intermediate transparency.

## Exact files for this refinement

Modified:

- `Assets/Scripts/WorldMap/Celestial/CelestialSkyProjectionSettings.cs` — scene-only extension settings.
- `Assets/Defs/WorldMap/Celestial/CelestialSkyProjectionSettings.asset` — the two 0.30 Inspector values above.
- `Assets/Scripts/WorldMap/Celestial/CelestialSkyProjection.cs` — expanded scene query/cull/branch envelope, instrument path preserved.
- `Assets/Scripts/WorldMap/Celestial/CelestialSkyRenderer.cs` — scene query and shared sky-frame conversion/pixel scale.
- `Assets/Scripts/WorldMap/Celestial/CelestialSkyRenderer.Constellations.cs` — use matching frame for branches/labels.
- `Assets/Scripts/Inventory/BoatEquipment/BoatObservationPresentationController.cs` — local sky-frame lifetime/exit blend and optional layer selection.
- `Assets/Scripts/Inventory/BoatEquipment/ObservationTelescopeInteractable.cs` — both layer picker fields, empty by default.

Added:

- `Assets/Scripts/WorldMap/Celestial/CelestialSkyRenderer.ObservationFrame.cs` — shared scene conversion seam.
- `Assets/Scripts/Inventory/BoatEquipment/ObservationSortingLayerAttribute.cs` — sorting-layer picker attribute.
- `Assets/Editor/ObservationSortingLayerDrawer.cs` — Inspector dropdown drawing only; never assigns values unless you choose them.
- `Assets/Codex/TELESCOPE_SKY_COVERAGE_AND_LAYERS.md` — this report.

## Verification and remaining live checks

Full production runtime compilation and compilation including the new Editor drawer pass. The isolated Unity harness passes **6,522 assertions**, including **14 new checks** for anchored telescope panning, camera isolation, restoring the normal frame, left/right/top reserve, unchanged bottom/instrument coverage, scene query inclusion, matching constellation endpoint and optional GameObject/sorting-layer hiding/restoration. Existing tests still cover Escape and fades. The legacy branch test explicitly sets new extension/cull fields to zero to keep testing its original viewport-boundary case.

This is headless Editor verification with adapted surrounding boat/camera/item hosts. Live URP rasterization, the new Inspector dropdown appearance, actual NPC/building layer assignment and performance with the expanded star pool still need a gameplay check.

1. Enter observation at night; hold RMB and pan left/right/up. Stars should enter from the reserve instead of following the camera. Constellations should stay attached to their member stars.
2. Try 1–1.5x zoom and exit. The sky should return smoothly to its ordinary frame; repeat entry with RMB already held.
3. Set Agent and/or WorldBuildings using the new fields, re-enter, and confirm those visuals fade away and restore correctly. Terrain/cloud/weather should remain unless their layers were intentionally selected.
4. Tune the two scene extension values between 0 and 0.30 or higher as desired, keeping the bottom unchanged. Confirm stars at field boundaries remain truthful and watch the additional queried/render slots.

Commit after live checks pass. 🍌
