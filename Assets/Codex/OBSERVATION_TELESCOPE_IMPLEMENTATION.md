# Observation Telescope — Bosun implementation

Implemented 2026-10-02 from `Observation_Telescope_Bosun_Handoff.md`. Contained physical equipment and local presentation pass.

## Player flow and setup

Use the existing inventory debug spawner or vendor/inventory path with `item_telescope_observation`. Drop the item on the boat, let it rest upright on a deck, stand beside it within 2 units, press **E to deploy**, then **E to observe**. **RMB Look** continues through the existing input/camera/celestial systems. Wheel zoom is 1–1.5x. **E or Escape exits**. After exiting, **X unpins**, then the existing **F pickup** works.

The supplied assets are wired:

- Prefab: `Assets/Resources/Prefabs/Items/Placeable/ObservationTelescope.prefab`
- Definition: `Assets/Defs/Items/Placeable/item_telescope_observeration.asset` (existing filename spelling preserved)
- Catalog: `Assets/Defs/Items/Catalogs/itemCatalog.asset`

The definition previously pointed at Rope for both world prefab and icon; those references now point at the supplied telescope assets. Existing sprite import, collider, highlight, item policies, vendor settings and mass remain intact.

No new camera or required scene reference. The normal fallback requires `CameraManager.FollowTarget` to identify the interacting player's `PlayerBoardingState`, and uses `CameraManager.ActiveCamera`. A future local multi-view bootstrap can pre-add `BoatObservationPresentationController` to each actor, assign its own existing `View Camera`, and supply `ILocalPlayerAuthority`. Remote markers cannot enter a session. Sessions are attached automatically for the current single-player interaction flow.

**Raw prefab placement is not an item spawn:** like the existing WorldItem prefabs, this prefab has a null serialized item instance. A scene-authoring/test spawn must call `WorldItem.Initialize(ItemInstance.Create(definition, 1))` and assign boat ownership, or use the existing inventory spawn/drop path. Merely dragging the prefab into the hierarchy does not create inventory identity/ownership.

## Responsibilities and state

- `PlaceableBoatEquipment`: lightweight general non-module equipment deployment. Validity needs initialized physical WorldItem, owning boat, simulated Rigidbody2D, upright orientation relative to boat-up, and a small foot overlap on that boat's non-item structure. Deployment creates a removable FixedJoint2D to the existing boat body. Pickup is blocked through the existing pickup-participant transaction. Host authority gates deploy/unpin/restore. Support is rechecked every 0.25 seconds; loss of support/ownership/body validity releases the pin. `BreakDeployment()` is the host seam for future impulses; no impulse/damage rule is implemented.
- `SkyClearanceRequirement`: world-up physical overlap box, excludes this telescope's collider hierarchy and triggers; honors configurable obstruction layers. Own boat geometry is not exempt. Every other included physical collider inside the volume blocks. Full query buffers fail closed. Selected gizmo displays the box.
- `ObservationTelescopeInteractable`: existing E/X interaction and prompt interfaces, same-boat requester/distance validation, inspector zoom limits.
- `BoatObservationPresentationController`: actor-local transient session. Uses the existing camera and existing RMB soft pan. Snapshots orthographic size, perspective FOV, current manager soft-pan offset and enabled state of an optional CameraWASDController. Restores those exact values on exit. It does not replace camera follow or add telescope panning.

Loose/unplaced equipment is not usable. Deploying requires valid support; overhead clearance gates observation, not deployment. Multiple physical telescopes can coexist. E during observation is read actor-locally even if the hidden telescope is offscreen; other world-item interaction is suppressed for that actor until exit. Leaving the boat, moving out of reach, losing local authority, changing/losing camera, disabling/destroying user/equipment, opening an input-blocking UI or losing clearance exits safely.

## Local visibility

URP `beginCameraRendering`/`endCameraRendering` scope suppression to the session camera. A built-in-pipeline fallback uses Camera pre-cull/post-render callbacks. `Renderer.forceRenderingOff` is snapshotted and restored exactly; renderer enabled flags are not changed. World-space CanvasRenderer cull flags are similarly scoped/restored. Disabled/pre-hidden objects remain so.

Collected visuals come from the owning boat hierarchy, boat-owned registered items, telescope and onboard player hierarchies. Collection refreshes at each render, including newly spawned objects. Environmental objects outside that ownership remain visible. Screen-space HUD canvases are left alone. No GameObjects, physics, materials, sky visibility, celestial knowledge, weather or chart progress are changed by presentation.

Between camera renders all suppression is restored. Different local actors can have independent sessions on different existing cameras; two sessions cannot own the same camera. Render callbacks are subscribed only during observation and removed during cleanup.

## Inspector defaults

| Field | Component default | Supplied prefab override |
| --- | --- | --- |
| Support width | 0.75 | 1.1 |
| Support depth | 0.16 | 0.2 |
| Foot offset Y | -0.64 | -1 |
| Support layers | All | All |
| Clearance width | 1.25 | 1.5 |
| Clearance height | 3 | 3 |
| Clearance origin height | 0.7 | 1.05 |
| Obstruction layers | All | All |
| Clearance interval | 0.15 seconds | Same |
| Interaction distance | 2 | Same |
| Interaction priority | 1100 | Same |
| Minimum/maximum zoom | 1 / 1.5 | Same |
| Zoom step per wheel unit | 0.1 | Same |

The prefab offsets fit its supplied 2-unit-high collider. Adjust these fields when changing its scale/geometry. Included physical player colliders also obstruct if actually inside the sky box; stand beside the telescope. Enter/exit now use a smooth 0.75-second transition (see refinement below); steady-state boat visuals are fully hidden during the viewing camera render.

## Persistence

`BoatLooseItemSnapshot` version 4 adds `isEquipmentDeployed` (default false). Existing identity and boat-local pose remain the source of truth. Capture excludes contradictory cargo/money-slot/bell-contained state. Restore first recreates the item/pose/ownership through the existing path, then asks its prefab equipment component to restore deployment on valid support. If support is invalid, the item remains loose. Ordinary items never gain equipment components. Legacy saves remain loose by default.

No new telescope save channel. Carried telescopes use normal item inventory state. Observation session, renderer suppression, look offset and telescope zoom are not saved; restored telescopes never enter observation automatically.

## Exact production files

Modified existing sources/assets:

1. `Assets/Scripts/Camera/CameraManager.cs` — additive active-camera and focus-pan snapshot access.
2. `Assets/Scripts/Inventory/Item/BoatLooseItemSnapshot.cs` — additive version-4 deployment field.
3. `Assets/Scripts/Inventory/Item/BoatLooseItemPersistence.cs` — capture/restore that field.
4. `Assets/Defs/Items/Catalogs/itemCatalog.asset` — register supplied definition.
5. `Assets/Defs/Items/Placeable/item_telescope_observeration.asset` — correct prefab/icon references.
6. `Assets/Resources/Prefabs/Items/Placeable/ObservationTelescope.prefab` — add three equipment/interaction components, tuned to supplied geometry.
7. `Assets/Codex/ENHANCEMENTS.md` — future impulse/replication notes.

New files, with normal Unity metadata:

1. `Assets/Scripts/Inventory/BoatEquipment/PlaceableBoatEquipment.cs`
2. `Assets/Scripts/Inventory/BoatEquipment/SkyClearanceRequirement.cs`
3. `Assets/Scripts/Inventory/BoatEquipment/ObservationTelescopeInteractable.cs`
4. `Assets/Scripts/Inventory/BoatEquipment/BoatObservationPresentationController.cs`
5. `Assets/Codex/OBSERVATION_TELESCOPE_IMPLEMENTATION.md`

The supplied prefab/definition/sprite folders were already untracked user work, not replacement assets created by this pass. Existing scene, material, sprite and earlier-pass changes are preserved.

## Verification and limits

Full production runtime C# compilation passes using Unity's installed Roslyn/runtime references. The ignored isolated Unity harness passes **6,499 assertions**, including **32 new telescope checks**, with all four telescope sources copied unchanged. Surrounding boat/item/camera/input/message hosts are adapters.

Native 2D queries checked valid deck support, telescope-self exclusion, distant geometry, overhead blockage and trigger exclusion. Deployment checks cover authority rejection, pin creation/release, pickup blocking and restore. Presentation checks cover zoom/pan restoration, actor-local E exit, scoped boat/item/player hiding, environment preservation, pre-hidden/disabled renderer preservation, another camera, independent local camera sessions, newly blocked clearance, invalid boarding and explicit teardown callback. Snapshot checks cover legacy defaults and deployment/pose JSON round trip. The existing celestial generation, evidence, constellation layer/projection, time/sky rebinding, treasury/inventory reset and starter-chest checks also pass.

This is an **Editor/headless** test, not live URP play-mode acceptance. Render callbacks and teardown callbacks are invoked explicitly; the harness cannot prove visual rasterization or automatic play-mode lifecycle timing. Its existing edit-mode lifecycle assertions and the deployment Destroy-in-edit-mode diagnostic do not represent a live game test. Tests live under ignored `Temp/CodexPhase7`; they are not production assets.

Still required in the live project:

1. Spawn through inventory; drop on deck; E deploy; E observe; E/Escape exit; X unpin/F pickup.
2. Confirm boat/modules/onboard items/player/telescope disappear completely and island/cloud/fog/weather remain visible, including any world-space labels. Try a second camera if available.
3. Hold/release RMB and check the existing Phase 7 known/debug constellation rules and soft pan; test wheel limits and return to the exact prior zoom.
4. Place a nearby overhead obstruction; confirm exact warning `No unobstructed view of sky.` on entry and after adding an obstruction during use. Move a hull wall outside the box; it must not block.
5. Save deployed equipment; load/transition scenes; confirm identical physical pose/pin and no automatic observation. Also save it loose and carried. Ordinary cargo/bell/money-chest persistence must remain unchanged.
6. Test daytime/nighttime/weather, leaving range/boarding, opening UI and scene teardown while observing.

Future networking must transport physical deployment and send host requests through the project's eventual transport. This pass adds host gates and local camera ownership, not a network protocol. The existing GameMessageService is process-wide; a future same-process multi-view setup needs player-addressed messages for warnings. Same-process render scopes are validated logically, but a live multi-camera URP render is still required.

No Charting Instrument, evidence, fragments, paper consumption, impulse effects, damage, scope UI, new camera, alternate stars or night gate. Commit after the live smoke checks pass. 🍌

## Refinement: Escape routing and smooth transitions

Updated 2026-10-02 at user request. **Code and documentation only; no scene, prefab, material, item definition or other Inspector data edits.**

- `BoatObservationPresentationController` now implements `IEscapeClosable`, priority 1000, and registers with the existing EscapeCloseRegistry during observation. It no longer independently reads the Escape key. The router consumes the first Escape to request observation exit. Registration is removed when exit begins, allowing the next Escape to open the normal menu even if the return fade is finishing.
- `ObservationTelescopeInteractable` adds serialized `fadeDuration`, initialized to 0.75 seconds, Inspector range/runtime clamp 0.5–1 seconds. This code default is not an explicit prefab edit.
- New `BoatObservationRenderFade.cs` applies temporary render-scoped sprite alpha, complete line-gradient alpha, transparent mesh/TMP material color overrides and world-space canvas alpha. Exact prior colors/gradients/property blocks/alpha are restored before other camera renders. It never modifies shared material assets. Mesh materials must support alpha blending for gradual transparency; opaque/custom shaders without alpha support still disappear at full observation.
- Transition time uses unscaled delta time and smoothstep. At full observation, renderers are fully suppressed. Zoom eases with visibility and returns to its prior value. Exiting during entry reverses from the current blend without jumping. Interaction remains blocked for the observing actor until the return transition completes. A second Escape can open the menu; the fade continues in unscaled time while paused.
- Routine E/Escape exits and use invalidation return smoothly. Disabled/destroyed session components, missing/disabled cameras and scene teardown restore immediately because no safe continuing render/update owner remains.
- Full production runtime compile passes. Isolated Unity harness passes **6,508 assertions**: the previous 6,499 plus nine checks for the fade default, unchanged entry zoom, intermediate sprite alpha, other-camera alpha restoration, first Escape consumption, second Escape fallthrough, partial-entry reversal and final restoration. Live URP visuals/menu input timing remain required checks.

Inspector tasks: see `Assets/Codex/INSPECTOR_TASKS_OBSERVATION_TELESCOPE.md`. Future Inspector/asset wiring is performed by the user unless explicitly authorized afterward.
