# Ocean bug patch checkpoint

2026-10-04 — Bosun 🍌

Implemented the main ocean-testing bug group. Live confirmation is still needed; these were reported symptoms, not an assembled-scene reproduction by Bosun.

## Changes

- **Inventory drag/drop:** BoatScene's drag controller has empty inventory/input references, although its linked PlayerInventoryUI has the correct inventory. The controller now resolves that exact existing UI binding and the player's drop input. Explicit references still win; it never selects an arbitrary player. Fallback release positions use the player, not the scene Canvas.
- **Dropped-item motion:** the shared WorldItemDropUtility initializes each dynamic body with carrier point velocity and spin. Bell occupancy wins over boat boarding; an unboarded actor falls back to their own body. Covers normal inventory, equipment, overflow and purchase release paths using this utility. Ballast dumps now include rotational point velocity. The already-deployed sounding weight retains its existing physical velocity rather than receiving an extra boost. Persistence restoration remains unchanged.
- **Moving hatches:** box occupancy checks use the Rigidbody simulation pose, with a 0.01-unit inward margin on each side. Own ghost colliders resolve back to their source; hull/deck structure attached to the same boat body is ignored. Independently bodied cargo and players remain blockers. Existing obstruction-mask and trigger preferences remain respected.
- **Player/bell motion:** moving support comes from actual upward contacts rather than an arbitrary overlap result. The ground probe uses the simulation pose and excludes the player's own body. Walking/grip/jump forces stop during swimming. Swimming caps, dive-assist vertical damping and player water drag use the bell's velocity at the player's position when the local bell-water context applies. Ordinary ocean drag resumes outside the bell.
- **Depth darkness:** EnvironmentDepthLighting is created at runtime alongside GlobalBrightnessManager. Ambient fade begins at 10 physical depth units and reaches exactly zero at 300. URP global ambient light uses the active local camera's depth; terrain, ocean, ropes, stairs and bubbles use world-position depth in their shaders. Bell fallback water uses its own per-pixel depth shader, including local lamp illumination. Nearby active Point Light2D lamps, including spot cones, provide local illumination; sunlight/moonlight are excluded from that lamp list. No artificial head glow was added.
- **Propulsion:** CanProduceThrust now requires water at the propulsion point, and rejects a point buried in streamed ground. An optional EngineModule Propulsion Point can specify the real propeller/intake. If blank, the fallback is the hull bottom below the engine; deck-mounted engines therefore remain valid. Forces sample the physics pose. Engine on/off and idling/fuel behavior remain separate from thrust eligibility.
- **Grounding:** the terrain streamer adds BoatGroundContactResistance2D to boats at runtime. Actual real-hull contacts with streamed terrain receive up to 80 units/second² of tangential resistance. Multiple seam contacts do not multiply it. No resistance is applied while clear of terrain, lifting away, or on a non-authoritative client. No shared friction material or layer matrix was modified.

## Inspector checklist — no saved Inspector data was changed

No new assignment is required to begin testing.

1. **BoatScene InventoryDragController:** in Play mode, verify Inventory resolves to the same player used by the linked PlayerInventoryUI, and Inventory Input resolves to that player's input component. You can explicitly assign those fields outside Play if desired; the fallback covers the present empty references.
2. **HatchRuntime:** keep player/item layers in Close Obstruction Mask. Do not enable Ignore Same Boat Root merely to fix moving hatches; independently bodied cargo should continue blocking. Verify the Blocking Collider on HatchAuthoring still describes the opening.
3. **EngineModule:** optionally create/assign a Propulsion Point at the real propeller/intake. Leaving it blank uses the hull-bottom fallback. This is a gameplay approximation until a physical propeller is authored.
4. **Depth lights:** use active Point Light2D lamps with appropriate radius/cone. Keep player/item sorting layers included in their usual URP light target layers. The custom unlit terrain/water illumination approximates these point lights; it does not reproduce URP shadow occlusion or sorting-layer exclusions. Freeform/Sprite lights are not included in that custom sampler.
5. **Optional saved tuning:** add EnvironmentDepthLighting yourself to the same object as GlobalBrightnessManager to save custom Fade Start Depth/Black Depth values. Otherwise its runtime defaults apply. Likewise, manually adding BoatGroundContactResistance2D to the boat root permits saved Ground Deceleration tuning; the streamer will not add a duplicate.

## Live tests before committing

1. In BoatScene, drag a normal item and a stack outside inventory. Repeat Q-drop and single-item drop while cruising in both directions. Confirm rejected container deposits still restore inventory safely. Check NodeScene dropping too.
2. At full speed, open/close a clear hatch, then deliberately place cargo/player in the closing volume. Clear openings should close; real occupancy should deny closing.
3. Ride the diving bell down and up: idle, walk in the air pocket, swim in its water, jump, exit and re-enter. Check for abrupt velocity changes and compare NodeScene. The original strange-motion report remains unconfirmed until this test.
4. Visit approximately 0, 100, 200, 300 and 500 units below water. Without lamps, deep terrain, actors, ropes and bell water should disappear into black. Enable a lamp, check its local cone/range, then return to the surface and verify daylight recovers. Repeat at night. Camera/global-light attenuation and per-pixel shader attenuation have different spatial granularity; assess transitions in actual play.
5. Run aground fast, check rapid slowdown, then test a completely dry propeller versus a submerged one. Verify reverse/retreat/refloat behavior. Idling engines can still consume resources while unable to propel.
6. Save/load, return to menu, and revisit BoatScene/NodeScene to verify the new runtime lighting and grounding components do not duplicate or retain stale presentation.

## Verification

- Full production runtime C# compile passes.
- Isolated Unity harness passes 1,420 new bug-patch assertions plus the existing feature/regression suites.
- New checks cover exact UI-binding fallback (extracted production resolver), full production drop utility with adapted item/ownership hosts, linear/angular carrier velocity, bell precedence, no double initialization, actual hatch/cargo geometry, bell-relative swimming/dive damping/water drag, propulsion eligibility (extracted production query), zero-friction hull grounding, airborne/non-authoritative exclusions, monotonic depth attenuation, lamp radius/cone and seven shader imports.
- Existing eight moving-floor physics checks remain passing.
- Harness shaders use production code/includes. Harness scene, attribute, lamp and service hosts are adapted; this is not a rendered lighting playtest or an actual inventory pointer-event test. Ignored test artifacts remain under Temp/CodexPhase7.

### Shader compilation regression and corrected validation

The initial headless shader-import checks missed an actual Direct3D11 compile failure: the shared include used the reserved HLSL keyword `point` as a parameter name. This caused magenta water/terrain/other affected materials in the real editor. Renamed it to `worldPosition` and normalized the touched shader files' line endings. The isolated harness now also has a graphics-enabled check that synchronously compiles each pass with ShaderUtil.CompilePass and checks support/errors. All seven affected shaders passed on Direct3D11; the running project editor reimported the corrected include without new shader errors. Headless import success must not be reported as GPU compilation success.

The map shoreline mismatch, node spacing, more aggressive ordinary depth slopes, old New Game world-map/dock state and broader MP work remain separate backlog items.

Once the live tests look right, commit this patch before the next pass.

### Depth-lighting refinement

Full darkness now occurs at 300m (fade starts at 10m), with about 77% ambient remaining at 100m and 28% at 200m before time-of-day scaling. Global light binding recovers if the persistent service loses its scene light. A negligible 0.000001 intensity floor keeps ambient effectively black while guarding against URP's unlit-albedo fallback when lighting is absent. Bell fallback water now samples depth/lamps per pixel instead of depending on mesh vertex colors. No saved scene, prefab, material or Inspector values were changed by this refinement. If you manually saved Black Depth=120 on EnvironmentDepthLighting, set it to 300 yourself.

Validation: production C# compile passes; all eight depth shaders compile synchronously on Direct3D11. Rendered bell-water pixel checks pass at 100m (visible), 300m (black), and 300m with a nearby lamp (visible). Full assembled-scene sprite lighting still needs live confirmation at those depths and on surfacing.

