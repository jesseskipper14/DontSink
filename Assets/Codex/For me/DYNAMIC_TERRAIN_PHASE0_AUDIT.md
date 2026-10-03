# Dynamic BoatScene terrain — Phase 0 audit

2026-10-03. Audit only. No runtime source, scenes, prefabs, materials, profiles, or Inspector settings changed. The committed wrapping implementation and the restored finite map-table presentation remain intact.

## Conclusion

NodeScene and BoatScene use separate terrain generators, not a scene-name branch inside one generator. Preserve NodeScene's finite island/dock terrain. Replace only BoatScene's finite terrain authority, retaining compatible rendering/query adapters for both scenes.

There is enough existing navigation and ground infrastructure to begin the voyage-coordinate checkpoint without inventing another boat movement model. Streaming then requires a scene-level multi-chunk query facade, explicit initialization readiness, immutable committed history, and resource-event isolation. Merely making the current ground wider would retain the wrong architecture.

The handoff's older “no wraparound” and east/west lethal-boundary requirements are superseded by the user's newer wrapping decision. Geographic X wraps, Y remains finite with polar terrain. The map/table stays a finite sheet. Do not change either as part of terrain streaming. No networking transport or MP implementation belongs in this pass.

## Current scene wiring and physical scale

Both scenes have a ground root named **SeaFloor**. Read-only YAML inspection confirmed:

| Setting | NodeScene | BoatScene |
| --- | --- | --- |
| Generator | NodeGroundGenerator2D | BoatSeaFloorGenerator2D |
| Ground surface | EdgeCollider2D, fileID 1821200604 | EdgeCollider2D, fileID 1516219388 |
| Sampler | Explicitly references that one edge | Explicitly references that one edge |
| Fill | GroundFillMeshRenderer2D, decimation step 2 | GroundFillMeshRenderer2D, decimation step 2 |
| Horizontal span | worldWidth 240; left islandLength 120 | Derived from BoatSceneController sourceDockX -20 and nominal distance 2000, giving local -20 to 1980 |
| Surface points | 240 | 320, about 6.27 local units between samples across that span |
| Boundaries | Enabled; padding -20, height 200, thickness 2 | Enabled; padding 1.5, height 60, thickness 1 |
| Seed | Randomized on generation | Randomized on generation, independently of TravelPayload.seed |
| Fill material | GroundMaterial_Style_1 | GroundMaterial_TempGround |
| Resource generation | Whole-span population, regeneration respawn enabled | Same whole-span model, regeneration respawn enabled |
| Water presentation | Three WaterMeshRenderer components, width 240, explicit center target | Three WaterMeshRenderer components, width 240, center target blank / Camera.main fallback |

BoatSeaFloorProfile_Test has zero end plateaus, two 200-unit shore slopes, base depth 120, a 200–300-wide/deep ravine, minimum dock clearance 50, and maximum depth 500. These are the assigned profile's values, not the source class defaults. The finite ends come from the generation span, end-shore shaping, fill extent, and optional boundary walls. The screenshots agree with that structure; exact screenshot coordinates were not measured.

Two wiring observations need explicit later Inspector review:

1. BoatScene's mesh fill uses `Assets/Resources/Materials/Ground Materials/GroundMaterial_TempGround.mat`, whose shader GUID resolves to `WaterSideView2D_Transparent.shader`. NodeScene's Style_1 uses `NodeGroundTerrain.shader`. Do not silently replace a material: use a separately reviewed BoatScene terrain material setup and preserve NodeScene's material.
2. BoatScene's NodeWaterBottomBinder references fileID 1516219392, the BoatSeaFloorGenerator2D. NodeScene's binder similarly references fileID 1821200605, the NodeGroundGenerator2D. Both generators implement IGroundGeneratedNotifier, not IGroundFillBottomSource, so neither assigned source supplies bottom updates to this binder. Aggregate loaded-chunk bottom depth should replace the BoatScene binding later. NodeScene's existing binding merits a separate reviewed Inspector correction; do not silently fix it during BoatScene replacement.

No active GroundSpriteShapeBinder2D component was found in either scene by its script GUID; the active fill path is the mesh renderer. The SpriteShape binder exists as an alternative, not the primary renderer to rewrite.

## Dependency map

Paths below are relative to `Assets/Scripts` unless otherwise noted.

| Current system | Risk with dynamic terrain | Proposed modification / new responsibility |
| --- | --- | --- |
| `Environment/GroundGeneration/NodeGroundGenerator2D.cs` | Shared helper changes could alter the dock, land slope, walls, or randomization | Keep generation and finite NodeScene behavior unchanged. Test the single-edge adapter as a compatibility path. |
| `Environment/GroundGeneration/BoatSeaFloorGenerator2D.cs` | GetWorldSpan reflects private controller fields; always makes one finite edge and shores at both ends; Generate may randomize anew | Retain legacy component for migration/reference. New BoatScene streamer owns generated chunks; disable the old runtime generator/walls only through reviewed Inspector tasks. Do not reinterpret every chunk end as a shore. |
| `GameState/Scene/BoatScene/BoatSceneController.cs` | NominalTravelDistance and physical source/target dock layout describe a finite trip, not an infinite seabed | Retain nominal distance as existing projection scale for now. Remove terrain dependence on dock coordinates. Keep legacy docking available during early checkpoints; geographic arrival is a later seam, not an excuse to break docking now. |
| `Boats/Simulation/BoatPilotingSimulation.cs` | PhysicalTravelDelta is a last-tick value, not accumulated strip state; independent FixedUpdate consumers could observe it out of order | Update strip state in the same explicit motion-sampling flow, once per authoritative tick. Preserve actual signed displacement and piloting feel. |
| `Boats/Simulation/BoatSceneWorldPositionBridge.cs` | Local navigation +Y is route-forward, unlike physical scene +X; direct use of map X for seabed would be wrong | Reuse WorldUnitsPerLocalUnit, GeographicHeadingDegrees, and ProjectNavigationVector for forecasts; use WorldNavigationService for current canonical geography. Keep strip/local conversion independent. |
| `GameState/GameState.cs` TravelPayload | Route seed/endpoints are available, but are not currently the seabed seed hierarchy | Define a terrain-generation version and deterministic terrain salt using existing voyage/world identity and logical chunk coordinate. Separate resource/feature RNG channels. |
| `WorldMap/Data/WorldMapRuntimeCache.cs` | BoatScene must not generate an unrelated world when a NodeScene source is absent | Resolve persisted/cached Field, EffectiveSeaLevel01, and BiomeLayer once. Validate readiness and identity; use an explicit safe fallback if real context is unavailable. |
| `WorldMap/Topography/WorldMapTopographyField.cs`, `WorldMapTopographyAnalysis.cs` | Height01 is macro geography, not literal local depth; Classify takes settings whose sea level can differ from the effective generated sea level | Central query/depth mapping uses the actual effective sea level, normalized X, finite Y, and configurable depth/slope limits. Classify land/water consistently with saved world truth. |
| `Environment/GroundGeneration/GeneratedGroundSampler2D.cs` | Caches one edge; sorts its points; subscribes only to notifiers on the same object; TryGetWorldSpan assumes one contiguous range | Keep one stable scene facade that delegates to a chunk provider in BoatScene and retains its single-edge path in NodeScene. Query the containing loaded chunk; never interpolate across an unloaded gap or select an arbitrary chunk sampler globally. |
| `GeneratedGroundClearanceUtility2D.cs`, `GeneratedGroundPenetrationGuard2D.cs` | Player rescue and placement clearance depend on that sampler; missing sample currently means no placement rejection | Preserve bell-occupant suppression and authority checks. Restore workflows must explicitly await required terrain rather than treating missing terrain as safe. Reuse correction math, with wider footprint checks where needed. |
| `Controller/Player/CharacterPlayer.cs`, `CharacterMotor2D.cs`, `Physics/GroundingSensor2D.cs`, `Agents/Runtime/AgentGroundSnapper.cs` | CharacterPlayer auto-adds penetration guard; ordinary grounding uses physics masks/contacts/raycasting | Keep compatible Ground layer/collider surfaces. Do not replace normal collision grounding with terrain teleports. Adjacent chunk contacts must remain smooth. |
| `GroundFillMeshRenderer2D.cs`, `IGroundFillSource.cs` | One renderer subscribes to one generator; local per-mesh UVs restart; bottom depth comes from that fill | Reuse per-chunk mesh building behind a chunk-specific notifier. Share boundary samples and fill depth. Aggregate bottom for water. Terrain shader uses world-space pattern, useful for seamless chunks; verify texture/UV behavior too. |
| `GroundSpriteShapeBinder2D.cs` | Alternative path also closes one shape to its bottom and subscribes to generator events | Preserve as Node/legacy option; do not add it to streamed chunks without need. |
| `Water/WaterMeshRenderer.cs`, `NodeWaterBottomBinder.cs` | Camera-centered water is presentation; one arbitrary chunk bottom is insufficient; multiple water layers exist | Keep water centering local. Provide aggregate depth coverage to appropriate BoatScene layers, preserving foreground/background ownership and NodeScene binding. Cameras never determine physical terrain truth. |
| `Global/Physics/WaveManager.cs`, `Interfaces/IWaveService.cs` | Surface height varies; terrain should not shift with waves | Keep seabed relative to stable ocean datum; use existing wave service for actual surface queries. Preserve shader global water-level behavior. |
| `Modules/Tethers/AnchorPayload.cs` | Holds contact Collider2D references and creates a static hold joint; retiring contact terrain breaks holding | Active anchors retain required chunks; committed collider identity/positions stay fixed while needed. Preserve layer and normal filters. |
| `Modules/Tethers/DivingBell/DivingBellOccupancy.cs`, `DivingBellCollisionContext.cs`, `TetherPayloadCollisionScope.cs` | Bell exit samples scene floor; external bell colliders must hit seabed while occupants use exclusive ghost geometry | Retain bell and occupied-player interest regions; query the stable facade for exit safety. Preserve current ghost routing and payload layers; no global collision-matrix rewrite. Bell uses ordinary external physics, not a dedicated BellPayload ground sampler class. |
| `Inventory/Tools/Sounding/SoundingWeightBottomContact2D.cs` | Sounding weight is another actual seabed-contact consumer, beyond anchor/bell | Include deployed sounding gear when needed; preserve ITetherBottomContactProvider semantics and contact filters. |
| `Environment/Resources/UnderwaterResourceSceneSpawner.cs` | OnGenerated calls Respawn, which starts with ClearSpawned; whole-span budgets and random visit salt are not chunk spawning | Keep legacy NodeScene behavior. Do not broadcast every chunk commit/load as whole-ground generation. Preserve the existing initial BoatScene resource batch for early checks; dynamic resource spawning remains the following pass. |
| `GameState/Boat/BoatSpawner.cs` | Start immediately restores transform, modules, tethers, and loose items without a terrain-ready contract | Establish startup terrain readiness before restoring ground-dependent dynamic bodies, then restore and clear embedding before resuming physics. Avoid an arbitrary one-frame delay as the guarantee. |
| `Modules/Tethers/TetherDeploymentModule.Persistence.cs` | Safe restored pose uses Rigidbody casts along a path; not a general “already below an edge” check | Retain cast protection, add terrain-ready/post-restore clearance for applicable external bodies. Respect stowed/docked versus deployed context. |
| `Inventory/Item/BoatLooseItemPersistence.cs` | PersistentWorld items instantiate at saved worldPosition; loose/secured/bell-contained items follow other restore paths | Correct eligible external dynamic objects against new terrain; do not move secured boat items or bell occupants out of their intended context. Existing item policy/ownership stays authoritative. |
| `GameState/Scene/PlayerSceneContextRestorer.cs` | Waits for boat, not terrain | Coordinate actor restore with terrain readiness where world-space placement needs ground. Preserve boarded restore and player identity behavior. |

## Proposed checkpoints and exact scope

### 1. Voyage coordinate, diagnostics, no terrain replacement

Add a small BoatScene voyage state with double-precision accumulated signed strip distance and explicit local/strip mapping. Consume the exact position-delta sample produced by BoatPilotingSimulation, not throttle, hull rotation, route progress, or geographic heading. Reset cleanly on a new voyage; distinguish an intentional physical teleport from travel. Update ownership/ordering explicitly so no tick is counted twice or one tick late by accident.

Extend F4 with strip distance, local origin, signed last delta, generation/session context, and projection scale. Geographic-only F4 warp must not change strip distance or move boat/player/tethers. Record DebugWarpRevision so future terrain forecasts can refresh without morphing committed floor. No floating origin in this checkpoint.

Acceptance: forward/reverse, stationary heading spin, 180-degree geographic turn while still physically forward, return to menu/new voyage, and geographic-only warp. NodeScene and existing boat handling remain unchanged. Commit after live checks.

### 2. Streamed floor and compatible queries

New narrowly scoped responsibilities: a terrain profile/planner, streamer, committed chunk records, chunk collider/visual component, and a multi-chunk query provider consumed by the existing sampler facade. Names are proposals, not a requirement to create an interface for every concept.

Start tuning around 128-unit horizontal chunks with configurable sample spacing and actor safety/preload radii; evaluate against the current ~100-unit visible scale and actual boat size. Current 320-point/2000-unit whole-floor density is not a suitable constant to carry into every chunk. Support negative chunk indices and reversed travel. Each chunk spans the full vertical column; no vertical chunk grid.

Plan in global strip sample coordinates so neighbors share boundary values and compatible tangents. Configure EdgeCollider2D adjacency as appropriate to avoid endpoint collision catches. Use world-consistent visual pattern and shared bottom depth so differing chunk bottoms do not expose seams. Keep physical colliders static after commit. Pool/unload geometry only after all authoritative interest sources release it; retain sufficient compact in-memory committed profile/context to regenerate identical history during this voyage. Do not retain every GameObject forever.

Interest is the union of boat, living players, deployed bell, active anchor/sounding gear, and specifically justified persistent dynamic bodies. No camera authority and no permanent retention for every discarded item. Define a safe sleep/retire/restore policy for non-interest objects before retiring their supporting collider.

Keep forecast records separate from committed records. Replan only beyond gameplay/visual safety margins. Turning or warping must never rewrite ground already under actors. Emit chunk-specific events, not the legacy resource-wide respawn signal. Establish a terrain-ready contract for startup/restore and complete safe external-body placement before physics resumes.

Inspector tasks will explicitly identify BoatScene's legacy generator/walls to disable, the new streamer/profile/query-provider references, the fill material choice, and water-bottom bindings. NodeScene's generator, walls, resource population, and material stay as configured. No automatic scene/prefab edits.

Acceptance: many chunks forward/backward, contacts across edges, boat + remote swimmer + deployed bell + anchor + sounding weight, return to committed history, below-floor restore, no resource reset on chunk changes, stable water depth coverage, and NodeScene regression. Commit before adding geography.

### 3. Geographic depth and feature forecast

Use cached topography plus the bridge's actual world/local scale to predict true navigation along current heading in both travel directions. Normalize global longitude through the existing topology service; validate polar Y. Map height below EffectiveSeaLevel01 into one configurable depth curve up to the handoff's ~500-unit maximum. Sea level does not make the local seabed disappear or emerge into an island mesh. Smooth target depth and enforce configurable slope limits from fixed committed boundaries.

Add one development-only feature directive before commit, using a separate deterministic channel. Check hidden replan versus committed immutability after heading changes/F4 warp. Expose chunk seed, strip interval, geographic context, depth, normal/slope, and commit/load/unload events for the following resource pass. Do not implement that resource pass here.

### 4. Island encounters and boat-only geographic obstruction

Use world-space land classification/nearby landmass information for encounter truth. Project one dominant island with sticky approach/alongside/receding state; generic visuals can reuse the terrain style with water in front. Heading changes do not swing committed island presentation across the screen. Local seabed remains present for swimmers and gear.

Separate geographic penetration detection from replaceable physical enforcement. A boat-only proxy must permit reverse escape and parallel/away movement, without catching players, bell, anchor, or cargo. Dedicated layer/material setup requires a reviewed Inspector list. Keep legacy physical docking during early stages; expose configurable geographic destination acquisition as the later arrival seam, without implementing a new Node transition here.

North/south handling follows the newer polar-boundary direction. No east/west storm walls or lethal void. Any later polar danger-state hooks must remain separate from longitude wrapping and finite map presentation.

## Remaining design risks, not blockers for checkpoint 1

- Heading can revisit the same strip interval with different geography. Committed local history wins; only unseen forecast replans. Preserve this deliberately rather than trying to recreate an exact map slice.
- F4 relocates geography without physics. Existing committed terrain remains; debug testing must distinguish fresh-voyage depth tests from a warp into a different biome over existing floor.
- Long-term wrapped sailing removes the old handoff's assurance that physical coordinates stay small. Double strip state helps identity precision, but Unity Rigidbody positions remain floats. No floating origin now; measure long-voyage precision and revisit only with evidence.
- Chunk unload and persistent-object safety need an explicit policy, not just distance-based Destroy. Deployed/occupied gear must retain ground; disposable objects need a deliberate retirement path.
- Resource regeneration and water-bottom wiring are integration hazards. They are not reasons to rewrite NodeScene or silently change Inspector references.

## Verification and next action

The workspace was clean after the user's commit at audit start. Evidence came from current source, script/material GUIDs, scene YAML references, assigned profile values, and the supplied screenshots. No Unity runtime tests were run because this step changes documentation only. Runtime performance, collider seam behavior, shader appearance, and multi-actor streaming remain implementation acceptance checks.

No clarifying question is needed to begin checkpoint 1. No Inspector work is required for this audit. The next coding pass should be **voyage coordinate + F4 diagnostics only**, followed by its own report, live checks, and commit checkpoint.
